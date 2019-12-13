using System.Collections.Generic;
using System.Linq;
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

        public IEnumerable<Playlist> GetPlaylists(string channelId)
        {
            using (var context = _contextFactory.CreateVideoContext())
            {
                foreach (var playlist in context.Playlists.AsNoTracking().Include(x => x.Items).AsNoTracking()
                    .Where(x => x.ChannelId == channelId))
                {
                    yield return _mapper.Map<Playlist>(playlist);
                }
            }
        }

        public IEnumerable<Item> GetPlaylistsItems(WatchState state)
        {
            using (var context = _contextFactory.CreateVideoContext())
            {
                foreach (var item in context.Items.AsNoTracking().Include(x => x.Channel).AsNoTracking()
                    .Where(x => x.WatchState == (byte)state))
                {
                    yield return _mapper.Map<Item>(item);
                }
            }
        }

        public int[] GetStatePlaylistsItemsCount()
        {
            using (var context = _contextFactory.CreateVideoContext())
            {
                return new[]
                {
                    context.Items.AsNoTracking().Count(x => x.SyncState == 2),
                    context.Items.AsNoTracking().Count(x => x.WatchState == 2),
                    context.Items.AsNoTracking().Count(x => x.WatchState == 1)
                };
            }
        }

        public IEnumerable<Item> GetUnlistedPlaylistsItems()
        {
            using (var context = _contextFactory.CreateVideoContext())
            {
                foreach (var item in context.Items.AsNoTracking().Include(x => x.Channel).AsNoTracking()
                    .Where(x => x.SyncState == (byte)SyncState.Unlisted || x.SyncState == (byte)SyncState.Deleted))
                {
                    yield return _mapper.Map<Item>(item);
                }
            }
        }

        #endregion
    }
}
