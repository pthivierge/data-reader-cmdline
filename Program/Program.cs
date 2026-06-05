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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;
using DataReader.Core;
using NLog;
using OSIsoft.AF.Data;
using OSIsoft.AF.Time;

namespace DataReader.CommandLine
{
    /// <summary>
    ///     Command line application to make the data extraction
    /// <example>
    /// datareader.exe --server PIServer01 --testTagSearch "tag:=Unit1* AND Location1:=1 AND PointSource:=OPC" --printTags
    /// </example>
    /// </summary>
    internal class Program
    {

        private static void Main(string[] args)
        {
            var _logger = LogManager.GetCurrentClassLogger();

            try
            {
                var parser = new Parser(with => with.HelpWriter = null);
                var parserResult = parser.ParseArguments<RawDataOptions, SummaryDataOptions, TestTagSearchOptions>(args);
                
                parserResult
                    .WithParsed<RawDataOptions>(options => RunRawDataExtraction(options, _logger))
                    .WithParsed<SummaryDataOptions>(options => RunSummaryDataExtraction(options, _logger))
                    .WithParsed<TestTagSearchOptions>(options => RunTestTagSearch(options, _logger))
                    .WithNotParsed(errs => DisplayHelp(parserResult, errs, _logger));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, ex.Message);
                Environment.Exit(1);
            }
        }

        private static void DisplayHelp<T>(ParserResult<T> result, IEnumerable<global::CommandLine.Error> errs, Logger _logger)
        {
            var helpText = HelpText.AutoBuild(result, h =>
            {
                h.AdditionalNewLineAfterOption = false;
                h.Heading = "DataReader Command Line Tool";
                h.Copyright = "Copyright (c) 2016-2026 Patrice Thivierge";
                return HelpText.DefaultParsingErrorsHandler(result, h);
            }, e => e, verbsIndex: true);
            
            Console.WriteLine(helpText);
            _logger.Error("Command line parsing failed. See help above.");
            Environment.Exit(1);
        }

        private static void RunTestTagSearch(TestTagSearchOptions options, Logger _logger)
        {
            try
            {
                var serverArray = options.Server.ToArray();
                PIConnection piConnection;
                
                if (serverArray.Length == 1)
                    piConnection = new PIConnection(serverArray[0]);
                else
                    piConnection = new PIConnection(serverArray[0], serverArray[1]);

                _logger.Info("Tag search test started...");
                piConnection.Connect();

                var search = new TagsLoader(piConnection.GetPiServer());
                var queriesArray = options.Queries.ToArray();
                
                foreach (var query in queriesArray)
                {
                    var tags = search.Search(query).ToList();
                    _logger.Warn("Found {0} tags with query: {1}", tags.Count, query);

                    if (options.PrintTags)
                    {
                        tags.ForEach(t => _logger.Info("  Tag: {0}, PointClass: {1}", t.Name, t.PointClass));
                    }
                }

                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, ex.Message);
                Environment.Exit(1);
            }
        }

        private static void RunRawDataExtraction(RawDataOptions options, Logger _logger)
        {
            try
            {
                ValidateCommonSettings(options);

                var readerSettings = new DataReaderSettings();
                var piConnection = ConnectToServer(options.Server.ToArray());

                if (options.EventsPerDay > 0 && options.TagsCount > 0)
                {
                    var type = DataReaderSettings.ReadingType.Bulk;
                    readerSettings.AutoTune(type, options.EventsPerDay, options.TagsCount, options.EventsPerRead);
                }

                // Setup data filters
                var filtersFactory = new FiltersFactory();
                if (options.RemoveDuplicates)
                {
                    filtersFactory.AddFilter(new DuplicateValuesFilter());
                }

                if (options.FilterDigitalStates)
                {
                    filtersFactory.AddFilter(new SystemStatesFilter());
                }

                _logger.Info("Creating worker objects for RAW data extraction...");
                var dataWriter = new DataWriter(options.OutfileName, options.EventsPerFile, options.WritersCount, filtersFactory);
                var dataReader = new DataReaderBulk(readerSettings, dataWriter, options.EnableWrite);

                ExecuteDataExtraction(options, piConnection, readerSettings, dataReader, dataWriter, _logger);

                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, ex.Message);
                Environment.Exit(1);
            }
        }

        private static void RunSummaryDataExtraction(SummaryDataOptions options, Logger _logger)
        {
            try
            {
                ValidateCommonSettings(options);

                var readerSettings = new DataReaderSettings();
                var piConnection = ConnectToServer(options.Server.ToArray());

                // For summary mode, TagGroupSize controls how many tags TagsLoader sends at once
                // Default behavior is fine - it will send all tags together for optimal batching
                
                // Calculate optimal TimeIntervalPerDataRequest based on summary interval
                // This controls the Orchestrator's time-slicing, not the internal summary batching
                var summaryInterval = AFTimeSpan.Parse(options.SummaryInterval);
                TimeSpan intervalTimeSpan = summaryInterval.ToTimeSpan();
                int summaryTypesCount = CountSummaryTypes(options.SummaryTypes);
                
                // If tagFile is provided, count actual tags from file instead of using estimate
                int actualOrEstimatedTagCount = options.TagsCount;
                if (!string.IsNullOrEmpty(options.TagFile) && File.Exists(options.TagFile))
                {
                    actualOrEstimatedTagCount = File.ReadAllLines(options.TagFile)
                        .Count(line => !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith("#"));
                    _logger.Info("Counted {0} tags from file (ignoring estimatedTagsCount parameter)", actualOrEstimatedTagCount);
                }
                
                // Target 30000 events per request for optimal batching
                // IMPORTANT: this must be an *integer* number of summary intervals per request.
                // If we use a fractional number of intervals (via seconds math), Orchestrator boundaries drift
                // (e.g., 19:00 becomes 22:46), which creates partial summary intervals.
                double intervalsPerRequestRaw = 30000.0 / (actualOrEstimatedTagCount * summaryTypesCount);
                var intervalsPerRequest = Math.Max(1, (int)Math.Floor(intervalsPerRequestRaw));

                double requestSeconds = Math.Abs(intervalTimeSpan.TotalSeconds) * intervalsPerRequest;
                readerSettings.TimeIntervalPerDataRequest = TimeSpan.FromSeconds(requestSeconds);

                _logger.Info(
                    "Summary mode: {0} summary types, interval={1}, tags={2}, TimeIntervalPerDataRequest={3:F2} days ({4} intervals; raw={5:F2})",
                    summaryTypesCount, summaryInterval, actualOrEstimatedTagCount,
                    readerSettings.TimeIntervalPerDataRequest.TotalDays, intervalsPerRequest, intervalsPerRequestRaw);

                _logger.Info("Creating worker objects for SUMMARY data extraction...");
                var dataWriter = new DataWriter(options.OutfileName, options.EventsPerFile, options.WritersCount, null);
                
                // Pass intervalsPerBatch (0 means auto-calculate, >0 means use custom value)
                // Also pass tagsChunkSize for parallel tag chunking
                var dataReader = new DataReaderSummary(
                    readerSettings,
                    dataWriter,
                    options.EnableWrite,
                    options.SummaryTypes,
                    options.SummaryInterval,
                    options.CalculationBasis,
                    options.TimestampCalculation,
                    options.IntervalsPerBatch > 0 ? (int?)options.IntervalsPerBatch : null,
                    options.TagsChunkSize);

                ExecuteDataExtraction(options, piConnection, readerSettings, dataReader, dataWriter, _logger, summaryInterval, summaryTypesCount);

                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, ex.Message);
                Environment.Exit(1);
            }
        }

        private static PIConnection ConnectToServer(string[] serverArray)
        {
            PIConnection piConnection;
            if (serverArray.Length == 1)
                piConnection = new PIConnection(serverArray[0]);
            else
                piConnection = new PIConnection(serverArray[0], serverArray[1]);

            piConnection.Connect();
            return piConnection;
        }

        private static void ExecuteDataExtraction(CommonOptions options, PIConnection piConnection, DataReaderSettings readerSettings, IDataReader dataReader, DataWriter dataWriter, Logger _logger, AFTimeSpan? summaryInterval = null, int summaryTypesCount = 0)
        {
            // TimeIntervalPerDataRequest has already been calculated with actual tag count from file
            // No need to recalculate here
            var orchestrator = new Orchestrator(options.StartTime, options.EndTime,
                readerSettings.TimeIntervalPerDataRequest, dataReader);

            // Combine tag queries from command line and file
            var allTagQueries = new List<string>();
            var tagQueriesArray = options.TagQueries != null ? options.TagQueries.ToArray() : null;
            
            if (tagQueriesArray != null && tagQueriesArray.Length > 0)
            {
                allTagQueries.AddRange(tagQueriesArray);
            }

            if (!string.IsNullOrEmpty(options.TagFile))
            {
                _logger.Info("Reading tags from file: {0}", options.TagFile);
                var fileQueries = File.ReadAllLines(options.TagFile)
                    .Where(line => !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith("#"))
                    .Select(line => line.Trim())
                    .ToArray();
                _logger.Info("Loaded {0} tag queries from file", fileQueries.Length);
                allTagQueries.AddRange(fileQueries);
            }

            var tagsLoader = new TagsLoader(piConnection.GetPiServer(), allTagQueries.ToArray(),
                readerSettings.TagGroupSize, orchestrator);

            var statistics = new Statistics();

            // Start all workers
            _logger.Info("Starting workers...");
            var tagsLoaderTask = tagsLoader.Run();
            var writerTask = dataWriter.Run();
            var orchestratorTask = orchestrator.Run();
            var dataReaderTask = dataReader.Run();
            var statsTask = statistics.Run();

            // Wait for completion
            Task.WaitAll(orchestratorTask, writerTask, dataReaderTask, tagsLoaderTask);

            statistics.Stop();
            Task.WaitAll(statsTask);

            _logger.Info("All tasks completed successfully");
        }

        private static void ValidateCommonSettings(CommonOptions options)
        {
            // if write is enabled, file name is required
            if (options.EnableWrite)
            {
                if (string.IsNullOrEmpty(options.OutfileName))
                    throw new Exception("--outFileName parameter must be provided when --enableWrite is provided");

                if (!Directory.Exists(Path.GetDirectoryName(options.OutfileName) ?? ""))
                    throw new DirectoryNotFoundException("The directory does not exist for the file that is provided as --outFileName parameter");
            }

            // validate tag file exists if specified
            if (!string.IsNullOrEmpty(options.TagFile))
            {
                if (!File.Exists(options.TagFile))
                    throw new FileNotFoundException("The tag file specified with --tagFile does not exist", options.TagFile);
            }

            // must have either tag queries or tag file
            if ((options.TagQueries == null || !options.TagQueries.Any()) &&
                string.IsNullOrEmpty(options.TagFile))
            {
                throw new Exception("You must provide either --tagQueries or --tagFile parameter");
            }
        }

        private static int CountSummaryTypes(string summaryTypesString)
        {
            if (string.IsNullOrWhiteSpace(summaryTypesString))
                return 3; // Average, Minimum, Maximum

            var parts = summaryTypesString.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int count = 0;

            foreach (var part in parts)
            {
                AFSummaryTypes parsedType;
                if (Enum.TryParse(part.Trim(), true, out parsedType))
                {
                    if (parsedType == AFSummaryTypes.All)
                    {
                        return 14; // All standard summary types
                    }
                    else if (parsedType == AFSummaryTypes.AllForNonNumeric)
                    {
                        return 2; // Count and PercentGood
                    }
                    else if (parsedType != AFSummaryTypes.None)
                    {
                        foreach (AFSummaryTypes value in Enum.GetValues(typeof(AFSummaryTypes)))
                        {
                            if (value != AFSummaryTypes.None && value != AFSummaryTypes.All && 
                                value != AFSummaryTypes.AllForNonNumeric && (parsedType & value) == value)
                            {
                                int intValue = (int)value;
                                if (intValue > 0 && (intValue & (intValue - 1)) == 0)
                                    count++;
                            }
                        }
                    }
                }
            }

            return Math.Max(1, count);
        }
    }
}