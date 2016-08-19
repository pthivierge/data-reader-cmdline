using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OSIsoft.AF.Asset;

namespace DataReader.Core
{
    public class DataInfo
    {
        public IEnumerable<AFValues> Data { get; set; }
        public StatisticsInfo StatsInfo { get; set; }
    }
}

