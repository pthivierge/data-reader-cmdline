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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;
using OSIsoft.AF.Asset;

namespace DataReader.Core
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
