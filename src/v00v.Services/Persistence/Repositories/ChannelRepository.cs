using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using v00v.Model.Enums;
using v00v.Model.SyncEntities;
using v00v.Services.Database;
using v00v.Services.Database.Models;
using v00v.Services.Persistence.Helpers;
using Channel = v00v.Model.Entities.Channel;

namespace v00v.Services.Persistence.Repositories
{
    public class ChannelRepository : IChannelRepository
    {
        #region Static and Readonly Fields

        private readonly IContextFactory _contextFactory;
        private readonly IMapper _mapper;

        #endregion

        #region Constructors

        public ChannelRepository(IContextFactory contextFactory, IMapper mapper)
        {
            _contextFactory = contextFactory;
            _mapper = mapper;
        }

        #endregion

        #region Methods

        public async Task<int> AddChannel(Channel channel)
        {
            using (var context = _contextFactory.CreateVideoContext())
            {
                using (var transaction = TransactionHelper.Get(context))
                {
                    try
                    {
                        channel.Playlists.RemoveAll(x => x.Id == channel.Id || x.Id == channel.ExCache || x.Id == channel.PlCache);
                        await context.Channels.AddAsync(_mapper.Map<Database.Models.Channel>(channel));
                        var res = await context.SaveChangesAsync();
                        transaction.Commit();
                        return res;
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine(exception);
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public async Task<int> AddChannels(List<Channel> channels)
        {
            using (var context = _contextFactory.CreateVideoContext())
            {
                using (var transaction = TransactionHelper.Get(context))
                {
                    try
                    {
                        channels.ForEach(x => x.Playlists.RemoveAll(y => y.Id == x.Id || y.Id == x.ExCache || y.Id == x.PlCache));
                        await context.Channels.AddRangeAsync(channels.Select(x => _mapper.Map<Database.Models.Channel>(x)));
                        var res = await context.SaveChangesAsync();
                        transaction.Commit();
                        return res;
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine(exception);
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public async Task<int> DeleteChannel(string channelId)
        {
            using (var context = _contextFactory.CreateVideoContext())
            {
                using (var transaction = TransactionHelper.Get(context))
                {
                    try
                    {
                        var channel = await context.Channels.AsTracking().FirstOrDefaultAsync(x => x.Id == channelId);
                        if (channel == null)
                        {
                            transaction.Rollback();
                            return -1;
                        }

                        context.Channels.Remove(channel);
                        var res = await context.SaveChangesAsync();
                        transaction.Commit();
                        return res;
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine(exception);
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public IEnumerable<Channel> GetChannels()
        {
            using (var context = _contextFactory.CreateVideoContext())
            {
                var i = 0;
                foreach (var channel in context.Channels.AsNoTracking().Include(x => x.Tags).AsNoTracking().OrderBy(x => x.Title))
                {
                    var ch = _mapper.Map<Channel>(channel);
                    ch.Order = i;
                    i++;
                    yield return ch;
                }
            }
        }

        public async Task<List<ChannelStruct>> GetChannelsStruct(bool syncPls, HashSet<string> channels)
        {
            using (var context = _contextFactory.CreateVideoContext())
            {
                if (channels.Count == 2)
                {
                    // one channel
                    var id = channels.Last();
                    if (syncPls)
                    {
                        return await context.Channels.AsNoTracking().Include(x => x.Items).AsNoTracking().Include(x => x.Playlists)
                            .Where(x => x.Id == id).Select(channel => new ChannelStruct
                            {
                                ChannelId = channel.Id,
                                ChannelTitle = channel.Title,
                                Items = channel.Items.Select(y => y.Id),
                                UnlistedItems =
                                    channel.Items.Where(x => x.SyncState == 2 || x.SyncState == 3)
                                        .Select(y => y.Id).ToHashSet(),
                                Playlists = channel.Playlists.Select(x => x.Id)
                            }).ToListAsync();
                    }
                    else
                    {
                        return await context.Channels.AsNoTracking().Include(x => x.Items).AsNoTracking().Where(x => x.Id == id)
                            .Select(channel => new ChannelStruct
                            {
                                ChannelId = channel.Id,
                                ChannelTitle = channel.Title,
                                Items = channel.Items.Select(y => y.Id),
                                UnlistedItems = channel.Items.Where(x => x.SyncState == 2 || x.SyncState == 3)
                                    .Select(y => y.Id).ToHashSet()
                            }).ToListAsync();
                    }
                }
                else
                {
                    if (syncPls)
                    {
                        return await context.Channels.AsNoTracking().Include(x => x.Items).AsNoTracking().Include(x => x.Playlists)
                            .Select(channel => new ChannelStruct
                            {
                                ChannelId = channel.Id,
                                ChannelTitle = channel.Title,
                                Items = channel.Items.Select(y => y.Id),
                                UnlistedItems =
                                    channel.Items.Where(x => x.SyncState == 2 || x.SyncState == 3).Select(y => y.Id)
                                        .ToHashSet(),
                                Playlists = channel.Playlists.Select(x => x.Id)
                            }).ToListAsync();
                    }
                    else
                    {
                        return await context.Channels.AsNoTracking().Include(x => x.Items).AsNoTracking()
                            .Select(channel => new ChannelStruct
                            {
                                ChannelId = channel.Id,
                                ChannelTitle = channel.Title,
                                Items = channel.Items.Select(y => y.Id),
                                UnlistedItems = channel.Items.Where(x => x.SyncState == 2 || x.SyncState == 3)
                                    .Select(y => y.Id).ToHashSet()
                            }).ToListAsync();
                    }
                }
            }
        }

        public IEnumerable<ChannelStruct> GetChannelsStructYeild(bool syncPls, HashSet<string> channels)
        {
            using (var context = _contextFactory.CreateVideoContext())
            {
                if (channels.Count == 2)
                {
                    // one channel
                    var id = channels.Last();
                    if (syncPls)
                    {
                        var channel = context.Channels.AsNoTracking().Include(x => x.Items).AsNoTracking().Include(x => x.Playlists)
                            .FirstOrDefault(x => x.Id == id);

                        if (channel != null)
                        {
                            yield return new ChannelStruct
                            {
                                ChannelId = channel.Id,
                                ChannelTitle = channel.Title,
                                Items = channel.Items.Select(y => y.Id),
                                UnlistedItems =
                                    channel.Items.Where(x => x.SyncState == 2 || x.SyncState == 3).Select(y => y.Id).ToHashSet(),
                                Playlists = channel.Playlists.Select(x => x.Id)
                            };
                        }
                    }
                    else
                    {
                        var channel = context.Channels.AsNoTracking().Include(x => x.Items).AsNoTracking()
                            .FirstOrDefault(x => x.Id == id);

                        if (channel != null)
                        {
                            yield return new ChannelStruct
                            {
                                ChannelId = channel.Id,
                                ChannelTitle = channel.Title,
                                Items = channel.Items.Select(y => y.Id),
                                UnlistedItems = channel.Items.Where(x => x.SyncState == 2 || x.SyncState == 3).Select(y => y.Id)
                                    .ToHashSet()
                            };
                        }
                    }
                }
                else
                {
                    if (syncPls)
                    {
                        foreach (var channel in context.Channels.AsNoTracking().Include(x => x.Items).AsNoTracking()
                            .Include(x => x.Playlists))
                        {
                            yield return new ChannelStruct
                            {
                                ChannelId = channel.Id,
                                ChannelTitle = channel.Title,
                                Items = channel.Items.Select(y => y.Id),
                                UnlistedItems =
                                    channel.Items.Where(x => x.SyncState == 2 || x.SyncState == 3).Select(y => y.Id).ToHashSet(),
                                Playlists = channel.Playlists.Select(x => x.Id)
                            };
                        }
                    }
                    else
                    {
                        foreach (var channel in context.Channels.AsNoTracking().Include(x => x.Items).AsNoTracking())
                        {
                            yield return new ChannelStruct
                            {
                                ChannelId = channel.Id,
                                ChannelTitle = channel.Title,
                                Items = channel.Items.Select(y => y.Id),
                                UnlistedItems = channel.Items.Where(x => x.SyncState == 2 || x.SyncState == 3).Select(y => y.Id)
                                    .ToHashSet()
                            };
                        }
                    }
                }
            }
        }

        public async Task<Dictionary<string, int>> GetChannelStateCount(WatchState watchState)
        {
            using (var context = _contextFactory.CreateVideoContext())
            {
                return await context.Items.AsNoTracking().GroupBy(x => x.ChannelId)
                    .ToDictionaryAsync(x => x.Key, y => y.Count(x => x.WatchState == (byte)watchState));
            }
        }

        public string GetChannelSubtitle(string channelId)
        {
            using (var context = _contextFactory.CreateVideoContext())
            {
                return context.Channels.AsNoTracking().FirstOrDefault(x => x.Id == channelId)?.SubTitle;
            }
        }

        public int GetItemsCount(SyncState state, string channelId = null)
        {
            using (var context = _contextFactory.CreateVideoContext())
            {
                return channelId == null
                    ? context.Items.AsNoTracking().Count(x => x.SyncState == (byte)state)
                    : context.Items.AsNoTracking().Count(x => x.ChannelId == channelId && x.SyncState == (byte)state);
            }
        }

        public async Task<int> SaveChannel(string channelId, string newTitle, IEnumerable<int> tags)
        {
            using (var context = _contextFactory.CreateVideoContext())
            {
                using (var transaction = TransactionHelper.Get(context))
                {
                    try
                    {
                        var ch = await context.Channels.AsNoTracking().Include(x => x.Tags).AsNoTracking()
                            .FirstOrDefaultAsync(x => x.Id == channelId);

                        if (ch == null)
                        {
                            transaction.Rollback();
                            return -1;
                        }

                        if (!string.IsNullOrEmpty(newTitle) && ch.Title != newTitle)
                        {
                            ch.Title = newTitle;
                        }

                        if (ch.Tags.Count > 0)
                        {
                            context.ChannelTags.RemoveRange(ch.Tags);
                        }

                        await context.ChannelTags.AddRangeAsync(tags.Select(x => new ChannelTag { TagId = x, ChannelId = channelId }));

                        context.Entry(ch).State = EntityState.Modified;
                        var res = await context.SaveChangesAsync();
                        transaction.Commit();
                        return res;
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine(exception);
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public async Task<int> StoreDiff(SyncDiff fdiff)
        {
            using (var context = _contextFactory.CreateVideoContext())
            {
                using (var transaction = TransactionHelper.Get(context))
                {
                    try
                    {
                        if (fdiff.SyncPls)
                        {
                            // playlists
                            if (fdiff.DeletedPlaylists.Count > 0)
                            {
                                context.Playlists.RemoveRange(context.Playlists.AsNoTracking()
                                                                  .Where(x => fdiff.DeletedPlaylists.Contains(x.Id)));
                            }

                            if (fdiff.NewPlaylists.Count > 0)
                            {
                                var dbPls = fdiff.NewPlaylists.Select(x => _mapper.Map<Playlist>(x)).ToList();
                                await context.Playlists.AddRangeAsync(dbPls);
                                await context.ItemPlaylists.AddRangeAsync(dbPls.SelectMany(x => x.Items));
                            }

                            // playlist-items
                            if (fdiff.ExistPlaylists.Count > 0)
                            {
                                var current = new List<ItemPlaylist>();
                                foreach ((string key, var value) in fdiff.ExistPlaylists)
                                {
                                    foreach (var item in value)
                                    {
                                        current.Add(new ItemPlaylist { ItemId = item.Id, PlaylistId = key });
                                    }
                                }

                                var exist = context.ItemPlaylists.AsNoTracking()
                                    .Where(x => current.Select(y => y.PlaylistId).Contains(x.PlaylistId)).ToList();

                                var deleted = exist.Except(current, ItemPlaylist.ItemIdPlaylistIdComparer).ToList();

                                if (deleted.Count > 0)
                                {
                                    context.ItemPlaylists.RemoveRange(deleted);
                                    foreach (var playlist in deleted.GroupBy(x => x.PlaylistId))
                                    {
                                        var pl = await context.Playlists.AsTracking().FirstOrDefaultAsync(x => x.Id == playlist.Key);
                                        if (pl == null)
                                        {
                                            continue;
                                        }

                                        pl.Count -= playlist.Count();
                                        context.Entry(pl).Property(x => x.Count).IsModified = true;
                                    }
                                }

                                var added = current.Except(exist, ItemPlaylist.ItemIdPlaylistIdComparer).ToList();
                                if (added.Count > 0)
                                {
                                    await context.ItemPlaylists.AddRangeAsync(added);
                                    foreach (var playlist in added.GroupBy(x => x.PlaylistId))
                                    {
                                        var pl = await context.Playlists.AsTracking().FirstOrDefaultAsync(x => x.Id == playlist.Key);
                                        if (pl == null)
                                        {
                                            continue;
                                        }

                                        pl.Count += playlist.Count();
                                        context.Entry(pl).Property(x => x.Count).IsModified = true;
                                    }
                                }
                            }
                        }

                        // items
                        if (fdiff.NewItems.Count > 0)
                        {
                            await context.Items.AddRangeAsync(fdiff.NewItems.Select(x => _mapper.Map<Item>(x)));
                        }

                        //become visible
                        foreach (string s in fdiff.NoUnlistedAgain)
                        {
                            var item = await context.Items.AsTracking().FirstOrDefaultAsync(x => x.Id == s);
                            if (item != null && (item.SyncState == 2 || item.SyncState == 3))
                            {
                                item.SyncState = 0;
                                context.Entry(item).Property(x => x.SyncState).IsModified = true;
                            }
                        }

                        // channels
                        if (fdiff.Channels.Count > 0)
                        {
                            var chs = context.Channels.AsNoTracking().Where(x => fdiff.Channels.ContainsKey(x.Id)).ToList();
                            foreach ((string s, var value) in fdiff.Channels)
                            {
                                var ch = chs.First(x => x.Id == s);
                                ch.ViewCount = value.ViewCount;
                                ch.SubsCount = value.SubsCount;
                                if (!string.IsNullOrEmpty(value.Description))
                                {
                                    ch.SubTitle = value.Description;
                                }

                                if (value.ItemsCount > 0)
                                {
                                    ch.ItemsCount += value.ItemsCount;
                                    ch.Count += (int)value.ItemsCount;
                                }

                                if (value.Timestamp != DateTimeOffset.MinValue)
                                {
                                    ch.Timestamp = value.Timestamp;
                                }
                            }

                            context.Channels.UpdateRange(chs);

                            foreach (var channel in chs)
                            {
                                context.Entry(channel).Property(x => x.SubsCountDiff).IsModified = false;
                                context.Entry(channel).Property(x => x.ViewCountDiff).IsModified = false;
                            }
                        }

                        var res = await context.SaveChangesAsync();
                        transaction.Commit();

                        return res;
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine(exception);
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public async Task<int> UpdateChannelSyncState(string channelId, byte state)
        {
            using (var context = _contextFactory.CreateVideoContext())
            {
                using (var transaction = TransactionHelper.Get(context))
                {
                    try
                    {
                        var items = channelId == null
                            ? context.Items.AsNoTracking().Where(x => x.SyncState == 1)
                            : context.Items.AsNoTracking().Where(x => x.ChannelId == channelId && x.SyncState == 1);

                        foreach (var item in items)
                        {
                            item.SyncState = state;
                            context.Entry(item).Property(x => x.SyncState).IsModified = true;
                        }

                        var res = await context.SaveChangesAsync();
                        transaction.Commit();
                        return res;
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine(exception);
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public async Task<int> UpdateItemsCount(string channelId, int count)
        {
            using (var context = _contextFactory.CreateVideoContext())
            {
                using (var transaction = TransactionHelper.Get(context))
                {
                    try
                    {
                        if (channelId == null)
                        {
                            foreach (var channel in context.Channels.AsNoTracking())
                            {
                                channel.Count = count;
                                context.Entry(channel).Property(x => x.Count).IsModified = true;
                            }
                        }
                        else
                        {
                            var channel = await context.Channels.AsNoTracking().FirstOrDefaultAsync(x => x.Id == channelId);
                            if (channel != null)
                            {
                                channel.Count = count;
                                context.Entry(channel).Property(x => x.Count).IsModified = true;
                            }
                        }

                        var res = await context.SaveChangesAsync();
                        transaction.Commit();
                        return res;
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine(exception);
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public async Task<int> UpdatePlannedCount(string channelId, int count, bool decrease = false)
        {
            using (var context = _contextFactory.CreateVideoContext())
            {
                using (var transaction = TransactionHelper.Get(context))
                {
                    try
                    {
                        var channel = await context.Channels.AsNoTracking().FirstOrDefaultAsync(x => x.Id == channelId);
                        if (channel != null)
                        {
                            if (count == 0)
                            {
                                if (decrease)
                                {
                                    channel.PlannedCount -= 1;
                                }
                                else
                                {
                                    channel.PlannedCount += 1;
                                }
                            }
                            else
                            {
                                channel.PlannedCount = count;
                            }

                            context.Entry(channel).Property(x => x.PlannedCount).IsModified = true;
                        }

                        var res = await context.SaveChangesAsync();
                        transaction.Commit();
                        return res;
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine(exception);
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public async Task<int> UpdateWatchedCount(string channelId, int count, bool decrease = false)
        {
            using (var context = _contextFactory.CreateVideoContext())
            {
                using (var transaction = TransactionHelper.Get(context))
                {
                    try
                    {
                        var channel = await context.Channels.AsNoTracking().FirstOrDefaultAsync(x => x.Id == channelId);
                        if (channel != null)
                        {
                            if (count == 0)
                            {
                                if (decrease)
                                {
                                    channel.WatchedCount -= 1;
                                }
                                else
                                {
                                    channel.WatchedCount += 1;
                                }
                            }
                            else
                            {
                                channel.WatchedCount = count;
                            }

                            context.Entry(channel).Property(x => x.WatchedCount).IsModified = true;
                        }

                        var res = await context.SaveChangesAsync();
                        transaction.Commit();
                        return res;
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine(exception);
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        #endregion
    }
}
