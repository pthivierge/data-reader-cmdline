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
