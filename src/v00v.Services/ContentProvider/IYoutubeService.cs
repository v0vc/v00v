using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using v00v.Model.Entities;
using v00v.Model.SyncEntities;

namespace v00v.Services.ContentProvider
{
    public interface IYoutubeService
    {
        #region Methods

        Task AddPlaylists(Channel channel);

        Task FillThumbs(IReadOnlyCollection<Playlist> items);

        Task<Channel> GetChannelAsync(string channelId, bool withoutPl, string channelTitle = null);

        Task<ChannelDiff> GetChannelDiffAsync(ChannelStruct cs, bool syncPls, Action<string> setLog);

        Task<string> GetChannelId(string inputChannelLink);

        Task<List<Item>> GetItems(Dictionary<string, SyncPrivacy> privacyItems);

        Task<List<Item>> GetPopularItems(string country, IEnumerable<string> existChannelsIds);

        Task<List<Channel>> GetRelatedChannelsAsync(string channelId, IEnumerable<string> existChannelsIds);

        Task<HashSet<Comment>> GetReplyCommentsAsync(string commentId, string channelId);

        Task<List<Item>> GetSearchedItems(string searchText, IEnumerable<string> existChannelsIds, string region);

        Task<byte[]> GetStreamFromUrl(string dataurl);

        Task<IEnumerable<Comment>> GetVideoCommentsAsync(string itemlId, string channelId);

        Task SetItemsStatistic(Channel channel, bool isDur, IEnumerable<string> ids = null);

        #endregion
    }
}
