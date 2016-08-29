using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;

namespace DataReader.Core.Helpers
{
    public class FileWriter  : IDisposable
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof(FileWriter));
        FileStream _fileStream;
        StreamWriter _streamWriter;
        private int _lineCount;
        private string _fileName;
        private string _writerIndex;
        private int _eventsPerFile;

        public Task ActiveTask { get; set; }

        public FileWriter(int eventsPerFile, string fileName, string writerIndex)
        {
            _fileName = fileName;
            _writerIndex = writerIndex;
            CreateNewFile(_fileName);
            _eventsPerFile = eventsPerFile;
        }

        public void WriteLine(string line)
        {

            if (_lineCount + 1 >= _eventsPerFile)
            {
                CreateNewFile(_fileName);
                _lineCount = 0;
            }
            
            _streamWriter.WriteLine(line);
            _lineCount++;
        }

        private void CreateNewFile(string fileName)
        {
            var fullFileNAme = fileName + "_" + DateTime.Now.ToIsoReadable() + "_" + _writerIndex + ".csv";

            Dispose();
            
            _fileStream = new FileStream(fullFileNAme, FileMode.CreateNew);
            _streamWriter = new StreamWriter(_fileStream);

            _logger.InfoFormat("Created a new file: {0}.", fullFileNAme);
        }


        public void Dispose()
        {
            if(_streamWriter!=null)
                _streamWriter.Dispose();

            if(_fileStream!=null)
                _fileStream.Dispose();
        }
    }
}
