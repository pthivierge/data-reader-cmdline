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
using CommandLine;
using log4net;

namespace DataReader.CommandLine
{
    /// <summary>
    /// This program was built to make data read comparisons between different PI Data Archives
    /// ex: 
    /// </summary>
    internal class Program
    {
        private static void Main(string[] args)
        {
            var _logger = LogManager.GetLogger(typeof (Program));
            var writer = Console.Out;

            try
            {
                
                _logger.Info("Data reading testing starting...");

                var options = new CommandLineOptions();

                if (Parser.Default.ParseArguments(args,options))
                {
                    var dataReader = DataReadersFactory.GetReader(options.Mode);
                    dataReader.Run(options);
                    _logger.Info("Operation Completed");

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
                Console.SetOut(writer);
                Console.WriteLine("Error: " + ex);
            }
        }
    }
}