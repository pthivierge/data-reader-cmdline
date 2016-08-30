using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;
using OSIsoft.AF.Asset;

namespace DataReader.Core
{
    public class DigitalStatesFilter : IDataFilter
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof(DigitalStatesFilter));

        readonly string[] _rejectedStates=null;

        public DigitalStatesFilter(string[] statesToIgnore)
        {
            if (statesToIgnore != null && statesToIgnore.Length > 0)
            {
                _rejectedStates = statesToIgnore;
            }
        }


        public bool IsFiltered(AFValue value)
        {
            bool isFiltered = false;
            var enumerationValue = value.Value as AFEnumerationValue;
            if (enumerationValue != null)
            {
                var digValue = enumerationValue;
                
                isFiltered = _rejectedStates.Contains(digValue.Name.ToLower());

                if(isFiltered)
                    _logger.DebugFormat("Filtered digital value: {0} - {1} - {2}", value.PIPoint.Name, value.Timestamp, enumerationValue);

            }

            return isFiltered;
        }
    }
}
