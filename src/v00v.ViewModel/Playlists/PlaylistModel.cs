using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using DynamicData;
using DynamicData.Binding;
using v00v.Model.Core;
using v00v.Model.Entities;
using v00v.Model.Entities.Instance;
using v00v.Model.Enums;
using v00v.Services.Persistence;
using v00v.ViewModel.Catalog;
using v00v.ViewModel.Explorer;

namespace v00v.ViewModel.Playlists
{
    public class PlaylistModel : ViewModelBase, IDisposable
    {
        #region Static and Readonly Fields

        private readonly CatalogModel _catalogModel;
        private readonly IDisposable _cleanUp;
        private readonly ReadOnlyObservableCollection<Playlist> _entries;
        private readonly ExplorerModel _explorerModel;
        private readonly IPlaylistRepository _playlistRepository;

        #endregion

        #region Fields

        private string _searchText;

        private Playlist _selectedEntry;

        #endregion

        #region Constructors

        public PlaylistModel(Channel channel, CatalogModel catalogModel, ExplorerModel explorerModel) : this(AvaloniaLocator.Current
                                                                                                                 .GetService<
                                                                                                                     IPlaylistRepository
                                                                                                                 >())
        {
            _catalogModel = catalogModel;
            _explorerModel = explorerModel;

            All = new SourceCache<Playlist, string>(m => m.Id);

            if (channel.IsStateChannel)
            {
                PlannedPlaylist planned = PlannedPlaylist.Instance;
                WatchedPlaylist watched = WatchedPlaylist.Instance;
                UnlistedPlaylist unlisted = UnlistedPlaylist.Instance;
                planned.Count = _playlistRepository.GetPlaylistsItemsCount(WatchState.Planned).GetAwaiter().GetResult();
                watched.Count = _playlistRepository.GetPlaylistsItemsCount(WatchState.Watched).GetAwaiter().GetResult();
                unlisted.Count = _playlistRepository.GetPlaylistsItemsCount(SyncState.Unlisted).GetAwaiter().GetResult();
                All.AddOrUpdate(new List<Playlist> { planned, watched, unlisted });
            }
            else
            {
                if (channel.Playlists.Count == 0)
                {
                    List<Playlist> res = _playlistRepository.GetPlaylists(channel.Id).GetAwaiter().GetResult();

                    if (res.Count != 0)
                    {
                        int i = 0;
                        foreach (var pl in res.SkipLast(1).OrderBy(y => y.Title.ToLower()))
                        {
                            pl.Order = i;
                            i++;
                        }

                        res.Last().Order = i;

                        res.Sort(SortExpressionComparer<Playlist>.Ascending(x => x.Order));

                        channel.Playlists.AddRange(res);

                        var unlisted = channel.Items.Where(x => x.SyncState == SyncState.Unlisted || x.SyncState == SyncState.Deleted)
                            .ToList();

                        if (unlisted.Count > 0)
                        {
                            UnlistedPlaylist unpl = UnlistedPlaylist.Instance;
                            unpl.IsStatePlaylist = false;
                            unpl.Id = channel.Id;
                            unpl.Order = channel.Playlists.Count;
                            unpl.Count = unlisted.Count;
                            unpl.Items.AddRange(unlisted.Select(x => x.Id));
                            channel.Playlists.Add(unpl);
                        }

                        All.AddOrUpdate(channel.Playlists);
                    }
                }
                else
                {
                    All.AddOrUpdate(channel.Playlists);
                }
            }

            var selector = this.WhenValueChanged(x => x.SelectedEntry).Subscribe(async entry =>
            {
                if (entry != null)
                {
                    if (entry.IsStatePlaylist)
                    {
                        await FillPlaylistItems(entry);
                    }

                    _explorerModel.SelectedPlaylistId = entry.Id;
                }
                else
                {
                    _explorerModel.SelectedPlaylistId = null;
                    if (_catalogModel.SelectedEntry.IsStateChannel)
                    {
                        _explorerModel.All.Clear();
                        _explorerModel.All.AddOrUpdate(_catalogModel.SelectedEntry.Items);
                    }
                }
            });

            IDisposable loader = All.Connect().Filter(this.WhenValueChanged(t => t.SearchText).Select(BuildFilter))
                .Sort(SortExpressionComparer<Playlist>.Ascending(t => t.Order), SortOptimisations.ComparesImmutableValuesOnly, 25)
                .Bind(out _entries).DisposeMany().Subscribe();

            _cleanUp = new CompositeDisposable(All, selector, loader, _catalogModel, _explorerModel);
        }

        private PlaylistModel(IPlaylistRepository playlistRepository)
        {
            _playlistRepository = playlistRepository;
        }

        #endregion

        #region Properties

        public SourceCache<Playlist, string> All { get; }

        public IEnumerable<Playlist> Entries => _entries;

        public string SearchText
        {
            get => _searchText;
            set => Update(ref _searchText, value);
        }

        public Playlist SelectedEntry
        {
            get => _selectedEntry;
            set => Update(ref _selectedEntry, value);
        }

        #endregion

        #region Static Methods

        private static Func<Playlist, bool> BuildFilter(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return x => true;
            }

            return x => x.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Methods

        public void Dispose()
        {
            _cleanUp?.Dispose();
        }

        private async Task FillPlaylistItems(Playlist playlist)
        {
            _explorerModel.All.Clear();
            if (playlist.HasFullLoad)
            {
                _explorerModel.All.AddOrUpdate(playlist.StateItems);
            }
            else
            {
                List<Item> newItems = null;
                switch (playlist.Id)
                {
                    case "-2":
                        newItems = await _playlistRepository.GetUnlistedPlaylistsItems();
                        break;
                    case "-1":
                        newItems = await _playlistRepository.GetPlaylistsItems(WatchState.Planned);
                        break;
                    case "0":
                        newItems = await _playlistRepository.GetPlaylistsItems(WatchState.Watched);
                        break;
                }

                if (newItems != null && newItems.Any())
                {
                    playlist.StateItems = newItems;
                    //playlist.Items.AddRange(newItems.Select(x => x.Id));
                    playlist.HasFullLoad = playlist.Items.Count > 0;
                    _explorerModel.All.AddOrUpdate(newItems);
                }
            }
        }

        #endregion
    }
}
