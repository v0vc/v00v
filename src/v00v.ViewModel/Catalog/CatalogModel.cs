using System;
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
                            if (entry.Items.Count > 0)
                            {
                                _baseExplorerModel.All.AddOrUpdate(entry.Items);
                            }
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
                    ExplorerModel = ViewModelCache.GetOrAdd(entry.ExCache, () => new ExplorerModel(entry, this, setPageIndex, setTitle));
                    PlaylistModel = ViewModelCache.GetOrAdd(entry.PlCache,
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

        public ICommand SaveChannelCommand { get; set; }

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

        private static void MarkUnlisted(Channel channel, ICollection<string> noUnlisted, PlaylistModel plmodel)
        {
            Parallel.ForEach(channel.Items.Where(x => noUnlisted.Contains(x.Id)),
                             item =>
                             {
                                 if (item.SyncState != SyncState.Notset)
                                 {
                                     item.SyncState = SyncState.Notset;
                                 }
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

        public void AddChannelToList(Channel channel, bool updateList)
        {
            var lockMe = new object();
            lock (lockMe)
            {
                if (_entries.Select(x => x.Id).Contains(channel.Id))
                {
                    return;
                }

                channel.IsNew = true;
                channel.Order = GetMinOrder() - 1;
                if (!updateList)
                {
                    return;
                }

                UpdateList(channel);
                SetSelected(channel.Id);
            }
        }

        public ExplorerModel GetCachedExplorerModel(string channelId)
        {
            if (channelId == null)
            {
                return _baseExplorerModel;
            }

            var exc = _entries.FirstOrDefault(x => x.Id == channelId)?.ExCache;
            return exc != null ? ViewModelCache.Get<ExplorerModel>(exc) : null;
        }

        public PlaylistModel GetCachedPlaylistModel(string channelId)
        {
            if (channelId == null)
            {
                return _basePlaylistModel;
            }

            var plc = _entries.FirstOrDefault(x => x.Id == channelId)?.PlCache;
            return plc != null ? ViewModelCache.Get<PlaylistModel>(plc) : null;
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
            var task = _backupService.Backup(_entries.Where(x => !x.IsStateChannel), SetLog);
            await Task.WhenAll(task).ContinueWith(done =>
            {
                IsWorking = false;
            });

            if (task.Status == TaskStatus.Faulted)
            {
                _setTitle?.Invoke(task.Exception == null ? "Faulted" : $"{task.Exception.Message}");
                return;
            }

            _setTitle?.Invoke(string.Empty.MakeTitle(task.Result, sw));
        }

        private async Task ClearAdded()
        {
            if (SelectedEntry == null || SelectedEntry.IsNew)
            {
                return;
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
                    return;
                }

                _baseChannel.Items.Clear();

                Parallel.ForEach(_entries.Where(x => x.Count > 0),
                                 channel =>
                                 {
                                     channel.Count = 0;
                                     if (channel.IsStateChannel)
                                     {
                                         ExplorerModel.All.Clear();
                                         ExplorerModel.EnableLog = false;
                                     }
                                     else
                                     {
                                         if (channel.Items.Count > 0)
                                         {
                                             var cmodel = GetCachedExplorerModel(channel.Id);
                                             if (cmodel != null)
                                             {
                                                 Parallel.ForEach(cmodel.All.Items.Where(x => x.SyncState == SyncState.Added),
                                                                  item =>
                                                                  {
                                                                      item.SyncState = SyncState.Notset;
                                                                  });
                                             }
                                         }
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

            var task1 = _channelRepository.UpdateChannelSyncState(chId, 0);
            var task2 = _channelRepository.UpdateItemsCount(chId, 0);
            await Task.WhenAll(task1, task2).ContinueWith(done =>
            {
                IsWorking = false;
            });

            if (task1.Status != TaskStatus.Faulted && task2.Status != TaskStatus.Faulted)
            {
                _setTitle.Invoke(string.Empty.MakeTitle(count, sw));
            }
        }

        private async Task DeleteChannel()
        {
            if (SelectedEntry == null || SelectedEntry.Working)
            {
                return;
            }

            var title = SelectedEntry.Title;
            var deletedId = SelectedEntry.Id;
            var ch = _entries.First(x => x.Id == deletedId);
            var count = ch.Count;
            var index = All.Items.IndexOf(ch);
            ViewModelCache.Remove(ch.ExCache);
            ViewModelCache.Remove(ch.PlCache);
            if (ch.IsNew)
            {
                _explorerModel.All.RemoveKeys(ch.Items.Select(x => x.Id));
                All.RemoveKey(ch.Id);
                SelectedEntry = _baseChannel;
                return;
            }

            IsWorking = true;
            var sw = Stopwatch.StartNew();
            All.RemoveKey(ch.Id);
            _baseChannel.Count -= count;
            _baseChannel.Items.RemoveAll(x => x.ChannelId == deletedId);
            GetCachedExplorerModel(null)?.All.RemoveKeys(ch.Items.Select(x => x.Id));
            if (ch.Items.Any(x => x.WatchStateSet)
                || ch.Items.Any(x => x.SyncState == SyncState.Unlisted || x.SyncState == SyncState.Deleted))
            {
                var plmodel = GetCachedPlaylistModel(null);
                if (plmodel != null)
                {
                    var watched = ch.Items.Where(x => x.WatchState == WatchState.Watched).ToList();
                    if (watched.Any())
                    {
                        var pl = plmodel.Entries.First(x => x.Id == "0");
                        pl.Count -= watched.Count;
                        pl.StateItems?.RemoveAll(x => x.ChannelId == ch.Id && x.WatchState == WatchState.Watched);
                    }

                    var planned = ch.Items.Where(x => x.WatchState == WatchState.Planned).ToList();
                    if (planned.Any())
                    {
                        var pl = plmodel.Entries.First(x => x.Id == "-1");
                        pl.Count -= planned.Count;
                        pl.StateItems?.RemoveAll(x => x.ChannelId == ch.Id && x.WatchState == WatchState.Planned);
                    }

                    var unlisted = ch.Items.Where(x => x.SyncState == SyncState.Unlisted || x.SyncState == SyncState.Deleted).ToList();
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
                IsWorking = false;
            });

            if (task.Status == TaskStatus.Faulted)
            {
                _setTitle?.Invoke(task.Exception == null ? "Faulted" : $"{task.Exception.Message}");
                return;
            }

            _setTitle?.Invoke(string.Empty.MakeTitle(task.Result, sw));

            GetCachedExplorerModel(null)?.SetLog($"Deleted: {deletedId} - {title}");
            await _appLogRepository.SetStatus(AppStatus.ChannelDeleted, $"Delete channel: {deletedId} - {title}");
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
            var task = _youtubeService.GetRelatedChannelsAsync(channel.Id, _entries.Where(x => !x.IsStateChannel).Select(x => x.Id));
            await Task.WhenAll(task).ContinueWith(done =>
            {
                IsWorking = false;
            });

            if (task.Status == TaskStatus.Faulted)
            {
                _setTitle?.Invoke(task.Exception == null ? "Faulted" : $"{task.Exception.Message}");
                return;
            }

            if (task.Result != null)
            {
                _setTitle?.Invoke(string.Empty.MakeTitle(task.Result.Length, sw));
                Parallel.ForEach(task.Result,
                                 x =>
                                 {
                                     AddChannelToList(x, false);
                                 });
                All.AddOrUpdate(task.Result);
                SetSelected(_baseChannel.Id);
            }
            else
            {
                _setTitle?.Invoke(string.Empty.MakeTitle(0, sw));
                SetSelected(oldId);
            }
        }

        private void MarkDeleted(Channel channel, ICollection<string> deletedIds)
        {
            var unlistedpl = _baseChannel.Playlists.First(x => x.Id == "-2");
            unlistedpl.StateItems?.AddRange(channel.Items.Where(x => deletedIds.Contains(x.Id)));
            unlistedpl.Count += deletedIds.Count;

            Parallel.ForEach(channel.Items.Where(x => deletedIds.Contains(x.Id)),
                             item =>
                             {
                                 if (item.SyncState != SyncState.Deleted)
                                 {
                                     item.SyncState = SyncState.Deleted;
                                 }
                             });

            var unlistpl = channel.Playlists.FirstOrDefault(x => x.Id == channel.Id);
            if (unlistpl == null)
            {
                var unpl = UnlistedPlaylist.Instance;
                unpl.IsStatePlaylist = false;
                unpl.Id = channel.Id;
                unpl.Order = channel.Playlists.Count;
                unpl.Count = deletedIds.Count;
                unpl.Items.AddRange(deletedIds);
                channel.Playlists.Add(unpl);
            }
            else
            {
                var deldiff = deletedIds.Except(unlistpl.Items).ToHashSet();
                if (deldiff.Count > 0)
                {
                    unlistpl.Items.AddRange(deldiff);
                    unlistpl.Count += deldiff.Count;
                }
            }
        }

        private void OnException(Exception exception)
        {
            _popupController.Hide();
            var exm = GetCachedExplorerModel(null);
            if (exm == null)
            {
                return;
            }

            exm.SetLog(exception.Message);
            if (exception.InnerException != null && !string.IsNullOrEmpty(exception.InnerException.Message))
            {
                exm.SetLog(exception.InnerException.Message);
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
            await _youtubeService.SetItemsStatistic(ch, false);
            var chId = ch.IsStateChannel ? null : ch.Id;
            var task = _itemRepository.UpdateItemsStats(ch.Items, chId);

            await Task.WhenAll(task).ContinueWith(done =>
            {
                IsWorking = false;
            });

            if (task.Status == TaskStatus.Faulted)
            {
                _setTitle?.Invoke(task.Exception == null ? "Faulted" : $"{task.Exception.Message}");
                return;
            }

            _setTitle?.Invoke(string.Empty.MakeTitle(ch.Items.Count, sw));

            if (task.Result.Count > 0)
            {
                Parallel.ForEach(ch.Items,
                                 x =>
                                 {
                                     x.ViewDiff = task.Result.TryGetValue(x.Id, out var vdiff) ? vdiff : 0;
                                 });
            }
            else
            {
                Parallel.ForEach(ch.Items,
                                 x =>
                                 {
                                     x.ViewDiff = 0;
                                 });
            }

            GetCachedExplorerModel(chId)?.All.AddOrUpdate(ch.Items);
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
            GetCachedExplorerModel(null)?.SetLog($"Saved {r} rows");
        }

        private async Task RestoreChannels()
        {
            IsWorking = true;
            var sw = Stopwatch.StartNew();

            var task = _backupService.Restore(_entries.Where(x => !x.IsStateChannel).Select(x => x.Id),
                                              MassSync,
                                              _setTitle,
                                              UpdateList,
                                              SetLog);

            await Task.WhenAll(task).ContinueWith(done =>
            {
                IsWorking = false;
            });

            if (task.Status == TaskStatus.Faulted)
            {
                _setTitle?.Invoke(task.Exception == null ? "Faulted" : $"{task.Exception.Message}");
                return;
            }

            _setTitle?.Invoke(string.Empty.MakeTitle(task.Result.ChannelsCount, sw));

            var pl = _baseChannel.Playlists.First(x => x.Id == "-1");
            pl.Count += task.Result.PlannedCount;
            var wl = _baseChannel.Playlists.First(x => x.Id == "0");
            wl.Count += task.Result.WatchedCount;
            GetCachedPlaylistModel(null)?.All.AddOrUpdate(new[] { pl, wl });
        }

        private async Task SaveChannel()
        {
            if (SelectedEntry == null || !SelectedEntry.IsNew)
            {
                return;
            }

            var oldId = SelectedEntry.Id;
            var channel = _entries.First(x => x.Id == oldId);

            channel.Title = channel.Title.Trim();
            _setTitle.Invoke($"Working {channel.Title}..");
            IsWorking = true;
            var sw = Stopwatch.StartNew();

            await _youtubeService.AddPlaylists(channel);
            var task = _channelRepository.AddChannel(channel);
            await Task.WhenAll(task).ContinueWith(done =>
            {
                IsWorking = false;
            });

            if (task.Status == TaskStatus.Faulted)
            {
                _setTitle?.Invoke(task.Exception == null ? "Faulted" : $"{task.Exception.Message}");
                return;
            }

            _setTitle?.Invoke(string.Empty.MakeTitle(task.Result, sw));

            channel.IsNew = false;
            ResortList(task.Result);
            UpdatePlaylist(channel);
        }

        private void SetLog(string log)
        {
            if (string.IsNullOrEmpty(log))
            {
                return;
            }

            var exmodel = GetCachedExplorerModel(null);
            if (exmodel != null)
            {
                exmodel.LogText += log + Environment.NewLine;
            }
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
            if (_settings.EnableDailySchedule && _settings.DailyParsed)
            {
                _taskDispatcher.DailySync = _settings.DailySyncTime;
                var task = Task.Factory.StartNew(() => _taskDispatcher.RunDaily(_syncService,
                                                                                _appLogRepository,
                                                                                _entries.Where(x => !x.IsNew).ToList(),
                                                                                true,
                                                                                SetLog,
                                                                                UpdateChannels),
                                                 TaskCreationOptions.LongRunning).ContinueWith(t =>
                {
                    SetLog(t.Exception?.Message);
                });
            }

            if (_settings.EnableRepeatSchedule && _settings.RepeatParsed)
            {
                _taskDispatcher.RepeatSync = _settings.RepeatMin;
                Task.Factory.StartNew(() => _taskDispatcher.RunRepeat(_syncService,
                                                                      _appLogRepository,
                                                                      _entries.Where(x => !x.IsNew).ToList(),
                                                                      false,
                                                                      SetLog,
                                                                      UpdateChannels),
                                      TaskCreationOptions.LongRunning).ContinueWith(t =>
                {
                    SetLog(t.Exception?.Message);
                });
            }

            if (_settings.EnableParserUpdateSchedule && _settings.ParserUpdateParsed)
            {
                _taskDispatcher.ParserUpdate = _settings.DailyParserUpdateTime;
                Task.Factory.StartNew(() => _taskDispatcher.RunUpdateParser(SetLog, _settings.UpdateParser),
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
            var task = _syncService.Sync(true, true, lst, SetLog);
            await Task.WhenAll(task).ContinueWith(done =>
            {
                IsWorking = false;
            });

            if (task.Status == TaskStatus.Faulted)
            {
                var str = task.Exception == null ? "Faulted" : $"{task.Exception.Message}";
                _setTitle?.Invoke(str);
                await _appLogRepository.SetStatus(AppStatus.SyncPlaylistFinished, str);
                return;
            }

            _setTitle?.Invoke(string.Empty.MakeTitle(task.Result.NewItems.Count, sw));

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
                plmodel?.All.RemoveKeys(deletedpl);
            }

            Parallel.ForEach(task.Result.ExistPlaylists,
                             pair =>
                             {
                                 var (key, value) = pair;
                                 var pl = plmodel?.All.Items.FirstOrDefault(x => x.Id == key);
                                 if (pl != null)
                                 {
                                     pl.Count = value.Count;
                                     pl.Items.Clear();
                                     pl.Items.AddRange(value.Select(x => x.Id));
                                 }
                             });

            if (task.Result.NoUnlistedAgain.Count > 0)
            {
                var unlistedpl = _baseChannel.Playlists.First(x => x.Id == "-2");
                unlistedpl.StateItems?.RemoveAll(x => task.Result.NoUnlistedAgain.Contains(x.Id));
                unlistedpl.Count -= task.Result.NoUnlistedAgain.Count;
                MarkUnlisted(channel, task.Result.NoUnlistedAgain, plmodel);
            }

            if (task.Result.DeletedItems.Count > 0)
            {
                MarkDeleted(channel, task.Result.DeletedItems);
            }

            All.AddOrUpdate(lst);
            if (task.Result.NewItems.Count > 0)
            {
                GetCachedExplorerModel(channel.Id)?.All.AddOrUpdate(task.Result.NewItems);
            }

            if (SelectedEntry == null || SelectedEntry.Id != oldId)
            {
                SelectedEntry = _entries.First(x => x.Id == oldId);
            }

            await _appLogRepository.SetStatus(AppStatus.SyncPlaylistFinished, $"Finish sync: {sw.Elapsed.Duration()}");
        }

        private async Task SyncChannels()
        {
            _setTitle?.Invoke($"Working {_entries.Count(x => !x.IsNew) - 1} channels..");
            IsWorking = true;
            var syncPls = SyncPls;
            var sw = Stopwatch.StartNew();
            await _appLogRepository.SetStatus(syncPls ? AppStatus.SyncPlaylistStarted : AppStatus.SyncWithoutPlaylistStarted,
                                              $"Start sync: {_entries.Count(x => !x.IsNew) - 1}");

            var task = _syncService.Sync(MassSync, syncPls, _entries.Where(x => !x.IsNew).ToList(), SetLog);

            await Task.WhenAll(task).ContinueWith(done =>
            {
                IsWorking = false;
            });

            if (task.Status == TaskStatus.Faulted)
            {
                var str = task.Exception == null ? "Faulted" : $"{task.Exception.Message}";
                await _appLogRepository.SetStatus(AppStatus.SyncPlaylistFinished, str);
                _setTitle?.Invoke(str);
                return;
            }

            _setTitle?.Invoke(string.Empty.MakeTitle(task.Result.NewItems.Count, sw));

            UpdateChannels(task.Result);

            await _appLogRepository.SetStatus(syncPls ? AppStatus.SyncPlaylistFinished : AppStatus.SyncWithoutPlaylistFinished,
                                              $"Finished sync: {sw.Elapsed.Duration()}");
            //SetErroSyncChannels(diff.ErrorSyncChannels);
        }

        private void UpdateChannels(SyncDiff diff)
        {
            if (diff.NoUnlistedAgain.Count > 0)
            {
                var unlistedpl = _baseChannel.Playlists.First(x => x.Id == "-2");
                unlistedpl.StateItems?.RemoveAll(x => diff.NoUnlistedAgain.Contains(x.Id));
                unlistedpl.Count -= diff.NoUnlistedAgain.Count;
                Parallel.ForEach(_entries.Where(x => !x.IsStateChannel && !x.IsNew),
                                 channel =>
                                 {
                                     MarkUnlisted(channel, diff.NoUnlistedAgain, GetCachedPlaylistModel(channel.Id));
                                 });
            }

            if (diff.DeletedItems.Count > 0)
            {
                Parallel.ForEach(_entries.Where(x => !x.IsStateChannel && !x.IsNew),
                                 channel =>
                                 {
                                     MarkDeleted(channel, diff.DeletedItems);
                                 });
            }

            All.AddOrUpdate(_entries.Where(x => !x.IsNew));
            if (diff.NewItems.Count > 0)
            {
                var expmodel = GetCachedExplorerModel(null);
                if (expmodel != null)
                {
                    expmodel.All.AddOrUpdate(diff.NewItems);
                    expmodel.EnableLog = expmodel.All.Items.Any();
                    _setPageIndex?.Invoke(0);
                }
            }

            if (SelectedEntry != _baseChannel)
            {
                SelectedEntry = _baseChannel;
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
                GetCachedPlaylistModel(channel.Id)?.All.AddOrUpdate(channel.Playlists);
            }
        }

        #endregion
    }
}
