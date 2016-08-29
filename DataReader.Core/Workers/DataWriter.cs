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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataReader.Core.Helpers;
using OSIsoft.AF.Asset;

namespace DataReader.Core
{
    /// <summary>
    ///     This class
    /// </summary>
    public class DataWriter : TaskBase, IDisposable
    {
        public readonly BlockingCollection<IEnumerable<AFValues>> DataQueue =
            new BlockingCollection<IEnumerable<AFValues>>();

        private readonly List<FileWriter> writers = new List<FileWriter>();


        public DataWriter(string outputFileName, int eventsPerFile)
        {
            writers.Add(new FileWriter(eventsPerFile, outputFileName, "1"));
            writers.Add(new FileWriter(eventsPerFile, outputFileName, "2"));
            writers.Add(new FileWriter(eventsPerFile, outputFileName, "3"));
            writers.Add(new FileWriter(eventsPerFile, outputFileName, "4"));
        }


        public void Dispose()
        {
        }

        public override void Stop()
        {
            DataQueue.CompleteAdding();
            base.Stop();
        }

        protected override void DoTask(CancellationToken cancelToken)
        {
            _logger.InfoFormat("Writing data task started...");


            // gets currently available values from the queue
            foreach (var valuesList in DataQueue.GetConsumingEnumerable(cancelToken))
            {
                // find an available writer to write the results into a file
                var writer =
                    writers.FirstOrDefault(
                        w =>
                            w.ActiveTask == null ||
                            (w.ActiveTask.IsCompleted && w.ActiveTask.Status != TaskStatus.WaitingForActivation));


                // incase no writer was available, null was returned, here we wait until we get an available writer
                if (writer == null)
                {
                    Task.WaitAny(writers.Select(w => w.ActiveTask).ToArray());
                    writer =
                        writers.FirstOrDefault(
                            w =>
                                w.ActiveTask == null ||
                                (w.ActiveTask.IsCompleted && w.ActiveTask.Status != TaskStatus.WaitingForActivation));
                }


                // safety check to avoid null, at this point this should neve occur
                if (writer != null)
                {

                    writer.ActiveTask = Task.Run(() =>
                    {
                        foreach (var afValues in valuesList)
                        {
                            foreach (var afValue in afValues)
                            {
                            
                            // if you need to check data - we can do it here

                            var line = afValue.Timestamp.LocalTime + "," + afValue.Value + "," + afValue.PIPoint.Name;
                                writer.WriteLine(line);
                            }
                        }
                    }, cancelToken);
                }
                else
                {
                    _logger.Error("DataWriter encounterd a null, FileWriter.  This situation should never occur, please report the issue.");
                }
            }

            _logger.InfoFormat("Datawriter completed.");
        }
    }
}