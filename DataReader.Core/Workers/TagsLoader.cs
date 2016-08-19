using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using OSIsoft.AF.PI;
using OSIsoft.AF.Search;

namespace DataReader.Core
{
    public class TagsLoader :TaskBase
    {
        PIServer _server;
        string[] _queries;
        int _tagChunkSize;
        Orchestrator _orchestrator;


        public TagsLoader(PIServer server, string[] queries, int tagChunkSize, Orchestrator orchestrator)
        {
            _server = server;
            _queries = queries;
            _tagChunkSize = tagChunkSize;
            _orchestrator = orchestrator;
        }

        public TagsLoader(PIServer server)
        {
            _server = server;
        }


        protected override void DoTask(CancellationToken cancelToken)
        {
            _logger.InfoFormat("TagsLoader started - iterating over {0} query(ies)", _queries.Length);
            
            // execute each query, one after the other to find the tags on the server
            foreach (var s in _queries)
            {
                var tags = Search(s).ToList();
                _logger.WarnFormat("Found {0} tags with query {1}", tags.Count, s);

                var tagLists = tags.ChunkBy(_tagChunkSize);

                // once tags are found we split them into smaller chunk for a better re-use later on.
                
                foreach (var tagList in tagLists)
                {
                    // creates the query object
                    var dataQuery=new DataQuery()
                    {
                        PiPoints = tagList
                    };

                    // enqueue the query to be processed
                    _orchestrator.IncomingPiPoints.Add(dataQuery,cancelToken);

                    // wait a littke if there are too much queued queries
                    while (_orchestrator.IncomingPiPoints.Count>100 && !cancelToken.IsCancellationRequested)
                    {
                        Thread.Sleep(1000);
                    }


                }


            }

            _orchestrator.IncomingPiPoints.CompleteAdding();

            _logger.Info("TagsLoader has completed to load all the PIPoints");


        }

        public IEnumerable<PIPoint> Search(string query)
        {
            var queries = PIPointQuery.ParseQuery(_server, query);
            var points=PIPoint.FindPIPoints(_server, queries);
            return points;

        }
    }
}
