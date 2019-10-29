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
        private readonly IChannelRepository _channelRepository;
        private readonly ReadOnlyObservableCollection<Channel> _entries;
        private readonly IItemRepository _itemRepository;
        private readonly MainWindowViewModel _mainWindowViewModel;
        private readonly IPopupController _popupController;
        private readonly ISyncService _syncService;
        private readonly ITagRepository _tagRepository;
        private readonly IYoutubeService _youtubeService;

        #endregion

        #region Fields

        private ChannelSort _channelSort;
        private ExplorerModel _explorerModel;
        private bool _isWorking;
        private PlaylistModel _playlistModel;
        private string _searchText;
        private Channel _selectedEntry;
        private Tag _selectedTag;

        #endregion

        #region Constructors

        public CatalogModel(MainWindowViewModel mainWindowViewModel) : this(AvaloniaLocator.Current.GetService<IChannelRepository>(),
                                                                            AvaloniaLocator.Current.GetService<ITagRepository>(),
                                                                            AvaloniaLocator.Current.GetService<IPopupController>(),
                                                                            AvaloniaLocator.Current.GetService<ISyncService>(),
                                                                            AvaloniaLocator.Current.GetService<IYoutubeService>(),
                                                                            AvaloniaLocator.Current.GetService<IItemRepository>(),
                                                                            AvaloniaLocator.Current.GetService<IBackupService>())
        {
            _mainWindowViewModel = mainWindowViewModel;
            All = new SourceCache<Channel, string>(m => m.Id);

            BaseChannel = StateChannel.Instance;
            BaseChannel.Count = _channelRepository.GetItemsCount(SyncState.Added).GetAwaiter().GetResult();

            var channels = _channelRepository?.GetChannels().GetAwaiter().GetResult();
            channels.Insert(0, BaseChannel);

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

                ExplorerModel = ViewModelCache.GetOrAdd(entry.ExCache, () => new ExplorerModel(entry, this));
                PlaylistModel = ViewModelCache.GetOrAdd(entry.PlCache,
                                                        () => new PlaylistModel(entry, ExplorerModel, mainWindowViewModel));
                //ExplorerModel = new ExplorerModel(entry, this);
                //PlaylistModel = new PlaylistModel(entry, this, ExplorerModel);
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

                var index = (byte)(ExplorerModel.Items.Any() ? 0 : 1);
                if (mainWindowViewModel.PageIndex != index)
                {
                    mainWindowViewModel.PageIndex = index;
                }
            });

            SelectedEntry = BaseChannel;

            Tags.AddRange(_tagRepository.GetTags(false).GetAwaiter().GetResult());

            AddChannelCommand = new Command(x => _popupController.Show(new ChannelPopupContext(null, this)));
            EditChannelCommand = new Command(x => _popupController.Show(new ChannelPopupContext(SelectedEntry, this)));
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
        public Channel BaseChannel { get; set; }

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

        public bool IsWorking
        {
            get => _isWorking;
            set => Update(ref _isWorking, value);
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

        #endregion

        #region Methods

        public ExplorerModel GetCachedExplorerModel(string channelId)
        {
            return ViewModelCache.Get<ExplorerModel>(channelId == null
                                                         ? BaseChannel.ExCache
                                                         : All.Items.Single(x => x.Id == channelId).ExCache);
        }

        public PlaylistModel GetCachedPlaylistModel(string channelId)
        {
            return ViewModelCache.Get<PlaylistModel>(channelId == null
                                                         ? BaseChannel.PlCache
                                                         : All.Items.Single(x => x.Id == channelId).PlCache);
        }

        private async Task BackupChannels()
        {
            IsWorking = true;
            //_mainWindowModel.WindowTitle = "Backup channels..";
            //Stopwatch sw = Stopwatch.StartNew();

            await _backupService.Backup(Entries.Where(x => !x.IsStateChannel));

            //_mainWindowModel.WindowTitle =
            //    $"Finished : {Entries.Count(x => !x.IsStateChannel)} : Elapsed: {sw.Elapsed.Hours}h {sw.Elapsed.Minutes}m {sw.Elapsed.Seconds}s {sw.Elapsed.Milliseconds}ms";

            IsWorking = false;
        }

        private async Task ClearAdded()
        {
            if (SelectedEntry == null)
            {
                return;
            }

            IsWorking = true;

            //var sw = Stopwatch.StartNew();
            var ch = SelectedEntry;
            var chId = ch.IsStateChannel ? null : ch.Id;
            var count = await _channelRepository.UpdateChannelSyncState(chId, 0);
            if (count == 0)
            {
                return;
            }

            await _channelRepository.UpdateChannelsCount(chId, 0);
            BaseChannel.Count -= count;
            All.AddOrUpdate(BaseChannel);

            if (chId == null)
            {
                BaseChannel.Items.Clear();
                for (int i = 0; i < Entries.Count; i++)
                {
                    var channel = Entries.ElementAt(i);
                    if (channel.IsStateChannel || channel.Working || channel.Count == 0)
                    {
                        continue;
                    }

                    channel.Count = 0;

                    if (channel.Loaded)
                    {
                        ClearAddedItems(channel, false);
                    }

                    All.AddOrUpdate(channel);
                }

                ViewModelCache.Remove(BaseChannel.ExCache);
                ViewModelCache.Remove(BaseChannel.PlCache);
                SelectedEntry = BaseChannel;
                //_mainWindowModel.WindowTitle = $"Finished : {_stateChannel.Count} : Elapsed {sw.ElapsedMilliseconds} ms";
            }
            else
            {
                ch.Count = 0;
                ClearAddedItems(ch, true);
                //_mainWindowModel.WindowTitle = $"Finished {ch.Title.Trim()}: Elapsed {sw.ElapsedMilliseconds} ms";
            }

            _mainWindowViewModel.PageIndex = 1;
            IsWorking = false;
        }

        private void ClearAddedItems(Channel channel, bool cleanBase)
        {
            var added = channel.Items.Where(y => y.SyncState == SyncState.Added).ToList();
            foreach (Item item in added)
            {
                item.SyncState = SyncState.Notset;
            }

            BaseChannel.Items.RemoveAll(x => added.Select(z => z.Id).Contains(x.Id));

            var chhache = ViewModelCache.Get<ExplorerModel>(channel.ExCache);
            if (chhache != null)
            {
                foreach (Item item in chhache.All.Items.Where(x => added.Select(y => y.Id).Contains(x.Id)))
                {
                    item.SyncState = SyncState.Notset;
                }
            }

            if (cleanBase)
            {
                var basecache = ViewModelCache.Get<ExplorerModel>(BaseChannel.ExCache);
                basecache?.All.Remove(basecache.All.Items.Where(x => added.Select(y => y.Id).Contains(x.Id)));
            }
        }

        private async Task DeleteChannel()
        {
            if (SelectedEntry == null || SelectedEntry.Working)
            {
                return;
            }

            var deletedId = SelectedEntry.Id;
            var count = SelectedEntry.Count;
            var index = All.Items.IndexOf(SelectedEntry);
            ViewModelCache.Remove(SelectedEntry.ExCache);
            ViewModelCache.Remove(SelectedEntry.PlCache);
            All.Remove(SelectedEntry);

            BaseChannel.Count -= count;
            All.AddOrUpdate(BaseChannel);

            //_mainWindowModel.WindowTitle = $"Total : {_entries.Count(x => !x.IsStateChannel)}";

            SelectedEntry = All.Items.ElementAt(index == 0 ? 0 : index - 1) ?? BaseChannel;

            var res = await _channelRepository.DeleteChannel(deletedId);
            //if (res > 0)
            //{
            //    await _appLogRepository.SetStatus(AppStatus.ChannelDeleted, $"Delete channel:{deletedId}");
            //}
        }

        private IObservable<SortExpressionComparer<Channel>> GetChannelSorter()
        {
            return this.WhenValueChanged(x => x.ChannelSort).Select(x =>
            {
                switch (x)
                {
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

                    default:
                        return SortExpressionComparer<Channel>.Ascending(t => t.Title);
                }
            });
        }

        private async Task ReloadStatistics()
        {
            //_mainWindowModel.WindowTitle = "Update statistics..";

            IsWorking = true;

            //Stopwatch sw = Stopwatch.StartNew();

            var ch = SelectedEntry;

            await _youtubeService.SetItemsStatistic(ch, false);

            var res = await _itemRepository.UpdateItemsStats(ch.Items, ch.IsStateChannel ? null : ch.Id);
            if (res.Count > 0)
            {
                ch.Items.ForEach(x =>
                {
                    x.ViewDiff = res.TryGetValue(x.Id, out long vdiff) ? vdiff : 0;
                });
            }
            else
            {
                ch.Items.ForEach(x => x.ViewDiff = 0);
            }

            ExplorerModel.All.AddOrUpdate(ch.Items);

            //_mainWindowModel.WindowTitle =
            //    $"Finished : {ch.Items.Count} items. Elapsed: {sw.Elapsed.Hours}h {sw.Elapsed.Minutes}m {sw.Elapsed.Seconds}s {sw.Elapsed.Milliseconds}ms";

            IsWorking = false;
        }

        private async Task RestoreChannels()
        {
            IsWorking = true;
            //_mainWindowModel.WindowTitle = "Restore channels..";
            //Stopwatch sw = Stopwatch.StartNew();

            List<Channel> channels = await _backupService.Restore(All.Items.Where(x => !x.IsStateChannel).Select(x => x.Id), false);

            if (channels.Count > 0)
            {
                All.AddOrUpdate(channels);
            }

            //_mainWindowModel.WindowTitle =
            //    $"Finished : {channels.Count} : Elapsed: {sw.Elapsed.Hours}h {sw.Elapsed.Minutes}m {sw.Elapsed.Seconds}s {sw.Elapsed.Milliseconds}ms";

            IsWorking = false;
        }

        private async Task SyncChannel()
        {
            if (SelectedEntry == null)
            {
                return;
            }

            IsWorking = true;

            //var oldId = SelectedEntry.Id;
            //_mainWindowModel.WindowTitle = $"Working {SelectedEntry.Title.Trim()}..";
            //Stopwatch sw = Stopwatch.StartNew();
            //await _appLogRepository.SetStatus(AppStatus.SyncPlaylistStarted, $"Start full sync: {SelectedEntry.Id}");

            SyncDiff diff = await _syncService.Sync(true, new List<Channel> { BaseChannel, SelectedEntry });

            if (diff != null)
            {
                if (diff.NewPlaylists.Count > 0)
                {
                    PlaylistModel?.All.AddOrUpdate(diff.NewPlaylists);
                }

                if (diff.DeletedPlaylists.Count > 0)
                {
                    PlaylistModel?.All.Remove(PlaylistModel?.All.Items.Where(x => diff.DeletedPlaylists.Contains(x.Id)));
                }

                foreach ((string key, List<ItemPrivacy> value) in diff.ExistPlaylists)
                {
                    var pl = PlaylistModel?.All.Items.FirstOrDefault(x => x.Id == key);
                    if (pl != null)
                    {
                        pl.Count = value.Count;
                        pl.Items.Clear();
                        pl.Items.AddRange(value.Select(x => x.Id));
                    }
                }

                _explorerModel.All.AddOrUpdate(diff.NewItems);
                //SetErroSyncChannels(diff.ErrorSyncChannels);

                //SelectChannel(true, Entries.First(x => x.Id == oldId));

                //_mainWindowModel.WindowTitle =
                //    $"Finished {diff.Channels.First().Key} : new {diff.Channels.First().Value.ItemsCount} : Elapsed: {sw.Elapsed.Minutes}m {sw.Elapsed.Seconds}s {sw.Elapsed.Milliseconds}ms";
            }
            else
            {
                //_mainWindowModel.WindowTitle =
                //    $"Elapsed: {sw.Elapsed.Minutes}m {sw.Elapsed.Seconds}s {sw.Elapsed.Milliseconds}ms";
            }

            //await _appLogRepository.SetStatus(AppStatus.SyncPlaylistFinished,
            //                                  diff == null
            //                                      ? "Finished full sync with error"
            //                                      : $"Finish full sync: {sw.Elapsed.Duration()}");

            IsWorking = false;
        }

        private async Task SyncChannels()
        {
            IsWorking = true;

            var oldId = SelectedEntry.IsStateChannel ? null : SelectedEntry.Id;
            //_mainWindowModel.WindowTitle = $"Working {Entries.Count(x => !x.IsStateChannel)} channels..";
            //Stopwatch sw = Stopwatch.StartNew();

            //await _appLogRepository.SetStatus(AppStatus.SyncWithoutPlaylistStarted,
            //                                  $"Start simple sync:{Entries.Count(x => !x.IsStateChannel)}");

            SyncDiff diff = await _syncService.Sync(true, Entries);

            foreach (KeyValuePair<string, ChannelStats> channel in diff.Channels)
            {
                ViewModelCache.Remove("e" + channel.Key);
            }

            //await _appLogRepository.SetStatus(AppStatus.SyncWithoutPlaylistFinished,
            //                                  diff == null
            //                                      ? "Finished simple sync with error"
            //                                      : $"Finished simple sync: {sw.Elapsed.Duration()}");

            //if (diff != null)
            //{
            //    SetErroSyncChannels(diff.ErrorSyncChannels);
            //}

            SelectedEntry = oldId == null ? BaseChannel : Entries.First(x => x.Id == oldId);

            //_mainWindowModel.WindowTitle =
            //    $"Finished : {_stateChannel.Count} : Elapsed: {sw.Elapsed.Hours}h {sw.Elapsed.Minutes}m {sw.Elapsed.Seconds}s {sw.Elapsed.Milliseconds}ms";

            IsWorking = false;
        }

        #endregion
    }
}
