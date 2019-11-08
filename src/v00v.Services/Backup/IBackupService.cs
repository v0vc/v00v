using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using v00v.Model.Entities;

namespace v00v.Services.Backup
{
    public interface IBackupService
    {
        #region Methods

        Task<int> Backup(IEnumerable<Channel> entries, Action<string> setLog);

        Task<RestoreResult> Restore(IEnumerable<string> existChannel,
            bool isFast,
            Action<string> setTitle,
            Action<Channel> updateList,
            Action<string> setLog);

        #endregion
    }
}
