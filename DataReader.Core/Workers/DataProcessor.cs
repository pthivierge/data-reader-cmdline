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


        public  DataProcessor(bool enableWrite,DataWriter dataWriter)
        {
            _enableWrite = enableWrite;
            _dataWriter = dataWriter;
        }

        protected override void DoTask(CancellationToken cancelToken)
        {
            try
            {
                _logger.Info("Starting the process data task");

                foreach (var dataInfo in DataQueue.GetConsumingEnumerable(cancelToken))
                {
                    
                    var listData = dataInfo.Data.ToList();

                    var count = listData.Sum(v => v.Count);

                    TotalEventProcessed += count;

                    // working out some stats
                    dataInfo.StatsInfo.EventsCount = count;
                    dataInfo.StatsInfo.Stopwatch.Stop();
                    Statistics.StatisticsQueue.Add(dataInfo.StatsInfo,cancelToken);


                    if (_enableWrite)
                    {
                        _dataWriter.DataQueue.Add(listData,cancelToken);
                    }
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
