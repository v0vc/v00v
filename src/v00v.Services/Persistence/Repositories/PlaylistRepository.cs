using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using v00v.Model.Entities;
using v00v.Model.Enums;
using v00v.Services.Database;

namespace v00v.Services.Persistence.Repositories
{
    public class PlaylistRepository : IPlaylistRepository
    {
        #region Static and Readonly Fields

        private readonly IContextFactory _contextFactory;

        private readonly IMapper _mapper;

        #endregion

        #region Constructors

        public PlaylistRepository(IContextFactory contextFactory, IMapper mapper)
        {
            _contextFactory = contextFactory;
            _mapper = mapper;
        }

        #endregion

        #region Methods

        public async Task<List<Playlist>> GetPlaylists(string channelId)
        {
            using (VideoContext context = _contextFactory.CreateVideoContext())
            {
                try
                {
                    return await context.Playlists.AsNoTracking().Include(x => x.Items).AsNoTracking()
                        .Where(x => x.ChannelId == channelId).Select(x => _mapper.Map<Playlist>(x)).ToListAsync();
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
                    throw;
                }
            }
        }

        public async Task<List<Item>> GetPlaylistsItems(WatchState state)
        {
            using (VideoContext context = _contextFactory.CreateVideoContext())
            {
                try
                {
                    return await context.Items.AsNoTracking().Include(x => x.Channel).AsNoTracking()
                        .Where(x => x.WatchState == (byte)state).Select(x => _mapper.Map<Item>(x)).ToListAsync();
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
                    throw;
                }
            }
        }

        public async Task<int> GetPlaylistsItemsCount(WatchState state)
        {
            using (VideoContext context = _contextFactory.CreateVideoContext())
            {
                try
                {
                    return await context.Items.AsNoTracking().Where(x => x.WatchState == (byte)state).CountAsync();
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
                    throw;
                }
            }
        }

        public async Task<int> GetPlaylistsItemsCount(SyncState state)
        {
            using (VideoContext context = _contextFactory.CreateVideoContext())
            {
                try
                {
                    return await context.Items.AsNoTracking().Where(x => x.SyncState == (byte)state).CountAsync();
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
                    throw;
                }
            }
        }

        public async Task<List<int>> GetStatePlaylistsItemsCount()
        {
            using (VideoContext context = _contextFactory.CreateVideoContext())
            {
                try
                {
                    return new List<int>
                    {
                        await context.Items.AsNoTracking().Where(x => x.SyncState == 2).CountAsync(),
                        await context.Items.AsNoTracking().Where(x => x.WatchState == 2).CountAsync(),
                        await context.Items.AsNoTracking().Where(x => x.WatchState == 1).CountAsync()
                    };
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
                    throw;
                }
            }
        }

        public async Task<List<Item>> GetUnlistedPlaylistsItems()
        {
            using (VideoContext context = _contextFactory.CreateVideoContext())
            {
                try
                {
                    return await context.Items.AsNoTracking().Include(x => x.Channel).AsNoTracking()
                        .Where(x => x.SyncState == (byte)SyncState.Unlisted || x.SyncState == (byte)SyncState.Deleted)
                        .Select(x => _mapper.Map<Item>(x)).ToListAsync();
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
                    throw;
                }
            }
        }

        #endregion
    }
}
