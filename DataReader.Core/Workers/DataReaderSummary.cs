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

        public DataReaderSummary(
            DataReaderSettings dataReaderSettings, 
            DataWriter dataWriter, 
            bool enableWrite,
            string summaryTypes,
            string summaryInterval,
            string calculationBasis,
            string timestampCalculation)
        {
            _dataReaderSettings = dataReaderSettings;
            _dataWriter = dataWriter;
            _enableWrite = enableWrite;
            
            _summaryTypes = ParseSummaryTypes(summaryTypes);
            _summaryInterval = AFTimeSpan.Parse(summaryInterval);
            _calculationBasis = ParseCalculationBasis(calculationBasis);
            _timestampCalculation = ParseTimestampCalculation(timestampCalculation);
            
            _logger.InfoFormat("DataReaderSummary initialized - SummaryTypes: {0}, Interval: {1}, Basis: {2}, Timestamp: {3}",
                _summaryTypes, _summaryInterval, _calculationBasis, _timestampCalculation);
        }

        public BlockingCollection<DataQuery> GetQueriesQueue()
        {
            return QueriesQueue;
        }

        protected override void DoTask(CancellationToken cancelToken)
        {
            foreach (var query in QueriesQueue.GetConsumingEnumerable(cancelToken))
            {
                var timeRange = new AFTimeRange(query.StartTime, query.EndTime);
                GetSummariesParallel(query, timeRange, _dataReaderSettings.MaxDegreeOfParallelism, 
                    _dataReaderSettings.BulkParallelChunkSize, cancelToken);
            }

            _dataWriter.DataQueue.CompleteAdding();
        }

        /// <summary>
        /// Retrieves summary data for multiple points in parallel
        /// </summary>
        private void GetSummariesParallel(DataQuery query, AFTimeRange timeRange, int maxDegOfParallel, 
            int chunkSize, CancellationToken cancelToken)
        {
            _logger.WarnFormat("QUERY (SUMMARY) # {0} - TAGS: {1} - PERIOD: {2} to {3} - MAX DEG. PAR. {4}, TAG_CHUNK_SIZE {5}",
                query.QueryId, query.PiPoints.Count, timeRange.StartTime, timeRange.EndTime, maxDegOfParallel, chunkSize);

            var pointListList = query.PiPoints.ToList().ChunkBy(chunkSize);
            
            Parallel.ForEach(pointListList, 
                new ParallelOptions { MaxDegreeOfParallelism = maxDegOfParallel, CancellationToken = cancelToken },
                (pts, state, index) =>
                {
                    var stats = new StatisticsInfo();
                    stats.Stopwatch.Start();

                    try
                    {
                        var summaryDataList = new List<AFValues>();

                        foreach (var point in pts)
                        {
                            var summaryTask = point.SummariesAsync(
                                timeRange,
                                _summaryInterval,
                                _summaryTypes,
                                _calculationBasis,
                                _timestampCalculation,
                                cancelToken);

                            var summaries = summaryTask.Result;

                            foreach (var kvp in summaries)
                            {
                                var summaryType = kvp.Key;
                                var values = kvp.Value;
                                
                                var summaryValues = new AFValues();
                                summaryValues.AddRange(values);
                                summaryDataList.Add(summaryValues);
                            }
                        }

                        if (_enableWrite && summaryDataList.Count > 0)
                        {
                            var writeInfo = new WriteInfo()
                            {
                                Data = summaryDataList,
                                StartTime = timeRange.StartTime.UtcTime,
                                EndTime = timeRange.EndTime.UtcTime,
                                ChunkId = query.ChunkId,
                                SubChunkId = index,
                                IsSummaryData = true,
                                Metadata = new Dictionary<string, string>
                                {
                                    { "SummaryTypes", _summaryTypes.ToString() },
                                    { "Interval", _summaryInterval.ToString() },
                                    { "CalculationBasis", _calculationBasis.ToString() }
                                }
                            };

                            _dataWriter.DataQueue.Add(writeInfo, cancelToken);
                        }

                        stats.EventsCount = summaryDataList.Sum(s => s.Count);
                        stats.Stopwatch.Stop();
                        stats.EventsInWritingQueue = _dataWriter.DataQueue.Count;
                        Statistics.StatisticsQueue.Add(stats, cancelToken);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.Warn("Summary operation was cancelled");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(string.Format("Error retrieving summaries for chunk {0}", index), ex);
                    }
                });
        }

        /// <summary>
        /// Parse comma-separated summary types string into AFSummaryTypes flags
        /// </summary>
        private AFSummaryTypes ParseSummaryTypes(string summaryTypesString)
        {
            if (string.IsNullOrWhiteSpace(summaryTypesString))
                return AFSummaryTypes.Average | AFSummaryTypes.Minimum | AFSummaryTypes.Maximum;

            var types = AFSummaryTypes.None;
            var parts = summaryTypesString.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in parts)
            {
                AFSummaryTypes parsedType;
                if (Enum.TryParse(part.Trim(), true, out parsedType))
                {
                    types |= parsedType;
                }
                else
                {
                    _logger.WarnFormat("Unknown summary type: {0}. Ignoring.", part);
                }
            }

            if (types == AFSummaryTypes.None)
            {
                _logger.Warn("No valid summary types specified. Using default: Average, Minimum, Maximum");
                types = AFSummaryTypes.Average | AFSummaryTypes.Minimum | AFSummaryTypes.Maximum;
            }

            return types;
        }

        /// <summary>
        /// Parse calculation basis string
        /// </summary>
        private AFCalculationBasis ParseCalculationBasis(string calculationBasisString)
        {
            AFCalculationBasis basis;
            if (Enum.TryParse(calculationBasisString, true, out basis))
            {
                return basis;
            }

            _logger.WarnFormat("Unknown calculation basis: {0}. Using default: TimeWeighted", calculationBasisString);
            return AFCalculationBasis.TimeWeighted;
        }

        /// <summary>
        /// Parse timestamp calculation string
        /// </summary>
        private AFTimestampCalculation ParseTimestampCalculation(string timestampCalculationString)
        {
            AFTimestampCalculation calc;
            if (Enum.TryParse(timestampCalculationString, true, out calc))
            {
                return calc;
            }

            _logger.WarnFormat("Unknown timestamp calculation: {0}. Using default: Auto", timestampCalculationString);
            return AFTimestampCalculation.Auto;
        }
    }
}
