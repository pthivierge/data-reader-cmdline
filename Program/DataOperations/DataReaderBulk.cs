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
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using DataReader.CommandLine.DataOperations;
using log4net;
using OSIsoft.AF.PI;
using OSIsoft.AF.Time;

namespace DataReader.CommandLine
{
    public class DataReaderBulk : IDataReader
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof(Program));

        private readonly DataWriter _dataWriter = new DataWriter();
        private readonly DataProcessor _dataProcessor = new DataProcessor();

        private TaskFactory _taskFactory;
        private readonly List<Task> _tasks = new List<Task>();

        private PIServer _piServer;


        /// <summary>
        /// Initializes the Reader class
        /// </summary>
        /// <param name="options"></param>
        private void init(CommandLineOptions options)
        {
            _taskFactory = new TaskFactory(TaskCreationOptions.LongRunning, TaskContinuationOptions.None);

            // data processing task
            _tasks.Add(_taskFactory.StartNew(() => _dataProcessor.Run(options.EnableWrite, _dataWriter)));

            // data writing task
            if (options.EnableWrite)
                _tasks.Add(_dataWriter.Run(3, options.OutfileName, options.EventsPerFile));

            // PI System Connection
            var connection = new PIConnection(options.Server);
            connection.Connect();

            _piServer = connection.GetPiServer();
        }


        public void Run(CommandLineOptions options)
        {
            init(options);

            _logger.InfoFormat("starting gathering data in bulk for period {0} to {1}", options.StartTime, options.EndTime);

            var totalDuration = Stopwatch.StartNew();

            // prepares date-related data
            var st = new AFTime(options.StartTime);
            var et = new AFTime(options.EndTime);
            var intervalDuration = et - st;
            var querySpanMilliSeconds = intervalDuration.TotalMilliseconds / options.Intervals;

            // prepares the point list
            var points = PIPoint.FindPIPoints(_piServer, options.Tags);
            var ptList = new PIPointList(points);


            for (var i = 0; i < options.Intervals; i++)
            {
                
                var intervalStart = st + TimeSpan.FromMilliseconds(i*querySpanMilliSeconds);
                var intervalEnd = st + TimeSpan.FromMilliseconds((i+1) * querySpanMilliSeconds);
                
                _logger.InfoFormat("Query sent for interval {0}, {1} to {2}", i, intervalStart, intervalEnd);

                var timeRange = new AFTimeRange(intervalStart, intervalEnd);

                // sends the data call, for all tags at the same time to the PI Data Archive
                var data = ptList.PlotValues(timeRange,options.PlotValuesIntervals, new PIPagingConfiguration(PIPageType.TagCount, 1000));

                // adds the raw data into the processor queue
                _dataProcessor.DataQueue.Add(data);
            }

            // tell that we are done with adding new data to the data processor.  The task will complete after that
            _dataProcessor.DataQueue.CompleteAdding();

            // wait for all tasks to complete: data processor and data reader
            _logger.InfoFormat("Waiting for remaining tasks to complete");
            Task.WaitAll(_tasks.ToArray());

            totalDuration.Stop();

            var totalSeconds = TimeSpan.FromMilliseconds(totalDuration.ElapsedMilliseconds).TotalSeconds;

            _logger.InfoFormat(" RESULT > Entire operation completed in {0} seconds", totalSeconds);
            _logger.InfoFormat(" RESULT > Processed {0} events", _dataProcessor.TotalEventProcessed);
            _logger.InfoFormat(" RESULT > Read and processing rate of {0} events/sec", Math.Round(_dataProcessor.TotalEventProcessed / totalSeconds, 0));
        }




    }
}