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

namespace DataReader.CommandLine
{
    /// <summary>
    ///     This class returns the relevant object to do data gathering, according to the mode selected
    /// </summary>
    public static class DataReadersFactory
    {
        public static IDataReader GetReader(string mode)
        {
            IDataReader obj = null;
            switch (mode.ToLower())
            {
                case "bulk":
                    obj = new DataReaderBulk();
                    break;

                // case... bulk+parallel
            }
            return obj;
        }
    }
}