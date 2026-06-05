#region Copyright
//  Copyright 2016 Patrice Thivierge F.
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
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OSIsoft.AF.Asset;

namespace DataReader.Core
{

    /// <summary>
    ///     This class
    /// </summary>
    public class DataWriter : TaskBase
    {
        private class ValueWithIndex
        {
            public AFValue Value { get; set; }
            public int Index { get; set; }
        }

        private string _baseOutputFileName = null;
        FiltersFactory _filtersFactory;

        private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
        private readonly string _decimalSeparator;
        private readonly string _listSeparator;

        /// <summary>
        /// Escapes and quotes a CSV field according to RFC 4180
        /// </summary>
        private static string QuoteCsvField(string field)
        {
            if (field == null)
                return "\"\"";
            
            // Escape double quotes by doubling them
            string escaped = field.Replace("\"", "\"\"");
            
            // Wrap in double quotes
            return "\"" + escaped + "\"";
        }

        public readonly BlockingCollection<WriteInfo> DataQueue =
            new BlockingCollection<WriteInfo>();

        private readonly List<FileWriter> writers = new List<FileWriter>();
        

        public DataWriter(string outputFileName, int eventsPerFile, int writersCount, FiltersFactory filtersFactory)
        {

           _decimalSeparator = _culture.NumberFormat.NumberDecimalSeparator;
        _listSeparator = _culture.TextInfo.ListSeparator;

        _baseOutputFileName = outputFileName;
            _filtersFactory = filtersFactory;

            // here we create the instances of the writers we need
            for (int i = 1; i < writersCount + 1; i++)
            {
                writers.Add(new FileWriter(eventsPerFile, i.ToString()));
            }


        }

        public override void Stop()
        {
            DataQueue.CompleteAdding();
            base.Stop();
        }

        protected override void DoTask(CancellationToken cancelToken)
        {
            _logger.Info("Writing data task started...");



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

                            IDataFilter[] dataFilters=null;

                            // Create time-sortable filename: start with timestamp, then add identifiers for uniqueness
                            // Format: {base}_{startTime}_{chunkId}_{subChunkId}[_summary]_w{writer}.csv
                            // This ensures chronological sorting while maintaining uniqueness
                            var fileName = string.Format("{0}_{1}_{2}_{3}",
                                _baseOutputFileName,
                                writeInfo.StartTime.ToString("yyyy-MM-dd_HH-mm-ss"),
                                writeInfo.ChunkId,
                                writeInfo.SubChunkId);
                            
                            if (writeInfo.IsSummaryData)
                            {
                                fileName += "_summary";
                            }

                            writer.SetName(fileName);


                            if (_filtersFactory != null)
                                dataFilters = _filtersFactory.GetFilters();

                            if (writeInfo.IsSummaryData && writeInfo.Metadata != null)
                            {
                                writer.WriteLine("# Summary Data Export");
                                writer.WriteLine(string.Format("# Generated (Local Time): {0}", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz")));
                                foreach (var kvp in writeInfo.Metadata)
                                {
                                    writer.WriteLine(string.Format("# {0}: {1}", kvp.Key, kvp.Value));
                                }
                                writer.WriteLine(QuoteCsvField("Timestamp") + _listSeparator + QuoteCsvField("Value") + _listSeparator + QuoteCsvField("TagName") + _listSeparator + QuoteCsvField("AggregateType"));
                            }

                            // Summary export: use stable row DTOs when provided
                            if (writeInfo.IsSummaryData && writeInfo.SummaryRecords != null)
                            {
                                var rows = writeInfo.SummaryRecords.ToList();

                                rows.Sort((a, b) =>
                                {
                                    int cmp = a.TimestampLocal.CompareTo(b.TimestampLocal);
                                    if (cmp != 0) return cmp;
                                    cmp = string.CompareOrdinal(a.TagName ?? string.Empty, b.TagName ?? string.Empty);
                                    if (cmp != 0) return cmp;
                                    return string.CompareOrdinal(a.AggregateType ?? string.Empty, b.AggregateType ?? string.Empty);
                                });

                                foreach (var r in rows)
                                {
                                    var line = QuoteCsvField(r.TimestampLocal.ToString("yyyy-MM-ddTHH:mm:sszzz")) + _listSeparator +
                                               QuoteCsvField(r.ValueString ?? string.Empty) + _listSeparator +
                                               QuoteCsvField(r.TagName ?? "Unknown") + _listSeparator +
                                               QuoteCsvField(r.AggregateType ?? string.Empty);
                                    writer.WriteLine(line);
                                }

                                return;
                            }

                            // Collect all values with their metadata for sorting (raw export + legacy summary export)
                            var valuesToWrite = new List<ValueWithIndex>();
                            var valueIndex = 0;
                            foreach (var afValues in writeInfo.Data)
                            {
                                foreach (var afValue in afValues)
                                {
                                    valuesToWrite.Add(new ValueWithIndex { Value = afValue, Index = valueIndex });
                                    valueIndex++;
                                }
                            }

                            // Sort by timestamp ascending
                            valuesToWrite.Sort((a, b) => a.Value.Timestamp.CompareTo(b.Value.Timestamp));

                            // Write sorted values
                            foreach (var item in valuesToWrite)
                            {
                                var afValue = item.Value;
                                var index = item.Index;

                                var isFiltered = CheckFilters(afValue, dataFilters);

                                if (!isFiltered)
                                {
                                    string tagName;
                                    string line;

                                    if (writeInfo.IsSummaryData && writeInfo.TagNames != null && writeInfo.TagNames.ContainsKey(index))
                                    {
                                        tagName = writeInfo.TagNames[index];
                                        string aggregateType = writeInfo.SummaryTypes != null && writeInfo.SummaryTypes.ContainsKey(index)
                                            ? writeInfo.SummaryTypes[index]
                                            : "";
                                        // Use Local time format with timezone offset for timestamps in CSV
                                        line = QuoteCsvField(afValue.Timestamp.LocalTime.ToString("yyyy-MM-ddTHH:mm:sszzz")) + _listSeparator +
                                               QuoteCsvField(afValue.Value != null ? afValue.Value.ToString() : "") + _listSeparator +
                                               QuoteCsvField(tagName) + _listSeparator +
                                               QuoteCsvField(aggregateType);
                                    }
                                    else if (afValue.PIPoint != null)
                                    {
                                        tagName = afValue.PIPoint.Name;
                                        line = QuoteCsvField(afValue.Timestamp.LocalTime.ToString("yyyy-MM-ddTHH:mm:sszzz")) + _listSeparator +
                                               QuoteCsvField(afValue.Value != null ? afValue.Value.ToString() : "") + _listSeparator +
                                               QuoteCsvField(tagName);
                                    }
                                    else
                                    {
                                        tagName = "Unknown";
                                        line = QuoteCsvField(afValue.Timestamp.LocalTime.ToString("yyyy-MM-ddTHH:mm:sszzz")) + _listSeparator +
                                               QuoteCsvField(afValue.Value != null ? afValue.Value.ToString() : "") + _listSeparator +
                                               QuoteCsvField(tagName);
                                    }

                                    writer.WriteLine(line);
                                }
                            }

                        }
                        catch (Exception ex)
                        {
                            _logger.Error(ex, ex.Message);
                        }

                    }, cancelToken);
                }
                else
                {
                    _logger.Error(
                        "DataWriter encounterd a null, FileWriter.  This situation should never occur, please report the issue.");
                }
            }

            // wait for any remaining task
            Task.WaitAll(writers.Where(w => w.ActiveTask!=null).Select(w=>w.ActiveTask).ToArray());

            // dispose the writers properly to flush the data 
            foreach (var writer in writers)
            {
                if (writer != null) writer.Dispose();
            }

            _logger.Info("Datawriter completed.");
        }

        private static bool CheckFilters(AFValue afValue, IDataFilter[] dataFilters)
        {
            if (dataFilters == null)
                return false;

            var isFiltered = false;
            foreach (var filter in dataFilters)
            {
                isFiltered = filter.IsFiltered(afValue);

                if (isFiltered)
                {
                    break;
                }
            }
            return isFiltered;
        }
    }
}