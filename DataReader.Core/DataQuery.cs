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
