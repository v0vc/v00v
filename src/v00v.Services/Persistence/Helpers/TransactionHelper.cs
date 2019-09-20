using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace v00v.Services.Persistence.Helpers
{
    public static class TransactionHelper
    {
        #region Static Methods

        public static IDbContextTransaction Get(DbContext context)
        {
            return context.Database.BeginTransaction(IsolationLevel.ReadCommitted);
        }

        #endregion
    }
}
