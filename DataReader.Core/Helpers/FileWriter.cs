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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;

namespace DataReader.Core
{
    public class FileWriter : IDisposable
    {
        private readonly Logger _logger = LogManager.GetCurrentClassLogger();
        FileStream _fileStream;
        StreamWriter _streamWriter;
        private int _lineCount;
        private string _fileName;

        private string _writerIndex;
        private int _eventsPerFile;

        public Task ActiveTask { get; set; }

        public FileWriter(int eventsPerFile, string writerIndex)
        {
            _writerIndex = writerIndex;
            _eventsPerFile = eventsPerFile;
        }

        public void SetName(string fileName)
        {
            if (_fileName != fileName)
            {
                _fileName = fileName;
                CreateNewFile(_fileName);
            }
        }

        public void WriteLine(string line)
        {
            try
            {
                if (_lineCount + 1 >= _eventsPerFile)
                {
                    CreateNewFile(_fileName);
                }

                _streamWriter.WriteLine(line);
                _lineCount++;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, ex.Message);
            }
        }

        private void CreateNewFile(string fileName)
        {
            try
            {
                Dispose();

                // Simple filename: base with writer ID and line-based splitting
                // If this is the first file for this base name, just use w{id}
                // If we need to split due to line count, append a counter
                var fullFileName = string.Format("{0}_w{1}.csv", fileName, _writerIndex);
                
                // Handle case where file already exists (multiple writes to same time range)
                int splitCounter = 1;
                while (File.Exists(fullFileName))
                {
                    fullFileName = string.Format("{0}_w{1}_p{2}.csv", fileName, _writerIndex, splitCounter);
                    splitCounter++;
                }

                _fileStream = new FileStream(fullFileName, FileMode.CreateNew);
                _streamWriter = new StreamWriter(_fileStream);

                _lineCount = 0;
                
                _logger.Info("Created a new file: {0}.", fullFileName);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, ex.Message);
            }
        }

        public void Dispose()
        {
            if (_streamWriter != null)
            {
                _streamWriter.Dispose();
                _streamWriter = null;
            }

            if (_fileStream != null)
            {
                _fileStream.Dispose();
                _fileStream = null;
            }
        }
    }
}
