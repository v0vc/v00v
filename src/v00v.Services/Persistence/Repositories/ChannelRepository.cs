using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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
                        channel.Playlists.RemoveAll(x => x.Id == channel.Id);
                        await context.Channels.AddAsync(_mapper.Map<Database.Models.Channel>(channel));
                        int res = await context.SaveChangesAsync();
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
            using (VideoContext context = _contextFactory.CreateVideoContext())
            {
                using (IDbContextTransaction transaction = TransactionHelper.Get(context))
                {
                    try
                    {
                        channels.ForEach(x => x.Playlists.RemoveAll(y => y.Id == x.Id));
                        await context.Channels.AddRangeAsync(channels.Select(x => _mapper.Map<Database.Models.Channel>(x)));
                        int res = await context.SaveChangesAsync();
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
            using (VideoContext context = _contextFactory.CreateVideoContext())
            {
                using (IDbContextTransaction transaction = TransactionHelper.Get(context))
                {
                    try
                    {
                        //int res = await context.Database.ExecuteSqlRawAsync($"DELETE FROM [Channels] WHERE [Id]='{channelId}'");
                        int res = await context.Database.ExecuteSqlCommandAsync($"DELETE FROM [Channels] WHERE [Id]='{channelId}'");
                        //await context.SaveChangesAsync();
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

        public async Task<List<Channel>> GetChannels()
        {
            using (VideoContext context = _contextFactory.CreateVideoContext())
            {
                try
                {
                    return await context.Channels.AsNoTracking().Include(x => x.Tags).AsNoTracking().OrderBy(x => x.Title)
                        .Select(x => _mapper.Map<Channel>(x)).ToListAsync();
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
                    throw;
                }
            }
        }

        public async Task<List<ChannelStruct>> GetChannelsStruct(bool syncPls)
        {
            using (VideoContext context = _contextFactory.CreateVideoContext())
            {
                try
                {
                    return await (syncPls
                        ? context.Channels.AsNoTracking().Include(x => x.Items).AsNoTracking().Include(x => x.Playlists)
                        : context.Channels.AsNoTracking().Include(x => x.Items).AsNoTracking()).Select(ch => new ChannelStruct
                    {
                        ChannelId = ch.Id,
                        ChannelTitle = ch.Title,
                        Items = ch.Items.Select(y => y.Id),
                        Playlists = syncPls ? ch.Playlists.Select(x => x.Id) : null
                    }).ToListAsync();
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
                    throw;
                }
            }
        }

        public async Task<int> GetItemsCount(SyncState state, string channelId = null)
        {
            using (VideoContext context = _contextFactory.CreateVideoContext())
            {
                try
                {
                    return channelId == null
                        ? await context.Items.AsNoTracking().CountAsync(x => x.SyncState == (byte)state)
                        : await context.Items.AsNoTracking().CountAsync(x => x.ChannelId == channelId && x.SyncState == (byte)state);
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
                    throw;
                }
            }
        }

        public async Task<int> SaveChannel(string channelId, string newTitle, IEnumerable<int> tags)
        {
            using (VideoContext context = _contextFactory.CreateVideoContext())
            {
                using (IDbContextTransaction transaction = TransactionHelper.Get(context))
                {
                    try
                    {
                        Database.Models.Channel ch = await context.Channels.AsNoTracking().Include(x => x.Tags).AsNoTracking()
                            .FirstOrDefaultAsync(x => x.Id == channelId);
                        if (ch != null)
                        {
                            if (!string.IsNullOrEmpty(newTitle) && ch.Title != newTitle)
                            {
                                ch.Title = newTitle;
                            }

                            if (ch.Tags.Count > 0)
                            {
                                context.ChannelTags.RemoveRange(ch.Tags);
                            }

                            await context.ChannelTags.AddRangeAsync(tags.Select(x => new ChannelTag
                            {
                                TagId = x, ChannelId = channelId
                            }));

                            context.Entry(ch).State = EntityState.Modified;
                            int res = await context.SaveChangesAsync();
                            transaction.Commit();
                            return res;
                        }

                        transaction.Rollback();
                        return -1;
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
            using (VideoContext context = _contextFactory.CreateVideoContext())
            {
                using (IDbContextTransaction transaction = TransactionHelper.Get(context))
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
                                foreach ((string key, List<ItemPrivacy> value) in fdiff.ExistPlaylists)
                                {
                                    foreach (ItemPrivacy item in value)
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
                                        using (var command = context.Database.GetDbConnection().CreateCommand())
                                        {
                                            command.CommandText =
                                                $"UPDATE [Playlists] SET Count=Count-{playlist.Count()} WHERE Id='{playlist.Key}'";
                                            //context.Database.OpenConnection();
                                            command.ExecuteNonQuery();
                                        }
                                    }
                                }

                                var added = current.Except(exist, ItemPlaylist.ItemIdPlaylistIdComparer).ToList();
                                if (added.Count > 0)
                                {
                                    await context.ItemPlaylists.AddRangeAsync(added);
                                    foreach (var playlist in added.GroupBy(x => x.PlaylistId))
                                    {
                                        using (var command = context.Database.GetDbConnection().CreateCommand())
                                        {
                                            command.CommandText =
                                                $"UPDATE [Playlists] SET Count=Count+{playlist.Count()} WHERE Id='{playlist.Key}'";
                                            command.ExecuteNonQuery();
                                        }
                                    }
                                }
                            }
                        }

                        // items
                        if (fdiff.NewItems.Count > 0)
                        {
                            await context.Items.AddRangeAsync(fdiff.NewItems.Select(x => _mapper.Map<Item>(x)));
                        }

                        // channels
                        if (fdiff.Channels.Count > 0)
                        {
                            var chs = context.Channels.AsNoTracking().Where(x => fdiff.Channels.ContainsKey(x.Id)).ToList();
                            foreach ((string s, ChannelStats value) in fdiff.Channels)
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

                            foreach (Database.Models.Channel channel in chs)
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

        public async Task<int> UpdateChannelsCount(string channelId, int count)
        {
            using (VideoContext context = _contextFactory.CreateVideoContext())
            {
                using (IDbContextTransaction transaction = TransactionHelper.Get(context))
                {
                    try
                    {
                        int res = channelId != null
                            ? await
                                context.Database.ExecuteSqlCommandAsync($"UPDATE [Channels] SET [Count]='{count}' WHERE [Id]='{channelId}'")
                            : await context.Database.ExecuteSqlCommandAsync($"UPDATE [Channels] SET [Count]='{count}'");

                        //await context.SaveChangesAsync();
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
            using (VideoContext context = _contextFactory.CreateVideoContext())
            {
                using (IDbContextTransaction transaction = TransactionHelper.Get(context))
                {
                    try
                    {
                        int res = channelId != null
                            ? await
                                context.Database
                                    .ExecuteSqlCommandAsync($"UPDATE [Items] SET [SyncState]='{state}' WHERE [ChannelId]='{channelId}' AND [SyncState]=1")
                            : await context.Database.ExecuteSqlCommandAsync($"UPDATE [Items] SET [SyncState]='{state}' WHERE [SyncState]=1");

                        //await context.SaveChangesAsync();
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
