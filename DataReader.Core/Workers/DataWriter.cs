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
using OSIsoft.AF.Asset;

namespace DataReader.Core
{
    /// <summary>
    /// This class
    /// </summary>
    public class DataWriter : TaskBase
    {

        public readonly BlockingCollection<List<AFValues>> DataQueue = new BlockingCollection<List<AFValues>>();
        private readonly CancellationTokenSource _cancellationToken = new CancellationTokenSource();
        private string _outputFileName;
        private int _eventsPerFile;


        public DataWriter(string outputFileName, int eventsPerFile)
        {
            _outputFileName = outputFileName;
            _eventsPerFile = eventsPerFile;
        }

        public override void Stop()
        {
            DataQueue.CompleteAdding();
            base.Stop();
        }

        protected override void DoTask(CancellationToken cancelToken)
        {
            WriteData(_cancellationToken.Token, _outputFileName, _eventsPerFile);
        }

        private void WriteData(CancellationToken cancelToken, string fileName, int eventsPerFile)
        {

            List<AFValue> remainingValues = new List<AFValue>();

            List<Task> writeTask = new List<Task>();

            _logger.InfoFormat("Writing data task started...");



            // gets currently available values from the queue
            foreach (var afValues in DataQueue.GetConsumingEnumerable(cancelToken))
            {

                var values = new List<AFValue>();
                values.AddRange(remainingValues);
                remainingValues.Clear();

                values.AddRange(afValues.SelectMany(v => v));

                if (values.Count > 0)
                {
                    var subsets = values.ChunkBy(_eventsPerFile);
                    for (int index = 0; index < subsets.Count - 1; index++)
                    {
                        var subset = subsets[index];
                        if (subset.Count == eventsPerFile)
                            writeTask.Add(WriteValues(subset, fileName));
                        else
                        {
                            // it could be that the last element does not contain the required nuber of events
                            remainingValues.AddRange(subset);
                        }
                    }
                }



                if (DataQueue.IsAddingCompleted && remainingValues.Count > 0)
                {
                    writeTask.Add(WriteValues(remainingValues, fileName));
                    remainingValues.Clear();
                }

                if (DataQueue.IsAddingCompleted || cancelToken.IsCancellationRequested)
                {
                    _logger.InfoFormat("Datawriter completing, waiting for remaining files to be written to complete.");
                    Task.WaitAll(writeTask.ToArray());

                }

                writeTask.RemoveAll(t => t.IsCompleted);

            }

            _logger.InfoFormat("Datawriter completed.");
        }

        private Task WriteValues(List<AFValue> allValues, string fileName)
        {

            var fullFileNAme = fileName + "_" + DateTime.Now.ToIsoReadable() + ".csv";

            _logger.InfoFormat("Writing {0} values into text file {1}", allValues.Count, fullFileNAme);

            var text = new StringBuilder();

            allValues.ForEach(v => text.AppendLine(v.Timestamp.LocalTime + "," + v.Value + "," + v.PIPoint.Name));
            
            allValues.Clear();

            return WriteTextAsync(fullFileNAme, text.ToString());
        }

        static async Task WriteTextAsync(string filePath, string text)
        {
            byte[] encodedText = Encoding.Unicode.GetBytes(text);

            using (FileStream sourceStream = new FileStream(filePath,
                FileMode.Create, FileAccess.Write, FileShare.None,
                bufferSize: 4096, useAsync: true))
            {
                await sourceStream.WriteAsync(encodedText, 0, encodedText.Length);
            };
        }
    }
}