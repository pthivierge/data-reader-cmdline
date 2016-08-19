using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DataReader.Core;

namespace DataReader.Core
{
    public interface IDataReader
    {
        BlockingCollection<DataQuery> GetQueriesQueue();
        Task Run();
    }
}
