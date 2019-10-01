using System.Collections.Generic;
using System.Threading.Tasks;
using v00v.Model.Entities;
using v00v.Model.SyncEntities;

namespace v00v.Services.Synchronization
{
    public interface ISyncService
    {
        #region Methods

        Task<SyncDiff> Sync(bool syncPls, IReadOnlyCollection<Channel> channels);

        #endregion
    }
}
