using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;

namespace DataReader.Core
{
    public class FileWriter : IDisposable
    {
        private static volatile int fileIndex=0;
        private static object fileIndexLock=new object();
        //private static DateTime BatchOperationTime;

        private readonly ILog _logger = LogManager.GetLogger(typeof(FileWriter));
        FileStream _fileStream;
        StreamWriter _streamWriter;
        private int _lineCount;
        private string _fileName;

        private string _writerIndex;
        private int _eventsPerFile;

        public Task ActiveTask { get; set; }

        public FileWriter(int eventsPerFile, string writerIndex)
        {
            //if(BatchOperationTime==DateTime.MinValue)
            //    BatchOperationTime=DateTime.Now;

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

                _logger.Error(ex);
            }
        }

        private void CreateNewFile(string fileName)
        {
            try
            {


                Dispose();

                lock (fileIndexLock)
                {
                    fileIndex++;
                }

               // var time = (BatchOperationTime - new DateTime(1970, 1, 1)).TotalSeconds;
                // i=file index(count), w=writer id, b=batch time when the command line was run
                //  var fullFileName = string.Format("{0}_i{2}_w{3}_b{1}", fileName, time, fileIndex, _writerIndex);

                var fullFileName = string.Format("{0}_i{1}_w{2}", fileName, fileIndex, _writerIndex);


                _fileStream = new FileStream(fullFileName, FileMode.CreateNew);
                _streamWriter = new StreamWriter(_fileStream);

                _lineCount = 0;
                

                _logger.InfoFormat("Created a new file: {0}.", fullFileName);
            }
            catch (Exception ex)
            {
                
             _logger.Error(ex);
            }

         
        }


        public void Dispose()
        {
            if (_streamWriter != null)
                _streamWriter.Dispose();

            if (_fileStream != null)
                _fileStream.Dispose();
        }
    }
}
