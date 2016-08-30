using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OSIsoft.AF.Asset;

namespace DataReader.Core
{
    public interface IDataFilter
    {
        bool IsFiltered(AFValue value);

    }
}
