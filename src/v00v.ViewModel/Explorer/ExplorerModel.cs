using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Media.Imaging;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using v00v.Model;
using v00v.Model.Entities;
using v00v.Model.Entities.Instance;
using v00v.Model.Enums;
using v00v.Services.ContentProvider;
using v00v.Services.Persistence;
using v00v.ViewModel.Catalog;
using v00v.ViewModel.Playlists;
using v00v.ViewModel.Popup;
using v00v.ViewModel.Popup.Item;
using v00v.ViewModel.Startup;

namespace v00v.ViewModel.Explorer
{
    public class ExplorerModel : ViewModelBase
    {
        #region Static and Readonly Fields

        private readonly CatalogModel _catalogModel;
        private readonly Channel _channel;
        private readonly IItemRepository _itemRepository;
        private readonly ReadOnlyObservableCollection<Item> _items;
        private readonly IPopupController _popupController;
        private readonly Action<byte> _setPageIndex;
        private readonly IStartupModel _settings;
        private readonly Action<string> _setTitle;
        private readonly ITagRepository _tagRepository;
        private readonly IYoutubeService _youtubeService;

        #endregion

        #region Fields

        private bool _enableLog;
        private bool _enableTags;
        private string _gotoMenu;
        private ItemSort _itemSort;
        private string _logText;
        private string _searchText;
        private Item _selectedEntry;
        private string _selectedPlaylistId;
        private KeyValuePair<int, string> _selectedTag;

        #endregion

        #region Constructors

        public ExplorerModel(Channel channel, CatalogModel catalogModel, Action<byte> setPageIndex, Action<string> setTitle) :
            this(AvaloniaLocator.Current.GetService<IItemRepository>(),
                 AvaloniaLocator.Current.GetService<ITagRepository>(),
                 AvaloniaLocator.Current.GetService<IPopupController>(),
                 AvaloniaLocator.Current.GetService<IYoutubeService>(),
                 AvaloniaLocator.Current.GetService<IStartupModel>())
        {
            _channel = channel;
            _catalogModel = catalogModel;
            _setPageIndex = setPageIndex;
            _setTitle = setTitle;

            All = new SourceCache<Item, string>(m => m.Id);

            if (channel.Items.Count == 0)
            {
                channel.Items.AddRange(channel.IsStateChannel
                                           ? _itemRepository.GetItemsBySyncState(SyncState.Added)
                                           : _itemRepository.GetItems(channel.Id));
            }

            All.AddOrUpdate(channel.Items);

            All.Connect().Filter(this.WhenValueChanged(t => t.SearchText).Select(BuildFilter))
                .Filter(this.WhenValueChanged(t => t.SelectedPlaylistId).Select(BuildPlFilter))
                .Filter(this.WhenValueChanged(t => t.SelectedTag).Select(BuildTagFilter))
                .Sort(GetSorter(), SortOptimisations.ComparesImmutableValuesOnly, 25).ObserveOn(RxApp.MainThreadScheduler)
                .Bind(out _items).DisposeMany().Subscribe();

            IsParentState = channel.IsStateChannel;
            if (IsParentState && All.Items.Any())
            {
                CreateTags(channel.Items.SelectMany(x => x.Tags).Distinct());
            }

            GoToParentCommand = IsParentState
                ? ReactiveCommand.CreateFromTask(SelectChannel, null, RxApp.MainThreadScheduler)
                : ReactiveCommand.Create(SelectPlaylist, null, RxApp.MainThreadScheduler);
            OpenCommand = ReactiveCommand.CreateFromTask((Item item) => OpenItem(item), null, RxApp.MainThreadScheduler);
            DownloadCommand = ReactiveCommand.Create((Item item) => Download("simple", item), null, RxApp.MainThreadScheduler);
            DownloadItemCommand =
                ReactiveCommand.Create((string par) => Download(par, SelectedEntry), null, RxApp.MainThreadScheduler);
            RunItemCommand = ReactiveCommand.CreateFromTask(RunItem, null, RxApp.MainThreadScheduler);
            CopyItemCommand = ReactiveCommand.CreateFromTask((string par) => CopyItem(par), null, RxApp.MainThreadScheduler);
            DeleteItemCommand = ReactiveCommand.CreateFromTask(DeleteItem, null, RxApp.MainThreadScheduler);
            SetItemWatchStateCommand =
                ReactiveCommand.CreateFromTask((WatchState par) => SetItemState(par), null, RxApp.MainThreadScheduler);
            SetSortCommand = ReactiveCommand.Create((string par) => ItemSort = (ItemSort)Enum.Parse(typeof(ItemSort), par),
                                                    null,
                                                    RxApp.MainThreadScheduler);
        }

        private ExplorerModel(IItemRepository itemRepository,
            ITagRepository tagRepository,
            IPopupController popupController,
            IYoutubeService youtubeService,
            IStartupModel settings)
        {
            _itemRepository = itemRepository;
            _tagRepository = tagRepository;
            _popupController = popupController;
            _youtubeService = youtubeService;
            _settings = settings;
        }

        #endregion

        #region Properties

        public SourceCache<Item, string> All { get; }
        public ICommand CopyItemCommand { get; }
        public ICommand DeleteItemCommand { get; }
        public ICommand DownloadCommand { get; }
        public ICommand DownloadItemCommand { get; }

        public bool EnableLog
        {
            get => _enableLog;
            set => Update(ref _enableLog, value);
        }

        public bool EnableTags
        {
            get => _enableTags;
            set => Update(ref _enableTags, value);
        }

        public string GotoMenu
        {
            get => _gotoMenu;
            set => Update(ref _gotoMenu, value);
        }

        public ICommand GoToParentCommand { get; }

        public bool IsParentState { get; }

        public IEnumerable<Item> Items => _items;

        public ItemSort ItemSort
        {
            get => _itemSort;
            set => Update(ref _itemSort, value);
        }

        public ItemSort ItemSortBase { get; set; }

        public string LogText
        {
            get => _logText;
            set => Update(ref _logText, value);
        }

        public ICommand OpenCommand { get; }
        public ICommand RunItemCommand { get; }

        public string SearchText
        {
            get => _searchText;
            set
            {
                Update(ref _searchText, value);
                _setPageIndex.Invoke(Items.Any() ? (byte)0 : (byte)1);
            }
        }

        public Item SelectedEntry
        {
            get => _selectedEntry;
            set => Update(ref _selectedEntry, value);
        }

        public string SelectedPlaylistId
        {
            get => _selectedPlaylistId;
            set => Update(ref _selectedPlaylistId, value);
        }

        public KeyValuePair<int, string> SelectedTag
        {
            get => _selectedTag;
            set => Update(ref _selectedTag, value);
        }

        public ICommand SetItemWatchStateCommand { get; }
        public ICommand SetSortCommand { get; }

        public List<KeyValuePair<int, string>> Tags { get; private set; }

        #endregion

        #region Static Methods

        private static Func<Item, bool> BuildFilter(string searchText)
        {
            return string.IsNullOrWhiteSpace(searchText)
                ? _ => true
                : x => x.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Methods

        public void CreateTags(IEnumerable<int> ids)
        {
            Tags = _tagRepository.GetTagsByIds(ids).ToList();
            if (Tags.Count <= 0)
            {
                return;
            }

            Tags.Insert(0, new KeyValuePair<int, string>(0, " "));
            EnableTags = true;
        }

        public Task DeleteItem(Item item)
        {
            item.Downloaded = false;
            return _itemRepository.UpdateItemFileName(item.Id, null).ContinueWith(_ =>
            {
                if (item.FileName == null)
                {
                    return;
                }

                var fn = new FileInfo(Path.Combine(_settings.DownloadDir, item.ChannelId, item.FileName));
                if (!fn.Exists)
                {
                    return;
                }

                try
                {
                    fn.Delete();
                    SetLog($"{fn.FullName} deleted");
                }
                catch (Exception e)
                {
                    SetLog($"Error {e.Message}");
                }
            });
        }

        public async Task Download(string par, Item item)
        {
            var skip = par == "subs";
            item.SaveDir = $"{Path.Combine(_settings.DownloadDir, item.ChannelId)}";
            var ok = await item.Download(_settings.YouParser, _settings.YouParam, par, $"{_youtubeService.ItemLink}{item.Id}", skip, SetLog);
            if (ok && !skip)
            {
                await _itemRepository.UpdateItemFileName(item.Id, item.FileName);
            }
        }

        public Task SetItemState(WatchState par)
        {
            if (SelectedEntry == null || par == SelectedEntry.WatchState)
            {
                return Task.CompletedTask;
            }

            var id = SelectedEntry.Id;
            var item = _items.First(x => x.Id == id);
            var channelId = item.ChannelId;
            var oldState = item.WatchState;
            item.WatchState = par;

            var bItem = _catalogModel.GetBaseItems.FirstOrDefault(y => y.Id == id);
            if (bItem != null && bItem.WatchState != par)
            {
                bItem.WatchState = par;
            }

            var cItem = _catalogModel.GetCachedExplorerModel(_catalogModel.SelectedEntry.IsStateChannel ? channelId : null, true)?.All
                .Items.FirstOrDefault(y => y.Id == id);
            if (cItem != null && cItem.WatchState != par)
            {
                cItem.WatchState = par;
            }

            PlaylistArrange(_catalogModel.GetCachedPlaylistModel(null), par, oldState, item, true);
            PlaylistArrange(_catalogModel.GetCachedPlaylistModel(_channel.Id, true), par, oldState, item, false);

            return _itemRepository.SetItemsWatchState(par, id, channelId);
        }

        public void SetLog(string log)
        {
            LogText += log + Environment.NewLine;
        }

        public void SetMenu(bool isSearch)
        {
            GotoMenu = isSearch ? "Subscribe" : "Go to Channel";
        }

        private void AddNewPl(PlaylistModel plModel, Item item, WatchState par, bool isState, bool isPlanned)
        {
            Playlist plp;
            if (isState)
            {
                plp = plModel.Entries.Single(x => x.State == par);
            }
            else
            {
                plp = plModel.Entries.FirstOrDefault(x => x.State == par);
                if (plp == null)
                {
                    if (isPlanned)
                    {
                        plp = PlannedPlaylist.Instance;
                        plp.Id = _channel.PlCache;
                    }
                    else
                    {
                        plp = WatchedPlaylist.Instance;
                        plp.Id = _channel.ExCache;
                    }

                    plp.IsStatePlaylist = false;
                    plp.Order = _channel.Playlists.Count;
                    _channel.Playlists.Add(plp);
                    plModel.All.AddOrUpdate(plp);
                }
            }

            plp.Count += 1;
            if (isState)
            {
                plp.StateItems?.Add(item);
            }
            else
            {
                plp.Items.Add(item.Id);
            }
        }

        private Func<Item, bool> BuildPlFilter(string playlistId)
        {
            return playlistId is null or "0" or "-1" or "-2" or "-3" or "-4"
                ? _ => true
                : x => _catalogModel.PlaylistModel.Entries.First(y => y.Id == playlistId).Items.Contains(x.Id);
        }

        private Func<Item, bool> BuildTagFilter(KeyValuePair<int, string> tag)
        {
            return SelectedTag.Key == 0 ? _ => true : x => x.Tags.Contains(tag.Key);
        }

        private Task CopyItem(string par)
        {
            if (SelectedEntry == null)
            {
                return Task.CompletedTask;
            }

            string res = null;
            switch (par)
            {
                case "link":
                    res = $"{_youtubeService.ItemLink}{SelectedEntry.Id}";
                    SetItemState(WatchState.Watched);
                    break;
                case "title":
                    res = SelectedEntry.Title;
                    break;
            }

            return !string.IsNullOrEmpty(res) ? Application.Current?.Clipboard?.SetTextAsync(res) : Task.CompletedTask;
        }

        private Task DeleteItem()
        {
            return SelectedEntry != null && SelectedEntry.Downloaded ? DeleteItem(SelectedEntry) : Task.CompletedTask;
        }

        private IObservable<SortExpressionComparer<Item>> GetSorter()
        {
            return this.WhenValueChanged(x => x.ItemSort).Select(x =>
            {
                return x switch
                {
                    ItemSort.Timestamp => SortExpressionComparer<Item>.Descending(t => t.Timestamp),
                    ItemSort.View => SortExpressionComparer<Item>.Descending(t => t.ViewCount),
                    ItemSort.Like => SortExpressionComparer<Item>.Descending(t => t.LikeCount),
                    ItemSort.Dislike => SortExpressionComparer<Item>.Descending(t => t.DislikeCount),
                    ItemSort.Comment => SortExpressionComparer<Item>.Descending(t => t.Comments),
                    ItemSort.Channel => SortExpressionComparer<Item>.Descending(t => t.ChannelId),
                    ItemSort.Duration => SortExpressionComparer<Item>.Ascending(t => t.Duration),
                    ItemSort.Diff => SortExpressionComparer<Item>.Descending(t => t.ViewDiff),
                    ItemSort.File => SortExpressionComparer<Item>.Descending(t => t.Downloaded),
                    ItemSort.Title => SortExpressionComparer<Item>.Ascending(t => t.Title),
                    ItemSort.Quality => SortExpressionComparer<Item>.Descending(t => t.Quality),
                    _ => SortExpressionComparer<Item>.Descending(t => t.Timestamp),
                };
            });
        }

        private async Task OpenItem(Item item)
        {
            item.Description ??= await _itemRepository.GetItemDescription(item.Id);

            if (item.LargeThumb == null)
            {
                byte[] th;
                try
                {
                    th = await _youtubeService.GetStreamFromUrl(_youtubeService.GetPreviewThumbLink(item.Id));
                }
                catch
                {
                    th = Array.Empty<byte>();
                }

                if (th.Length > 0)
                {
                    await using var ms = new MemoryStream(th);
                    item.LargeThumb = new Bitmap(ms);
                }
            }

            item.ChannelTitle ??= _catalogModel.Entries.FirstOrDefault(x => x.Id == item.ChannelId)?.Title;

            _popupController.Show(new ItemPopupContext(item));
        }

        private void PlaylistArrange(PlaylistModel plModel, WatchState par, WatchState oldState, Item item, bool isState)
        {
            if (plModel == null)
            {
                return;
            }

            switch (par)
            {
                case WatchState.Notset:

                    var pln = plModel.Entries.Single(x => x.State == oldState);
                    pln.Count -= 1;

                    if (isState)
                    {
                        pln.StateItems?.RemoveAll(x => x.Id == item.Id);
                    }
                    else
                    {
                        pln.Items.RemoveAll(x => x == item.Id);
                    }

                    if (!isState && pln.Count == 0)
                    {
                        _channel.Playlists.Remove(pln);
                        plModel.All.RemoveKey(pln.Id);
                    }

                    break;

                case WatchState.Watched:

                    AddNewPl(plModel, item, par, isState, false);

                    if (oldState == WatchState.Planned)
                    {
                        RemovePrevPl(plModel, item, oldState, isState);
                    }

                    break;

                case WatchState.Planned:

                    AddNewPl(plModel, item, par, isState, true);

                    if (oldState == WatchState.Watched)
                    {
                        RemovePrevPl(plModel, item, oldState, isState);
                    }

                    break;
            }
        }

        private void RemovePrevPl(PlaylistModel plModel, Item item, WatchState oldState, bool isState)
        {
            var pl = isState
                ? plModel.Entries.Single(x => x.State == oldState)
                : plModel.Entries.FirstOrDefault(x => x.State == oldState);

            if (pl == null)
            {
                return;
            }

            pl.Count -= 1;
            if (isState)
            {
                pl.StateItems?.RemoveAll(x => x.Id == item.Id);
            }
            else
            {
                pl.Items.RemoveAll(x => x == item.Id);
            }

            if (isState || pl.Count != 0)
            {
                return;
            }

            _channel.Playlists.Remove(pl);
            plModel.All.RemoveKey(pl.Id);
        }

        private Task RunItem()
        {
            if (string.IsNullOrEmpty(_settings.WatchApp))
            {
                SetLog("Please select media player in setting");
                return Task.CompletedTask;
            }

            SelectedEntry?.RunItem(_settings.WatchApp, _settings.DownloadDir, $"{_youtubeService.ItemLink}{SelectedEntry.Id}");
            return SetItemState(WatchState.Watched);
        }

        private Task SelectChannel()
        {
            if (SelectedEntry == null)
            {
                return Task.CompletedTask;
            }

            var oldId = SelectedEntry.ChannelId;
            var ch = _catalogModel.Entries.FirstOrDefault(y => y.Id == oldId);
            if (ch == null)
            {
                _setTitle?.Invoke($"Working: {oldId}..");
                return _youtubeService.GetChannelAsync(oldId, true).ContinueWith(x =>
                {
                    var channel = x.GetAwaiter().GetResult();
                    if (channel.Items.Count > 0)
                    {
                        _catalogModel.AddChannelToList(channel, false);
                        _setTitle?.Invoke($"Ready: {channel.Title}");
                    }
                    else
                    {
                        SetLog($"Empty channel: {channel.Title}");
                    }
                });
            }

            if (_catalogModel.SelectedEntry == null || _catalogModel.SelectedEntry.Id != oldId)
            {
                _catalogModel.SelectedEntry = ch;
            }

            return Task.CompletedTask;
        }

        private void SelectPlaylist()
        {
            if (SelectedEntry == null)
            {
                return;
            }

            var oldId = SelectedEntry.Id;
            var pl = _catalogModel.PlaylistModel.Entries.FirstOrDefault(x => x.Items.Contains(oldId));
            if (pl == null)
            {
                return;
            }

            if (_catalogModel.PlaylistModel.SelectedEntry == null || _catalogModel.PlaylistModel.SelectedEntry.Id != pl.Id)
            {
                _catalogModel.PlaylistModel.SelectedEntry = pl;
            }
        }

        #endregion
    }
}
