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
        private int _tagsChunkSize;

        public DataReaderSummary(
            DataReaderSettings dataReaderSettings, 
            DataWriter dataWriter, 
            bool enableWrite,
            string summaryTypes,
            string summaryInterval,
            string calculationBasis,
            string timestampCalculation,
            int? intervalsPerBatch = null,
            int tagsChunkSize = 50)
         {
             _dataReaderSettings = dataReaderSettings;
             _dataWriter = dataWriter;
             _enableWrite = enableWrite;
             
             _summaryTypes = ParseSummaryTypes(summaryTypes);
             _summaryInterval = AFTimeSpan.Parse(summaryInterval);
             _calculationBasis = ParseCalculationBasis(calculationBasis);
             _timestampCalculation = ParseTimestampCalculation(timestampCalculation);
             _customIntervalsPerBatch = intervalsPerBatch;
             _tagsChunkSize = tagsChunkSize;
             
             // Increased from 10000 to 30000 to fetch ~3x more data per API call
             // This reduces the number of round-trips to PI Data Archive
             // For 233 tags × 10 summary types: 4 intervals ? 12 intervals per batch
             _targetEventsPerRequest = 10000;
             
             _logger.Info("DataReaderSummary initialized - SummaryTypes: {0}, Interval: {1}, Basis: {2}, Timestamp: {3}, CustomIntervalsPerBatch: {4}, TagsChunkSize: {5}, MaxParallelThreads: {6}",
                 _summaryTypes, _summaryInterval, _calculationBasis, _timestampCalculation, 
                 _customIntervalsPerBatch.HasValue ? _customIntervalsPerBatch.Value.ToString() : "Auto",
                 _tagsChunkSize, _dataReaderSettings.MaxDegreeOfParallelism);
         }

        /// <summary>
        /// Retrieves summary data using PIPointList.Summaries with parallel tag chunking
        /// Tags are split into chunks (default 50) and processed in parallel (max 4 threads) within each time batch
        /// Time batches are processed sequentially to maintain order
        /// All time ranges are in local time to ensure daily intervals align with calendar days and handle DST correctly.
        /// </summary>
        private void GetSummariesBulkSequential(DataQuery query, AFTimeRange timeRange, CancellationToken cancelToken)
        {
            // timeRange parameter is always in Local time (created from Local DateTimes in DoTask)
            // This ensures daily summaries align with calendar days and handle DST transitions correctly
            _logger.Warn("QUERY (SUMMARY-BULK) # {0} - TAGS: {1} - PERIOD Local: {2} to {3} | UTC: {4} to {5} - PARALLEL TAG CHUNKING MODE ({6} tags per chunk, max {7} threads)",
                query.QueryId, query.PiPoints.Count,
                timeRange.StartTime.LocalTime, timeRange.EndTime.LocalTime,
                timeRange.StartTime.UtcTime.ToIso8601Utc(), timeRange.EndTime.UtcTime.ToIso8601Utc(),
                _tagsChunkSize, _dataReaderSettings.MaxDegreeOfParallelism);

            // Calculate batching based on chunk size (not all tags)
            int summaryTypesCount = CountSummaryTypes(_summaryTypes);
            int totalTags = query.PiPoints.Count;
            
            // Use custom intervalsPerBatch if provided, otherwise calculate based on chunk size
            int intervalsPerBatch;
            if (_customIntervalsPerBatch.HasValue && _customIntervalsPerBatch.Value > 0)
            {
                intervalsPerBatch = _customIntervalsPerBatch.Value;
                _logger.Info("Using custom intervalsPerBatch: {0}", intervalsPerBatch);
            }
            else
            {
                // Target 10,000 events per call based on chunk size: 10000 / (summaryTypes * chunkSize)
                intervalsPerBatch = CalculateIntervalsPerBatch(summaryTypesCount, _tagsChunkSize);
            }
            
            _logger.Info("Batch calculation: {0} tags in chunks of {1} × {2} summary types × {3} intervals = ~{4} events per chunk per batch",
                totalTags, _tagsChunkSize, summaryTypesCount, intervalsPerBatch, _tagsChunkSize * summaryTypesCount * intervalsPerBatch);

            var batchTimeRanges = SplitTimeRangeByIntervals(timeRange, _summaryInterval, intervalsPerBatch);
            
            _logger.Info("Split time range into {0} time batches, each batch will process tags in parallel chunks", batchTimeRanges.Count);

            // Split tags into chunks for parallel processing
            var tagChunks = query.PiPoints.ToList().ChunkBy(_tagsChunkSize);
            int totalChunks = tagChunks.Count();
            
            _logger.Info("Split {0} tags into {1} chunks of up to {2} tags each", totalTags, totalChunks, _tagsChunkSize);

            int batchIndex = 0;

            // Process time batches sequentially
            foreach (var batchTimeRange in batchTimeRanges)
            {
                if (cancelToken.IsCancellationRequested)
                {
                    _logger.Warn("Cancellation requested, stopping processing");
                    break;
                }

                var batchStats = new StatisticsInfo();
                batchStats.Stopwatch.Start();

                _logger.Debug("Time Batch {0}: Processing {1} tag chunks in parallel | Local: {2} to {3} | UTC: {4} to {5}", 
                    batchIndex, totalChunks,
                    batchTimeRange.StartTime.LocalTime, batchTimeRange.EndTime.LocalTime,
                    batchTimeRange.StartTime.UtcTime.ToIso8601Utc(), batchTimeRange.EndTime.UtcTime.ToIso8601Utc());

                // Thread-safe collections for aggregating results from parallel chunks
                var batchSummaryData = new ConcurrentBag<AFValues>();
                var summaryRecords = new ConcurrentBag<SummaryRecord>();
                var chunkExceptions = new ConcurrentBag<Exception>();

                // Process tag chunks in parallel (max 4 threads)
                Parallel.ForEach(tagChunks, 
                    new ParallelOptions { MaxDegreeOfParallelism = _dataReaderSettings.MaxDegreeOfParallelism, CancellationToken = cancelToken },
                    (tagChunk, state, chunkIndex) =>
                    {
                        try
                        {
                            var chunkPointList = new PIPointList(tagChunk);
                            PIPagingConfiguration pagingConfig = new PIPagingConfiguration(PIPageType.TagCount, 1000);

                            _logger.Debug("Time Batch {0}, Tag Chunk {1}: Processing {2} tags", 
                                batchIndex, chunkIndex, tagChunk.Count);

                            var bulkResults = chunkPointList.Summaries(
                                batchTimeRange,
                                _summaryInterval,
                                _summaryTypes,
                                _calculationBasis,
                                _timestampCalculation,
                                pagingConfig);

                            int chunkEventCount = 0;

                            foreach (var pointResult in bulkResults)
                            {
                                foreach (var summaryTypesResultsDictionary in pointResult)
                                {
                                    var summaryType = summaryTypesResultsDictionary.Key;
                                    var values = summaryTypesResultsDictionary.Value;

                                    if (values != null && values.Count > 0)
                                    {
                                        var piPoint = values.PIPoint;
                                        var tagName = piPoint != null ? piPoint.Name : "Unknown";
                                        var summaryTypeName = GetSummaryTypeName(summaryType);

                                        var summaryValues = new AFValues();
                                        foreach (var val in values)
                                        {
                                            summaryValues.Add(val);
                                            chunkEventCount++;

                                            summaryRecords.Add(new SummaryRecord
                                            {
                                                TimestampLocal = val.Timestamp.LocalTime,
                                                TagName = tagName,
                                                AggregateType = summaryTypeName,
                                                ValueString = val.Value != null ? val.Value.ToString() : "",
                                                SourceValue = val
                                            });
                                        }

                                        batchSummaryData.Add(summaryValues);
                                    }
                                }
                            }

                            _logger.Debug("Time Batch {0}, Tag Chunk {1}: Completed - {2} events retrieved", 
                                batchIndex, chunkIndex, chunkEventCount);
                        }
                        catch (Exception ex)
                        {
                            _logger.Error("Error processing Time Batch {0}, Tag Chunk {1} - {2}", 
                                batchIndex, chunkIndex, ex.Message);
                            chunkExceptions.Add(ex);
                        }
                    });

                // Check if any chunks had errors
                if (chunkExceptions.Count > 0)
                {
                    _logger.Warn("Time Batch {0}: {1} chunk(s) encountered errors", batchIndex, chunkExceptions.Count);
                }

                // Send the aggregated batch data to the write queue
                if (_enableWrite && batchSummaryData.Count > 0 && !cancelToken.IsCancellationRequested)
                {
                    var writeInfo = new WriteInfo()
                    {
                        Data = batchSummaryData.ToList(),
                        SummaryRecords = summaryRecords.ToList(),
                        StartTime = batchTimeRange.StartTime.LocalTime,
                        EndTime = batchTimeRange.EndTime.LocalTime,
                        ChunkId = query.ChunkId,
                        SubChunkId = batchIndex,
                        IsSummaryData = true,
                        Metadata = new Dictionary<string, string>()
                        {
                            { "OriginalStart", timeRange.StartTime.LocalTime.ToString("yyyy-MM-ddTHH:mm:ss") },
                            { "OriginalEnd", timeRange.EndTime.LocalTime.ToString("yyyy-MM-ddTHH:mm:ss") },
                            { "SummaryInterval", _summaryInterval.ToString() },
                            { "SummaryTypes", _summaryTypes.ToString() },
                            { "CalculationBasis", _calculationBasis.ToString() },
                            { "TimestampCalculation", _timestampCalculation.ToString() }
                        }
                    };

                    _dataWriter.DataQueue.Add(writeInfo, cancelToken);
                    _logger.Debug("Time Batch {0}: Enqueued {1} summary events for writing", 
                        batchIndex, batchSummaryData.Sum(b => b.Count));
                }
                
                batchStats.EventsCount = batchSummaryData.Sum(b => b.Count);
                batchStats.EventsInWritingQueue = _dataWriter.DataQueue.Count;
                batchStats.Stopwatch.Stop();
                Statistics.StatisticsQueue.Add(batchStats, cancelToken);
                
                _logger.Info("SUMMARY-BULK Time Batch {0} processed - Duration: {1} ms, Events: {2}",
                    batchIndex, batchStats.Stopwatch.ElapsedMilliseconds, batchStats.EventsCount);
                
                batchIndex++;
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
            if (intervalsPerBatch < 1)
            {
                _logger.Warn("CalculateIntervalsPerBatch: {0} summary types x {1} tags/chunk = {2} events/interval exceeds the ~{3} events/bulk-call target; clamping to 1 interval per batch. Consider lowering --tagsChunkSize.",
                    summaryTypesCount, totalTags, summaryTypesCount * totalTags, _targetEventsPerRequest);
            }
            return Math.Max(1, intervalsPerBatch);
        }

        private List<AFTimeRange> SplitTimeRangeByIntervals(AFTimeRange timeRange, AFTimeSpan interval, int intervalsPerBatch)
        {
            var result = new List<AFTimeRange>();
            var currentStart = timeRange.StartTime;
            
            // Use interval-based arithmetic to maintain alignment with summary boundaries
            // For daily intervals at 19:00, each batch end must align to 19:00 
            // This is achieved by adding the interval N times using calendar-aware arithmetic
            var intervalTimeSpan = interval.ToTimeSpan();
            
            // Detect if this is a day-based interval (within 1 second of 24 hours)
            // For day intervals, use calendar date arithmetic to preserve time-of-day
            bool isDayInterval = Math.Abs(intervalTimeSpan.TotalDays - Math.Round(intervalTimeSpan.TotalDays)) < 0.00002; // ~1.7 seconds tolerance
            int daysToAdd = isDayInterval ? (int)Math.Round(intervalTimeSpan.TotalDays) : 0;

            while (currentStart < timeRange.EndTime)
            {
                // Calculate the end of this batch by adding intervalsPerBatch times the interval
                // Use LocalTime to preserve time-of-day and handle DST correctly
                var currentLocalTime = currentStart.LocalTime;
                AFTime currentEnd;
                
                if (isDayInterval && daysToAdd > 0)
                {
                    // For day-based intervals, use calendar day addition to preserve time-of-day
                    // AddDays ensures 19:00:00 remains 19:00:00 regardless of DST transitions
                    var nextLocalTime = currentLocalTime.AddDays(daysToAdd * intervalsPerBatch);
                    currentEnd = new AFTime(nextLocalTime);
                }
                else
                {
                    // For non-day intervals, add the interval multiple times.
                    // Mirror TimeStampsGenerator: if a step lands in a DST spring-forward gap
                    // (an invalid local time), advance past the gap so AFTime gets a valid time.
                    var nextLocalTime = currentLocalTime;
                    for (int i = 0; i < intervalsPerBatch; i++)
                    {
                        nextLocalTime = nextLocalTime.Add(intervalTimeSpan);

                        if (TimeZoneInfo.Local.IsInvalidTime(nextLocalTime))
                        {
                            TimeSpan dstDelta = TimeSpan.FromHours(1);
                            foreach (var rule in TimeZoneInfo.Local.GetAdjustmentRules())
                            {
                                if (rule.DateStart <= nextLocalTime.Date && nextLocalTime.Date <= rule.DateEnd)
                                {
                                    dstDelta = rule.DaylightDelta;
                                    break;
                                }
                            }
                            nextLocalTime = nextLocalTime.Add(dstDelta);
                        }
                    }
                    currentEnd = new AFTime(nextLocalTime);
                }
                
                if (currentEnd > timeRange.EndTime)
                    currentEnd = timeRange.EndTime;

                result.Add(new AFTimeRange(currentStart, currentEnd));
                currentStart = currentEnd;
            }

            return result;
        }

        /// <summary>
        /// Converts AFSummaryTypes enum to a reliable string representation
        /// This avoids issues with ToString() on flags enums
        /// </summary>
        private string GetSummaryTypeName(AFSummaryTypes summaryType)
        {
            // Handle each individual summary type explicitly
            if (summaryType == AFSummaryTypes.Total) return "Total";
            if (summaryType == AFSummaryTypes.Average) return "Average";
            if (summaryType == AFSummaryTypes.Minimum) return "Minimum";
            if (summaryType == AFSummaryTypes.Maximum) return "Maximum";
            if (summaryType == AFSummaryTypes.Range) return "Range";
            if (summaryType == AFSummaryTypes.StdDev) return "StdDev";
            if (summaryType == AFSummaryTypes.PopulationStdDev) return "PopulationStdDev";
            if (summaryType == AFSummaryTypes.Count) return "Count";
            if (summaryType == AFSummaryTypes.PercentGood) return "PercentGood";
            if (summaryType == AFSummaryTypes.TotalWithUOM) return "TotalWithUOM";
            
          
            // Fallback to ToString() for any unhandled cases
            return summaryType.ToString();
        }
    }
}
