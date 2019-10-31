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
using v00v.Model.Core;
using v00v.Model.Entities;
using v00v.Model.Enums;
using v00v.Services.ContentProvider;
using v00v.Services.Persistence;
using v00v.ViewModel.Catalog;
using v00v.ViewModel.Playlists;
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
        private readonly IYoutubeService _youtubeService;

        #endregion

        #region Fields

        private ItemSort _itemSort;
        private string _searchText;
        private Item _selectedEntry;
        private string _selectedPlaylistId;

        #endregion

        #region Constructors

        public ExplorerModel(Channel channel, CatalogModel catalogModel) : this(AvaloniaLocator.Current.GetService<IItemRepository>(),
                                                                                AvaloniaLocator.Current.GetService<IPopupController>(),
                                                                                AvaloniaLocator.Current.GetService<IYoutubeService>(),
                                                                                AvaloniaLocator.Current.GetService<IConfigurationRoot>())
        {
            _catalogModel = catalogModel;

            All = new SourceCache<Item, string>(m => m.Id);

            if (channel.Items.Count == 0)
            {
                channel.Items.AddRange(channel.IsStateChannel
                                           ? _itemRepository.GetItemsBySyncState(SyncState.Added).GetAwaiter().GetResult()
                                           : _itemRepository.GetItems(channel.Id).GetAwaiter().GetResult());
            }

            All.AddOrUpdate(channel.Items);

            All.Connect().Filter(this.WhenValueChanged(t => t.SearchText).Select(BuildFilter))
                .Filter(this.WhenValueChanged(t => t.SelectedPlaylistId).Select(BuildPlFilter))
                .Sort(GetSorter(), SortOptimisations.ComparesImmutableValuesOnly, 25).Bind(out _items).DisposeMany().Subscribe();

            OpenCommand = new Command(async x => await OpenItem(x));

            DownloadCommand = new Command(async x => await Download("simple", (Item)x));

            DownloadItemCommand = new Command(async x => await Download((string)x, SelectedEntry));

            RunItemCommand = new Command(async x => await RunItem(true));

            CopyItemCommand = new Command(async x => await CopyItem((string)x));

            IsParentState = channel.IsStateChannel;
            if (IsParentState)
            {
                GoToParentCommand =
                    new Command(x => _catalogModel.SelectedEntry = _catalogModel.Entries.First(y => y.Id == SelectedEntry.ChannelId));
            }

            DeleteItemCommand = new Command(async x => await DeleteItem());

            SetSortCommand = new Command(x => ItemSort = (ItemSort)Enum.Parse(typeof(ItemSort), (string)x));

            SetItemWatchStateCommand = new Command(async x => await SetItemState((WatchState)x));
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
        public ICommand GoToParentCommand { get; }
        public bool IsParentState { get; }
        public IEnumerable<Item> Items => _items;

        public ItemSort ItemSort
        {
            get => _itemSort;
            set => Update(ref _itemSort, value);
        }

        public ItemSort ItemSortBase { get; set; }
        public ICommand OpenCommand { get; }
        public ICommand RunItemCommand { get; }

        public string SearchText
        {
            get => _searchText;
            set => Update(ref _searchText, value);
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
            await _itemRepository.UpdateItemFileName(item.Id, null);
            if (item.FileName != null)
            {
                var fn = new FileInfo(Path.Combine(_configuration.GetValue<string>("AppSettings:BaseDir"),
                                                   item.ChannelId,
                                                   item.FileName));
                if (fn.Exists)
                {
                    try
                    {
                        fn.Delete();
                        await item.Log($"{fn.FullName} deleted").ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        await item.Log($"Error {e.Message}").ConfigureAwait(false);
                    }
                }
            }
        }

        public async Task Download(string par, Item item)
        {
            bool skip = par == "subs";
            item.SaveDir = $"{Path.Combine(_configuration.GetValue<string>("AppSettings:BaseDir"), item.ChannelId)}";
            var success = await item.Download(_configuration.GetValue<string>("AppSettings:YouParser"),
                                              _configuration.GetValue<string>("AppSettings:YouParam"),
                                              par,
                                              skip);
            if (success && !skip)
            {
                await _itemRepository.UpdateItemFileName(item.Id, item.FileName);
            }
        }

        private Func<Item, bool> BuildPlFilter(string playlistId)
        {
            if (playlistId == null || _catalogModel.SelectedEntry.IsStateChannel)
            {
                return x => true;
            }

            return x => _catalogModel.PlaylistModel.SelectedEntry.Items.Contains(x.Id);
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

        private IObservable<SortExpressionComparer<Item>> GetSorter()
        {
            return this.WhenValueChanged(x => x.ItemSort).Select(x =>
            {
                switch (x)
                {
                    //case ItemSort.Timestamp:
                    //    return SortExpressionComparer<Item>.Descending(t => t.Timestamp);
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
                    case ItemSort.Title:
                        return SortExpressionComparer<Item>.Ascending(t => t.Title);

                    default:
                        return SortExpressionComparer<Item>.Descending(t => t.Timestamp);
                }
            });
        }

        private async Task OpenItem(object o)
        {
            var item = (Item)o;
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

        private async Task RunItem(bool setState)
        {
            SelectedEntry?.RunItem(_configuration.GetValue<string>("AppSettings:WatchApp"),
                                   _configuration.GetValue<string>("AppSettings:BaseDir"));
            if (setState)
            {
                await SetItemState(WatchState.Watched);
            }
        }

        private async Task SetItemState(WatchState par)
        {
            if (SelectedEntry == null || par == SelectedEntry.WatchState)
            {
                return;
            }

            var id = SelectedEntry.Id;
            var item = All.Items.Single(x => x.Id == id);
            var oldState = item.WatchState;

            item.WatchState = par;
            var res = await _itemRepository.SetItemsWatchState(par, item.Id);
            if (res != 1)
            {
                return;
            }

            All.AddOrUpdate(item);
            Item citem = _catalogModel.GetCachedExplorerModel(_catalogModel.SelectedEntry.IsStateChannel ? null : item.ChannelId)?.All
                .Items.FirstOrDefault(x => x.Id == id);
            if (citem != null)
            {
                citem.WatchState = par;
            }

            PlaylistModel plmodel = _catalogModel.GetCachedPlaylistModel(null);

            if (plmodel == null)
            {
                return;
            }

            switch (par)
            {
                case WatchState.Notset:

                    var pln = plmodel.All.Items.Single(x => x.State == oldState);
                    pln.Count -= 1;
                    pln.StateItems?.RemoveAll(x => x.Id == item.Id);

                    break;

                case WatchState.Watched:

                    var plw = plmodel.All.Items.Single(x => x.State == par);
                    plw.Count += 1;
                    plw.StateItems?.Add(item);

                    if (oldState == WatchState.Planned)
                    {
                        var pl = plmodel.All.Items.Single(x => x.State == oldState);
                        pl.Count -= 1;
                        pl.StateItems?.RemoveAll(x => x.Id == item.Id);
                    }

                    break;

                case WatchState.Planned:

                    var plp = plmodel.All.Items.Single(x => x.State == par);
                    plp.Count += 1;
                    plp.StateItems?.Add(item);

                    if (oldState == WatchState.Watched)
                    {
                        Playlist pl = plmodel.All.Items.Single(x => x.State == oldState);
                        pl.Count -= 1;
                        pl.StateItems?.RemoveAll(x => x.Id == item.Id);
                    }

                    break;
            }
        }

        #endregion
    }
}
