#region Copyright

//  Copyright 2015 Patrice Thivierge Fortin
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
using log4net;
using OSIsoft.AF.PI;

namespace DataReader.Core
{
    /// <summary>
    ///     Helps managing connection to PI Server
    /// </summary>
    public class PIConnection
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof (PIConnection));
        private readonly PIServer _piServer;
        private readonly PIServers _piServers = new PIServers();

        /// <summary>
        ///     Initialize a PI Server Connnection object.
        ///     You should call the Connect method before access data with the PIServer property
        /// </summary>
        /// <param name="server">Name of the PI System (AF Server) to connect to</param>
        public PIConnection(string server)
        {
            if (_piServers.Contains(server))
                _piServer = _piServers[server];
            else
            {
                throw new KeyNotFoundException("Specified PI System does not exist");
            }
        }

        public PIServer GetPiServer()
        {
            return _piServer;
        }

        public bool Connect()
        {
            _logger.InfoFormat("Trying to connect to PI Data Archive {0}. As {1}", _piServer.Name,
                _piServer.CurrentUserName);

            try
            {
                _piServer.Connect();

                _logger.InfoFormat("Connected to {0}. As {1}", _piServer.Name, _piServer.CurrentUserName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
            }

            return false;
        }
    }
}