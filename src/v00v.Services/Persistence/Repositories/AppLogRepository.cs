using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using v00v.Model.Enums;
using v00v.Services.Database;
using v00v.Services.Database.Models;
using v00v.Services.Persistence.Helpers;

namespace v00v.Services.Persistence.Repositories
{
    public class AppLogRepository : IAppLogRepository
    {
        #region Static and Readonly Fields

        private readonly IContextFactory _contextFactory;

        #endregion

        #region Constructors

        public AppLogRepository(IContextFactory contextFactory)
        {
            _contextFactory = contextFactory;
        }

        #endregion

        #region Properties

        public string AppId { get; set; }

        #endregion

        #region Methods

        public async Task<AppStatus> GetAppSyncStatus(string appId)
        {
            using (VideoContext context = _contextFactory.CreateVideoContext())
            {
                try
                {
                    var log = await context.AppLogs.AsNoTracking()
                        .Where(x => x.AppId == appId && (x.AppStatus == (byte)AppStatus.PeriodicSyncStarted
                                                         || x.AppStatus == (byte)AppStatus.PeriodicSyncFinished
                                                         || x.AppStatus == (byte)AppStatus.DailySyncStarted
                                                         || x.AppStatus == (byte)AppStatus.DailySyncFinished
                                                         || x.AppStatus == (byte)AppStatus.SyncPlaylistStarted
                                                         || x.AppStatus == (byte)AppStatus.SyncPlaylistFinished
                                                         || x.AppStatus == (byte)AppStatus.SyncWithoutPlaylistStarted
                                                         || x.AppStatus == (byte)AppStatus.SyncWithoutPlaylistFinished))
                        .OrderByDescending(x => x.Timestamp).FirstOrDefaultAsync();
                    return log != null ? (AppStatus)log.AppStatus : AppStatus.NoSync;
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
                    throw;
                }
            }
        }

        public int GetStatusCount(AppStatus status)
        {
            using (VideoContext context = _contextFactory.CreateVideoContext())
            {
                try
                {
                    return context.AppLogs.AsNoTracking().Count(x => x.AppStatus == (byte)status);
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
                    throw;
                }
            }
        }

        public async Task<int> SetStatus(AppStatus status, string comment = null)
        {
            if (AppId == null)
            {
                return -1;
            }

            using (VideoContext context = _contextFactory.CreateVideoContext())
            {
                using (IDbContextTransaction transaction = TransactionHelper.Get(context))
                {
                    try
                    {
                        await context.AppLogs.AddAsync(new AppLog { AppId = AppId, AppStatus = (byte)status, Comment = comment });
                        int res = await context.SaveChangesAsync();
                        transaction.Commit();
                        return res;
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine(exception);
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        #endregion
    }
}
