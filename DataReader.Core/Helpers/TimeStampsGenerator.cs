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
using OSIsoft.AF.Time;

namespace DataReader.Core
{
    public static class TimeStampsGenerator
    {

        public static List<AFTime> Get(TimeSpan interval, AFTime startTime, AFTime endTime)
        {

            var dates = new List<AFTime>();
            
            // For day-based intervals in local time, we need to use calendar day arithmetic
            // to maintain the same time-of-day across DST transitions
            // Example: 07:00:00 should remain 07:00:00 every day, not shift to 08:00:00 after DST
            
            var currentTime = startTime;
            while (currentTime < endTime)
            {
                dates.Add(currentTime);
                
                // Use calendar date arithmetic for local time to preserve time-of-day across DST
                // Add interval to LocalTime as DateTime, then convert back to AFTime
                var nextLocalTime = currentTime.LocalTime.Add(interval);
                currentTime = new AFTime(nextLocalTime);
            }

            dates.Add(endTime);
            
            return dates;
        }

    }
}
