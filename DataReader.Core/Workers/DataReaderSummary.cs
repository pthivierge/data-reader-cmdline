#region Copyright
//  Copyright 2016 Patrice Thivierge F.
// 
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
#endregion
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OSIsoft.AF.Asset;
using OSIsoft.AF.Data;
using OSIsoft.AF.PI;
using OSIsoft.AF.Time;

namespace DataReader.Core
{
    /// <summary>
    /// Data reader for extracting aggregate/summary data (average, min, max, etc.) from PI Data Archive
    /// Uses sequential batch processing - PIPointList.Summaries handles internal parallelism
    /// All time ranges are in local time to ensure daily intervals align with calendar days and handle DST correctly.
    /// </summary>
    public class DataReaderSummary : TaskBase, IDataReader
    {
        public readonly BlockingCollection<DataQuery> QueriesQueue = new BlockingCollection<DataQuery>();
        private DataReaderSettings _dataReaderSettings;
        private DataWriter _dataWriter;
        private bool _enableWrite;
        private AFSummaryTypes _summaryTypes;
        private AFTimeSpan _summaryInterval;
        private AFCalculationBasis _calculationBasis;
        private AFTimestampCalculation _timestampCalculation;
        private int _targetEventsPerRequest;
        private int? _customIntervalsPerBatch;

        public DataReaderSummary(
            DataReaderSettings dataReaderSettings, 
            DataWriter dataWriter, 
            bool enableWrite,
            string summaryTypes,
            string summaryInterval,
            string calculationBasis,
            string timestampCalculation,
            int? intervalsPerBatch = null)
         {
             _dataReaderSettings = dataReaderSettings;
             _dataWriter = dataWriter;
             _enableWrite = enableWrite;
             
             _summaryTypes = ParseSummaryTypes(summaryTypes);
             _summaryInterval = AFTimeSpan.Parse(summaryInterval);
             _calculationBasis = ParseCalculationBasis(calculationBasis);
             _timestampCalculation = ParseTimestampCalculation(timestampCalculation);
             _customIntervalsPerBatch = intervalsPerBatch;
             
             _targetEventsPerRequest = 10000;
             
             _logger.InfoFormat("DataReaderSummary initialized - SummaryTypes: {0}, Interval: {1}, Basis: {2}, Timestamp: {3}, CustomIntervalsPerBatch: {4}",
                 _summaryTypes, _summaryInterval, _calculationBasis, _timestampCalculation, 
                 _customIntervalsPerBatch.HasValue ? _customIntervalsPerBatch.Value.ToString() : "Auto");
         }

        /// <summary>
        /// Retrieves summary data using PIPointList.Summaries with optimized batching
        /// PIPointList.Summaries handles internal parallelism, so we process batches sequentially
        /// Batching is calculated as: (10000 target events) / (number of summary types * total tags)
        /// All time ranges are in local time to ensure daily intervals align with calendar days and handle DST correctly.
        /// </summary>
        private void GetSummariesBulkSequential(DataQuery query, AFTimeRange timeRange, CancellationToken cancelToken)
        {
            // timeRange parameter is always in Local time (created from Local DateTimes in DoTask)
            // This ensures daily summaries align with calendar days and handle DST transitions correctly
            _logger.WarnFormat("QUERY (SUMMARY-BULK) # {0} - TAGS: {1} - PERIOD Local: {2} to {3} | UTC: {4} to {5} - SEQUENTIAL MODE",
                query.QueryId, query.PiPoints.Count,
                timeRange.StartTime.LocalTime, timeRange.EndTime.LocalTime,
                timeRange.StartTime.UtcTime.ToIso8601Utc(), timeRange.EndTime.UtcTime.ToIso8601Utc());

            // Calculate batching based on ALL tags, not per chunk
            int summaryTypesCount = CountSummaryTypes(_summaryTypes);
            int totalTags = query.PiPoints.Count;
            
            // Use custom intervalsPerBatch if provided, otherwise calculate automatically
            int intervalsPerBatch;
            if (_customIntervalsPerBatch.HasValue && _customIntervalsPerBatch.Value > 0)
            {
                intervalsPerBatch = _customIntervalsPerBatch.Value;
                _logger.InfoFormat("Using custom intervalsPerBatch: {0}", intervalsPerBatch);
            }
            else
            {
                // Target 10,000 events per call: 10000 / (summaryTypes * totalTags)
                intervalsPerBatch = CalculateIntervalsPerBatch(summaryTypesCount, totalTags);
            }
            
            _logger.InfoFormat("Batch calculation: {0} tags × {1} summary types × {2} intervals = ~{3} events per batch",
                totalTags, summaryTypesCount, intervalsPerBatch, totalTags * summaryTypesCount * intervalsPerBatch);

            var batchTimeRanges = SplitTimeRangeByIntervals(timeRange, _summaryInterval, intervalsPerBatch);
            
            _logger.InfoFormat("Split time range into {0} batches for sequential processing", batchTimeRanges.Count);

            // Process all tags together in sequential batches
            // PIPointList.Summaries() handles internal parallelism efficiently
            var pointList = new PIPointList(query.PiPoints);
            int batchIndex = 0;

            foreach (var batchTimeRange in batchTimeRanges)
            {
                if (cancelToken.IsCancellationRequested)
                {
                    _logger.WarnFormat("Cancellation requested, stopping processing");
                    break;
                }

                var stats = new StatisticsInfo();
                stats.Stopwatch.Start();

                PIPagingConfiguration pagingConfig = new PIPagingConfiguration(PIPageType.TagCount, 1000);

                try
                {
                    _logger.DebugFormat("Batch {0}: Processing {1} tags | Local: {2} to {3} | UTC: {4} to {5}", 
                        batchIndex, totalTags, 
                        batchTimeRange.StartTime.LocalTime, batchTimeRange.EndTime.LocalTime,
                        batchTimeRange.StartTime.UtcTime.ToIso8601Utc(), batchTimeRange.EndTime.UtcTime.ToIso8601Utc());

                    var bulkResults = pointList.Summaries(
                        batchTimeRange,
                        _summaryInterval,
                        _summaryTypes,
                        _calculationBasis,
                        _timestampCalculation,
                        pagingConfig);

                    var batchSummaryData = new List<AFValues>();
                    var summaryTypeMap = new Dictionary<int, string>();
                    var tagNameMap = new Dictionary<int, string>();
                    var valueIndex = 0;

                    foreach (var pointResult in bulkResults)
                    {
                        foreach (var kvp in pointResult)
                        {
                            var summaryType = kvp.Key;
                            var values = kvp.Value;
                            
                            if (values != null && values.Count > 0)
                            {
                                var piPoint = values.PIPoint;
                                var tagName = piPoint != null ? piPoint.Name : "Unknown";
                                
                                var summaryValues = new AFValues();
                                foreach (var val in values)
                                {
                                    summaryTypeMap[valueIndex] = summaryType.ToString();
                                    tagNameMap[valueIndex] = tagName;
                                    summaryValues.Add(val);
                                    valueIndex++;
                                }
                                batchSummaryData.Add(summaryValues);
                            }
                        }
                    }

                    // Send each batch immediately to the write queue
                    if (_enableWrite && batchSummaryData.Count > 0 && !cancelToken.IsCancellationRequested)
                    {
                        var writeInfo = new WriteInfo()
                        {
                            Data = batchSummaryData,
                            StartTime = batchTimeRange.StartTime.UtcTime,
                            EndTime = batchTimeRange.EndTime.UtcTime,
                            ChunkId = query.ChunkId,
                            SubChunkId = batchIndex,
                            IsSummaryData = true,
                            Metadata = new Dictionary<string, string>()
                            {
                                { "OriginalStart", timeRange.StartTime.UtcTime.ToIso8601Utc() },
                                { "OriginalEnd", timeRange.EndTime.UtcTime.ToIso8601Utc() },
                                { "SummaryInterval", _summaryInterval.ToString() },
                                { "SummaryTypes", _summaryTypes.ToString() },
                                { "CalculationBasis", _calculationBasis.ToString() },
                                { "TimestampCalculation", _timestampCalculation.ToString() }
                            }
                        };

                        // Enqueue the write operation
                        _dataWriter.DataQueue.Add(writeInfo, cancelToken);
                        _logger.DebugFormat("Batch {0}: Enqueued {1} summary events for writing", 
                            batchIndex, batchSummaryData.Sum(b => b.Count));
                    }
                }
                catch (Exception ex)
                {
                    _logger.ErrorFormat("Error processing summary batch {0} - {1}", batchIndex, ex.Message);
                }
                finally
                {
                    stats.Stopwatch.Stop();
                    _logger.InfoFormat("SUMMARY-BULK Batch {0} processed - Duration: {1} ms",
                        batchIndex, stats.Stopwatch.ElapsedMilliseconds);
                    batchIndex++;
                }
            }
        }

        public BlockingCollection<DataQuery> GetQueriesQueue()
        {
            return QueriesQueue;
        }

        protected override void DoTask(CancellationToken cancelToken)
        {
            foreach (var query in QueriesQueue.GetConsumingEnumerable(cancelToken))
            {
                // Explicitly create AFTimeRange from Local DateTimes to ensure daily intervals align with calendar days
                // This is critical for summary calculations - a "daily" summary must align with local midnight-to-midnight
                // Using Local time ensures DST transitions are handled correctly (days can be 23, 24, or 25 hours)
                var startTimeLocal = DateTime.SpecifyKind(query.StartTime, DateTimeKind.Local);
                var endTimeLocal = DateTime.SpecifyKind(query.EndTime, DateTimeKind.Local);
                var timeRange = new AFTimeRange(new AFTime(startTimeLocal), new AFTime(endTimeLocal));
                
                GetSummariesBulkSequential(query, timeRange, cancelToken);
            }

            _dataWriter.DataQueue.CompleteAdding();
        }

        private AFSummaryTypes ParseSummaryTypes(string summaryTypesString)
        {
            if (string.IsNullOrWhiteSpace(summaryTypesString))
                return AFSummaryTypes.Average | AFSummaryTypes.Minimum | AFSummaryTypes.Maximum;

            AFSummaryTypes result = AFSummaryTypes.None;
            var parts = summaryTypesString.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in parts)
            {
                AFSummaryTypes parsedType;
                if (Enum.TryParse(part.Trim(), true, out parsedType))
                {
                    result |= parsedType;
                }
            }

            return result == AFSummaryTypes.None ? (AFSummaryTypes.Average | AFSummaryTypes.Minimum | AFSummaryTypes.Maximum) : result;
        }

        private AFCalculationBasis ParseCalculationBasis(string basis)
        {
            AFCalculationBasis result;
            if (Enum.TryParse(basis, true, out result))
                return result;
            return AFCalculationBasis.TimeWeighted;
        }

        private AFTimestampCalculation ParseTimestampCalculation(string calculation)
        {
            AFTimestampCalculation result;
            if (Enum.TryParse(calculation, true, out result))
                return result;
            return AFTimestampCalculation.Auto;
        }

        private int CountSummaryTypes(AFSummaryTypes summaryTypes)
        {
            int count = 0;
            foreach (AFSummaryTypes value in Enum.GetValues(typeof(AFSummaryTypes)))
            {
                if (value != AFSummaryTypes.None && value != AFSummaryTypes.All && 
                    value != AFSummaryTypes.AllForNonNumeric && (summaryTypes & value) == value)
                {
                    int intValue = (int)value;
                    if (intValue > 0 && (intValue & (intValue - 1)) == 0)
                        count++;
                }
            }
            return Math.Max(1, count);
        }

        private int CalculateIntervalsPerBatch(int summaryTypesCount, int totalTags)
        {
            if (totalTags == 0 || summaryTypesCount == 0)
                return 1;
            
            int intervalsPerBatch = _targetEventsPerRequest / (summaryTypesCount * totalTags);
            return Math.Max(1, intervalsPerBatch);
        }

        private List<AFTimeRange> SplitTimeRangeByIntervals(AFTimeRange timeRange, AFTimeSpan interval, int intervalsPerBatch)
        {
            var result = new List<AFTimeRange>();
            var currentStart = timeRange.StartTime;
            
            // Calculate batch duration by multiplying the interval TimeSpan
            var intervalTimeSpan = interval.ToTimeSpan();
            var batchDurationSeconds = intervalTimeSpan.TotalSeconds * intervalsPerBatch;
            var batchDuration = new AFTimeSpan(TimeSpan.FromSeconds(batchDurationSeconds));

            while (currentStart < timeRange.EndTime)
            {
                var currentEnd = currentStart + batchDuration;
                if (currentEnd > timeRange.EndTime)
                    currentEnd = timeRange.EndTime;

                result.Add(new AFTimeRange(currentStart, currentEnd));
                currentStart = currentEnd;
            }

            return result;
        }
    }
}
