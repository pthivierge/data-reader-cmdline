#region Copyright

//  Copyright 2015 Patrice Thivierge Fortin
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
using System.Threading;
using System.Threading.Tasks;
using OSIsoft.AF.Data;
using OSIsoft.AF.PI;
using OSIsoft.AF.Time;

namespace DataReader.Core
{
    public class DataReaderParallel : TaskBase, IDataReader
    {

        public readonly BlockingCollection<DataQuery> QueriesQueue = new BlockingCollection<DataQuery>();
        private PIServer _piServer;
        private DataReaderSettings _dataReaderSettings;
        private DataProcessor _dataProcessor;

        public DataReaderParallel(DataReaderSettings dataReaderSettings, DataProcessor dataProcessor)
        {
            _dataReaderSettings = dataReaderSettings;
            _dataProcessor = dataProcessor;
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

                _logger.WarnFormat("GET RECORDED PARRALEL - maxDegOfParallel {0} - Points Count {1}", _dataReaderSettings.MaxDegreeOfParallelism, query.PiPoints.Count);


                // Using the TPL to perform reads
                try
                {


                    Parallel.ForEach(
                        query.PiPoints
                        , new ParallelOptions { MaxDegreeOfParallelism = _dataReaderSettings.MaxDegreeOfParallelism, CancellationToken = cancelToken }
                        , pt =>
                    {

                        var stats = new StatisticsInfo() { Print = false };
                        stats.Stopwatch.Start();

                        var bulkData = new[]
                                {
                                pt.RecordedValues(timeRange, AFBoundaryType.Interpolated, String.Empty, false)
                            };

                        var data = new DataInfo()
                        {
                            Data = bulkData,
                            StatsInfo = stats

                        };



                        _dataProcessor.DataQueue.Add(data, cancelToken);




                    });

                    // small hack just to force stats to print for parallel, as we dont want all records to print
                    // that would be too much
                    var dummyStats = new StatisticsInfo();
                    Statistics.StatisticsQueue.Add(dummyStats);


                }



                catch (Exception ex)
                {
                    _logger.Error(ex);
                }






            }

            // tell that we are done with adding new data to the data processor.  The task will complete after that
            _dataProcessor.DataQueue.CompleteAdding();

        }




    }
}