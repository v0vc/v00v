﻿using System.Collections.Generic;
using System.Threading.Tasks;
using v00v.Model.Entities;
using v00v.Model.Enums;
using v00v.Model.SyncEntities;

namespace v00v.Services.Persistence
{
    public interface IChannelRepository
    {
        #region Methods

        Task<int> AddChannel(Channel channel);

        Task<int> AddChannels(List<Channel> channels);

        Task<int> DeleteChannel(string channelId);

        Task<List<Channel>> GetChannels();

        Task<List<ChannelStruct>> GetChannelsStruct(bool syncPls, IReadOnlyCollection<Channel> channels);

        Task<Dictionary<string, int>> GetChannelStateCount(WatchState watchState);

        Task<string> GetChannelSubtitle(string channelId);

        Task<int> GetItemsCount(SyncState state, string channelId = null);

        Task<int> SaveChannel(string channelId, string newTitle, IEnumerable<int> tags);

        Task<int> StoreDiff(SyncDiff fdiff);

        Task<int> UpdateChannelSyncState(string channelId, byte state);

        Task<int> UpdateItemsCount(string channelId, int count);

        Task<int> UpdatePlannedCount(string channelId, int count);

        Task<int> UpdateWatchedCount(string channelId, int count);

        #endregion
    }
}
