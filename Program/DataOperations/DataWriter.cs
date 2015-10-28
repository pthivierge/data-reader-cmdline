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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using OSIsoft.AF.Asset;
using DataReader.CommandLine.Helpers;

namespace DataReader.CommandLine
{
    /// <summary>
    /// This class
    /// </summary>
    public class DataWriter
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof (DataWriter));
        private readonly BlockingCollection<List<AFValues>> _dataQueue = new BlockingCollection<List<AFValues>>();
        private readonly CancellationTokenSource _cancellationToken = new CancellationTokenSource();

        public BlockingCollection<List<AFValues>> DataQueue
        {
            get { return _dataQueue; }
        }

        public Task Run(int writingDelay, string fileName, int eventsPerFile)
        {
            var task = Task.Run(() => WriteData(_cancellationToken.Token, fileName, eventsPerFile));
            return task;
        }

        public void Stop()
        {
            _dataQueue.CompleteAdding();
            _cancellationToken.Cancel();
        }

        private void WriteData(CancellationToken cancelToken, string fileName, int eventsPerFile)
        {
            var allValues = new List<AFValue>();

            _logger.InfoFormat("Writing data task started...");

            // gets currently available values from the queue
            foreach (var afValues in _dataQueue.GetConsumingEnumerable(cancelToken))
            {
                var singleValues = afValues.SelectMany(v => v);
                allValues.AddRange(singleValues);

                if (allValues.Count >= eventsPerFile || _dataQueue.IsAddingCompleted)
                {
                    WriteValues(allValues, fileName);
                }
            }
        }

        private void WriteValues(List<AFValue> allValues, string fileName)
        {
            var fullFileNAme = fileName + "_" + DateTime.Now.ToIsoReadable() + ".csv";
            _logger.InfoFormat("Writing {0} values into text file {1}", allValues.Count, fullFileNAme);

            // write values to the file
            File.WriteAllLines(fullFileNAme,
                allValues.Select(v => v.Timestamp.LocalTime.ToIsoReadable() + "," + v.Value + "," + v.PIPoint.Name));

            // after values are written, we dont need them anymore
            allValues.Clear();

            _logger.InfoFormat("Writing completed");
        }
    }
}