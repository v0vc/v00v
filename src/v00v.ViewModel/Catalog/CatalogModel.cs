using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using Avalonia;
using DynamicData;
using DynamicData.Binding;
using LazyCache;
using v00v.Model.Core;
using v00v.Model.Entities;
using v00v.Model.Entities.Instance;
using v00v.Model.Enums;
using v00v.Services.Persistence;
using v00v.ViewModel.Explorer;
using v00v.ViewModel.Playlists;
using v00v.ViewModel.Popup;
using v00v.ViewModel.Popup.Channel;

namespace v00v.ViewModel.Catalog
{
    public class CatalogModel : ViewModelBase, IDisposable
    {
        #region Static and Readonly Fields

        private readonly IChannelRepository _channelRepository;
        private readonly IDisposable _cleanUp;
        private readonly ReadOnlyObservableCollection<Channel> _entries;
        private readonly ITagRepository _tagRepository;
        private readonly IPopupController _popupController;
        #endregion

        #region Fields

        private ExplorerModel _explorerModel;
        private PlaylistModel _playlistModel;
        private string _searchText;
        private Channel _selectedEntry;
        private Tag _selectedTag;

        #endregion

        #region Constructors

        public CatalogModel() : this(AvaloniaLocator.Current.GetService<IChannelRepository>(),
                                     AvaloniaLocator.Current.GetService<ITagRepository>(),
                                     AvaloniaLocator.Current.GetService<IPopupController>())
        {
            All = new SourceCache<Channel, string>(m => m.Id);

            BaseChannel = StateChannel.Instance;
            BaseChannel.Count = _channelRepository.GetItemsCount(SyncState.Added).GetAwaiter().GetResult();

            var channels = _channelRepository?.GetChannels().GetAwaiter().GetResult();
            channels.Insert(0, BaseChannel);

            All.AddOrUpdate(channels);

            IDisposable loader = All.Connect().Filter(this.WhenValueChanged(t => t.SearchText).Select(BuildSearchFilter))
                .Filter(this.WhenValueChanged(t => t.SelectedTag).Select(BuildTagFilter))
                .Sort(GetChannelSorter(), SortOptimisations.ComparesImmutableValuesOnly, 25).Bind(out _entries).DisposeMany().Subscribe();

            var selector = this.WhenValueChanged(x => x.SelectedEntry).Subscribe(entry =>
            {
                if (entry == null)
                {
                    return;
                }

                ExplorerModel = ViewModelCache.GetOrAdd(entry.ExCache, () => new ExplorerModel(entry, this));
                PlaylistModel = ViewModelCache.GetOrAdd(entry.PlCache, () => new PlaylistModel(entry, this, ExplorerModel));
                if (PlaylistModel.SelectedEntry != null)
                {
                    PlaylistModel.SelectedEntry = null;
                }

                if (!string.IsNullOrEmpty(PlaylistModel.SearchText))
                {
                    PlaylistModel.SearchText = null;
                }

                if (!string.IsNullOrEmpty(ExplorerModel.SearchText))
                {
                    ExplorerModel.SearchText = null;
                }

                if (ExplorerModel.SortingEnum != SortingEnum.Timestamp)
                {
                    ExplorerModel.SortingEnum = SortingEnum.Timestamp;
                }
            });

            _cleanUp = new CompositeDisposable(All, loader, selector);
            SelectedEntry = BaseChannel;
            Tags.AddRange(_tagRepository.GetTags(false).GetAwaiter().GetResult());
        }

        private CatalogModel(IChannelRepository channelRepository, ITagRepository tagRepository, IPopupController popupController)
        {
            _channelRepository = channelRepository;
            _tagRepository = tagRepository;
            _popupController = popupController;
            AddChannelCommand = new Command(x => _popupController.Show(new ChannelPopupContext(null, this)));
        }

        #endregion

        #region Properties

        public bool AddDisable { get; set; }
        public SourceCache<Channel, string> All { get; }
        public Channel BaseChannel { get; set; }
        public ChannelSort ChannelSort { get; set; } = ChannelSort.Title;
        public IReadOnlyCollection<Channel> Entries => _entries;
        public ExplorerModel ExplorerModel
        {
            get => _explorerModel;
            set => Update(ref _explorerModel, value);
        }
        public PlaylistModel PlaylistModel
        {
            get => _playlistModel;
            set => Update(ref _playlistModel, value);
        }

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

        public List<Tag> Tags { get; } = new List<Tag> { new Tag { Id = -2, Text = "[no tag]" }, new Tag { Id = -1, Text = " " } };

        public IAppCache ViewModelCache { get; } = new CachingService();

        public ICommand AddChannelCommand { get; }
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

        public void Dispose()
        {
            _cleanUp?.Dispose();
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

        #endregion
    }
}
