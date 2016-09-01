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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataReader.Core
{
    /// <summary>
    /// This class contains default settings to use the data reader
    /// It also allows to set different values than the default.
    /// Also, there is an experimental method AutoTune(...) that allows to change settings automatically based on information about the data source
    /// </summary>
    public class DataReaderSettings
    {
        private ReadingType _dataReadType = ReadingType.Bulk;
        private int _tagGroupSize = 50000;
        private int _maxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 16);
        private TimeSpan _timeIntervalPerDataRequest = TimeSpan.FromDays(1);
        private int _bulkParallelChunkSize = 10000;
        private int _bulkPageSize = 1000;

        public enum ReadingType
        {
            Bulk,
            Parallel
        }


        // General settings

        /// <summary>
        /// The read method that will be used.  Can be bulk or parallel.  Default is Bulk.
        /// </summary>
        public ReadingType DataReadType
        {
            get { return _dataReadType; }
            set { _dataReadType = value; }
        }

        /// <summary>
        /// Period of time of each data request against the PI Data Archive.
        /// Default: 1 day.
        /// </summary>
        public TimeSpan TimeIntervalPerDataRequest
        {
            get { return _timeIntervalPerDataRequest; }
            set { _timeIntervalPerDataRequest = value; }
        }

        /// <summary>
        /// Defines the number of tags that will be put in groups that will be formed after the tag search.
        /// These groups will be passed to the DataReader for the read to be performed. Default: 50 000.
        /// </summary>
        public int TagGroupSize
        {
            get { return _tagGroupSize; }
            set { _tagGroupSize = value; }
        }

        /// <summary>
        /// Defines the maximum number of threads to be used.
        /// PI Data Archive has 16 threads to server bulk calls, so it may be good to limit a little
        /// Default: Math.Min(Environment.ProcessorCount, 16)
        /// </summary>
        public int MaxDegreeOfParallelism
        {
            get { return _maxDegreeOfParallelism; }
            set { _maxDegreeOfParallelism = value; }
        }


        // settings for bulk

        /// <summary>
        /// Defines how many tags will be used per bulk requests.
        /// Before performing the bulk queries, the TagGroupSize is splitted into smaller groups that will be the size of this parameter.
        /// These smaller groups will then be called in a multi-threded way. Default 10 000;
        /// </summary>
        public int BulkParallelChunkSize
        {
            get { return _bulkParallelChunkSize; }
            set { _bulkParallelChunkSize = value; }
        }

        /// <summary>
        /// Page size for bulk queries.  
        /// </summary>
        public int BulkPageSize
        {
            get { return _bulkPageSize; }
            set { _bulkPageSize = value; }
        }


        /// <summary>
        /// Calling this method will reconfigure some of the settings in this class to try to optimize them for better performances
        /// </summary>
        /// <param name="estimatedEventsPerDay">Estimation of the average number of events per day, per tag</param>
        /// <param name="estimatedTagsCount"> Estimation of the total amount of tags that needs to be processed.</param>
        /// <param name="eventsPerRead">Defines the number of events that should be read per data call.  somewhere between 10000 and 50000 is a good start.</param>
        /// <param name="readType">Type of read that you want to perform.  Bulk performs better if network has latency.  Paralell can outperform bulk if latency is low.</param>
        public void AutoTune(ReadingType readType, int estimatedEventsPerDay, int estimatedTagsCount, int eventsPerRead)
        {
            

            double days = 1;

            if (estimatedTagsCount < 200000)
            {
                TagGroupSize = estimatedTagsCount/10;
                TagGroupSize = TagGroupSize <= 0 ? 1 : TagGroupSize;

            }
            else
            {
                TagGroupSize = 50000;
            }

            if (readType == ReadingType.Bulk)
            {
                // eventsWanted / Total events per day
                // bulkPageSize is the number of tags that will be queried per page.
                BulkPageSize = 1000;
                days = ((double)eventsPerRead / ((double)estimatedEventsPerDay*BulkPageSize));
                BulkParallelChunkSize = (TagGroupSize/5) <=1 ? 1 : (TagGroupSize / 5);

            }
            else
            {
                // in case we do parallel, we target 10 000 events per read
                days = ((double)eventsPerRead / estimatedEventsPerDay);
            }
            
            // set a time span that will give us approximately "eventsPerRead" events per call
            TimeIntervalPerDataRequest = TimeSpan.FromDays(days);
           
           

        }








    }

}




