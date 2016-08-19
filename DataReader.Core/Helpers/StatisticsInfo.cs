#region Copyright
// /*
// 
//    Copyright 2015 Patrice Thivierge Fortin
// 
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
// 
//    http://www.apache.org/licenses/LICENSE-2.0
// 
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
//  
//  */
#endregion
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataReader.Core
{
    /// <summary>
    /// Data Structure that represents statistical information about a calculations done for one analyse
    /// </summary>
    public class StatisticsInfo
    {
        
        public Stopwatch Stopwatch { get; }=new Stopwatch();

        public int EventsCount { get; set; } = 0;

        public DateTime PeriodStartTime { get; set; }

        public DateTime PeriodEndTime { get; set; }

        public double EventsProcessedPerSecond
        {
            get
            {
                double eventsProcessed = 0;

                if (EventsCount!=0 && Stopwatch.ElapsedMilliseconds!=0)
                    eventsProcessed=EventsCount/TimeSpan.FromMilliseconds(Stopwatch.ElapsedMilliseconds).TotalSeconds;

                return eventsProcessed;


            }

        }

        public bool Print { get; set; } = true;


    }
}