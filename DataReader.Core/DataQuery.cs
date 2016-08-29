using System;
using System.Collections.Generic;
using System.Diagnostics;
using OSIsoft.AF.Asset;
using OSIsoft.AF.PI;

namespace DataReader.Core
{
    public class DataQuery
    {

        public List<PIPoint> PiPoints { get; set; } = new List<PIPoint>();
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        public long QueryId { get; set; }
        public long ChunkId { get; set; }
    }
}
