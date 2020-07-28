using System.Data;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace v00v.Services.Persistence.Helpers
{
    public static class TransactionHelper
    {
        #region Static Methods

        public static Task<IDbContextTransaction> Get(DbContext context)
        {
            return context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        }

        #endregion
    }
}
