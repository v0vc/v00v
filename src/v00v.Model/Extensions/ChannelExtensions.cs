using System;
using System.Collections.Generic;
using System.Linq;
using v00v.Model.Entities;
using v00v.Model.SyncEntities;

namespace v00v.Model.Extensions
{
    public static class ChannelExtensions
    {
        #region Static Methods

        public static ChannelStruct ToChannelStruct(this Channel channel)
        {
            return new ChannelStruct
            {
                ChannelId = channel.Id,
                Pls = channel.Playlists.Select(x => new Tuple<string, List<string>>(x.Id, x.Items)).ToHashSet(),
                Items = channel.Items.Select(x => new ItemPrivacy { Id = x.Id, Status = x.SyncState }).ToHashSet()
            };
        }

        #endregion
    }
}
