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

        Task<List<Item>> GetItems(string channelId);

        Task<List<Item>> GetItemsBySyncState(SyncState state);

        Task<List<Item>> GetItemsByTitle(string search, int resultCount);

        Task<Dictionary<string, byte>> GetItemsState();

        Task<int> SetItemsWatchState(WatchState state, string itemId, string channelId = null);

        Task<int> UpdateItemFileName(string itemId, string filename);

        Task<Dictionary<string, long>> UpdateItemsStats(List<Item> items, string channelId = null);

        Task<int> UpdateItemsWatchState(string parsedId, byte watch);

        #endregion
    }
}
