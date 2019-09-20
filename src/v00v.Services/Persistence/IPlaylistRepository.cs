using System.Collections.Generic;
using System.Threading.Tasks;
using v00v.Model.Entities;
using v00v.Model.Enums;

namespace v00v.Services.Persistence
{
    public interface IPlaylistRepository
    {
        #region Methods

        Task<List<Playlist>> GetPlaylists(string channelId);

        Task<List<Item>> GetPlaylistsItems(WatchState state);

        Task<int> GetPlaylistsItemsCount(WatchState state);

        Task<int> GetPlaylistsItemsCount(SyncState state);

        Task<List<Item>> GetUnlistedPlaylistsItems();

        #endregion
    }
}
