using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DataReader.Core.Helpers;
using log4net;
using OSIsoft.AF.Asset;

namespace DataReader.Core.Filters
{
    public class DuplicateValuesFilter : IDataFilter
    {
        AFValue lastValue = null;
        private readonly ILog _logger = LogManager.GetLogger(typeof(DuplicateValuesFilter));

        public bool IsFiltered(AFValue value)
        {
            bool isFiltered = false;
            if (lastValue != null)
            {
                if (lastValue.Timestamp==value.Timestamp && lastValue.Value==value.Value)
                {
                    isFiltered = true;
                    _logger.DebugFormat("Fitlered duplicated value: {0} - {1} - {2}",value.PIPoint.Name, value.Timestamp,value.Value);
                }
            }

            lastValue = value;
            return isFiltered;
        }
    }
}
