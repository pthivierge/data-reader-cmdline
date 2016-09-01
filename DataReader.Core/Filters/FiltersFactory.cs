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
using log4net.Filter;

namespace DataReader.Core
{
    public class FiltersFactory
    {


        private readonly ILog _logger = LogManager.GetLogger(typeof(FiltersFactory));
        private string[] _digitalStatesToIgnore;
        FiltersTypesEnum[] _filtersTypes;


        public enum FiltersTypesEnum
        {
            DigitalStatesFilter,
            DuplicateValuesFilter
        }

        public void SetFilters(params FiltersTypesEnum[] filtersTypes)
        {
            _filtersTypes = filtersTypes;
        }

        public void SetDigitalStatesFilters(params string[] statesToIgnore)
        {
            _digitalStatesToIgnore = statesToIgnore.ToArray();
        }

        


        public IDataFilter[] GetFilters()
        {
            if (_filtersTypes == null || _filtersTypes.Length<1)
                return null;

            var filters = new List<IDataFilter>();

            foreach (var filtersTypesEnum in _filtersTypes)
            {
                switch (filtersTypesEnum)
                {
                    case FiltersTypesEnum.DigitalStatesFilter:
                        filters.Add(new DigitalStatesFilter(_digitalStatesToIgnore));
                        break;

                    case FiltersTypesEnum.DuplicateValuesFilter:
                        filters.Add(new DuplicateValuesFilter());
                        break;

                    default:
                        _logger.Warn("Filter specified does not exist");
                        break;
                }
            }

            return filters.ToArray();


        }
    }
}
