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
using System.Collections.Generic;
using CommandLine;
using CommandLine.Text;


namespace DataReader.CommandLine
{
    /// <summary>
    /// Base class for common options shared across all verbs
    /// </summary>
    public abstract class CommonOptions
    {
        [Option('s', "server", Required = true, Separator = ' ', HelpText = "PI Data Archive Server name to connect to. You can connect to a specific collective member by passing 2 strings: [collectiveName] [memberName]")]
        public IEnumerable<string> Server { get; set; }

        [Option('t', "tagQueries", Separator = ' ', HelpText = "Queries to load the tags. Accepts multiple queries separated by space. e.g. sinus* SSN_NP60* \"tag:<>sin* DataType:Float\"")]
        public IEnumerable<string> TagQueries { get; set; }

        [Option("tagFile", HelpText = "Path to a text file containing tag names or queries, one per line. Use this when you have many tags to avoid command line length limits.")]
        public string TagFile { get; set; }

        [Option("st", Default = "*-1d", HelpText = "Start Time to query data")]
        public string StartTime { get; set; }

        [Option("et", Default = "*", HelpText = "End Time to query data")]
        public string EndTime { get; set; }

        [Option("estimatedEventsPerDay", Default = 4, HelpText = "Estimated number of events per tag per day, to help optimize reading speed")]
        public int EventsPerDay { get; set; }

        [Option("estimatedTagsCount", Default = 10000, HelpText = "Estimated total number of tags to read, to help optimize the application")]
        public int TagsCount { get; set; }

        [Option("enableWrite", Default = false, HelpText = "Outputs the data into CSV files")]
        public bool EnableWrite { get; set; }

        [Option("outFileName", HelpText = "File name to output data. A datetime and .csv extension will be appended. Example: C:\\temp\\data")]
        public string OutfileName { get; set; }

        [Option("writersCount", Default = 4, HelpText = "Number of file writers to run simultaneously")]
        public int WritersCount { get; set; }

        [Option("eventsPerFile", Default = 500000, HelpText = "Number of events to write per file")]
        public int EventsPerFile { get; set; }
    }

    /// <summary>
    /// Options for extracting raw archived data from PI tags
    /// </summary>
    [Verb("raw", isDefault: true, HelpText = "Extract raw archived values from PI tags")]
    public class RawDataOptions : CommonOptions
    {
        [Option("eventsPerRead", Default = 10000, HelpText = "Number of events to read per data call")]
        public int EventsPerRead { get; set; }

        [Option("removeDuplicates", HelpText = "Output values will not contain duplicated values")]
        public bool RemoveDuplicates { get; set; }

        [Option("filterDigitalStates", HelpText = "Output values will not contain digital states")]
        public bool FilterDigitalStates { get; set; }

        [Usage(ApplicationAlias = "DataReader.exe")]
        public static IEnumerable<Example> Examples
        {
            get
            {
                return new List<Example>() {
                    new Example("Read raw data for the last 7 days", new RawDataOptions { Server = new[] { "PIServer01" }, TagQueries = new[] { "sinus*" }, StartTime = "*-7d", EndTime = "*", EnableWrite = true, OutfileName = "C:\\temp\\rawdata" }),
                    new Example("Read raw data from file", new RawDataOptions { Server = new[] { "PIServer01" }, TagFile = "tags.txt", StartTime = "*-30d", EndTime = "*", EnableWrite = true, OutfileName = "C:\\temp\\data" }),
                    new Example("Read raw data without duplicates", new RawDataOptions { Server = new[] { "PIServer01" }, TagQueries = new[] { "PointSource:=OPC" }, StartTime = "*-7d", EndTime = "*", RemoveDuplicates = true, FilterDigitalStates = true, EnableWrite = true, OutfileName = "C:\\temp\\filtered" })
                };
            }
        }
    }

    /// <summary>
    /// Options for extracting summary/aggregate data from PI tags
    /// </summary>
    [Verb("summary", HelpText = "Extract calculated summaries (average, min, max, totals, etc.) over specified intervals")]
    public class SummaryDataOptions : CommonOptions
    {
        [Option("summaryTypes", Default = "Average,Minimum,Maximum", HelpText = "Summary types to calculate: Total, Average, Minimum, Maximum, Range, StdDev, PopulationStdDev, Count, PercentGood, TotalWithUOM, All, AllForNonNumeric. Use comma-separated values for multiple types.")]
        public string SummaryTypes { get; set; }

        [Option("summaryInterval", Default = "1d", HelpText = "Interval duration for each summary calculation. Examples: '1d' (1 day), '1h' (1 hour), '30m' (30 minutes), '15s' (15 seconds)")]
        public string SummaryInterval { get; set; }

        [Option("calculationBasis", Default = "TimeWeighted", HelpText = "Method for evaluating data: TimeWeighted, EventWeighted, TimeWeightedContinuous, TimeWeightedDiscrete, EventWeightedExcludeMostRecentEvent, EventWeightedExcludeEarliestEvent, EventWeightedIncludeBothEnds")]
        public string CalculationBasis { get; set; }

        [Option("timestampCalculation", Default = "Auto", HelpText = "Timestamp to return for each summary: Auto, EarliestTime, MostRecentTime")]
        public string TimestampCalculation { get; set; }

        [Option("intervalsPerBatch", Default = 0, HelpText = "Number of summary intervals to process per batch. If 0 (default), automatically calculated to target ~10,000 events per batch. For 233 tags with 10 summary types and 1-day intervals, use 2-4 intervals. Higher values = larger batches = fewer API calls but more memory.")]
        public int IntervalsPerBatch { get; set; }

        [Option("tagsChunkSize", Default = 50, HelpText = "Number of tags to process per parallel chunk. Tags are split into chunks of this size and processed in parallel (max 4 threads) within each time batch. Default: 50.")]
        public int TagsChunkSize { get; set; }

        [Usage(ApplicationAlias = "DataReader.exe")]
        public static IEnumerable<Example> Examples
        {
            get
            {
                return new List<Example>() {
                    new Example("Extract daily averages for last 30 days", new SummaryDataOptions { Server = new[] { "PIServer01" }, TagQueries = new[] { "tag:=Reactor*" }, StartTime = "*-30d", EndTime = "*", SummaryTypes = "Average,Minimum,Maximum", SummaryInterval = "1d", EnableWrite = true, OutfileName = "C:\\temp\\daily_summary" }),
                    new Example("Calculate hourly totals", new SummaryDataOptions { Server = new[] { "PIServer01" }, TagQueries = new[] { "tag:=Flow*" }, StartTime = "*-7d", EndTime = "*", SummaryTypes = "Total", SummaryInterval = "1h", CalculationBasis = "TimeWeighted", EnableWrite = true, OutfileName = "C:\\temp\\hourly_totals" }),
                    new Example("All summaries with custom timestamp", new SummaryDataOptions { Server = new[] { "PIServer01" }, TagFile = "tags.txt", StartTime = "2024-01-01", EndTime = "2024-01-31", SummaryTypes = "All", SummaryInterval = "1d", TimestampCalculation = "MostRecentTime", EnableWrite = true, OutfileName = "C:\\temp\\complete_summary" })
                };
            }
        }
    }

    /// <summary>
    /// Options for testing tag search queries
    /// </summary>
    [Verb("test", HelpText = "Test tag search queries to see which tags will be found")]
    public class TestTagSearchOptions
    {
        [Option('s', "server", Required = true, Separator = ' ', HelpText = "PI Data Archive Server name to connect to")]
        public IEnumerable<string> Server { get; set; }

        [Option('q', "queries", Required = true, Separator = ' ', HelpText = "Tag queries to test. e.g. sinus* \"tag:<>sin* DataType:Float\"")]
        public IEnumerable<string> Queries { get; set; }

        [Option("printTags", HelpText = "Print all tag names found by the queries")]
        public bool PrintTags { get; set; }

        [Usage(ApplicationAlias = "DataReader.exe")]
        public static IEnumerable<Example> Examples
        {
            get
            {
                return new List<Example>() {
                    new Example("Test tag queries", new TestTagSearchOptions { Server = new[] { "PIServer01" }, Queries = new[] { "sinus*", "cdt*" } }),
                    new Example("Test and print all matching tags", new TestTagSearchOptions { Server = new[] { "PIServer01" }, Queries = new[] { "tag:=Unit1* AND Location1:=1" }, PrintTags = true })
                };
            }
        }
    }
}