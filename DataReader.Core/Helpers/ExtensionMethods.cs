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
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DataReader.Core
{
    public static class ExtensionMethods
    {
        /// <summary>
        ///     Convert a date to a human readable ISO datetime format. ie. 2012-12-12 23:01:12
        ///     this method must be put in a static class. This will appear as an available function
        ///     on every datetime objects if your static class namespace is declared.
        /// </summary>
        public static string ToIsoReadable(this DateTime dateTime)
        {
            return dateTime.ToString("yyyy-MM-dd_HH'.'mm'.'ss'.'ffff");
        }

        /// <summary>
        ///     Helper methods for the lists.
        /// </summary>
        public static List<List<T>> ChunkBy<T>(this List<T> source, int chunkSize)
        {
            return source
                .Select((x, i) => new {Index = i, Value = x})
                .GroupBy(x => x.Index/chunkSize)
                .Select(x => x.Select(v => v.Value).ToList())
                .ToList();
        }

        public static DateTime toDate(this string value)
        {
            DateTime date;
            if (!DateTime.TryParse(value, out date))
            {
                throw new InvalidDataException(string.Format("Could not convert string \"{0}\" to a date object.", value));
            }

            return date;
        }

        public static T ToEnum<T>(this string value)
        {
            return (T) Enum.Parse(typeof (T), value, true);
        }
    }
}