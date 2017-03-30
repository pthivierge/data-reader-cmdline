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
        
        private List<IDataFilter> _filters=new List<IDataFilter>();


        public void AddFilter(IDataFilter filter)
        {
            _logger.InfoFormat("Added {0}.  Will be used to process each of the values.",filter.GetType().Name);
            _filters.Add(filter);
        }

        public IDataFilter[] GetFilters()
        {
            return _filters.ToArray();

        }
    }
}
