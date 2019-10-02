using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using v00v.Model.Enums;
using v00v.Services.Database;
using v00v.Services.Database.Models;
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

        public async Task<List<Model.Entities.Item>> GetItems(string channelId)
        {
            using (VideoContext context = _contextFactory.CreateVideoContext())
            {
                try
                {
                    return await context.Items.AsNoTracking().Where(x => x.ChannelId == channelId).Include(x => x.Channel).AsNoTracking()
                        .Select(x => _mapper.Map<v00v.Model.Entities.Item>(x)).ToListAsync();
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
                    throw;
                }
            }
        }

        public async Task<List<Model.Entities.Item>> GetItemsBySyncState(SyncState state)
        {
            using (VideoContext context = _contextFactory.CreateVideoContext())
            {
                try
                {
                    return await context.Items.AsNoTracking().Where(x => x.SyncState == (byte)state).Include(x => x.Channel)
                        .AsNoTracking().Select(x => _mapper.Map<v00v.Model.Entities.Item>(x)).ToListAsync();
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
                    throw;
                }
            }
        }

        public async Task<List<Model.Entities.Item>> GetItemsByTitle(string search, string channelId, int resultCount)
        {
            using (VideoContext context = _contextFactory.CreateVideoContext())
            {
                try
                {
                    return channelId == null
                        ? await context.Items.AsNoTracking().Where(x => x.Title.ToLower().Contains(search))
                            .OrderByDescending(x => x.Timestamp).Take(resultCount).Select(x => _mapper.Map<v00v.Model.Entities.Item>(x)).ToListAsync()
                        : await context.Items.AsNoTracking().Where(x => x.Title.ToLower().Contains(search) && x.ChannelId == channelId)
                            .OrderByDescending(x => x.Timestamp).Take(resultCount).Select(x => _mapper.Map<v00v.Model.Entities.Item>(x)).ToListAsync();
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
                    throw;
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

        public async Task<int> SetItemsWatchState(WatchState state, string itemId)
        {
            using (VideoContext context = _contextFactory.CreateVideoContext())
            {
                using (IDbContextTransaction transaction = TransactionHelper.Get(context))
                {
                    try
                    {
                        int res = await
                            context.Database.ExecuteSqlCommandAsync($"UPDATE [Items] SET [WatchState]='{state}' WHERE [Id]='{itemId}'");

                        //context.SaveChanges();
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

        //public long Update(Model.Entities.Item item)
        //{
        //    using (VideoContext context = _contextFactory.CreateVideoContext())
        //    {
        //        using (IDbContextTransaction transaction = TransactionHelper.Get(context))
        //        {
        //            try
        //            {
        //                context.Items.Update(item);
        //                context.Entry(item).Property(x => x.ViewDiff).IsModified = false; // ignore trigger field
        //                context.SaveChanges();
        //                transaction.Commit();
        //                return context.Items.AsNoTracking().Single(x => x.Id == item.Id).ViewDiff;
        //            }
        //            catch (Exception exception)
        //            {
        //                Console.WriteLine(exception);
        //                transaction.Rollback();
        //                throw;
        //            }
        //        }
        //    }
        //}

        public async Task<int> UpdateItemFileName(string itemId, string filename)
        {
            using (VideoContext context = _contextFactory.CreateVideoContext())
            {
                using (IDbContextTransaction transaction = TransactionHelper.Get(context))
                {
                    try
                    {
                        int res =
                            await context.Database
                                .ExecuteSqlCommandAsync($"UPDATE [Items] SET [FileName]='{filename}' WHERE [Id]='{itemId}'");
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

        public async Task<Dictionary<string, long>> UpdateItemsStats(List<Model.Entities.Item> items, string channelId = null)
        {
            using (VideoContext context = _contextFactory.CreateVideoContext())
            {
                using (IDbContextTransaction transaction = TransactionHelper.Get(context))
                {
                    try
                    {
                        var itemsdb = items.Select(x => _mapper.Map<Item>(x)).ToList();
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
                        int res =
                            await context.Database
                                .ExecuteSqlCommandAsync($"UPDATE [Items] SET [WatchState]='{watch}' WHERE [Id]='{parsedId}'");

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
