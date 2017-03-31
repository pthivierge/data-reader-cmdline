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
using System.Threading;
using OSIsoft.AF.Time;

namespace DataReader.Core
{
    /// <summary>
    /// This class manages the way data is read
    /// It focuses on ready data from all tags in a specific time range
    /// Then if goes over the next time range and starts the reads for all tags again.
    /// </summary>
    public class Orchestrator : TaskBase
    {
        public BlockingCollection<DataQuery> IncomingPiPoints = new BlockingCollection<DataQuery>();
        public readonly ConcurrentQueue<DataQuery> PointsToRead = new ConcurrentQueue<DataQuery>();
        private static int _queryId = 0;
        IDataReader _dataReader;

        List<DateTime> _datesIntervals;

        public Orchestrator(string startTime, string endTime, TimeSpan interval, IDataReader dataReader)
        {
            // prepares dates to read
            var st = new AFTime(startTime);
            var et = new AFTime(endTime);

            _logger.InfoFormat("Getting time intervals: {0},{1},{2}", interval.TotalSeconds, st.LocalTime, et.LocalTime);
            _datesIntervals = TimeStampsGenerator.Get(interval, st, et);

            _logger.InfoFormat("Will work with {0} dates intervals", _datesIntervals.Count);

            _dataReader = dataReader;
        }



        protected override void DoTask(CancellationToken cancelToken)
        {
            _logger.Info("Orchestrator started and ready to receive tags to send data queries to the DataReader");

            // process the first intervall
            foreach (var dataQuery in IncomingPiPoints.GetConsumingEnumerable(cancelToken))
            {

                dataQuery.StartTime = _datesIntervals[0];
                dataQuery.EndTime = _datesIntervals[1].AddSeconds(-1);
                dataQuery.QueryId = _queryId++;
                dataQuery.ChunkId = 1;
                // keep the taglist for the next time period query
                PointsToRead.Enqueue(dataQuery);

                _dataReader.GetQueriesQueue().Add(dataQuery, cancelToken);

            }

            _logger.Info("Orchestrator completed initial queries for all tags. Will continue for all remaining intervals.");

            // GetConsumingEnumarable() will resume and release the wait in the loop when all tags will be loaded.
            // once all the tags are loaded we can continue again with the other time periods


            // for each time period, triggers the read for all the tags
            for (var i = 1; i < _datesIntervals.Count - 1; i++)
            {

                _logger.DebugFormat("Times:{0:G} - {1:G}", _datesIntervals[i].ToLocalTime(), _datesIntervals[i + 1].AddSeconds(-1).ToLocalTime());
               

                if (cancelToken.IsCancellationRequested)
                    break;

                foreach (var dataQuery in PointsToRead)
                {
                    var newQuery = new DataQuery()
                    {
                        StartTime = _datesIntervals[i],
                        EndTime = _datesIntervals[i + 1].AddSeconds(-1), // we remove one second to avoid getting duplicate values at this same time each time
                        QueryId = _queryId++,
                        PiPoints = dataQuery.PiPoints,
                        ChunkId = i
                    };


                    _dataReader.GetQueriesQueue().Add(newQuery, cancelToken);

                    if (cancelToken.IsCancellationRequested)
                        break;
                }


            }

            // we are done and no more data query will be added
            _dataReader.GetQueriesQueue().CompleteAdding();

            _logger.Info("Orchestrator has completed its task. All queries were sent.");
        }


    }

}
