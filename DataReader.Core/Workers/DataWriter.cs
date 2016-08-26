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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DataReader.Core.Helpers;
using log4net;
using OSIsoft.AF.Asset;

namespace DataReader.Core
{
    /// <summary>
    /// This class
    /// </summary>
    public class DataWriter : TaskBase, IDisposable
    {

        public readonly BlockingCollection<IEnumerable<AFValues>> DataQueue = new BlockingCollection<IEnumerable<AFValues>>();
        private readonly CancellationTokenSource _cancellationToken = new CancellationTokenSource();
        private string _outputFileName;
        private int _eventsPerFile;


        private List<FileWriter> writers = new List<FileWriter>();


        public DataWriter(string outputFileName, int eventsPerFile)
        {
            _outputFileName = outputFileName;
            _eventsPerFile = eventsPerFile;

            writers.Add(new FileWriter(eventsPerFile, outputFileName ,"1"));
            writers.Add(new FileWriter(eventsPerFile, outputFileName , "2"));
            writers.Add(new FileWriter(eventsPerFile, outputFileName , "3"));
            writers.Add(new FileWriter(eventsPerFile, outputFileName , "4"));
        }

        public override void Stop()
        {
            DataQueue.CompleteAdding();
            base.Stop();
        }

        protected override void DoTask(CancellationToken cancelToken)
        {
            WriteData(_cancellationToken.Token, _outputFileName);
        }

        private void WriteData(CancellationToken cancelToken, string fileName)
        {


            _logger.InfoFormat("Writing data task started...");


            // gets currently available values from the queue
            foreach (IEnumerable<AFValues> valuesList in DataQueue.GetConsumingEnumerable(cancelToken))
            {


                
                var writer = writers.FirstOrDefault(w => w.ActiveTask == null || (w.ActiveTask.IsCompleted && w.ActiveTask.Status != TaskStatus.WaitingForActivation));

                if (writer == null)
                {
                    Task.WaitAny(writers.Select(w => w.ActiveTask).ToArray());
                    writer = writers.FirstOrDefault(w => w.ActiveTask == null || (w.ActiveTask.IsCompleted && w.ActiveTask.Status != TaskStatus.WaitingForActivation));
                }


                writer.ActiveTask = Task.Run(() =>
                {
                    foreach (AFValues afValues in valuesList)
                    {

                        foreach (AFValue afValue in afValues)
                        {
                            var line = afValue.Timestamp.LocalTime + "," + afValue.Value + "," + afValue.PIPoint.Name;
                            writer.WriteLine(line);
                        }
                    }
                });





            }

            _logger.InfoFormat("Datawriter completed.");
        }






        public void Dispose()
        {

        }
    }
}