using System.Collections.Generic;
using System.Threading.Tasks;
using v00v.Model.Entities;

namespace v00v.Services.Backup
{
    public interface IBackupService
    {
        #region Methods

        Task Backup(IEnumerable<Channel> entries);

        Task<List<Channel>> Restore(IEnumerable<string> existChannel, bool isFast);

        #endregion
    }
}