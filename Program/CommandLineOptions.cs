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
        [OptionArray('s', "server", HelpText = "PI Data Archive Server name to connect to.  You can connect to a specific collective member by passing 2 strings: [collectiveName] [memberName], names must be same as the ones defined in the KST.",Required = true)]
        public string[] Server { get; set; }

        [OptionArray('t', "tagQueries", HelpText = "Queries to load the tags, the more you add the best and the sooner that app will start reading data. This option accepts many queries separeted by a space. e.g. sinus* SSN_NP60* \"tag:<>sin* DataType:Float\"")]
        public string[] TagQueries { get; set; }

        [OptionArray("testTagSearch", HelpText = "Makes a serch will all passed filters and prints the results to the screen. e.g. sinus* SSN_NP60* \"tag:<>sin* DataType:Float\"", MutuallyExclusiveSet = "TestTagSearch")]
        public string[] testTagSearch { get; set; }

        [Option("printTags", HelpText = "Print all tag names when doing the testTagSearch", MutuallyExclusiveSet = "TestTagSearch")]
        public bool testTagSearchPrintAllTags { get; set; }

        [Option("estimatedEventsPerDay", HelpText = "provides an estimate of the number of events per tag per day, to help optimising the speed of reading", DefaultValue = 4)]
        public int EventsPerDay { get; set; }

        [Option("estimatedTagsCount", HelpText = "estimate of the total number of tags that will be read, this will also help optimizing the application", DefaultValue = 10000)]
        public int TagsCount { get; set; }


        //[Option('p',"parallel", HelpText = "Gather data using parallel calls instead of bulk-Parrallel.  This is another good performing technique, it uses more network calls though.  depending on your network this may or may not give good performances.")]
        //public bool UseParallel { get; set; }

        [Option("eventsPerRead", HelpText = "Defines how many events should be read per data call.", DefaultValue = 10000)]
        public int EventsPerRead { get; set; }


        [Option("st", HelpText = "Start Time to query data", DefaultValue = "*-1d")]
        public string StartTime { get; set; }

        [Option("et", HelpText = "End Time to query data", DefaultValue = "*")]
        public string EndTime { get; set; }
        
        // options related to write

        [Option("enableWrite", HelpText = "Outputs the data into text files", DefaultValue = false, MutuallyExclusiveSet = "WriteData")]
        public bool EnableWrite { get; set; }

        [Option("writersCount", HelpText = "Defines the numbers of files writers that will runs simultaneously.", DefaultValue = 4, MutuallyExclusiveSet = "WriteData")]
        public int WritersCount { get; set; }

        [Option("outFileName",HelpText ="file name to output data.  Works with the EnableWrite option. A datetime and a .csv extension wil appended the the name.  ex: c:\\temp\\data would suffice",MutuallyExclusiveSet = "WriteData")]
        public string OutfileName { get; set; }

        [Option("eventsPerFile", HelpText = "Number of events to write per file", DefaultValue = 50000, MutuallyExclusiveSet = "WriteData")]
        public int EventsPerFile { get; set; }




        [HelpOption]
        public string GetUsage()
        {
            
            return HelpText.AutoBuild(this,
                (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }
}