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
using OSIsoft.AF.Asset;

namespace DataReader.Core
{
    public class WriteInfo
    {
        public IEnumerable<AFValues> Data { get; set; }

        // Stable row model for summary exports
        public IEnumerable<SummaryRecord> SummaryRecords { get; set; }

        public long ChunkId { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public long SubChunkId { get; set; }

        public bool IsSummaryData { get; set; }

        public Dictionary<string, string> Metadata { get; set; }

        // Legacy index-based mappings (kept for backward compatibility; not used when SummaryRecords is provided)
        public Dictionary<int, string> TagNames { get; set; }

        public Dictionary<int, string> SummaryTypes { get; set; }
    }
}

