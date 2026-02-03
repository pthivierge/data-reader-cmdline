using System;
using OSIsoft.AF.Asset;

namespace DataReader.Core
{
    public class SummaryRecord
    {
        public DateTime TimestampLocal { get; set; }
        public string TagName { get; set; }
        public string AggregateType { get; set; }
        public string ValueString { get; set; }

        // Optional: keep original AFValue for future extensions (status, error, etc.)
        public AFValue SourceValue { get; set; }
    }
}
