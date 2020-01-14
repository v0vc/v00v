using System;
using System.Threading.Tasks;
using Quartz;

namespace v00v.Services.Dispatcher
{
    [DisallowConcurrentExecution]
    internal class UpdateDaily : IJob
    {
        #region Methods

        public async Task Execute(IJobExecutionContext context)
        {
            var setLog = (Action<string>)context.JobDetail.JobDataMap[BaseSync.Log];
            setLog?.Invoke($"-=start {BaseSync.DailyUpdate}=-");
            var updateTask = (Action<int>)context.JobDetail.JobDataMap[BaseSync.UpdateParser];
            await Task.Run(() => updateTask?.Invoke(0)).ContinueWith(x =>
            {
                if (x.IsCompletedSuccessfully)
                {
                    setLog?.Invoke($"{BaseSync.DailyUpdate} completed");
                }
                else
                {
                    setLog?.Invoke(x.Exception == null
                                       ? $"{BaseSync.DailyUpdate} failed"
                                       : $"{BaseSync.DailyUpdate} failed: {x.Exception.Message}");
                }
            });
            setLog?.Invoke($"-=stop {BaseSync.DailyUpdate}=-");
        }

        #endregion
    }
}
