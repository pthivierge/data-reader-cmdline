#region Copyright
// /*
// 
//    Copyright 2015 Patrice Thivierge Fortin
// 
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
// 
//    http://www.apache.org/licenses/LICENSE-2.0
// 
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
//  
//  */
#endregion

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using log4net;

namespace DataReader.Core
{

    /// <summary>
    /// This class exposes a BlockingCollection (based on the ConcurrentQueue) to make sure information can be gathered smotthly form other threads.
    /// This class is dedicated to print out statistical information.
    /// </summary>
    public class Statistics : TaskBase
    {


        private readonly ILog _logger = LogManager.GetLogger(typeof(Statistics));
        private readonly Stopwatch _stopwatch = new Stopwatch();

        private static readonly BlockingCollection<StatisticsInfo> _statisticsQueue = new BlockingCollection<StatisticsInfo>();
        private Int64 _totalEventsCount;


        public static BlockingCollection<StatisticsInfo> StatisticsQueue
        {
            get { return _statisticsQueue; }
        }

        public override Task Run()
        {

            _logger.Info("Starting Statistics...");
            _stopwatch.Start();



            return base.Run();

        }

        public override void Stop()
        {


            // operation is done,this will make the blocking collection terminate
            _logger.Info("Stopping Statistics...");
            _statisticsQueue.CompleteAdding();
            Thread.Sleep(200);
            base.Stop();

        }

        protected override void DoTask(CancellationToken cancelToken)
        {

            Task.Run(() => PrintSystemStatistics(TimeSpan.FromSeconds(30), cancelToken));

            PrintEvaluationsStatistics(cancelToken);


        }


        /// <summary>
        /// prints statistics on the console
        /// </summary>
        private void PrintEvaluationsStatistics(CancellationToken cancellationToken)
        {

            try
            {



                foreach (var statInfo in _statisticsQueue.GetConsumingEnumerable(cancellationToken))
                {
                    _totalEventsCount += statInfo.EventsCount;

                    if (statInfo.Print)
                    {

                        _logger.InfoFormat(
                            "READ STATS - global-> processing rate: {0:#00.00} events/sec | Total Events: {1:#000} | Duration: {2}"
                            , Math.Round(_totalEventsCount / _stopwatch.Elapsed.TotalSeconds, 2)
                            , _totalEventsCount
                            , _stopwatch.Elapsed


                            );

                        if (statInfo.EventsCount > 0)
                        {
                            _logger.InfoFormat(
                                "READ STATS - Last query-> processing rate: {0:#00.00} events/sec | Total Events: {1:#000} | Duration: {2}"
                                , statInfo.EventsProcessedPerSecond
                                , statInfo.EventsCount
                                , TimeSpan.FromMilliseconds(statInfo.Stopwatch.ElapsedMilliseconds)
                                );
                        }
                    }




                }

            }
            catch (OperationCanceledException cEx)
            {

            }
            catch (Exception ex)
            {
                _logger.Error(ex);
            }

        }

        /// <summary>
        /// This function is not used, but could be useful to diagnose code performances further
        /// </summary>
        /// <param name="cancelToken"></param>
        public void PrintSystemStatistics(TimeSpan interval, CancellationToken cancelToken)
        {
            Process curProcess = Process.GetCurrentProcess();
            TimeSpan totcpu, usercpu;
            int convfactor = 1000000;
            double elapseCPUinMillisec;

            while (true)
            {

                if (cancelToken.IsCancellationRequested)
                {
                    return;
                }

                curProcess.Refresh();

                totcpu = curProcess.TotalProcessorTime;
                elapseCPUinMillisec = totcpu.TotalMilliseconds;
                usercpu = curProcess.UserProcessorTime;

                _logger.InfoFormat("SYSTEM STATS - totcpu {0:F0} usrcpu {1:F0} Privbyte {2} virtbyte {3} CLR {4} GC0 {5},GC1 {6},GC2 {7}",
                     elapseCPUinMillisec, usercpu.TotalMilliseconds,
                    (curProcess.PrivateMemorySize64 / convfactor), (curProcess.VirtualMemorySize64 / convfactor),
                    (GC.GetTotalMemory(false) / convfactor), GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2));



                cancelToken.WaitHandle.WaitOne(interval);
            }




        }

    }
}