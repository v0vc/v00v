using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using DynamicData;
using DynamicData.Binding;
using LazyCache;
using ReactiveUI;
using v00v.Model;
using v00v.Model.Entities;
using v00v.Model.Entities.Instance;
using v00v.Model.Enums;
using v00v.Model.Extensions;
using v00v.Model.SyncEntities;
using v00v.Services.Backup;
using v00v.Services.ContentProvider;
using v00v.Services.Dispatcher;
using v00v.Services.Persistence;
using v00v.Services.Synchronization;
using v00v.ViewModel.Explorer;
using v00v.ViewModel.Playlists;
using v00v.ViewModel.Popup;
using v00v.ViewModel.Popup.Channel;
using v00v.ViewModel.Startup;

namespace v00v.ViewModel.Catalog
{
    public class CatalogModel : ViewModelBase
    {
        #region Static and Readonly Fields

        private readonly IAppLogRepository _appLogRepository;
        private readonly IBackupService _backupService;
        private readonly Channel _baseChannel;
        private readonly ExplorerModel _baseExplorerModel;
        private readonly PlaylistModel _basePlaylistModel;
        private readonly IChannelRepository _channelRepository;
        private readonly ReadOnlyObservableCollection<Channel> _entries;
        private readonly IItemRepository _itemRepository;
        private readonly IPopupController _popupController;
        private readonly Action<byte> _setPageIndex;
        private readonly IStartupModel _settings;
        private readonly Action<string> _setTitle;
        private readonly ISyncService _syncService;
        private readonly List<int> _tagOrder;
        private readonly ITagRepository _tagRepository;
        private readonly List<Tag> _tags;
        private readonly ITaskDispatcher _taskDispatcher;
        private readonly IAppCache _viewModelCache = new CachingService();
        private readonly IYoutubeService _youtubeService;

        #endregion

        #region Fields

        private ChannelSort _channelSort;
        private ExplorerModel _explorerModel;
        private bool _isWorking;
        private bool _massSync;
        private PlaylistModel _playlistModel;
        private string _searchText;
        private Channel _selectedEntry;
        private Tag _selectedTag;
        private bool _syncPls;

        #endregion

        #region Constructors

        public CatalogModel(Action<string> setTitle, Action<byte> setPageIndex) :
            this(AvaloniaLocator.Current.GetService<IChannelRepository>(),
                 AvaloniaLocator.Current.GetService<IItemRepository>(),
                 AvaloniaLocator.Current.GetService<ITagRepository>(),
                 AvaloniaLocator.Current.GetService<IAppLogRepository>(),
                 AvaloniaLocator.Current.GetService<IPopupController>(),
                 AvaloniaLocator.Current.GetService<ISyncService>(),
                 AvaloniaLocator.Current.GetService<IYoutubeService>(),
                 AvaloniaLocator.Current.GetService<IBackupService>(),
                 AvaloniaLocator.Current.GetService<ITaskDispatcher>(),
                 AvaloniaLocator.Current.GetService<IStartupModel>())
        {
            _setTitle = setTitle;
            _setPageIndex = setPageIndex;
            All = new SourceCache<Channel, string>(m => m.Id);
            _baseChannel = StateChannel.Instance;
            _baseChannel.Count = _channelRepository.GetItemsCount(SyncState.Added);
            _baseExplorerModel = new ExplorerModel(_baseChannel, this, setPageIndex, setTitle);
            _basePlaylistModel = new PlaylistModel(_baseChannel, _baseExplorerModel, setPageIndex, setTitle, SetSelected, GetExistIds);
            All.AddOrUpdate(_baseChannel);
            All.AddOrUpdate(_channelRepository.GetChannels());

            All.Connect().Filter(this.WhenValueChanged(t => t.SearchText).Select(BuildSearchFilter))
                .Filter(this.WhenValueChanged(t => t.SelectedTag).Select(BuildTagFilter))
                .Sort(GetChannelSorter(), SortOptimisations.ComparesImmutableValuesOnly, 25).ObserveOn(RxApp.MainThreadScheduler)
                .Bind(out _entries).DisposeMany().Subscribe();

            this.WhenValueChanged(x => x.SelectedEntry).Subscribe(entry =>
            {
                if (entry == null)
                {
                    return;
                }

                if (entry.IsStateChannel)
                {
                    if (_baseExplorerModel.All.Items.Any())
                    {
                        if (!_baseExplorerModel.All.Items.Select(x => x.Id).All(entry.Items.Select(x => x.Id).Contains))
                        {
                            _baseExplorerModel.All.Clear();
                        }

                        if (entry.Items.Count > 0 && entry.Items.Count != _baseExplorerModel.All.Items.Count())
                        {
                            _baseExplorerModel.All.AddOrUpdate(entry.Items);
                        }

                        if (_baseExplorerModel?.SelectedTag.Key != 0)
                        {
                            _baseExplorerModel.SelectedTag = _baseExplorerModel.Tags.FirstOrDefault();
                        }
                    }
                    else
                    {
                        if (entry.Items.Count > 0)
                        {
                            _baseExplorerModel.All.AddOrUpdate(entry.Items);
                        }
                    }

                    ExplorerModel = _baseExplorerModel;
                    PlaylistModel = _basePlaylistModel;
                }
                else
                {
                    ExplorerModel = _viewModelCache.GetOrAdd(entry.ExCache, () => new ExplorerModel(entry, this, setPageIndex, setTitle));
                    PlaylistModel = _viewModelCache.GetOrAdd(entry.PlCache,
                                                             () => new PlaylistModel(entry,
                                                                                     ExplorerModel,
                                                                                     setPageIndex,
                                                                                     setTitle,
                                                                                     SetSelected));
                }

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

                var any = ExplorerModel.Items.Any();
                ExplorerModel.EnableLog = any;
                setPageIndex.Invoke((byte)(any ? 0 : 1));
            });

            SelectedEntry = _baseChannel;
            _tags = _tagRepository.GetTags().ToList();
            _tagOrder = _tagRepository.GetOrder();
            Tags.AddRange(_tags);

            AddChannelCommand = ReactiveCommand.Create(AddChannel, null, RxApp.MainThreadScheduler);
            EditChannelCommand = ReactiveCommand.Create(EditChannel, null, RxApp.MainThreadScheduler);
            CopyChannelLinkCommand = ReactiveCommand.CreateFromTask(CopyItem, null, RxApp.MainThreadScheduler);
            SyncChannelCommand = ReactiveCommand.CreateFromTask(SyncChannel, null, RxApp.MainThreadScheduler);
            SyncChannelsCommand = ReactiveCommand.CreateFromTask(SyncChannels, null, RxApp.MainThreadScheduler);
            SaveChannelCommand = ReactiveCommand.CreateFromTask(SaveChannel, null, RxApp.MainThreadScheduler);
            ReloadCommand = ReactiveCommand.CreateFromTask(ReloadStatistics, null, RxApp.MainThreadScheduler);
            DeleteChannelCommand = ReactiveCommand.CreateFromTask(DeleteChannel, null, RxApp.MainThreadScheduler);
            ClearAddedCommand = ReactiveCommand.CreateFromTask(ClearAdded, null, RxApp.MainThreadScheduler);
            BackupCommand = ReactiveCommand.CreateFromTask(BackupChannels, null, RxApp.MainThreadScheduler);
            RestoreCommand = ReactiveCommand.CreateFromTask(RestoreChannels, null, RxApp.MainThreadScheduler);
            SelectChannelCommand = ReactiveCommand.Create(() => SelectedEntry = _baseChannel, null, RxApp.MainThreadScheduler);
            GetRelatedChannelCommand = ReactiveCommand.CreateFromTask(GetRelatedChannels, null, RxApp.MainThreadScheduler);
            SetSortCommand = ReactiveCommand.Create((string par) => ChannelSort = (ChannelSort)Enum.Parse(typeof(ChannelSort), par),
                                                    null,
                                                    RxApp.MainThreadScheduler);

            AddChannelCommand.ThrownExceptions.Subscribe(OnException);
            SyncChannelCommand.ThrownExceptions.Subscribe(OnException);
            SyncChannelsCommand.ThrownExceptions.Subscribe(OnException);
            RestoreCommand.ThrownExceptions.Subscribe(OnException);

            StartSchedulerTasks();
        }

        private CatalogModel(IChannelRepository channelRepository,
            IItemRepository itemRepository,
            ITagRepository tagRepository,
            IAppLogRepository appLogRepository,
            IPopupController popupController,
            ISyncService syncService,
            IYoutubeService youtubeService,
            IBackupService backupService,
            ITaskDispatcher taskDispatcher,
            IStartupModel settings)
        {
            _channelRepository = channelRepository;
            _tagRepository = tagRepository;
            _appLogRepository = appLogRepository;
            _itemRepository = itemRepository;
            _popupController = popupController;
            _syncService = syncService;
            _youtubeService = youtubeService;
            _backupService = backupService;
            _taskDispatcher = taskDispatcher;
            _settings = settings;
        }

        #endregion

        #region Properties

        public ReactiveCommand<Unit, Unit> AddChannelCommand { get; }
        public SourceCache<Channel, string> All { get; }
        public ICommand BackupCommand { get; }

        public ChannelSort ChannelSort
        {
            get => _channelSort;
            set => Update(ref _channelSort, value);
        }

        public ICommand ClearAddedCommand { get; }
        public ICommand CopyChannelLinkCommand { get; }
        public ICommand DeleteChannelCommand { get; }
        public ICommand EditChannelCommand { get; }
        public IReadOnlyCollection<Channel> Entries => _entries;

        public ExplorerModel ExplorerModel
        {
            get => _explorerModel;
            set => Update(ref _explorerModel, value);
        }

        public IEnumerable<Item> GetBaseItems => _baseChannel.Items;

        public ICommand GetRelatedChannelCommand { get; }

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
        public ReactiveCommand<Unit, Unit> RestoreCommand { get; }

        public ICommand SaveChannelCommand { get; }

        public string SearchText
        {
            get => _searchText;
            set => Update(ref _searchText, value);
        }

        public ICommand SelectChannelCommand { get; }

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

        public ReactiveCommand<Unit, Unit> SyncChannelCommand { get; }
        public ReactiveCommand<Unit, Unit> SyncChannelsCommand { get; }

        public bool SyncPls
        {
            get => _syncPls;
            set => Update(ref _syncPls, value);
        }

        public List<Tag> Tags { get; } = new() { new Tag { Id = -2, Text = "[no tag]" }, new Tag { Id = -1, Text = " " } };

        #endregion

        #region Static Methods

        private static Func<Channel, bool> BuildSearchFilter(string searchText)
        {
            return string.IsNullOrWhiteSpace(searchText)
                ? _ => true
                : x => x.Title.Contains(searchText, StringComparison.InvariantCultureIgnoreCase)
                       || string.Equals(x.Id, searchText, StringComparison.InvariantCultureIgnoreCase);
        }

        private static Func<Channel, bool> BuildTagFilter(Tag tag)
        {
            if (tag == null || tag.Id == -1)
            {
                return _ => true;
            }

            if (tag.Id == -2)
            {
                return x => x.IsStateChannel || x.Tags.Count == 0;
            }

            return x => x.IsStateChannel || x.Tags.Select(y => y.Id).Contains(tag.Id);
        }

        private static void MarkNoUnlisted(Channel channel, ICollection<string> noUnlisted, PlaylistModel plmodel)
        {
            Parallel.ForEach(channel.Items.Where(x => noUnlisted.Contains(x.Id) && x.SyncState != SyncState.Notset),
                             item =>
                             {
                                 item.SyncState = SyncState.Notset;
                             });

            var unlistpl = channel.Playlists.FirstOrDefault(x => x.Id == channel.Id);
            if (unlistpl == null)
            {
                return;
            }

            if (unlistpl.Count == noUnlisted.Count)
            {
                channel.Playlists.Remove(unlistpl);
                plmodel?.All.RemoveKey(unlistpl.Id);
            }
            else
            {
                unlistpl.Items.RemoveAll(noUnlisted.Contains);
                unlistpl.Count -= noUnlisted.Count;
            }
        }

        #endregion

        #region Methods

        public void AddChannelToList(Channel channel, bool setSelect)
        {
            if (_entries.Select(x => x.Id).Contains(channel.Id))
            {
                return;
            }

            channel.IsNew = true;
            channel.Order = GetMinOrder() - 1;
            UpdateList(channel);
            if (setSelect)
            {
                SetSelected(channel.Id);
            }
        }

        public ExplorerModel GetCachedExplorerModel(string id, bool isChannelId = false)
        {
            if (id == null)
            {
                return _baseExplorerModel;
            }

            if (!isChannelId)
            {
                return _viewModelCache.Get<ExplorerModel>(id);
            }

            var exc = _entries.FirstOrDefault(x => x.Id == id);
            return exc != null ? _viewModelCache.Get<ExplorerModel>(exc.ExCache) : null;
        }

        public PlaylistModel GetCachedPlaylistModel(string id, bool isChannelId = false)
        {
            if (id == null)
            {
                return _basePlaylistModel;
            }

            if (!isChannelId)
            {
                return _viewModelCache.Get<PlaylistModel>(id);
            }

            var plc = _entries.FirstOrDefault(x => x.Id == id);
            return plc != null ? _viewModelCache.Get<PlaylistModel>(plc.PlCache) : null;
        }

        private void AddChannel()
        {
            _popupController.Show(new ChannelPopupContext(null,
                                                          GetExistIds,
                                                          _tags.OrderBySequence(_tagOrder, x => x.Id).ToList(),
                                                          AddNewTag,
                                                          DeleteNewTag,
                                                          _setTitle,
                                                          UpdateList,
                                                          SetSelected,
                                                          OnException,
                                                          UpdatePlaylist,
                                                          GetMinOrder));
        }

        private void AddNewTag(Tag tag)
        {
            if (tag == null)
            {
                return;
            }

            Tags.Add(tag);
            _tagOrder.Add(tag.Id);
            _tags.Add(tag);
        }

        private async Task BackupChannels()
        {
            IsWorking = true;
            var sw = Stopwatch.StartNew();
            _setTitle.Invoke($"Backup {_entries.Count - 1} channels..");
            try
            {
                var count = await _backupService.Backup(_entries.Where(x => !x.IsNew && !x.IsStateChannel), SetLog);
                _setTitle?.Invoke(string.Empty.MakeTitle(count, sw));
            }
            catch (Exception ex)
            {
                _setTitle?.Invoke(ex.Message);
            }
            finally
            {
                IsWorking = false;
            }
        }

        private Task ClearAdded()
        {
            if (SelectedEntry == null || SelectedEntry.IsNew)
            {
                return Task.CompletedTask;
            }

            IsWorking = true;
            var sw = Stopwatch.StartNew();
            int count;
            var chId = SelectedEntry.IsStateChannel ? null : SelectedEntry.Id;
            if (chId == null)
            {
                count = _baseChannel.Items.Count;
                if (count == 0 && _baseChannel.Items.Count == 0)
                {
                    IsWorking = false;
                    return Task.CompletedTask;
                }

                _baseChannel.Items.Clear();
                _baseExplorerModel.Tags.RemoveAll(x => x.Key != 0);
                _baseChannel.Count = 0;
                ExplorerModel.All.Clear();
                ExplorerModel.EnableLog = false;
                Parallel.ForEach(_entries.Where(x => !x.IsStateChannel && x.Count > 0),
                                 channel =>
                                 {
                                     channel.Count = 0;
                                     if (channel.Items.Count <= 0)
                                     {
                                         return;
                                     }

                                     var cmodel = GetCachedExplorerModel(channel.ExCache);
                                     if (cmodel != null)
                                     {
                                         Parallel.ForEach(cmodel.All.Items.Where(x => x.SyncState == SyncState.Added),
                                                          item =>
                                                          {
                                                              item.SyncState = SyncState.Notset;
                                                          });
                                     }
                                 });
                _setPageIndex.Invoke(1);
            }
            else
            {
                var channel = _entries.First(x => x.Id == chId);
                var ids = channel.Items.Where(x => x.SyncState == SyncState.Added).Select(x => x.Id).ToHashSet();
                Parallel.ForEach(channel.Items.Where(x => ids.Contains(x.Id)),
                                 item =>
                                 {
                                     item.SyncState = SyncState.Notset;
                                 });
                _baseChannel.Items.RemoveAll(x => ids.Contains(x.Id));
                channel.Count -= ids.Count;
                _baseChannel.Count -= ids.Count;
                count = ids.Count;
            }

            return Task.WhenAll(_channelRepository.UpdateChannelSyncState(chId, 0), _channelRepository.UpdateItemsCount(chId, 0))
                .ContinueWith(_ =>
                {
                    IsWorking = false;
                    _setTitle.Invoke(string.Empty.MakeTitle(count, sw));
                });
        }

        private Task CopyItem()
        {
            if (SelectedEntry == null)
            {
                return Task.CompletedTask;
            }

            var res = $"{_youtubeService.ChannelLink}{SelectedEntry.Id}";

            return !string.IsNullOrEmpty(res) ? Application.Current.Clipboard.SetTextAsync(res) : Task.CompletedTask;
        }

        private Task DeleteChannel()
        {
            if (SelectedEntry == null || SelectedEntry.Working)
            {
                return Task.CompletedTask;
            }

            var title = SelectedEntry.Title;
            var deletedId = SelectedEntry.Id;
            var ch = _entries.First(x => x.Id == deletedId);
            var count = ch.Count;
            var index = All.Items.IndexOf(ch);
            _viewModelCache.Remove(ch.ExCache);
            _viewModelCache.Remove(ch.PlCache);
            if (ch.IsNew)
            {
                _explorerModel.All.RemoveKeys(ch.Items.Select(x => x.Id));
                All.RemoveKey(ch.Id);
                SelectedEntry = All.Items.ElementAt(index == 0 ? 0 : index - 1) ?? _baseChannel;
                return Task.CompletedTask;
            }

            IsWorking = true;
            All.RemoveKey(ch.Id);
            _baseChannel.Count -= count;
            _baseChannel.Items.RemoveAll(x => x.ChannelId == deletedId);
            _baseExplorerModel?.All.RemoveKeys(ch.Items.Select(x => x.Id));
            if (ch.Items.Any(x => x.WatchStateSet) || ch.Items.Any(x => x.SyncState is SyncState.Unlisted or SyncState.Deleted))
            {
                var watched = ch.Items.Where(x => x.WatchState == WatchState.Watched).ToList();
                if (watched.Any())
                {
                    var pl = _basePlaylistModel.Entries.First(x => x.Id == "0");
                    pl.Count -= watched.Count;
                    pl.StateItems?.RemoveAll(x => x.ChannelId == ch.Id && x.WatchState == WatchState.Watched);
                }

                var planned = ch.Items.Where(x => x.WatchState == WatchState.Planned).ToList();
                if (planned.Any())
                {
                    var pl = _basePlaylistModel.Entries.First(x => x.Id == "-1");
                    pl.Count -= planned.Count;
                    pl.StateItems?.RemoveAll(x => x.ChannelId == ch.Id && x.WatchState == WatchState.Planned);
                }

                var unlisted = ch.Items.Where(x => x.SyncState == SyncState.Unlisted || x.SyncState == SyncState.Deleted).ToList();
                if (unlisted.Any())
                {
                    var pl = _basePlaylistModel.Entries.First(x => x.Id == "-2");
                    pl.Count -= unlisted.Count;
                    pl.StateItems?.RemoveAll(x => x.ChannelId == ch.Id
                                                  && (x.SyncState == SyncState.Unlisted || x.SyncState == SyncState.Deleted));
                }
            }

            SelectedEntry = All.Items.ElementAt(index == 0 ? 0 : index - 1) ?? _baseChannel;
            return _channelRepository.DeleteChannel(deletedId).ContinueWith(_ =>
            {
                IsWorking = false;
                _baseExplorerModel?.SetLog($"Deleted: {deletedId} - {title}");
                _appLogRepository.SetStatus(AppStatus.ChannelDeleted, $"Delete channel: {deletedId} - {title}");
            });
        }

        private void DeleteNewTag(int tagId)
        {
            Tags.RemoveAll(x => x.Id == tagId);
            _tagOrder.RemoveAll(x => x == tagId);
            _tags.RemoveAll(x => x.Id == tagId);
        }

        private void EditChannel()
        {
            if (SelectedEntry == null || SelectedEntry.Working)
            {
                return;
            }

            _popupController.Show(new ChannelPopupContext(SelectedEntry,
                                                          GetExistIds,
                                                          _tags.OrderBySequence(_tagOrder, x => x.Id).ToList(),
                                                          AddNewTag,
                                                          DeleteNewTag,
                                                          _setTitle,
                                                          UpdateList,
                                                          SetSelected,
                                                          OnException,
                                                          UpdatePlaylist,
                                                          null,
                                                          ResortList));
        }

        private IObservable<SortExpressionComparer<Channel>> GetChannelSorter()
        {
            return this.WhenValueChanged(x => x.ChannelSort).Select(x =>
            {
                switch (x)
                {
                    case ChannelSort.Title:
                        return SortExpressionComparer<Channel>.Ascending(t => t.Order);
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

        private IEnumerable<string> GetExistIds()
        {
            return _entries.Where(x => !x.IsStateChannel && !x.IsNew).Select(x => x.Id);
        }

        private int GetMinOrder()
        {
            return _entries.Count > 1 ? _entries.Where(x => !x.IsStateChannel).Select(x => x.Order).Min() : 0;
        }

        private async Task GetRelatedChannels()
        {
            if (SelectedEntry == null || SelectedEntry.IsNew)
            {
                return;
            }

            IsWorking = true;
            var sw = Stopwatch.StartNew();
            var oldId = SelectedEntry.Id;
            var channel = _entries.First(x => x.Id == oldId);
            _setTitle.Invoke($"Search related to {channel.Title}..");
            try
            {
                var channels =
                    await _youtubeService.GetRelatedChannelsAsync(channel.Id, _entries.Where(x => !x.IsStateChannel).Select(x => x.Id));

                if (channels != null)
                {
                    _setTitle?.Invoke(string.Empty.MakeTitle(channels.Length, sw));
                    foreach (var ch in channels)
                    {
                        AddChannelToList(ch, false);
                    }

                    All.AddOrUpdate(channels);
                    SetSelected(_baseChannel.Id);
                }
                else
                {
                    _setTitle?.Invoke(string.Empty.MakeTitle(0, sw));
                    SetSelected(oldId);
                }
            }
            catch (Exception ex)
            {
                _setTitle?.Invoke(ex.Message);
            }
            finally
            {
                IsWorking = false;
            }
        }

        private void MarkHidden(Channel channel,
            ICollection<string> hiddenIds,
            SyncState syncState,
            ConcurrentDictionary<string, Item> hiddenItems)
        {
            Parallel.ForEach(channel.Items.Where(x => hiddenIds.Contains(x.Id) && x.SyncState != syncState),
                             item =>
                             {
                                 item.SyncState = syncState;
                             });

            var unlistedPl = channel.Playlists.FirstOrDefault(x => x.Id == channel.Id);
            if (unlistedPl == null)
            {
                var unPl = UnlistedPlaylist.Instance;
                unPl.IsStatePlaylist = false;
                unPl.Id = channel.Id;
                unPl.Order = channel.Playlists.Count;
                unPl.Count = hiddenIds.Count;
                unPl.Items.AddRange(hiddenIds);
                channel.Playlists.Add(unPl);
                GetCachedPlaylistModel(channel.PlCache)?.All.AddOrUpdate(unPl);
                Parallel.ForEach(channel.Items.Where(x => hiddenIds.Contains(x.Id)),
                                 item =>
                                 {
                                     hiddenItems.TryAdd(item.Id, item);
                                 });
            }
            else
            {
                var ids = hiddenIds.Except(unlistedPl.Items).ToHashSet();
                unlistedPl.Items.AddRange(ids);
                unlistedPl.Count += ids.Count;
                GetCachedPlaylistModel(channel.PlCache)?.All.AddOrUpdate(unlistedPl);
                Parallel.ForEach(channel.Items.Where(x => ids.Contains(x.Id)),
                                 item =>
                                 {
                                     hiddenItems.TryAdd(item.Id, item);
                                 });
            }
        }

        private void OnException(Exception exception)
        {
            _popupController.Hide();
            _baseExplorerModel.SetLog(exception.Message);
            if (exception.InnerException != null && !string.IsNullOrEmpty(exception.InnerException.Message))
            {
                _baseExplorerModel.SetLog(exception.InnerException.Message);
            }
        }

        private async Task ReloadStatistics()
        {
            if (SelectedEntry == null || SelectedEntry.IsNew)
            {
                return;
            }

            _setTitle.Invoke("Update statistics..");
            IsWorking = true;
            var sw = Stopwatch.StartNew();

            var oldId = SelectedEntry.Id;
            var ch = _entries.First(x => x.Id == oldId);
            var chId = ch.IsStateChannel ? null : ch.Id;
            try
            {
                await _youtubeService.SetItemsStatistic(ch, false);
                _setTitle?.Invoke(string.Empty.MakeTitle(ch.Items.Count, sw));
                var csd = await _itemRepository.UpdateItemsStats(ch.Items, chId);
                if (csd?.Count > 0)
                {
                    Parallel.ForEach(ch.Items,
                                     z =>
                                     {
                                         z.ViewDiff = csd.TryGetValue(z.Id, out var vdiff) ? vdiff : 0;
                                     });
                }
                else
                {
                    Parallel.ForEach(ch.Items,
                                     z =>
                                     {
                                         z.ViewDiff = 0;
                                     });
                }

                (ch.IsStateChannel ? _baseExplorerModel : GetCachedExplorerModel(ch.ExCache))?.All.AddOrUpdate(ch.Items);
            }
            catch (Exception ex)
            {
                _setTitle?.Invoke(ex.Message);
            }
            finally
            {
                IsWorking = false;
            }
        }

        private void ResortList(int r)
        {
            var channels = _entries.Where(x => !x.IsNew && !x.IsStateChannel).ToList();
            channels.Sort(SortExpressionComparer<Channel>.Ascending(x => x.Title));
            var i = 0;
            foreach (var ch in channels)
            {
                ch.Order = i;
                i++;
            }

            All.AddOrUpdate(channels);
            _baseExplorerModel?.SetLog($"Saved {r} rows");
        }

        private Task RestoreChannels()
        {
            IsWorking = true;
            var sw = Stopwatch.StartNew();
            return _backupService.Restore(_entries.Where(x => !x.IsStateChannel).Select(x => x.Id),
                                          MassSync,
                                          _setTitle,
                                          UpdateList,
                                          SetLog).ContinueWith(done =>
            {
                IsWorking = false;
                var res = done.GetAwaiter().GetResult();
                _setTitle?.Invoke(done.Status == TaskStatus.Faulted
                                      ? done.Exception == null ? "Faulted" : $"{done.Exception.Message}"
                                      : string.Empty.MakeTitle(res.ChannelsCount, sw));

                var pl = _baseChannel.Playlists.First(x => x.Id == "-1");
                pl.Count += res.PlannedCount;
                var wl = _baseChannel.Playlists.First(x => x.Id == "0");
                wl.Count += res.WatchedCount;
                _basePlaylistModel?.All.AddOrUpdate(new[] { pl, wl });
            });
        }

        private Task SaveChannel()
        {
            if (SelectedEntry == null || !SelectedEntry.IsNew)
            {
                return Task.CompletedTask;
            }

            var oldId = SelectedEntry.Id;
            var channel = _entries.First(x => x.Id == oldId);

            channel.Title = channel.Title.Trim();
            _setTitle.Invoke($"Working {channel.Title}..");
            IsWorking = true;
            var sw = Stopwatch.StartNew();
            return _youtubeService.AddPlaylists(channel).ContinueWith(_ =>
                                                                      {
                                                                          _channelRepository.AddChannel(channel).ContinueWith(done =>
                                                                          {
                                                                              IsWorking = false;
                                                                              _setTitle?.Invoke(done.Status == TaskStatus.Faulted
                                                                                  ? done.Exception == null ? "Faulted" :
                                                                                  $"{done.Exception.Message}"
                                                                                  : string.Empty.MakeTitle(done.GetAwaiter().GetResult(),
                                                                                   sw));
                                                                          });

                                                                          channel.IsNew = false;
                                                                          channel.FontStyle = "Normal";
                                                                          GetCachedExplorerModel(channel.ExCache)?.All.AddOrUpdate(channel.Items);
                                                                          UpdateList(channel);
                                                                          UpdatePlaylist(channel);
                                                                      },
                                                                      TaskContinuationOptions.OnlyOnRanToCompletion).ContinueWith(_ =>
             {
                 SetSelected(oldId);
             },
             TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void SetLog(string log)
        {
            if (string.IsNullOrEmpty(log))
            {
                return;
            }

            _baseExplorerModel.LogText += log + Environment.NewLine;
        }

        private void SetSelected(string channelId)
        {
            var channel = _entries.FirstOrDefault(x => x.Id == channelId);
            if (channel == null)
            {
                return;
            }

            if (SelectedEntry == null)
            {
                SelectedEntry = channel;
            }
            else
            {
                if (SelectedEntry.Id != channelId)
                {
                    SelectedEntry = channel;
                }
            }
        }

        private void StartSchedulerTasks()
        {
            if (_settings.EnableDailySyncSchedule && _settings.DailySyncParsed)
            {
                _taskDispatcher.DailySync = _settings.DailySyncTime;
                Task.Factory.StartNew(() => _taskDispatcher.RunSynchronization(_syncService,
                                                                               _appLogRepository,
                                                                               _entries.Where(x => !x.IsNew).ToList(),
                                                                               true,
                                                                               SetLog,
                                                                               UpdateChannels,
                                                                               false),
                                      TaskCreationOptions.LongRunning).ContinueWith(t =>
                {
                    SetLog(t.Exception?.Message);
                });
            }

            if (_settings.EnableRepeatSyncSchedule && _settings.RepeatSyncParsed)
            {
                _taskDispatcher.RepeatSync = _settings.RepeatSyncMin;
                Task.Factory.StartNew(() => _taskDispatcher.RunSynchronization(_syncService,
                                                                               _appLogRepository,
                                                                               _entries.Where(x => !x.IsNew).ToList(),
                                                                               false,
                                                                               SetLog,
                                                                               UpdateChannels,
                                                                               true),
                                      TaskCreationOptions.LongRunning).ContinueWith(t =>
                {
                    SetLog(t.Exception?.Message);
                });
            }

            if (_settings.EnableDailyParserUpdateSchedule && _settings.DailyParserUpdateParsed)
            {
                _taskDispatcher.DailyParser = _settings.DailyParserUpdateTime;
                Task.Factory.StartNew(() => _taskDispatcher.RunUpdateParser(SetLog, _settings.UpdateParser, false),
                                      TaskCreationOptions.LongRunning).ContinueWith(t =>
                {
                    SetLog(t.Exception?.Message);
                });
            }

            if (_settings.EnableRepeatParserUpdateSchedule && _settings.RepeatParserUpdateParsed)
            {
                _taskDispatcher.RepeatParser = _settings.RepeatParserMin;
                Task.Factory.StartNew(() => _taskDispatcher.RunUpdateParser(SetLog, _settings.UpdateParser, true),
                                      TaskCreationOptions.LongRunning).ContinueWith(t =>
                {
                    SetLog(t.Exception?.Message);
                });
            }

            if (_settings.EnableDailyBackupSchedule && _settings.DailyBackupParsed)
            {
                _taskDispatcher.DailyBackup = _settings.DailyBackupTime;
                Task.Factory
                    .StartNew(() => _taskDispatcher.RunBackup(_backupService,
                                                              _entries.Where(x => !x.IsNew && !x.IsStateChannel),
                                                              SetLog,
                                                              false),
                              TaskCreationOptions.LongRunning).ContinueWith(t =>
                    {
                        SetLog(t.Exception?.Message);
                    });
            }

            if (_settings.EnableRepeatBackupSchedule && _settings.RepeatBackupParsed)
            {
                _taskDispatcher.RepeatBackup = _settings.RepeatBackupMin;
                Task.Factory
                    .StartNew(() => _taskDispatcher.RunBackup(_backupService,
                                                              _entries.Where(x => !x.IsNew && !x.IsStateChannel),
                                                              SetLog,
                                                              true),
                              TaskCreationOptions.LongRunning).ContinueWith(t =>
                    {
                        SetLog(t.Exception?.Message);
                    });
            }
        }

        private async Task SyncChannel()
        {
            if (SelectedEntry == null || SelectedEntry.IsNew)
            {
                return;
            }

            var oldId = SelectedEntry.Id;
            var channel = _entries.First(x => x.Id == oldId);

            _setTitle.Invoke($"Working {channel.Title}..");
            IsWorking = true;
            var sw = Stopwatch.StartNew();
            await _appLogRepository.SetStatus(AppStatus.SyncPlaylistStarted, $"Start sync: {oldId}");
            var lst = new List<Channel> { _baseChannel, channel };

            var res = new SyncDiff(true);
            try
            {
                res = await _syncService.Sync(true, true, lst, SetLog, _setTitle);
                _setTitle?.Invoke(string.Empty.MakeTitle(res.NewItems.Count, sw));
                await _appLogRepository.SetStatus(AppStatus.SyncPlaylistFinished, $"Finish sync: {sw.Elapsed.Duration()}");
                var playlistModel = GetCachedPlaylistModel(channel.PlCache);

                if (playlistModel != null)
                {
                    if (res.NewPlaylists.Count > 0)
                    {
                        channel.Playlists.AddRange(res.NewPlaylists);
                        playlistModel.All.AddOrUpdate(res.NewPlaylists);
                    }

                    if (res.DeletedPlaylists.Count > 0)
                    {
                        channel.Playlists.RemoveAll(x => res.DeletedPlaylists.Contains(x.Id));
                        playlistModel.All.RemoveKeys(res.DeletedPlaylists);
                    }

                    Parallel.ForEach(res.ExistPlaylists,
                                     pair =>
                                     {
                                         var (key, value) = pair;
                                         var pl = playlistModel.All.Items.FirstOrDefault(x => x.Id == key);
                                         if (pl != null)
                                         {
                                             pl.Count = value.Count;
                                             pl.Items.Clear();
                                             pl.Items.AddRange(value.Select(x => x.Id));
                                         }
                                     });
                }
            }
            catch (Exception e)
            {
                _setTitle?.Invoke(e.Message);
                await _appLogRepository.SetStatus(AppStatus.SyncPlaylistFinished, e.Message);
            }
            finally
            {
                IsWorking = false;
                SelectedEntry = channel;
                UpdateChannels(res);
            }
        }

        private async Task SyncChannels()
        {
            _setTitle?.Invoke("Working channels...");
            IsWorking = true;
            var syncPls = SyncPls;
            var sw = Stopwatch.StartNew();
            await _appLogRepository.SetStatus(syncPls ? AppStatus.SyncPlaylistStarted : AppStatus.SyncWithoutPlaylistStarted,
                                              $"Start sync: {_entries.Count(x => !x.IsNew) - 1}");

            var res = new SyncDiff(syncPls);
            try
            {
                res = await _syncService.Sync(MassSync, syncPls, _entries.Where(x => !x.IsNew).ToList(), SetLog, _setTitle);
                _setTitle?.Invoke(string.Empty.MakeTitle(res.NewItems.Count, sw));
                await _appLogRepository.SetStatus(syncPls ? AppStatus.SyncPlaylistFinished : AppStatus.SyncWithoutPlaylistFinished,
                                                  $"Finished sync: {sw.Elapsed.Duration()}");
            }

            catch (Exception e)
            {
                await _appLogRepository.SetStatus(syncPls ? AppStatus.SyncPlaylistFinished : AppStatus.SyncWithoutPlaylistFinished,
                                                  e.Message);
                _setTitle?.Invoke(e.Message);
            }

            finally
            {
                IsWorking = false;
                foreach (var channel in _entries.Where(x => res.ErrorSyncChannels.Contains(x.Id)))
                {
                    channel.FontStyle = "Italic";
                }

                if (_baseChannel.Count > 0)
                {
                    _setPageIndex?.Invoke(0);
                }

                SelectedEntry = _baseChannel;
                UpdateChannels(res);
            }
        }

        private void UpdateChannels(SyncDiff diff)
        {
            if (diff.NoUnlistedAgain.Count > 0)
            {
                var unlistedPl = _baseChannel.Playlists.First(x => x.Id == "-2");
                unlistedPl.StateItems?.RemoveAll(x => diff.NoUnlistedAgain.Contains(x.Id));
                unlistedPl.Count -= diff.NoUnlistedAgain.Count;
                Parallel.ForEach(_entries.Where(x => !x.IsStateChannel && !x.IsNew && x.Loaded),
                                 channel =>
                                 {
                                     MarkNoUnlisted(channel, diff.NoUnlistedAgain, GetCachedPlaylistModel(channel.PlCache));
                                 });
            }

            var allUnlisted = diff.SyncPls
                ? diff.UnlistedItems.Union(diff.NewItems.Where(x => x.SyncState == SyncState.Unlisted).Select(x => x.Id))
                    .Union(diff.ExistPlaylists.SelectMany(x => x.Value).Where(x => x.Status == SyncState.Unlisted).Select(x => x.Id))
                    .Distinct().ToHashSet()
                : diff.UnlistedItems.Union(diff.NewItems.Where(x => x.SyncState == SyncState.Unlisted).Select(x => x.Id)).Distinct()
                    .ToHashSet();

            var hiddenDic = new ConcurrentDictionary<string, Item>();
            if (allUnlisted.Count > 0)
            {
                Parallel.ForEach(_entries.Where(x => !x.IsStateChannel && !x.IsNew && x.Loaded),
                                 channel =>
                                 {
                                     MarkHidden(channel, allUnlisted, SyncState.Unlisted, hiddenDic);
                                 });
            }

            if (diff.DeletedItems.Count > 0)
            {
                Parallel.ForEach(_entries.Where(x => !x.IsStateChannel && !x.IsNew && x.Loaded),
                                 channel =>
                                 {
                                     MarkHidden(channel, diff.DeletedItems, SyncState.Deleted, hiddenDic);
                                 });
            }

            if (!hiddenDic.IsEmpty)
            {
                var baseUnlisted = _baseChannel.Playlists.First(x => x.Id == "-2");
                baseUnlisted.StateItems?.AddRange(hiddenDic.Select(x => x.Value));
                baseUnlisted.Count += hiddenDic.Count;
                GetCachedPlaylistModel(null)?.All.AddOrUpdate(baseUnlisted);
            }

            if (diff.NewItems.Count > 0)
            {
                UpdateTags(diff.NewItems.SelectMany(x => x.Tags).Distinct());
                _baseExplorerModel.EnableLog = _baseExplorerModel.All.Items.Any();
                foreach (var (key, _) in diff.Channels)
                {
                    _explorerModel.All.AddOrUpdate(diff.NewItems.Where(x => x.ChannelId == key));
                }
            }
        }

        private void UpdateList(Channel channel)
        {
            if (channel != null)
            {
                All.AddOrUpdate(channel);
            }
        }

        private void UpdatePlaylist(Channel channel)
        {
            if (channel.Playlists.Count > 1)
            {
                GetCachedPlaylistModel(channel.PlCache)?.All.AddOrUpdate(channel.Playlists);
            }
        }

        private void UpdateTags(IEnumerable<int> tags)
        {
            if (_baseExplorerModel.Tags == null)
            {
                _baseExplorerModel.CreateTags(tags);
            }
            else
            {
                _baseExplorerModel.Tags.AddRange(_tagRepository.GetTagsByIds(tags).Except(_baseExplorerModel.Tags));
            }
        }

        #endregion
    }
}
