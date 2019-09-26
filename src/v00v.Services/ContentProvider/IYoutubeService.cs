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

        Task<byte[]> GetStreamFromUrl(string dataurl);

        Task SetItemsStatistic(Channel channel, bool isDur, IEnumerable<string> ids = null);

        #endregion
    }
}
