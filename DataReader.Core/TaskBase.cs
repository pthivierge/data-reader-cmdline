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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using log4net;

namespace DataReader.Core
{
    public abstract class TaskBase
    {
        protected readonly ILog _logger;
        private readonly List<Task> _tasks = new List<Task>();
        
        private readonly CancellationTokenSource _cancellationToken = new CancellationTokenSource();

        protected TaskBase()
        {
            _logger = LogManager.GetLogger(this.GetType());
        }


        public virtual Task Run()
        {
            var task = Task.Run(() => DoTask(_cancellationToken.Token));
            return task;
        }

        public virtual void Stop()
        {
            _cancellationToken.Cancel();
        }

        protected abstract void DoTask(CancellationToken cancelToken);


        /*
        // Example - working with the cancel token:
         
         try{
        while(true){
            cancelToken.ThrowIfCancellationRequested();

         // or
          if(cancelToken.IsCancellationRequested)
            break;

            //Long operation here...
        }

        }
        finally{
            //Do some cleanup
        }
         
         
         
         
         
         */





    }
}