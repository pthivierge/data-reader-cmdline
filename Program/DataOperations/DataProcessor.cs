using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;
using log4net.Core;
using OSIsoft.AF.Asset;

namespace DataReader.CommandLine.DataOperations
{
    /// <summary>
    /// This class makes the data processing
    /// It also keeps the count of the number of events processed
    /// IF applies, forwards the events to the data writer to have the data written
    /// </summary>
    public class DataProcessor
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof(DataProcessor));
        private readonly BlockingCollection<IEnumerable<AFValues>> _dataQueue = new BlockingCollection<IEnumerable<AFValues>>();
        public int TotalEventProcessed { get; private set; }

        public BlockingCollection<IEnumerable<AFValues>> DataQueue
        {
            get { return _dataQueue; }
        }

        public void Run(bool enableWrite,DataWriter dataWriter)
        {
            try
            {
                _logger.Info("Starting the process data task");

                foreach (var data in _dataQueue.GetConsumingEnumerable())
                {
                    // this will force enumeration of all the values, and may proceed with remaining data calls that were not yet completed
                    var listData = data.ToList();

                    var count = listData.Sum(v => v.Count);

                    TotalEventProcessed += count;


                    _logger.InfoFormat("Processed {0} values", count);

                    if (enableWrite)
                    {
                        dataWriter.DataQueue.Add(listData);
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
                dataWriter.DataQueue.CompleteAdding();
            }
        }

    }
}
