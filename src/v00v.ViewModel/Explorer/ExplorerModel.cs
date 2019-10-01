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
using v00v.ViewModel.Popup;
using v00v.ViewModel.Popup.Item;

namespace v00v.ViewModel.Explorer
{
    public class ExplorerModel : ViewModelBase
    {
        #region Static and Readonly Fields

        private readonly CatalogModel _catalogModel;
        private readonly IConfiguration _configuration;
        private readonly ReadOnlyObservableCollection<Item> _entries;
        private readonly IItemRepository _itemRepository;
        private readonly IPopupController _popupController;
        private readonly IYoutubeService _youtubeService;

        #endregion

        #region Fields

        private string _searchText;

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
                .Sort(GetSorter(), SortOptimisations.ComparesImmutableValuesOnly, 25).Bind(out _entries).DisposeMany().Subscribe();

            OpenCommand = new Command(async x => await OpenItem(x));

            DownloadCommand = new Command(async x => await Download("simple", (Item)x));

            DownloadItemCommand = new Command(async x => await Download((string)x, SelectedEntry));

            RunItemCommand = new Command(x => SelectedEntry?.RunItem(_configuration.GetValue<string>("AppSettings:WatchApp"),
                                                                     _configuration.GetValue<string>("AppSettings:BaseDir")));

            CopyItemCommand = new Command(async x => await CopyItem((string)x));

            IsParentState = channel.IsStateChannel;
            if (IsParentState)
            {
                GoToParentCommand =
                    new Command(x => _catalogModel.SelectedEntry = _catalogModel.Entries.First(y => y.Id == SelectedEntry.ChannelId));
            }

            DeleteItemCommand = new Command(async x => await DeleteItem());
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
        public IEnumerable<Item> Items => _entries;
        public ICommand OpenCommand { get; }
        public ICommand RunItemCommand { get; }
        public string SearchText
        {
            get => _searchText;
            set => Update(ref _searchText, value);
        }
        public Item SelectedEntry { get; set; }
        public string SelectedPlaylistId
        {
            get => _selectedPlaylistId;
            set => Update(ref _selectedPlaylistId, value);
        }
        public SortingEnum SortingEnum { get; set; } = SortingEnum.Timestamp;

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
            if (playlistId == null)
            {
                return x => true;
            }

            if (_catalogModel.SelectedEntry.IsStateChannel)
            {
                switch (playlistId)
                {
                    case "-2":
                        return x => x.SyncState == SyncState.Unlisted || x.SyncState == SyncState.Deleted;
                    case "-1":
                        return x => x.WatchState == WatchState.Planned;
                    case "0":
                        return x => x.WatchState == WatchState.Watched;
                    default:
                        return x => true;
                }
            }

            if (playlistId == _catalogModel.SelectedEntry.Id)
            {
                return x => x.ChannelId == _catalogModel.SelectedEntry.Id
                            && (x.SyncState == SyncState.Unlisted || x.SyncState == SyncState.Deleted);
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
            return this.WhenValueChanged(x => x.SortingEnum).Select(x =>
            {
                switch (x)
                {
                    case SortingEnum.Timestamp:
                        return SortExpressionComparer<Item>.Descending(t => t.Timestamp);
                    case SortingEnum.View:
                        return SortExpressionComparer<Item>.Descending(t => t.ViewCount);
                    case SortingEnum.Like:
                        return SortExpressionComparer<Item>.Descending(t => t.LikeCount);
                    case SortingEnum.Dislike:
                        return SortExpressionComparer<Item>.Descending(t => t.DislikeCount);
                    case SortingEnum.Comment:
                        return SortExpressionComparer<Item>.Descending(t => t.Comments);
                    case SortingEnum.Duration:
                        return SortExpressionComparer<Item>.Ascending(t => t.Duration);
                    case SortingEnum.Diff:
                        return SortExpressionComparer<Item>.Descending(t => t.ViewDiff);
                    case SortingEnum.Title:
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
                var th = await _youtubeService.GetStreamFromUrl($"http://img.youtube.com/vi/{item.Id}/0.jpg");

                //var th = new byte[0];
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

        #endregion
    }
}
