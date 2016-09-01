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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OSIsoft.AF.Asset;
using OSIsoft.AF.Data;
using OSIsoft.AF.PI;
using OSIsoft.AF.Time;

namespace DataReader.Core
{
    public class DataReaderBulk : TaskBase , IDataReader
    {
        
        public readonly BlockingCollection<DataQuery> QueriesQueue = new BlockingCollection<DataQuery>();
        private PIServer _piServer;
        private DataReaderSettings _dataReaderSettings;
        private DataWriter _dataWriter;
        bool _enableWrite;

        public DataReaderBulk(DataReaderSettings dataReaderSettings, DataWriter dataWriter, bool enableWrite)
        {
            _dataReaderSettings = dataReaderSettings;
            _dataWriter = dataWriter;
            _enableWrite = enableWrite;
        }

        public BlockingCollection<DataQuery> GetQueriesQueue()
        {
            return QueriesQueue;
        }
        
        protected override void DoTask(CancellationToken cancelToken)
        {
            

            foreach (var query in QueriesQueue.GetConsumingEnumerable(cancelToken))
            {
                var timeRange = new AFTimeRange(query.StartTime,query.EndTime);
                GetRecordedValuesBulkParrallel(query, timeRange, _dataReaderSettings.BulkPageSize, _dataReaderSettings.MaxDegreeOfParallelism, _dataReaderSettings.BulkParallelChunkSize, cancelToken);

            }

            // tell that we are done with adding new data to the data processor.  The task will complete after that
           _dataWriter.DataQueue.CompleteAdding();

        }



        /// <summary>
        /// This method splits a point list into severall smaller lists and perform bulk calls on each list
        /// In parallel.  
        /// </summary>
        private void GetRecordedValuesBulkParrallel(DataQuery query, AFTimeRange timeRange, int bulkPageSize, int maxDegOfParallel, int bulkParallelChunkSize, CancellationToken cancelToken)
        {

            _logger.WarnFormat("QUERY (BULK-P) # {5} - TAGS: {6} - PERIOD: {3} to {4} - MAX DEG. PAR. {0}, TAG_CHUNK_SIZE {1}, TAG_PAGE_SIZE {2},", maxDegOfParallel, bulkParallelChunkSize, bulkPageSize, timeRange.StartTime,timeRange.EndTime, query.QueryId, query.PiPoints.Count);

            // PARALLEL bulk 
            var pointListList = query.PiPoints.ToList().ChunkBy(bulkParallelChunkSize);
            Parallel.ForEach(pointListList, new ParallelOptions { MaxDegreeOfParallelism = maxDegOfParallel,CancellationToken = cancelToken },
                (pts,state,index) =>
                {
                   var stats=new StatisticsInfo();
                    stats.Stopwatch.Start();
    
                    PIPagingConfiguration pagingConfiguration = new PIPagingConfiguration(PIPageType.TagCount, bulkPageSize);
                    PIPointList pointList = new PIPointList(pts);

                    try
                    {
                       // _logger.InfoFormat("Bulk query");
                        IEnumerable<AFValues> bulkData = pointList.RecordedValues(timeRange,
                            AFBoundaryType.Inside, String.Empty, false, pagingConfiguration).ToList();


                        if (_enableWrite)
                        {
                            var writeInfo=new WriteInfo()
                            {
                                Data = bulkData,
                                StartTime = timeRange.StartTime,
                                EndTime = timeRange.EndTime,
                                ChunkId = query.ChunkId,
                                SubChunkId= index
                            };

                            _dataWriter.DataQueue.Add(writeInfo, cancelToken);
                        }
                            

                        stats.EventsCount = bulkData.Sum(s=>s.Count);
                        stats.Stopwatch.Stop();
                        stats.EventsInWritingQueue = _dataWriter.DataQueue.Count;
                        Statistics.StatisticsQueue.Add(stats, cancelToken);


                    }
                    catch (OperationCanceledException ex)
                    {
                        _logger.Error(pagingConfiguration.Error);
                    }
                    catch (Exception ex)
                    {

                        _logger.Error(ex);

                    }

                    

                });


        }




    }
}