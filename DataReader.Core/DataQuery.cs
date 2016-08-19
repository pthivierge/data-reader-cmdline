using System;
using System.Collections.Generic;
using System.Diagnostics;
using OSIsoft.AF.Asset;
using OSIsoft.AF.PI;

namespace DataReader.Core
{
    public class DataQuery
    {

        public List<PIPoint> PiPoints { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

    }
}
