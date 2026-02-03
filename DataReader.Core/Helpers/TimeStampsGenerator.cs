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
            // Example: 19:00:00 should remain 19:00:00 every day, not shift due to DST
            
            var currentTime = startTime;
            
            // Detect if this is a day-based interval (within 1 second of 24 hours)
            // For day intervals, use calendar date arithmetic to preserve time-of-day
            bool isDayInterval = Math.Abs(interval.TotalDays - Math.Round(interval.TotalDays)) < 0.00002; // ~1.7 seconds tolerance
            int daysToAdd = isDayInterval ? (int)Math.Round(interval.TotalDays) : 0;
            
            while (currentTime < endTime)
            {
                dates.Add(currentTime);
                
                if (isDayInterval && daysToAdd > 0)
                {
                    // Use calendar day addition to preserve time-of-day across DST transitions
                    // AddDays adds calendar days, so 19:00:00 remains 19:00:00 regardless of DST
                    var nextLocalTime = currentTime.LocalTime.AddDays(daysToAdd);
                    currentTime = new AFTime(nextLocalTime);
                }
                else
                {
                    // For non-day intervals (hours, minutes, etc.), use duration addition
                    var nextLocalTime = currentTime.LocalTime.Add(interval);
                    currentTime = new AFTime(nextLocalTime);
                }
            }

            dates.Add(endTime);
            
            return dates;
        }

    }
}
