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

using CommandLine;
using CommandLine.Text;


namespace DataReader.CommandLine
{
    /// <summary>
    ///     see http://commandline.codeplex.com/
    ///     see https://github.com/gsscoder/commandline/wiki
    /// </summary>
    public class CommandLineOptions
    {
        [Option('s', "server", HelpText = "PI Data Archive Server name to connect",Required = true)]
        public string Server { get; set; }

        [Option("st", HelpText = "Start Time to query data", DefaultValue = "*-1d")]
        public string StartTime { get; set; }

        [Option("et", HelpText = "End Time to query data", DefaultValue = "*")]
        public string EndTime { get; set; }

        [Option('i', "intervalsCount", HelpText = "Splits the main interval into sub-intervals. i.e. 30d span / 30 (intervalsCount) = 30 calls for 1 day.", DefaultValue = 1)]
        public int Intervals { get; set; }

        [Option('p', "plotValuesIntervals", HelpText = "Number of intervalls to pass to the plotvalues calls", DefaultValue = 1024)]
        public int PlotValuesIntervals { get; set; }

        [Option("enableWrite", HelpText = "Outputs the data into text files", DefaultValue = false, MutuallyExclusiveSet = "WriteData")]
        public bool EnableWrite { get; set; }

        [Option("outFileName",HelpText ="file name to output data.  Works with the EnableWrite option. A datetime and a .csv extension wil appended the the name.  ex: c:\\temp\\data would suffice",MutuallyExclusiveSet = "WriteData")]
        public string OutfileName { get; set; }

        [Option("eventsPerFile", HelpText = "Number of events to write per file", DefaultValue = 50000, MutuallyExclusiveSet = "WriteData")]
        public int EventsPerFile { get; set; }

        [Option('q', "queryMode", HelpText = "Mode of data query. Currenlty allowed values: Bulk", DefaultValue = "Bulk")]
        public string Mode { get; set; }

        [OptionArray('t', "tagsList", HelpText = "List of tags to query data for default: sinusoid cdt158", DefaultValue = new[] { "sinusoid", "cdt158" })]
        public string[] Tags { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            
            return HelpText.AutoBuild(this,
                (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }
}