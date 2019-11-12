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
using LazyCache;
using v00v.Model.Core;
using v00v.Model.Entities;
using v00v.Model.Entities.Instance;
using v00v.Model.Enums;
using v00v.Model.SyncEntities;
using v00v.Services.Backup;
using v00v.Services.ContentProvider;
using v00v.Services.Persistence;
using v00v.Services.Synchronization;
using v00v.ViewModel.Explorer;
using v00v.ViewModel.Playlists;
using v00v.ViewModel.Popup;
using v00v.ViewModel.Popup.Channel;

namespace v00v.ViewModel.Catalog
{
    public class CatalogModel : ViewModelBase
    {
        #region Static and Readonly Fields

        private readonly IBackupService _backupService;
        private readonly Channel _baseChannel;
        private readonly ExplorerModel _baseExplorerModel;
        private readonly PlaylistModel _basePlaylistModel;
        private readonly IChannelRepository _channelRepository;
        private readonly ReadOnlyObservableCollection<Channel> _entries;
        private readonly IItemRepository _itemRepository;
        private readonly IPopupController _popupController;
        private readonly Action<byte> _setPageIndex;
        private readonly Action<string> _setTitle;
        private readonly ISyncService _syncService;
        private readonly ITagRepository _tagRepository;
        private readonly IYoutubeService _youtubeService;

        #endregion

        #region Fields

        private ChannelSort _channelSort;
        private ExplorerModel _explorerModel;
        private bool _isWorking;
        private bool _massSync = true;
        private PlaylistModel _playlistModel;
        private string _searchText;
        private Channel _selectedEntry;
        private Tag _selectedTag;
        private bool _syncPls;

        #endregion

        #region Constructors

        public CatalogModel(Action<string> setTitle, Action<byte> setPageIndex) :
            this(AvaloniaLocator.Current.GetService<IChannelRepository>(),
                 AvaloniaLocator.Current.GetService<ITagRepository>(),
                 AvaloniaLocator.Current.GetService<IPopupController>(),
                 AvaloniaLocator.Current.GetService<ISyncService>(),
                 AvaloniaLocator.Current.GetService<IYoutubeService>(),
                 AvaloniaLocator.Current.GetService<IItemRepository>(),
                 AvaloniaLocator.Current.GetService<IBackupService>())
        {
            _setTitle = setTitle;
            _setPageIndex = setPageIndex;

            All = new SourceCache<Channel, string>(m => m.Id);

            _baseChannel = StateChannel.Instance;
            _baseChannel.Count = _channelRepository.GetItemsCount(SyncState.Added).GetAwaiter().GetResult();
            _baseExplorerModel = new ExplorerModel(_baseChannel, this, setPageIndex);
            _basePlaylistModel = new PlaylistModel(_baseChannel, _baseExplorerModel, setPageIndex);

            var channels = _channelRepository?.GetChannels().GetAwaiter().GetResult();
            channels.Insert(0, _baseChannel);
            All.AddOrUpdate(channels);

            All.Connect().Filter(this.WhenValueChanged(t => t.SearchText).Select(BuildSearchFilter))
                .Filter(this.WhenValueChanged(t => t.SelectedTag).Select(BuildTagFilter))
                .Sort(GetChannelSorter(), SortOptimisations.ComparesImmutableValuesOnly, 25).Bind(out _entries).DisposeMany().Subscribe();

            this.WhenValueChanged(x => x.SelectedEntry).Subscribe(entry =>
            {
                if (entry == null)
                {
                    return;
                }

                ExplorerModel = entry.IsStateChannel
                    ? _baseExplorerModel
                    : ViewModelCache.GetOrAdd(entry.ExCache, () => new ExplorerModel(entry, this, setPageIndex));

                PlaylistModel = entry.IsStateChannel
                    ? _basePlaylistModel
                    : ViewModelCache.GetOrAdd(entry.PlCache, () => new PlaylistModel(entry, ExplorerModel, setPageIndex));

                if (PlaylistModel?.SelectedEntry != null)
                {
                    PlaylistModel.SelectedEntry = null;
                }

                if (!string.IsNullOrEmpty(PlaylistModel?.SearchText))
                {
                    PlaylistModel.SearchText = null;
                }

                if (!string.IsNullOrEmpty(ExplorerModel.SearchText))
                {
                    ExplorerModel.SearchText = null;
                }

                if (ExplorerModel.ItemSort != ItemSort.Timestamp)
                {
                    ExplorerModel.ItemSort = ItemSort.Timestamp;
                }

                byte index;
                ExplorerModel.All.Clear();
                if (entry.Items.Count == 0)
                {
                    index = 1;
                }
                else
                {
                    index = 0;
                    ExplorerModel.All.AddOrUpdate(entry.Items);
                }

                setPageIndex.Invoke(index);
            });

            SelectedEntry = _baseChannel;

            Tags.AddRange(_tagRepository.GetTags(false).GetAwaiter().GetResult());

            AddChannelCommand =
                new Command(x => _popupController.Show(new ChannelPopupContext(null, Entries, setTitle, UpdateList, SetSelected)));
            EditChannelCommand =
                new Command(x =>
                                _popupController.Show(new ChannelPopupContext(SelectedEntry,
                                                                              Entries,
                                                                              setTitle,
                                                                              UpdateList,
                                                                              SetSelected)));
            SyncChannelCommand = new Command(async x => await SyncChannel());
            ReloadCommand = new Command(async x => await ReloadStatistics());
            DeleteChannelCommand = new Command(async x => await DeleteChannel());
            ClearAddedCommand = new Command(async x => await ClearAdded());
            BackupCommand = new Command(async x => await BackupChannels());
            RestoreCommand = new Command(async x => await RestoreChannels());
            SyncChannelsCommand = new Command(async x => await SyncChannels());
            SetSortCommand = new Command(x => ChannelSort = (ChannelSort)Enum.Parse(typeof(ChannelSort), (string)x));
        }

        private CatalogModel(IChannelRepository channelRepository,
            ITagRepository tagRepository,
            IPopupController popupController,
            ISyncService syncService,
            IYoutubeService youtubeService,
            IItemRepository itemRepository,
            IBackupService backupService)
        {
            _channelRepository = channelRepository;
            _tagRepository = tagRepository;
            _popupController = popupController;
            _syncService = syncService;
            _youtubeService = youtubeService;
            _itemRepository = itemRepository;
            _backupService = backupService;
        }

        #endregion

        #region Properties

        public ICommand AddChannelCommand { get; }
        public SourceCache<Channel, string> All { get; }
        public ICommand BackupCommand { get; }

        public ChannelSort ChannelSort
        {
            get => _channelSort;
            set => Update(ref _channelSort, value);
        }

        public ICommand ClearAddedCommand { get; }
        public ICommand DeleteChannelCommand { get; }
        public ICommand EditChannelCommand { get; }
        public IReadOnlyCollection<Channel> Entries => _entries;

        public ExplorerModel ExplorerModel
        {
            get => _explorerModel;
            set => Update(ref _explorerModel, value);
        }

        public IEnumerable<Item> GetBaseItems => _baseChannel.Items;

        public bool IsWorking
        {
            get => _isWorking;
            set => Update(ref _isWorking, value);
        }

        public bool MassSync
        {
            get => _massSync;
            set => Update(ref _massSync, value);
        }

        public PlaylistModel PlaylistModel
        {
            get => _playlistModel;
            set => Update(ref _playlistModel, value);
        }

        public ICommand ReloadCommand { get; }
        public ICommand RestoreCommand { get; }

        public string SearchText
        {
            get => _searchText;
            set => Update(ref _searchText, value);
        }

        public Channel SelectedEntry
        {
            get => _selectedEntry;
            set => Update(ref _selectedEntry, value);
        }

        public Tag SelectedTag
        {
            get => _selectedTag;
            set => Update(ref _selectedTag, value);
        }

        public ICommand SetSortCommand { get; }

        public ICommand SyncChannelCommand { get; }
        public ICommand SyncChannelsCommand { get; }

        public bool SyncPls
        {
            get => _syncPls;
            set => Update(ref _syncPls, value);
        }

        public List<Tag> Tags { get; } = new List<Tag> { new Tag { Id = -2, Text = "[no tag]" }, new Tag { Id = -1, Text = " " } };
        private IAppCache ViewModelCache { get; } = new CachingService();

        #endregion

        #region Static Methods

        private static Func<Channel, bool> BuildSearchFilter(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return x => true;
            }

            return x => x.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase);
        }

        private static Func<Channel, bool> BuildTagFilter(Tag tag)
        {
            if (tag == null || tag.Id == -1)
            {
                return x => true;
            }

            if (tag.Id == -2)
            {
                return x => x.IsStateChannel || x.Tags.Count == 0;
            }

            return x => x.IsStateChannel || x.Tags.Select(y => y.Id).Contains(tag.Id);
        }

        private static string MakeTitle(int count, Stopwatch sw)
        {
            string items = count == 1 ? "item" : "items";
            return
                $"Done {count} {items}. Elapsed: {sw.Elapsed.Hours}h {sw.Elapsed.Minutes}m {sw.Elapsed.Seconds}s {sw.Elapsed.Milliseconds}ms";
        }

        private static void MarkUnlisted(Channel channel, ICollection<string> noUnlisted, Playlist unlistedpl, PlaylistModel plmodel)
        {
            if (unlistedpl != null)
            {
                unlistedpl.StateItems?.RemoveAll(x => noUnlisted.Contains(x.Id));
                unlistedpl.Count -= noUnlisted.Count;
            }

            foreach (Item item in channel.Items.Where(x => noUnlisted.Contains(x.Id)))
            {
                if (item.SyncState != SyncState.Notset)
                {
                    item.SyncState = SyncState.Notset;
                }
            }

            var unlistpl = channel.Playlists.FirstOrDefault(x => x.Id == channel.Id);
            if (unlistpl == null)
            {
                return;
            }

            if (unlistpl.Count == noUnlisted.Count)
            {
                channel.Playlists.Remove(unlistpl);
                plmodel?.All.Remove(unlistpl);
            }
            else
            {
                unlistpl.Items.RemoveAll(noUnlisted.Contains);
                unlistpl.Count -= noUnlisted.Count;
            }
        }

        #endregion

        #region Methods

        public ExplorerModel GetCachedExplorerModel(string channelId)
        {
            return channelId == null
                ? _baseExplorerModel
                : ViewModelCache.Get<ExplorerModel>(_entries.Single(x => x.Id == channelId).ExCache);
        }

        public PlaylistModel GetCachedPlaylistModel(string channelId)
        {
            return channelId == null
                ? _basePlaylistModel
                : ViewModelCache.Get<PlaylistModel>(_entries.Single(x => x.Id == channelId).PlCache);
        }

        private async Task BackupChannels()
        {
            IsWorking = true;
            Stopwatch sw = Stopwatch.StartNew();
            _setTitle.Invoke($"Backup {_entries.Count - 1} channels..");
            var task = _backupService.Backup(_entries.Where(x => !x.IsStateChannel), SetLog);
            await Task.WhenAll(task).ContinueWith(done =>
            {
                _setTitle.Invoke(task.Exception == null ? MakeTitle(task.Result, sw) : $"Error: {task.Exception.Message}");
                IsWorking = false;
            });
        }

        private async Task ClearAdded()
        {
            if (SelectedEntry == null)
            {
                return;
            }

            IsWorking = true;
            Stopwatch sw = Stopwatch.StartNew();

            int count;

            var chId = SelectedEntry.IsStateChannel ? null : SelectedEntry.Id;
            if (chId == null)
            {
                count = _baseChannel.Items.Count;
                _baseChannel.Items.Clear();

                foreach (Channel channel in _entries.Where(x => x.Count > 0))
                {
                    channel.Count = 0;
                    if (channel.IsStateChannel)
                    {
                        ExplorerModel.All.Clear();
                    }
                    else
                    {
                        if (channel.Items.Count == 0)
                        {
                            continue;
                        }

                        var cmodel = GetCachedExplorerModel(channel.Id);
                        if (cmodel == null)
                        {
                            continue;
                        }

                        foreach (Item item in cmodel.All.Items.Where(x => x.SyncState == SyncState.Added))
                        {
                            item.SyncState = SyncState.Notset;
                        }
                    }
                }

                _setPageIndex.Invoke(1);
            }
            else
            {
                var channel = _entries.First(x => x.Id == chId);
                var ids = channel.Items.Where(x => x.SyncState == SyncState.Added).Select(x => x.Id).ToList();
                foreach (Item item in channel.Items.Where(x => ids.Contains(x.Id)))
                {
                    item.SyncState = SyncState.Notset;
                }

                _baseChannel.Items.RemoveAll(x => ids.Contains(x.Id));
                channel.Count -= ids.Count;
                _baseChannel.Count -= ids.Count;
                count = ids.Count;
            }

            var task1 = _channelRepository.UpdateChannelSyncState(chId, 0);
            var task2 = _channelRepository.UpdateItemsCount(chId, 0);
            await Task.WhenAll(task1, task2).ContinueWith(done =>
            {
                _setTitle.Invoke(MakeTitle(count, sw));
                IsWorking = false;
            });
        }

        private async Task DeleteChannel()
        {
            if (SelectedEntry == null || SelectedEntry.Working)
            {
                return;
            }

            IsWorking = true;
            Stopwatch sw = Stopwatch.StartNew();

            var title = SelectedEntry.Title;
            var deletedId = SelectedEntry.Id;
            var ch = _entries.First(x => x.Id == deletedId);
            var count = ch.Count;
            var index = All.Items.IndexOf(ch);
            ViewModelCache.Remove(ch.ExCache);
            ViewModelCache.Remove(ch.PlCache);
            All.Remove(ch);
            _baseChannel.Count -= count;
            _baseChannel.Items.RemoveAll(x => x.ChannelId == deletedId);
            GetCachedExplorerModel(null)?.All.Remove(ch.Items);
            if (ch.Items.Any(x => x.WatchStateSet)
                || ch.Items.Any(x => x.SyncState == SyncState.Unlisted || x.SyncState == SyncState.Deleted))
            {
                PlaylistModel plmodel = GetCachedPlaylistModel(null);
                if (plmodel != null)
                {
                    List<Item> watched = ch.Items.Where(x => x.WatchState == WatchState.Watched).ToList();
                    if (watched.Any())
                    {
                        var pl = plmodel.Entries.First(x => x.Id == "0");
                        pl.Count -= watched.Count;
                        pl.StateItems?.RemoveAll(x => x.ChannelId == ch.Id && x.WatchState == WatchState.Watched);
                    }

                    List<Item> planned = ch.Items.Where(x => x.WatchState == WatchState.Planned).ToList();
                    if (planned.Any())
                    {
                        var pl = plmodel.Entries.First(x => x.Id == "-1");
                        pl.Count -= planned.Count;
                        pl.StateItems?.RemoveAll(x => x.ChannelId == ch.Id && x.WatchState == WatchState.Planned);
                    }

                    List<Item> unlisted = ch.Items.Where(x => x.SyncState == SyncState.Unlisted || x.SyncState == SyncState.Deleted)
                        .ToList();
                    if (unlisted.Any())
                    {
                        var pl = plmodel.Entries.First(x => x.Id == "-2");
                        pl.Count -= unlisted.Count;
                        pl.StateItems?.RemoveAll(x => x.ChannelId == ch.Id
                                                      && (x.SyncState == SyncState.Unlisted || x.SyncState == SyncState.Deleted));
                    }
                }
            }

            SelectedEntry = All.Items.ElementAt(index == 0 ? 0 : index - 1) ?? _baseChannel;
            var task = _channelRepository.DeleteChannel(deletedId);
            await Task.WhenAll(task).ContinueWith(done =>
            {
                _setTitle.Invoke(task.Exception == null ? MakeTitle(task.Result, sw) : $"Error: {task.Exception.Message}");
                IsWorking = false;
                GetCachedExplorerModel(null)?.SetLog($"Deleted: {deletedId} - {title}");
            });
            // await _appLogRepository.SetStatus(AppStatus.ChannelDeleted, $"Delete channel:{deletedId}");
        }

        private IObservable<SortExpressionComparer<Channel>> GetChannelSorter()
        {
            return this.WhenValueChanged(x => x.ChannelSort).Select(x =>
            {
                switch (x)
                {
                    case ChannelSort.Title:
                        return SortExpressionComparer<Channel>.Ascending(t => t.Title);
                    case ChannelSort.Subs:
                        return SortExpressionComparer<Channel>.Descending(t => t.SubsCount);
                    case ChannelSort.SubsDiff:
                        return SortExpressionComparer<Channel>.Descending(t => t.SubsCountDiff);
                    case ChannelSort.View:
                        return SortExpressionComparer<Channel>.Descending(t => t.ViewCount);
                    case ChannelSort.ViewDiff:
                        return SortExpressionComparer<Channel>.Descending(t => t.ViewCountDiff);
                    case ChannelSort.Count:
                        return SortExpressionComparer<Channel>.Descending(t => t.ItemsCount);
                    case ChannelSort.LastDate:
                        return SortExpressionComparer<Channel>.Descending(t => t.Timestamp);
                    case ChannelSort.Watched:
                        return SortExpressionComparer<Channel>.Descending(t => t.WatchedCount);
                    case ChannelSort.Planned:
                        return SortExpressionComparer<Channel>.Descending(t => t.PlannedCount);

                    default:
                        return SortExpressionComparer<Channel>.Ascending(t => t.Title);
                }
            });
        }

        private async Task ReloadStatistics()
        {
            _setTitle.Invoke("Update statistics..");
            IsWorking = true;
            Stopwatch sw = Stopwatch.StartNew();

            var oldId = SelectedEntry.Id;
            var ch = _entries.First(x => x.Id == oldId);
            await _youtubeService.SetItemsStatistic(ch, false);
            var task = _itemRepository.UpdateItemsStats(ch.Items, ch.IsStateChannel ? null : ch.Id);

            await Task.WhenAll(task).ContinueWith(done =>
            {
                if (task.Result.Count > 0)
                {
                    ch.Items.ForEach(x =>
                    {
                        x.ViewDiff = task.Result.TryGetValue(x.Id, out long vdiff) ? vdiff : 0;
                    });
                }
                else
                {
                    ch.Items.ForEach(x => x.ViewDiff = 0);
                }

                _setTitle.Invoke(task.Exception == null ? MakeTitle(ch.Items.Count, sw) : $"Error: {task.Exception.Message}");
                IsWorking = false;
            });

            ExplorerModel.All.AddOrUpdate(ch.Items);
        }

        private async Task RestoreChannels()
        {
            IsWorking = true;
            Stopwatch sw = Stopwatch.StartNew();

            var task = _backupService.Restore(_entries.Where(x => !x.IsStateChannel).Select(x => x.Id),
                                              MassSync,
                                              _setTitle,
                                              UpdateList,
                                              SetLog);

            await Task.WhenAll(task).ContinueWith(done =>
            {
                _setTitle.Invoke(task.Exception == null ? MakeTitle(task.Result.ChannelsCount, sw) : $"Error: {task.Exception.Message}");
                IsWorking = false;
            });

            var pl = _baseChannel.Playlists.First(x => x.Id == "-1");
            pl.Count += task.Result.PlannedCount;
            var wl = _baseChannel.Playlists.First(x => x.Id == "0");
            wl.Count += task.Result.WatchedCount;
            GetCachedPlaylistModel(null)?.All.AddOrUpdate(new[] { pl, wl });
        }

        private void SetLog(string log)
        {
            var exmodel = GetCachedExplorerModel(null);
            if (exmodel != null)
            {
                exmodel.LogText += log + Environment.NewLine;
            }
        }

        private void SetSelected(Channel channel)
        {
            if (channel != null)
            {
                SelectedEntry = channel;
            }
        }

        private async Task SyncChannel()
        {
            if (SelectedEntry == null)
            {
                return;
            }

            var oldId = SelectedEntry.Id;
            var channel = _entries.First(x => x.Id == oldId);

            _setTitle.Invoke($"Working {channel.Title}..");
            IsWorking = true;
            Stopwatch sw = Stopwatch.StartNew();
            //await _appLogRepository.SetStatus(AppStatus.SyncPlaylistStarted, $"Start full sync: {SelectedEntry.Id}");

            var lst = new List<Channel> { _baseChannel, channel };
            var task = _syncService.Sync(true, true, lst, SetLog);
            await Task.WhenAll(task).ContinueWith(done =>
            {
                _setTitle.Invoke(task.Exception == null ? MakeTitle(task.Result.NewItems.Count, sw) : $"Error: {task.Exception.Message}");
                IsWorking = false;
            });

            var plmodel = GetCachedPlaylistModel(channel.Id);
            if (task.Result.NewPlaylists.Count > 0)
            {
                channel.Playlists.AddRange(task.Result.NewPlaylists);
                plmodel?.All.AddOrUpdate(task.Result.NewPlaylists);
            }

            if (task.Result.DeletedPlaylists.Count > 0)
            {
                channel.Playlists.RemoveAll(x => task.Result.DeletedPlaylists.Contains(x.Id));
                var deletedpl = task.Result.DeletedPlaylists;
                plmodel?.All.Remove(plmodel.All.Items.Where(x => deletedpl.Contains(x.Id)));
            }

            foreach ((string key, List<ItemPrivacy> value) in task.Result.ExistPlaylists)
            {
                var pl = plmodel?.All.Items.FirstOrDefault(x => x.Id == key);
                if (pl == null)
                {
                    continue;
                }

                pl.Count = value.Count;
                pl.Items.Clear();
                pl.Items.AddRange(value.Select(x => x.Id));
            }

            if (task.Result.NoUnlistedAgain.Count > 0)
            {
                MarkUnlisted(channel, task.Result.NoUnlistedAgain, _baseChannel.Playlists.FirstOrDefault(x => x.Id == "-2"), plmodel);
            }

            All.AddOrUpdate(lst);
            SelectedEntry = channel;
        }

        private async Task SyncChannels()
        {
            var oldId = SelectedEntry.IsStateChannel ? null : SelectedEntry.Id;
            _setTitle.Invoke($"Working {_entries.Count - 1} channels..");
            IsWorking = true;
            Stopwatch sw = Stopwatch.StartNew();
            //await _appLogRepository.SetStatus(AppStatus.SyncWithoutPlaylistStarted, $"Start simple sync:{Entries.Count(x => !x.IsStateChannel)}");

            var task = _syncService.Sync(MassSync, SyncPls, _entries, SetLog);
            await Task.WhenAll(task).ContinueWith(done =>
            {
                _setTitle.Invoke(task.Exception == null ? MakeTitle(task.Result.NewItems.Count, sw) : $"Error: {task.Exception.Message}");
                IsWorking = false;
            });

            if (task.Result.NoUnlistedAgain.Count > 0)
            {
                var pl = _baseChannel.Playlists.FirstOrDefault(x => x.Id == "-2");
                foreach (Channel channel in _entries.Where(x => !x.IsStateChannel))
                {
                    MarkUnlisted(channel, task.Result.NoUnlistedAgain, pl, GetCachedPlaylistModel(channel.Id));
                }
            }

            All.AddOrUpdate(_entries);
            SelectedEntry = oldId == null ? _baseChannel : _entries.First(x => x.Id == oldId);

            //await _appLogRepository.SetStatus(AppStatus.SyncWithoutPlaylistFinished, $"Finished simple sync: {sw.Elapsed.Duration()}")
            //SetErroSyncChannels(diff.ErrorSyncChannels);
        }

        private void UpdateList(Channel channel)
        {
            if (channel != null)
            {
                All.AddOrUpdate(channel);
            }
        }

        #endregion
    }
}
