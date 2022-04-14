using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using v00v.Model.Entities;
using v00v.Model.SyncEntities;

namespace v00v.Services.ContentProvider
{
    public interface IYoutubeService
    {
        #region Properties

        string ChannelLink { get; }
        string ItemLink { get; }
        string PlaylistLink { get; }

        #endregion

        #region Methods

        Task AddPlaylists(Channel channel);

        Task FillThumbs(IReadOnlyCollection<Playlist> items);

        Task<Channel> GetChannelAsync(string channelId, bool withoutPl, string channelTitle = null);

        Task<ChannelDiff> GetChannelDiffAsync(ChannelStruct cs, bool syncPls, Action<string> setLog);

        Task<string> GetChannelId(string inputChannelLink);

        Task<List<Item>> GetItems(Dictionary<string, SyncPrivacy> privacyItems);

        Task<List<Item>> GetPopularItems(string country, IEnumerable<string> existChannelsIds);

        string GetPreviewThumbLink(string itemId);

        Task<Channel[]> GetRelatedChannelsAsync(string channelId, IEnumerable<string> existChannelsIds);

        Task<HashSet<Comment>> GetReplyCommentsAsync(string commentId, string channelId);

        Task<List<Item>> GetSearchedItems(string searchText, IEnumerable<string> existChannelsIds, string region);

        Task<byte[]> GetStreamFromUrl(string dataUrl);

        Task<IEnumerable<Comment>> GetVideoCommentsAsync(string itemId, string channelId);

        bool IsYoutubeLink(string link, out string videoId);

        Task SetItemsStatistic(Channel channel, bool isDur, IEnumerable<string> ids = null);

        Task SetItemsStatistic(List<Item> items);

        #endregion
    }
}
