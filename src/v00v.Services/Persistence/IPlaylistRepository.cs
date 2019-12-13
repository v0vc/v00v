using System.Collections.Generic;
using v00v.Model.Entities;
using v00v.Model.Enums;

namespace v00v.Services.Persistence
{
    public interface IPlaylistRepository
    {
        #region Methods

        IEnumerable<Playlist> GetPlaylists(string channelId);

        IEnumerable<Item> GetPlaylistsItems(WatchState state);

        int[] GetStatePlaylistsItemsCount();

        IEnumerable<Item> GetUnlistedPlaylistsItems();

        #endregion
    }
}
