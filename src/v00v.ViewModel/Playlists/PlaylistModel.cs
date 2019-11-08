using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using v00v.Services.Persistence;
using v00v.ViewModel.Explorer;

namespace v00v.ViewModel.Playlists
{
    public class PlaylistModel : ViewModelBase
    {
        #region Static and Readonly Fields

        private readonly ReadOnlyObservableCollection<Playlist> _entries;
        private readonly ExplorerModel _explorerModel;
        private readonly IPlaylistRepository _playlistRepository;

        #endregion

        #region Fields

        private string _searchText;
        private Playlist _selectedEntry;

        #endregion

        #region Constructors

        public PlaylistModel(Channel channel, ExplorerModel explorerModel, Action<byte> setPageIndex) : this(AvaloniaLocator.Current
                                                                                                                 .GetService<
                                                                                                                     IPlaylistRepository
                                                                                                                 >())
        {
            _explorerModel = explorerModel;

            All = new SourceCache<Playlist, string>(m => m.Id);

            if (channel.IsStateChannel)
            {
                UnlistedPlaylist unlisted = UnlistedPlaylist.Instance;
                PlannedPlaylist planned = PlannedPlaylist.Instance;
                WatchedPlaylist watched = WatchedPlaylist.Instance;
                var stateCounts = _playlistRepository.GetStatePlaylistsItemsCount().GetAwaiter().GetResult();
                unlisted.Count = stateCounts[0];
                planned.Count = stateCounts[1];
                watched.Count = stateCounts[2];
                channel.Playlists.Add(planned);
                channel.Playlists.Add(watched);
                channel.Playlists.Add(unlisted);
                All.AddOrUpdate(channel.Playlists);
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

            this.WhenValueChanged(x => x.SelectedEntry).Subscribe(async entry =>
            {
                if (entry != null)
                {
                    byte index;
                    if (entry.IsStatePlaylist)
                    {
                        if (_explorerModel.All.Items.Any())
                        {
                            _explorerModel.All.Clear();
                        }

                        if (!entry.HasFullLoad)
                        {
                            await FillPlaylistItems(entry);
                        }

                        if (entry.StateItems == null)
                        {
                            index = 1;
                        }
                        else
                        {
                            _explorerModel.All.AddOrUpdate(entry.StateItems);
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
                    _explorerModel.SelectedPlaylistId = null;
                    if (channel.IsStateChannel)
                    {
                        //_explorerModel.All.Clear();
                        _explorerModel.All.Remove(_explorerModel.All.Items.Where(x => x.SyncState != SyncState.Added));
                    }
                }
            });

            All.Connect().Filter(this.WhenValueChanged(t => t.SearchText).Select(BuildFilter))
                .Sort(SortExpressionComparer<Playlist>.Ascending(t => t.Order), SortOptimisations.ComparesImmutableValuesOnly, 25)
                .Bind(out _entries).DisposeMany().Subscribe();

            CopyItemCommand = new Command(async x => await CopyItem((string)x));
            DownloadItemCommand = new Command(x => DownloadItem((string)x));
            DeleteCommand = new Command(x => DeleteFiles());
        }

        private PlaylistModel(IPlaylistRepository playlistRepository)
        {
            _playlistRepository = playlistRepository;
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
            playlist.HasFullLoad = true;
        }

        #endregion
    }
}
