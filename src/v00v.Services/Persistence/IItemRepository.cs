using System.Collections.Generic;
using System.Threading.Tasks;
using v00v.Model.Entities;
using v00v.Model.Enums;

namespace v00v.Services.Persistence
{
    public interface IItemRepository
    {
        #region Methods

        Task<string> GetItemDescription(string itemId);

        IEnumerable<Item> GetItems(string channelId);

        IEnumerable<Item> GetItemsBySyncState(SyncState state);

        IEnumerable<Item> GetItemsByTitle(string search, int resultCount);

        Task<Dictionary<string, byte>> GetItemsState();

        Task<int> SetItemCommentsCount(string itemId, long comments);

        Task<int> SetItemsWatchState(WatchState state, string itemId, string channelId = null);

        Task<int> UpdateItemFileName(string itemId, string filename);

        Task<Dictionary<string, long>> UpdateItemsStats(List<Item> items, string channelId = null);

        Task<int> UpdateItemsWatchState(string parsedId, byte watch);

        #endregion
    }
}
