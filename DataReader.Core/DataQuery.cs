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
using System.Diagnostics;
using OSIsoft.AF.Asset;
using OSIsoft.AF.PI;

namespace DataReader.Core
{
    public class DataQuery
    {
        private List<PIPoint> _piPoints = new List<PIPoint>();

        public List<PIPoint> PiPoints
        {
            get { return _piPoints; }
            set { _piPoints = value; }
        }

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        public long QueryId { get; set; }
        public long ChunkId { get; set; }
    }
}
