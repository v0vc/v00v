using System.Threading.Tasks;
using v00v.Model.Enums;

namespace v00v.Services.Persistence
{
    public interface IAppLogRepository
    {
        #region Properties

        string AppId { get; set; }

        #endregion

        #region Methods

        Task<AppStatus> GetAppSyncStatus(string appId);

        int GetStatusCount(AppStatus status);

        Task<int> SetStatus(AppStatus status, string comment = null);

        #endregion
    }
}
