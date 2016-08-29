using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;
using OSIsoft.AF.Asset;

namespace DataReader.Core.Filters
{
    public class DigitalStatesFilter : IDataFilter
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof(DigitalStatesFilter));

        public bool IsFiltered(AFValue value)
        {
            bool isFiltered = false;
            if (value.Value is AFEnumerationValue)
            {
                var digValue = (AFEnumerationValue)value.Value;

                var rejectedStates = new[]
                {
                    "ptcreated",
                    "snapfix",
                    "shutdown",
                };

                isFiltered = rejectedStates.Contains(digValue.Name.ToLower());

                if(isFiltered)
                    _logger.DebugFormat("Fitlered digital value: {0} - {1} - {2}", value.PIPoint.Name, value.Timestamp, value.Value);

            }

            return isFiltered;
        }
    }
}
