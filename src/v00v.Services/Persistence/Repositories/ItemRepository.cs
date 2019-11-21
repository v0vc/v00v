using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using v00v.Model.Entities;
using v00v.Model.Enums;
using v00v.Services.Database;
using v00v.Services.Persistence.Helpers;

namespace v00v.Services.Persistence.Repositories
{
    public class ItemRepository : IItemRepository
    {
        #region Static and Readonly Fields

        private readonly IContextFactory _contextFactory;

        private readonly IMapper _mapper;

        #endregion

        #region Constructors

        public ItemRepository(IContextFactory contextFactory, IMapper mapper)
        {
            _contextFactory = contextFactory;
            _mapper = mapper;
        }

        #endregion

        #region Methods

        public async Task<string> GetItemDescription(string itemId)
        {
            using (VideoContext context = _contextFactory.CreateVideoContext())
            {
                try
                {
                    var res = await context.Items.AsNoTracking().Where(x => x.Id == itemId).FirstOrDefaultAsync();

                    return res.Description?.Trim();
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
                    throw;
                }
            }
        }

        public IEnumerable<Item> GetItems(string channelId)
        {
            using (VideoContext context = _contextFactory.CreateVideoContext())
            {
                foreach (Database.Models.Item item in context.Items.AsNoTracking().Where(x => x.ChannelId == channelId)
                    .Include(x => x.Channel).AsNoTracking())
                {
                    yield return _mapper.Map<Item>(item);
                }
            }
        }

        public IEnumerable<Item> GetItemsBySyncState(SyncState state)
        {
            using (VideoContext context = _contextFactory.CreateVideoContext())
            {
                foreach (Database.Models.Item item in context.Items.AsNoTracking().Where(x => x.SyncState == (byte)state)
                    .Include(x => x.Channel).AsNoTracking())
                {
                    yield return _mapper.Map<Item>(item);
                }
            }
        }

        public IEnumerable<Item> GetItemsByTitle(string search, int resultCount)
        {
            using (VideoContext context = _contextFactory.CreateVideoContext())
            {
                var i = 0;
                foreach (Database.Models.Item item in context.Items.AsNoTracking()
                    .Where(x => x.Title.ToLower().Contains(search.ToLower())))
                {
                    if (i == resultCount)
                    {
                        yield break;
                    }

                    i++;
                    yield return _mapper.Map<Item>(item);
                }
            }
        }

        public async Task<Dictionary<string, byte>> GetItemsState()
        {
            using (VideoContext context = _contextFactory.CreateVideoContext())
            {
                try
                {
                    return await context.Items.AsNoTracking().Where(x => x.WatchState != 0)
                        .ToDictionaryAsync(x => x.Id, y => y.WatchState);
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
                    throw;
                }
            }
        }

        public async Task<int> SetItemsWatchState(WatchState state, string itemId, string channelId = null)
        {
            using (VideoContext context = _contextFactory.CreateVideoContext())
            {
                using (IDbContextTransaction transaction = TransactionHelper.Get(context))
                {
                    try
                    {
                        var item = await context.Items.AsNoTracking().FirstOrDefaultAsync(x => x.Id == itemId);
                        if (item == null)
                        {
                            transaction.Rollback();
                            return -1;
                        }

                        var oldState = item.WatchState;
                        item.WatchState = (byte)state;
                        context.Entry(item).Property(x => x.WatchState).IsModified = true;
                        if (channelId != null)
                        {
                            var channel = await context.Channels.AsNoTracking().FirstOrDefaultAsync(x => x.Id == channelId);
                            if (oldState == 0)
                            {
                                switch (state)
                                {
                                    case WatchState.Planned:
                                        channel.PlannedCount += 1;
                                        context.Entry(channel).Property(x => x.PlannedCount).IsModified = true;
                                        break;
                                    case WatchState.Watched:
                                        channel.WatchedCount += 1;
                                        context.Entry(channel).Property(x => x.WatchedCount).IsModified = true;
                                        break;
                                }
                            }

                            if (oldState == 2)
                            {
                                switch (state)
                                {
                                    case WatchState.Notset:
                                        channel.PlannedCount -= 1;
                                        context.Entry(channel).Property(x => x.PlannedCount).IsModified = true;
                                        break;
                                    case WatchState.Watched:
                                        channel.WatchedCount += 1;
                                        channel.PlannedCount -= 1;
                                        context.Entry(channel).Property(x => x.PlannedCount).IsModified = true;
                                        context.Entry(channel).Property(x => x.WatchedCount).IsModified = true;
                                        break;
                                }
                            }

                            if (oldState == 1)
                            {
                                switch (state)
                                {
                                    case WatchState.Notset:
                                        channel.WatchedCount -= 1;
                                        context.Entry(channel).Property(x => x.WatchedCount).IsModified = true;
                                        break;
                                    case WatchState.Planned:
                                        channel.PlannedCount += 1;
                                        channel.WatchedCount -= 1;
                                        context.Entry(channel).Property(x => x.PlannedCount).IsModified = true;
                                        context.Entry(channel).Property(x => x.WatchedCount).IsModified = true;
                                        break;
                                }
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

        public async Task<int> UpdateItemFileName(string itemId, string filename)
        {
            using (VideoContext context = _contextFactory.CreateVideoContext())
            {
                using (IDbContextTransaction transaction = TransactionHelper.Get(context))
                {
                    try
                    {
                        var item = await context.Items.AsTracking().FirstOrDefaultAsync(x => x.Id == itemId);
                        if (item != null)
                        {
                            item.FileName = filename;
                            context.Entry(item).Property(x => x.FileName).IsModified = true;
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

        public async Task<Dictionary<string, long>> UpdateItemsStats(List<Item> items, string channelId = null)
        {
            using (VideoContext context = _contextFactory.CreateVideoContext())
            {
                using (IDbContextTransaction transaction = TransactionHelper.Get(context))
                {
                    try
                    {
                        var itemsdb = items.Select(x => _mapper.Map<Database.Models.Item>(x)).ToList();
                        context.Items.UpdateRange(itemsdb);

                        foreach (var item in itemsdb)
                        {
                            context.Entry(item).Property(x => x.ViewDiff).IsModified = false;
                            context.Entry(item).Property(x => x.Thumbnail).IsModified = false;
                            context.Entry(item).Property(x => x.SyncState).IsModified = false;
                            context.Entry(item).Property(x => x.WatchState).IsModified = false;
                            context.Entry(item).Property(x => x.Title).IsModified = false;
                            context.Entry(item).Property(x => x.Duration).IsModified = false;
                            context.Entry(item).Property(x => x.Id).IsModified = false;
                            context.Entry(item).Property(x => x.FileName).IsModified = false;
                        }

                        await context.SaveChangesAsync();
                        transaction.Commit();

                        return channelId != null
                            ? await context.Items.AsNoTracking()
                                .Where(x => x.ChannelId == channelId && x.ViewDiff > 0 && items.Select(y => y.Id).Contains(x.Id))
                                .ToDictionaryAsync(id => id.Id, v => v.ViewDiff)
                            : await context.Items.AsNoTracking().Where(x => x.ViewDiff > 0 && items.Select(y => y.Id).Contains(x.Id))
                                .ToDictionaryAsync(id => id.Id, v => v.ViewDiff);
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

        public async Task<int> UpdateItemsWatchState(string parsedId, byte watch)
        {
            using (VideoContext context = _contextFactory.CreateVideoContext())
            {
                using (IDbContextTransaction transaction = TransactionHelper.Get(context))
                {
                    try
                    {
                        var item = await context.Items.AsTracking().FirstOrDefaultAsync(x => x.Id == parsedId);
                        if (item != null)
                        {
                            item.WatchState = watch;
                            context.Entry(item).Property(x => x.WatchState).IsModified = true;
                        }
                        //int res =
                        //    await context.Database
                        //        .ExecuteSqlCommandAsync($"UPDATE [Items] SET [WatchState]='{watch}' WHERE [Id]='{parsedId}'");

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
