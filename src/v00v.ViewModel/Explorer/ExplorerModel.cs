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
using Microsoft.Extensions.Configuration;
using ReactiveUI;
using v00v.Model;
using v00v.Model.Entities;
using v00v.Model.Enums;
using v00v.Services.ContentProvider;
using v00v.Services.Persistence;
using v00v.ViewModel.Catalog;
using v00v.ViewModel.Popup;
using v00v.ViewModel.Popup.Item;

namespace v00v.ViewModel.Explorer
{
    public class ExplorerModel : ViewModelBase
    {
        #region Static and Readonly Fields

        private readonly CatalogModel _catalogModel;
        private readonly IConfiguration _configuration;
        private readonly IItemRepository _itemRepository;
        private readonly ReadOnlyObservableCollection<Item> _items;
        private readonly IPopupController _popupController;
        private readonly Action<byte> _setPageIndex;
        private readonly Action<string> _setTitle;
        private readonly IYoutubeService _youtubeService;

        #endregion

        #region Fields

        private bool _enableLog;
        private string _gotoMenu;
        private ItemSort _itemSort;
        private string _logText;
        private string _searchText;
        private Item _selectedEntry;
        private string _selectedPlaylistId;

        #endregion

        #region Constructors

        public ExplorerModel(Channel channel, CatalogModel catalogModel, Action<byte> setPageIndex, Action<string> setTitle) :
            this(AvaloniaLocator.Current.GetService<IItemRepository>(),
                 AvaloniaLocator.Current.GetService<IPopupController>(),
                 AvaloniaLocator.Current.GetService<IYoutubeService>(),
                 AvaloniaLocator.Current.GetService<IConfigurationRoot>())
        {
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
                .Sort(GetSorter(), SortOptimisations.ComparesImmutableValuesOnly, 25).ObserveOn(RxApp.MainThreadScheduler)
                .Bind(out _items).DisposeMany().Subscribe();

            IsParentState = channel.IsStateChannel;
            GoToParentCommand = IsParentState
                ? ReactiveCommand.CreateFromTask(SelectChannel, null, RxApp.MainThreadScheduler)
                : ReactiveCommand.Create(SelectPlaylist, null, RxApp.MainThreadScheduler);
            OpenCommand = ReactiveCommand.Create((Item item) => OpenItem(item), null, RxApp.MainThreadScheduler);
            DownloadCommand =
                ReactiveCommand.CreateFromTask((Item item) => DownloadItem("simple", item), null, RxApp.MainThreadScheduler);
            DownloadItemCommand =
                ReactiveCommand.Create((string par) => DownloadItem(par, SelectedEntry), null, RxApp.MainThreadScheduler);
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
            IPopupController popupController,
            IYoutubeService youtubeService,
            IConfiguration configuration)
        {
            _itemRepository = itemRepository;
            _popupController = popupController;
            _youtubeService = youtubeService;
            _configuration = configuration;
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

        public ICommand SetItemWatchStateCommand { get; }
        public ICommand SetSortCommand { get; }

        #endregion

        #region Static Methods

        private static Func<Item, bool> BuildFilter(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return x => true;
            }

            return x => x.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Methods

        public async Task DeleteItem(Item item)
        {
            item.Downloaded = false;
            await Task.WhenAll(_itemRepository.UpdateItemFileName(item.Id, null)).ContinueWith(done =>
            {
                if (item.FileName == null)
                {
                    return;
                }

                var fn = new FileInfo(Path.Combine(_configuration.GetValue<string>("AppSettings:BaseDir"),
                                                   item.ChannelId,
                                                   item.FileName));
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
            bool skip = par == "subs";
            item.SaveDir = $"{Path.Combine(_configuration.GetValue<string>("AppSettings:BaseDir"), item.ChannelId)}";
            var task = item.Download(_configuration.GetValue<string>("AppSettings:YouParser"),
                                     _configuration.GetValue<string>("AppSettings:YouParam"),
                                     par,
                                     skip,
                                     SetLog);
            await Task.WhenAll(task).ContinueWith(done =>
            {
                if (task.Result && !skip)
                {
                    _itemRepository.UpdateItemFileName(item.Id, item.FileName);
                }
            });
        }

        public void SetLog(string log)
        {
            LogText += log + Environment.NewLine;
        }

        public void SetMenu(bool isSearch)
        {
            GotoMenu = isSearch ? "Subscribe" : "Go to Channel";
        }

        private Func<Item, bool> BuildPlFilter(string playlistId)
        {
            if (playlistId == null || playlistId == "0" || playlistId == "-1" || playlistId == "-2" || playlistId == "-3"
                || playlistId == "-4")
            {
                return x => true;
            }

            return x => _catalogModel.PlaylistModel.Entries.First(y => y.Id == playlistId).Items.Contains(x.Id);
        }

        private async Task CopyItem(string par)
        {
            if (SelectedEntry != null)
            {
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
        }

        private async Task DeleteItem()
        {
            if (SelectedEntry != null && SelectedEntry.Downloaded)
            {
                await DeleteItem(SelectedEntry);
            }
        }

        private async Task DownloadItem(string par, Item item)
        {
            await Download(par, item);
        }

        private IObservable<SortExpressionComparer<Item>> GetSorter()
        {
            return this.WhenValueChanged(x => x.ItemSort).Select(x =>
            {
                switch (x)
                {
                    case ItemSort.Timestamp:
                        return SortExpressionComparer<Item>.Descending(t => t.Timestamp);
                    case ItemSort.View:
                        return SortExpressionComparer<Item>.Descending(t => t.ViewCount);
                    case ItemSort.Like:
                        return SortExpressionComparer<Item>.Descending(t => t.LikeCount);
                    case ItemSort.Dislike:
                        return SortExpressionComparer<Item>.Descending(t => t.DislikeCount);
                    case ItemSort.Comment:
                        return SortExpressionComparer<Item>.Descending(t => t.Comments);
                    case ItemSort.Duration:
                        return SortExpressionComparer<Item>.Ascending(t => t.Duration);
                    case ItemSort.Diff:
                        return SortExpressionComparer<Item>.Descending(t => t.ViewDiff);
                    case ItemSort.File:
                        return SortExpressionComparer<Item>.Descending(t => t.Downloaded);
                    case ItemSort.Title:
                        return SortExpressionComparer<Item>.Ascending(t => t.Title);
                    default:
                        return SortExpressionComparer<Item>.Descending(t => t.Timestamp);
                }
            });
        }

        private async Task OpenItem(Item item)
        {
            if (item.Description == null)
            {
                item.Description = await _itemRepository.GetItemDescription(item.Id);
            }

            if (item.LargeThumb == null)
            {
                byte[] th;
                try
                {
                    th = await _youtubeService.GetStreamFromUrl(item.ThumbLink);
                }
                catch
                {
                    th = new byte[0];
                }

                if (th.Length > 0)
                {
                    using (var ms = new MemoryStream(th))
                    {
                        item.LargeThumb = new Bitmap(ms);
                    }
                }
            }

            _popupController.Show(new ItemPopupContext(item));
        }

        private async Task RunItem()
        {
            SelectedEntry?.RunItem(_configuration.GetValue<string>("AppSettings:WatchApp"),
                                   _configuration.GetValue<string>("AppSettings:BaseDir"));
            await SetItemState(WatchState.Watched);
        }

        private async Task SelectChannel()
        {
            if (SelectedEntry == null)
            {
                return;
            }

            var oldId = SelectedEntry.ChannelId;
            var ch = _catalogModel.Entries.FirstOrDefault(y => y.Id == oldId);
            if (ch == null)
            {
                _setTitle?.Invoke($"Working: {oldId}..");
                var chh = await _youtubeService.GetChannelAsync(oldId, true);
                if (chh.Items.Count > 0)
                {
                    _catalogModel.AddChannelToList(chh, true);
                    _setTitle?.Invoke($"Ready: {chh.Title}");
                }
                else
                {
                    SetLog($"Empty channel: {chh.Title}");
                }
            }
            else
            {
                if (_catalogModel.SelectedEntry == null || _catalogModel.SelectedEntry.Id != oldId)
                {
                    _catalogModel.SelectedEntry = ch;
                }
            }
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

        private async Task SetItemState(WatchState par)
        {
            if (SelectedEntry == null || par == SelectedEntry.WatchState)
            {
                return;
            }

            var id = SelectedEntry.Id;
            var item = _items.First(x => x.Id == id);
            var oldState = item.WatchState;
            item.WatchState = par;
            var task = _itemRepository.SetItemsWatchState(par, item.Id, item.ChannelId);
            await Task.WhenAll(task).ContinueWith(done =>
            {
                var bitem = _catalogModel.GetBaseItems.FirstOrDefault(x => x.Id == id);
                if (bitem != null && bitem.WatchState != par)
                {
                    bitem.WatchState = par;
                }

                var citem = _catalogModel.GetCachedExplorerModel(_catalogModel.SelectedEntry.IsStateChannel ? item.ChannelId : null)?.All
                    .Items.FirstOrDefault(x => x.Id == id);
                if (citem != null && citem.WatchState != par)
                {
                    citem.WatchState = par;
                }

                var plmodel = _catalogModel.GetCachedPlaylistModel(null);

                if (plmodel == null)
                {
                    return;
                }

                switch (par)
                {
                    case WatchState.Notset:

                        var pln = plmodel.Entries.Single(x => x.State == oldState);
                        pln.Count -= 1;
                        pln.StateItems?.RemoveAll(x => x.Id == item.Id);

                        break;

                    case WatchState.Watched:

                        var plw = plmodel.Entries.Single(x => x.State == par);
                        plw.Count += 1;
                        plw.StateItems?.Add(item);

                        if (oldState == WatchState.Planned)
                        {
                            var pl = plmodel.Entries.Single(x => x.State == oldState);
                            pl.Count -= 1;
                            pl.StateItems?.RemoveAll(x => x.Id == item.Id);
                        }

                        break;

                    case WatchState.Planned:

                        var plp = plmodel.Entries.Single(x => x.State == par);
                        plp.Count += 1;
                        plp.StateItems?.Add(item);

                        if (oldState == WatchState.Watched)
                        {
                            var pl = plmodel.Entries.Single(x => x.State == oldState);
                            pl.Count -= 1;
                            pl.StateItems?.RemoveAll(x => x.Id == item.Id);
                        }

                        break;
                }
            });

            All.AddOrUpdate(item);
            SelectedEntry = _items.First(x => x.Id == id);
        }

        #endregion
    }
}
