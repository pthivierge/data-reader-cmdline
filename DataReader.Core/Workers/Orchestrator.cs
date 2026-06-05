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

        List<AFTime> _datesIntervals;

        public Orchestrator(string startTime, string endTime, TimeSpan interval, IDataReader dataReader)
        {
            // Parse as AFTime to properly handle time strings like "2023-02-25 07:00:00"
            // For summary queries, local time is used to ensure daily intervals align with calendar days
            var st = new AFTime(startTime);
            var et = new AFTime(endTime);

            _logger.Info("Getting time intervals: {0} seconds, Start (Local): {1}, End (Local): {2}", 
                interval.TotalSeconds, st.LocalTime, et.LocalTime);
            _logger.Info("Getting time intervals: Start (UTC): {0}, End (UTC): {1}", 
                st.UtcTime, et.UtcTime);
            
            _datesIntervals = TimeStampsGenerator.Get(interval, st, et);

            _logger.Info("Will work with {0} dates intervals", _datesIntervals.Count);

            _dataReader = dataReader;
        }



        protected override void DoTask(CancellationToken cancelToken)
        {
            _logger.Info("Orchestrator started and ready to receive tags to send data queries to the DataReader");

            // For summary reads, we want contiguous, boundary-aligned ranges (no -1 second).
            // PIPointList.Summaries produces N intervals when (end-start) is exactly N*summaryInterval,
            // so using exact boundaries avoids partial/shifted intervals.
            bool isSummaryReader = _dataReader is DataReaderSummary;

            // process the first intervall
            foreach (var dataQuery in IncomingPiPoints.GetConsumingEnumerable(cancelToken))
            {
                // Use LocalTime for DateTime to ensure daily intervals align with calendar days (important for summary queries)
                dataQuery.StartTime = _datesIntervals[0].LocalTime;
                dataQuery.EndTime = isSummaryReader
                    ? _datesIntervals[1].LocalTime
                    : _datesIntervals[1].LocalTime.AddSeconds(-1);
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

                var intervalEndLocal = isSummaryReader
                    ? _datesIntervals[i + 1].LocalTime
                    : _datesIntervals[i + 1].LocalTime.AddSeconds(-1);

                _logger.Debug("Times (Local): {0:G} - {1:G}",
                    _datesIntervals[i].LocalTime,
                    intervalEndLocal);
               

                if (cancelToken.IsCancellationRequested)
                    break;

                foreach (var dataQuery in PointsToRead)
                {
                    var newQuery = new DataQuery()
                    {
                        StartTime = _datesIntervals[i].LocalTime,
                        // For non-summary reads, we remove one second to avoid duplicates at boundaries.
                        // For summaries, keep exact boundaries to avoid partial intervals.
                        EndTime = isSummaryReader
                            ? _datesIntervals[i + 1].LocalTime
                            : _datesIntervals[i + 1].LocalTime.AddSeconds(-1),
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
