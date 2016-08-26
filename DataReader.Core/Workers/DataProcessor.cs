using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using log4net.Core;
using OSIsoft.AF.Asset;

namespace DataReader.Core
{
    /// <summary>
    /// This class makes the data processing
    /// It also keeps the count of the number of events processed
    /// IF applies, forwards the events to the data writer to have the data written
    /// </summary>
    public class DataProcessor : TaskBase
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof(DataProcessor));
        public readonly BlockingCollection<DataInfo> DataQueue = new BlockingCollection<DataInfo>();

        bool _enableWrite;
        DataWriter _dataWriter;

        public int TotalEventProcessed { get; private set; }


        public DataProcessor(bool enableWrite, DataWriter dataWriter)
        {
            _enableWrite = enableWrite;
            _dataWriter = dataWriter;
        }

        protected override void DoTask(CancellationToken cancelToken)
        {
            try
            {
                _logger.Info("Starting the process data task");

                var tasks = new List<Task>();

                foreach (var dataInfo in DataQueue.GetConsumingEnumerable(cancelToken))
                {

                    //while (tasks.Count >= 2)
                    //{
                    //    tasks.RemoveAll(t => t.IsCompleted || t.IsFaulted || t.IsCanceled);
                    //    Thread.Sleep(500);
                    //}


                    //var newTask = Task.Run(() =>
                    //{
                        //    // process the results
                        //    var eventsProcessedInChunk = 0;
                        //foreach (AFValues values in dataInfo.Data)
                        //{
                        //    foreach (AFValue value in values)
                        //    {
                        //        if (_enableWrite)
                        //        {
                        //            _dataWriter.DataQueue.Add(value, cancelToken);
                                    
                        //        }
                        //    eventsProcessedInChunk++;
                        //}
                        //}

                        //TotalEventProcessed += eventsProcessedInChunk;


                        //    // working out some stats
                        //    dataInfo.StatsInfo.EventsCount = eventsProcessedInChunk;
                        //dataInfo.StatsInfo.Stopwatch.Stop();
                        //dataInfo.StatsInfo.EventsInWritingQueue = _dataWriter.DataQueue.Count;
                        //Statistics.StatisticsQueue.Add(dataInfo.StatsInfo, cancelToken);
                    //}, cancelToken);

                    //tasks.Add(newTask);







                }
            }

            catch (Exception ex)
            {
                if (!(ex is OperationCanceledException))
                    _logger.Error(ex);
            }
            finally
            {
                _dataWriter.DataQueue.CompleteAdding();
            }
        }
    }
}
