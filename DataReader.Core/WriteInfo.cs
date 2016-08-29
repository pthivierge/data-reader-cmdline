using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OSIsoft.AF.Asset;

namespace DataReader.Core
{
    public class WriteInfo
    {
        public IEnumerable<AFValues> Data { get; set; }
        
        public long ChunkId { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public long SubChunkId { get; set; }
    }
}

