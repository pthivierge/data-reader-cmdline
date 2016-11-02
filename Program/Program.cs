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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using DataReader.Core;
using log4net;

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

        private static void ValidateSettings(CommandLineOptions options)
        {
            // if write is enabled, file name is required
            if (options.EnableWrite)
            {
                if(string.IsNullOrEmpty(options.OutfileName))
                    throw new Exception("--outFileName parameter must be provided when --enableWrite is provided");

                
                if (!Directory.Exists(Path.GetDirectoryName(options.OutfileName) ?? ""))
                    throw new DirectoryNotFoundException("The directory does not exist for the file that is provided as --outFileName parameter");


            }
        }

        private static void Main(string[] args)
        {
            PIConnection piConnection;
            var _logger = LogManager.GetLogger(typeof (Program));
            

            try
            {
                var options = new CommandLineOptions();


                if (Parser.Default.ParseArguments(args, options))
                {

                    ValidateSettings(options);

                    var readerSettings = new DataReaderSettings();

                    if(options.Server.Length==1)
                        piConnection = new PIConnection(options.Server[0]);
                    else
                        piConnection = new PIConnection(options.Server[0], options.Server[1]);

                    if (options.testTagSearch != null && options.testTagSearch.Length > 0)
                    {
                        _logger.Info("Search test started...");

                        piConnection.Connect();

                        var search = new TagsLoader(piConnection.GetPiServer());
                        foreach (var s in options.testTagSearch)
                        {
                            var tags = search.Search(s).ToList();
                            _logger.WarnFormat("Found {0} tags with query {1}", tags.Count, s);

                            if (options.testTagSearchPrintAllTags)
                            {
                                tags.ForEach(t => _logger.InfoFormat("Tag: {0}, PointClass: {1}", t.Name, t.PointClass));
                            }
                        }
                    }

                    if (options.TagQueries != null && options.TagQueries.Length > 0)
                    {
                        _logger.Info("Data reader starting...");

                        
                        piConnection.Connect();

                        if (options.EventsPerDay > 0 && options.TagsCount > 0)
                        {
                            // var type = options.UseParallel? DataReaderSettings.ReadingType.Parallel: DataReaderSettings.ReadingType.Bulk;
                            var type = DataReaderSettings.ReadingType.Bulk;
                            readerSettings.AutoTune(type, options.EventsPerDay, options.TagsCount, options.EventsPerRead);
                        }


                        // starts the data writer

                        // defines rejected states to filter out
                        var rejectedStates = new[]
                                                {
                            "pt created",
                            "snapfix",
                            "shutdown",
                            "no data",
                            "bad",
                            "No Alarm",
                            "High Alarm",
                            "Low Alarm",
                            "Hi Alarm/Ack",
                            "Lo Alarm/Ack",
                            "NoAlrm/UnAck",
                            "Bad Quality",
                            "Rate Alarm",
                            "Rate Alm/Ack",
                            "Dig Alarm",
                            "Dig Alm/Ack",
                            "?204",
                            "?205",
                            "?206",
                            "?207",
                            "?208",
                            "?209",
                            "AccessDenied",
                            "No Sample",
                            "No Result",
                            "Unit Down",
                            "Sample Bad",
                            "Equip Fail",
                            "No Lab Data",
                            "Trace",
                            "GreaterMM",
                            "Bad Lab Data",
                            "Good-Off",
                            "Good-On",
                            "Alarm-Off",
                            "Alarm-On",
                            "Bad_Quality",
                            "BadQ-On",
                            "BadQ-Alrm-Of",
                            "BadQ-Alrm-On",
                            "?228",
                            "?229",
                            "Manual",
                            "Auto",
                            "Casc/Ratio",
                            "DCS failed",
                            "Manual Lock",
                            "CO Bypassed",
                            "?236",
                            "Bad Output",
                            "Scan Off",
                            "Scan On",
                            "Configure",
                            "Failed",
                            "Error",
                            "Execute",
                            "Filtered",
                            "Calc Off",
                            "I/O Timeout",
                            "Set to Bad",
                            "Calc Failed",
                            "Calc Overflw",
                            "Under Range",
                            "Over Range",
                            "Bad Input",
                            "Bad Total",
                            "No_Alarm",
                            "Over UCL",
                            "Under LCL",
                            "Over WL",
                            "Under WL",
                            "Over 1 Sigma",
                            "Under 1Sigma",
                            "Over Center",
                            "Under Center",
                            "Stratified",
                            "Mixture",
                            "Trend Up",
                            "Trend Down",
                            "No Alarm#",
                            "Over UCL#",
                            "Under LCL#",
                            "Over WL#",
                            "Under WL#",
                            "Over 1Sigma#",
                            "Under 1Sigm#",
                            "Over Center#",
                            "Under Centr#",
                            "Stratified#",
                            "Mixture#",
                            "Trend Up#",
                            "Trend Down#",
                            "?283",
                            "?284",
                            "?285",
                            "?286",
                            "?287",
                            "?288",
                            "ActiveBatch",
                            "Bad Data",
                            "Calc Crash",
                            "Calc Timeout",
                            "Bad Narg",
                            "Inp OutRange",
                            "Not Converge",
                            "DST Forward",
                            "DST Back",
                            "Substituted",
                            "Invalid Data",
                            "Scan Timeout",
                            "No_Sample",
                            "Arc Off-line",
                            "ISU Saw No Data",
                            "-err",
                            "Good",
                            "_SUBStituted",
                            "Doubtful",
                            "Wrong Type",
                            "Overflow_st",
                            "Intf Shut",
                            "Out of Serv",
                            "Comm Fail",
                            "Not Connect",
                            "Coercion Failed",
                            "Invalid Float",
                            "Future Data Unsupported"
                        };

                        var filtersFactory=new FiltersFactory();
                        filtersFactory.SetDigitalStatesFilters(rejectedStates);
                        filtersFactory.SetFilters(FiltersFactory.FiltersTypesEnum.DigitalStatesFilter,FiltersFactory.FiltersTypesEnum.DuplicateValuesFilter);
                        
                        _logger.Info("Creating worker objects...");
                        var dataWriter = new DataWriter(options.OutfileName, options.EventsPerFile, options.WritersCount, filtersFactory);

                       var dataReader = new DataReaderBulk(readerSettings, dataWriter, options.EnableWrite);

                        //dataReader = options.UseParallel
                        //    ? (IDataReader) new DataReaderParallel(readerSettings, dataWriter)
                        //    : new DataReaderBulk(readerSettings, dataWriter);

                        var orchestrator = new Orchestrator(options.StartTime, options.EndTime,
                            readerSettings.TimeIntervalPerDataRequest, dataReader);

                        var tagsLoader = new TagsLoader(piConnection.GetPiServer(), options.TagQueries,
                            readerSettings.TagGroupSize, orchestrator);

                        var statistics = new Statistics();

                        // starts the orchestrator
                        _logger.Info("Starting workers...");
                        var tagsLoaderTask = tagsLoader.Run();
                        var writerTask = dataWriter.Run();
                        // var processorTask = dataProcessor.Run();
                        var orchestratorTask = orchestrator.Run();
                        var dataReaderTask = dataReader.Run();
                        var statsTask = statistics.Run();


                        // starts the data reader
                        Task.WaitAll(orchestratorTask, writerTask, dataReaderTask, tagsLoaderTask);

                        statistics.Stop();

                        Task.WaitAll(statsTask);

                        _logger.Info("All tasks completed successfully");
                    }


                    // DEBUG
                    //  Console.ReadKey();

                    // exit ok
                    Environment.Exit(0);
                }
                else
                {
                    // exit with error
                    Environment.Exit(1);
                }
            }


            catch (Exception ex)
            {
                
                _logger.Error(ex);
            }
        }
    }
}