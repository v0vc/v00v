using System.Collections.Generic;
using System.Threading.Tasks;
using v00v.Model.Entities;
using v00v.Model.SyncEntities;

namespace v00v.Services.ContentProvider
{
    public interface IYoutubeService
    {
        #region Methods

        Task FillThumbs(IReadOnlyCollection<Playlist> items);

        Task<Channel> GetChannelAsync(string channelId, string channelTitle = null);

        Task<ChannelDiff> GetChannelDiffAsync(ChannelStruct cs, bool syncPls);

        Task<string> GetChannelId(string inputChannelLink);

        Task<List<Item>> GetItems(Dictionary<string, SyncPrivacy> privacyItems);

        Task<List<Item>> GetPopularItems(string country, IEnumerable<string> existChannelsIds);

        Task<List<Channel>> GetRelatedChannelsAsync(string channelId);

        Task<List<Item>> GetSearchedItems(string searchText, string region);

        Task<byte[]> GetStreamFromUrl(string dataurl);

        Task<List<string>> GetVideoCommentsAsync(string itemlId, int maxResult);

        Task SetItemsStatistic(Channel channel, bool isDur, IEnumerable<string> ids = null);

        #endregion
    }
}
