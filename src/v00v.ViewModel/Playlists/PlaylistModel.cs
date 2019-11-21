using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using DynamicData;
using DynamicData.Binding;
using v00v.Model.Core;
using v00v.Model.Entities;
using v00v.Model.Entities.Instance;
using v00v.Model.Enums;
using v00v.Services.ContentProvider;
using v00v.Services.Persistence;
using v00v.ViewModel.Explorer;

namespace v00v.ViewModel.Playlists
{
    public class PlaylistModel : ViewModelBase
    {
        #region Static and Readonly Fields

        private readonly ReadOnlyObservableCollection<Playlist> _entries;
        private readonly ExplorerModel _explorerModel;
        private readonly IItemRepository _itemRepository;
        private readonly IPlaylistRepository _playlistRepository;
        private readonly IYoutubeService _youtubeService;

        #endregion

        #region Fields

        private string _searchText;
        private Playlist _selectedEntry;

        #endregion

        #region Constructors

        public PlaylistModel(Channel channel,
            ExplorerModel exModel,
            Action<byte> setIndex,
            Action<string> setTitle,
            Action<string> setSelect,
            Func<IEnumerable<string>> getExistId = null) : this(AvaloniaLocator.Current.GetService<IPlaylistRepository>(),
                                                                AvaloniaLocator.Current.GetService<IItemRepository>(),
                                                                AvaloniaLocator.Current.GetService<IYoutubeService>())
        {
            _explorerModel = exModel;

            All = new SourceCache<Playlist, string>(m => m.Id);

            if (channel.IsStateChannel)
            {
                UnlistedPlaylist unlisted = UnlistedPlaylist.Instance;
                PlannedPlaylist planned = PlannedPlaylist.Instance;
                WatchedPlaylist watched = WatchedPlaylist.Instance;
                SearchPlaylist searched = SearchPlaylist.Instance;
                PopularPlaylist popular = PopularPlaylist.Instance;

                var stateCounts = _playlistRepository.GetStatePlaylistsItemsCount();
                unlisted.Count = stateCounts[0];
                planned.Count = stateCounts[1];
                watched.Count = stateCounts[2];

                channel.Playlists.AddRange(new Playlist[] { planned, watched, unlisted, searched, popular });
                All.AddOrUpdate(channel.Playlists);

                SearchedPl = searched;
                PopularPl = popular;

                SubscribeSearchChange(setIndex, setTitle, getExistId);
                SubscribePopular(setIndex, setTitle, getExistId);
            }
            else
            {
                if (channel.Playlists.Count == 0)
                {
                    var res = _playlistRepository.GetPlaylists(channel.Id).ToList();

                    if (res.Any())
                    {
                        int i = 0;
                        foreach (var pl in res.SkipLast(1).OrderBy(y => y.Title))
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

            SubscribePlChange(setIndex, setSelect, channel);

            All.Connect().Filter(this.WhenValueChanged(t => t.SearchText).Select(BuildFilter))
                .Sort(SortExpressionComparer<Playlist>.Ascending(t => t.Order), SortOptimisations.ComparesImmutableValuesOnly, 25)
                .Bind(out _entries).DisposeMany().Subscribe();

            CopyItemCommand = new Command(async x => await CopyItem((string)x));
            DownloadItemCommand = new Command(x => DownloadItem((string)x));
            DeleteCommand = new Command(x => DeleteFiles());
        }

        private PlaylistModel(IPlaylistRepository playlistRepository, IItemRepository itemRepository, IYoutubeService youtubeService)
        {
            _playlistRepository = playlistRepository;
            _itemRepository = itemRepository;
            _youtubeService = youtubeService;
        }

        #endregion

        #region Properties

        public SourceCache<Playlist, string> All { get; }
        public ICommand CopyItemCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand DownloadItemCommand { get; }
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

        private PopularPlaylist PopularPl { get; }

        private SearchPlaylist SearchedPl { get; }

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

        private async Task CopyItem(string par)
        {
            if (SelectedEntry == null)
            {
                return;
            }

            string res = null;
            switch (par)
            {
                case "link":
                    res = SelectedEntry.Link;
                    break;
                case "title":
                    res = SelectedEntry.Title;
                    break;
            }

            if (!string.IsNullOrEmpty(res))
            {
                await Application.Current.Clipboard.SetTextAsync(res);
            }
        }

        private void DeleteFiles()
        {
            if (SelectedEntry == null)
            {
                return;
            }

            Parallel.ForEach(_explorerModel.All.Items.Where(x => SelectedEntry.Items.Contains(x.Id) && x.Downloaded),
                             new ParallelOptions { MaxDegreeOfParallelism = SelectedEntry.Count },
                             async x =>
                             {
                                 await _explorerModel.DeleteItem(x);
                             });
        }

        private void DownloadItem(string par)
        {
            if (SelectedEntry == null)
            {
                return;
            }

            Parallel.ForEach(_explorerModel.All.Items.Where(x => SelectedEntry.Items.Contains(x.Id)),
                             new ParallelOptions { MaxDegreeOfParallelism = SelectedEntry.Count },
                             async x =>
                             {
                                 await _explorerModel.Download(par, x);
                             });
        }

        private async Task FillPlaylistItems(Playlist playlist)
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

            playlist.StateItems = newItems;
        }

        private void SubscribePlChange(Action<byte> setPageIndex, Action<string> setSelect, Channel channel)
        {
            this.WhenValueChanged(x => x.SelectedEntry).Subscribe(async entry =>
            {
                if (entry != null)
                {
                    setSelect?.Invoke(channel.Id);
                    byte index;
                    if (entry.IsStatePlaylist)
                    {
                        _explorerModel.SetMenu(false);
                        if (entry.Id == "-3")
                        {
                            entry.IsSearchPlaylist = true;
                            _explorerModel.SetMenu(true);
                        }
                        else
                        {
                            SearchedPl.IsSearchPlaylist = false;
                        }

                        if (entry.Id == "-4")
                        {
                            entry.IsPopularPlaylist = true;
                            _explorerModel.SetMenu(true);
                        }
                        else
                        {
                            PopularPl.IsPopularPlaylist = false;
                        }

                        if (_explorerModel.All.Items.Any())
                        {
                            _explorerModel.All.Clear();
                        }

                        if (entry.StateItems == null)
                        {
                            await FillPlaylistItems(entry);
                        }

                        if (entry.StateItems == null || entry.StateItems.Count == 0)
                        {
                            index = 1;
                        }
                        else
                        {
                            if (entry.StateItems != null)
                            {
                                _explorerModel.All.AddOrUpdate(entry.StateItems);
                            }

                            index = (byte)(_explorerModel.All.Count == 0 ? 1 : 0);
                        }
                    }
                    else
                    {
                        index = (byte)(entry.Items.Count == 0 ? 1 : 0);
                    }

                    setPageIndex.Invoke(index);
                    _explorerModel.SelectedPlaylistId = entry.Id;
                }
                else
                {
                    _explorerModel.SetMenu(false);
                    _explorerModel.SelectedPlaylistId = null;
                    if (channel.IsStateChannel)
                    {
                        SearchedPl.IsSearchPlaylist = false;
                        PopularPl.IsPopularPlaylist = false;
                        if (_explorerModel.All.Count > 0)
                        {
                            _explorerModel.All.Remove(_explorerModel.All.Items.Where(x => x.SyncState != SyncState.Added));
                        }
                    }
                }
            });
        }

        private void SubscribePopular(Action<byte> setPageIndex, Action<string> setTitle, Func<IEnumerable<string>> getExistId)
        {
            this.WhenValueChanged(x => x.PopularPl.SelectedCountry).Throttle(TimeSpan.FromMilliseconds(500)).Subscribe(entry =>
            {
                if (!string.IsNullOrWhiteSpace(entry))
                {
                    setTitle?.Invoke($"Working {PopularPl.SelectedCountry} popular..");
                    Stopwatch sw = Stopwatch.StartNew();
                    PopularPl.StateItems = _youtubeService.GetPopularItems(entry, getExistId.Invoke()).GetAwaiter().GetResult();
                    setTitle?.Invoke($"Done {PopularPl.SelectedCountry}. Elapsed: {sw.Elapsed.Hours}h {sw.Elapsed.Minutes}m {sw.Elapsed.Seconds}s {sw.Elapsed.Milliseconds}ms");
                    if (PopularPl.StateItems.Count > 0)
                    {
                        setPageIndex(0);
                        if (_explorerModel.All.Items.Any())
                        {
                            _explorerModel.All.Clear();
                        }

                        _explorerModel.All.AddOrUpdate(PopularPl.StateItems);
                        _explorerModel.SetMenu(true);
                    }
                }
                else
                {
                    PopularPl.StateItems?.Clear();
                    setPageIndex(1);
                }
            });
        }

        private void SubscribeSearchChange(Action<byte> setPageIndex, Action<string> setTitle, Func<IEnumerable<string>> getExistId)
        {
            this.WhenValueChanged(x => x.SearchedPl.SearchText).Throttle(TimeSpan.FromMilliseconds(2000)).Subscribe(entry =>
            {
                if (!string.IsNullOrEmpty(entry))
                {
                    if (entry.Length < 2)
                    {
                        return;
                    }

                    var term = entry.Trim();
                    setTitle?.Invoke($"Search {term}..");
                    bool menu;
                    if (SearchedPl.EnableGlobalSearch)
                    {
                        SearchedPl.StateItems = _youtubeService
                            .GetSearchedItems(term, getExistId.Invoke(), PopularPl.SelectedCountry ?? "RU").GetAwaiter().GetResult();
                        menu = true;
                    }
                    else
                    {
                        SearchedPl.StateItems = _itemRepository.GetItemsByTitle(term, 50).GetAwaiter().GetResult();
                        menu = false;
                    }

                    if (SearchedPl.StateItems?.Count > 0)
                    {
                        setPageIndex(0);
                        if (_explorerModel.All.Items.Any())
                        {
                            _explorerModel.All.Clear();
                        }

                        _explorerModel.All.AddOrUpdate(SearchedPl.StateItems);
                        _explorerModel.SetMenu(menu);
                        setTitle?.Invoke($"Found: {SearchedPl.StateItems?.Count}");
                    }
                    else
                    {
                        setTitle?.Invoke($"Not found: {term} :(");
                    }
                }
                else
                {
                    SearchedPl.StateItems?.Clear();
                    setPageIndex(1);
                }
            });
        }

        #endregion
    }
}
