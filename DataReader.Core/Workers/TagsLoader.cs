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
        int _tagCount = 0;
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
                var piPoints = Search(s);
                

                // var tagLists = tags.ChunkBy(_tagChunkSize);

                // once tags are found we split them into smaller chunk for a better re-use later on.
                
                var  pipoints=new List<PIPoint>();
                foreach (var point in piPoints)
                {
                    _tagCount++;
                    pipoints.Add(point);

                    if (pipoints.Count >= _tagChunkSize)
                    {

                        SendPointsForProcessing(cancelToken, pipoints);
                    }


                  //  _logger.DebugFormat("Processing tag {0}",tagCount);
                    
                    // wait a little if there are too much queued queries
                    while (_orchestrator.IncomingPiPoints.Count>10 && !cancelToken.IsCancellationRequested)
                    {
                        Thread.Sleep(1000);
                    }


                }

                if(pipoints.Count>0)
                    SendPointsForProcessing(cancelToken, pipoints);



            }

            _orchestrator.IncomingPiPoints.CompleteAdding();

            _logger.Info("TagsLoader has completed to load all the PIPoints");


        }

        private void SendPointsForProcessing(CancellationToken cancelToken, List<PIPoint> pipoints)
        {
// creates the query object witht the tag chunk to start processing the data
            var dataQuery = new DataQuery();
            dataQuery.PiPoints.AddRange(pipoints);
            pipoints.Clear();

            // enqueue the query to be processed
            _orchestrator.IncomingPiPoints.Add(dataQuery, cancelToken);

            _logger.InfoFormat("TagsLoader loaded {0} for data collection. Total {1} tags loaded.", pipoints.Count, _tagCount);
        }

        public IEnumerable<PIPoint> Search(string query)
        {
            var queries = PIPointQuery.ParseQuery(_server, query);
            var points=PIPoint.FindPIPoints(_server, queries);
            return points;

        }
    }
}
