using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using DynamicData;
using DynamicData.Binding;
using v00v.Model.Entities;
using v00v.Model.Enums;
using v00v.Services.Persistence;
using v00v.ViewModel.Catalog;
using v00v.ViewModel.Core;

namespace v00v.ViewModel.Explorer
{
    public class ExplorerModel : ViewModelBase, IDisposable
    {
        #region Static and Readonly Fields

        private readonly CatalogModel _catalogModel;
        private readonly IDisposable _cleanUp;
        private readonly ReadOnlyObservableCollection<Item> _entries;
        private readonly IItemRepository _itemRepository;

        #endregion

        #region Fields

        private string _searchText;

        private string _selectedPlaylistId;

        #endregion

        #region Constructors

        public ExplorerModel(Channel channel, CatalogModel catalogModel) : this(AvaloniaLocator.Current.GetService<IItemRepository>())
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

            IDisposable loader = All.Connect().Filter(this.WhenValueChanged(t => t.SearchText).Select(BuildFilter))
                .Filter(this.WhenValueChanged(t => t.SelectedPlaylistId).Select(BuildPlFilter))
                .Sort(GetSorter(), SortOptimisations.ComparesImmutableValuesOnly, 25).Bind(out _entries).DisposeMany().Subscribe();

            _cleanUp = new CompositeDisposable(All, loader, _catalogModel);
        }

        private ExplorerModel(IItemRepository itemRepository)
        {
            _itemRepository = itemRepository;
        }

        #endregion

        #region Properties

        public SourceCache<Item, string> All { get; }

        public IEnumerable<Item> Items => _entries;

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

        public void Dispose()
        {
            _cleanUp?.Dispose();
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

        #endregion
    }
}
