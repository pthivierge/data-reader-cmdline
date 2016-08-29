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
using DataReader.Core.Filters;
using DataReader.Core.Helpers;
using OSIsoft.AF.Asset;

namespace DataReader.Core
{



    /// <summary>
    ///     This class
    /// </summary>
    public class DataWriter : TaskBase, IDisposable
    {
        private string _baseOutputFileName = null;

        public readonly BlockingCollection<WriteInfo> DataQueue =
            new BlockingCollection<WriteInfo>();

        private readonly List<FileWriter> writers = new List<FileWriter>();

        public DataWriter(string outputFileName, int eventsPerFile, int writersCount)
        {
            _baseOutputFileName = outputFileName;

            // here we create the instances of the writers we need
            for (int i = 1; i < writersCount + 1; i++)
            {
                writers.Add(new FileWriter(eventsPerFile, i.ToString()));
            }


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
            foreach (var writeInfo in DataQueue.GetConsumingEnumerable(cancelToken))
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
                        try
                        {

                       

                        var fileName = _baseOutputFileName + "_" + writeInfo.ChunkId + "_" +
                                       writeInfo.StartTime.ToLocalTime().ToIsoReadable() + " to " + writeInfo.EndTime.ToLocalTime().ToIsoReadable();

                        writer.SetName(fileName);

                        var filters = new IDataFilter[]
                        {
                            new DigitalStatesFilter(),
                            new DuplicateValuesFilter(),
                        };

                        foreach (var afValues in writeInfo.Data)
                        {
                            foreach (var afValue in afValues)
                            {

                                // if you need to check data - we can do it here
                                var isFiltered = false;
                                foreach (var filter in filters)
                                {
                                    isFiltered = filter.IsFiltered(afValue);

                                    if (isFiltered)
                                    {
                                        break;
                                    }
                                }


                                if (!isFiltered)
                                {
                                    var line = afValue.Timestamp.LocalTime + "," + afValue.Value + "," + afValue.PIPoint.Name;

                                    writer.WriteLine(line);
                                }


                            }
                        }

                        }
                        catch (Exception ex)
                        {
                            _logger.Error(ex);
                        }

                    }, cancelToken);
                }
                else
                {
                    _logger.Error(
                        "DataWriter encounterd a null, FileWriter.  This situation should never occur, please report the issue.");
                }
            }

            _logger.InfoFormat("Datawriter completed.");
        }
    }
}