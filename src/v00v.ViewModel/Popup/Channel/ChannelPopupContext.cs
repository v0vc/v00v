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
using v00v.Model.Core;
using v00v.Model.Enums;
using v00v.Services.ContentProvider;
using v00v.Services.Persistence;
using v00v.ViewModel.Catalog;

namespace v00v.ViewModel.Popup.Channel
{
    public class ChannelPopupContext : PopupContext
    {
        #region Static and Readonly Fields

        private readonly IAppLogRepository _appLogRepository;
        private readonly IChannelRepository _channelRepository;
        private readonly ReadOnlyObservableCollection<TagModel> _entries;
        private readonly IPopupController _popupController;
        private readonly ITagRepository _tagRepository;
        private readonly IYoutubeService _youtubeService;

        #endregion

        #region Fields

        private string _filterTag;
        private TagModel _selectedTag;
        private string _closeText;

        #endregion

        #region Constructors

        public ChannelPopupContext(Model.Entities.Channel channel, CatalogModel catalogModel) :
            this(AvaloniaLocator.Current.GetService<IPopupController>(),
                 AvaloniaLocator.Current.GetService<ITagRepository>(),
                 AvaloniaLocator.Current.GetService<IChannelRepository>(),
                 AvaloniaLocator.Current.GetService<IAppLogRepository>(),
                 AvaloniaLocator.Current.GetService<IYoutubeService>())
        {
            CatalogModel = catalogModel;
            All = new SourceList<TagModel>();
            All.AddRange(_tagRepository.GetTags(true).GetAwaiter().GetResult().Select(x => TagModel.FromDbTag(x, channel?.Tags)));
            All.Connect().Filter(this.WhenValueChanged(t => t.FilterTag).Select(BuildSearchFilter)).Bind(out _entries).DisposeMany()
                .Subscribe();
            CloseText = channel == null ? "Add" : "Save";
            Title = channel == null ? "Add" : "Edit";
            ChannelId = channel?.Id;
            ChannelTitle = channel?.Title;
            IsChannelEnabled = channel == null;
        }

        private ChannelPopupContext(IPopupController popupController,
            ITagRepository tagRepository,
            IChannelRepository channelRepository,
            IAppLogRepository appLogRepository,
            IYoutubeService youtubeService)
        {
            _popupController = popupController;
            _tagRepository = tagRepository;
            _channelRepository = channelRepository;
            _appLogRepository = appLogRepository;
            _youtubeService = youtubeService;
            AddTagCommand = new Command(x => AddTag());
            SaveTagCommand = new Command(async x => await SaveTag());
            CloseChannelCommand = new Command(async x => await SaveChannel());
        }

        #endregion

        #region Properties

        public ICommand AddTagCommand { get; set; }
        public SourceList<TagModel> All { get; }
        public CatalogModel CatalogModel { get; }
        public string ChannelId { get; set; }
        public string ChannelTitle { get; set; }
        public ICommand CloseChannelCommand { get; set; }

        public string CloseText
        {
            get => _closeText;
            set => Update(ref _closeText, value);
        }

        public IEnumerable<TagModel> Entries => _entries;

        public string FilterTag
        {
            get => _filterTag;
            set => Update(ref _filterTag, value);
        }

        public bool IsChannelEnabled { get; set; }
        public ICommand SaveTagCommand { get; set; }

        public TagModel SelectedTag
        {
            get => _selectedTag;
            set => Update(ref _selectedTag, value);
        }

        #endregion

        #region Static Methods

        private static Func<TagModel, bool> BuildSearchFilter(string searchText)
        {
            if (string.IsNullOrEmpty(searchText))
            {
                return x => true;
            }

            return x => x.TagText.Contains(searchText, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Methods

        private void AddTag()
        {
            var tag = new TagModel { TagText = string.Empty, IsEditable = true, IsRemovable = true };
            tag.RemoveCommand = new Command(x => RemoveTag(tag));
            All.Add(tag);
            SelectedTag = tag;
        }

        private void RemoveTag(TagModel tag)
        {
            All.Remove(tag);
            if (tag.IsSaved && !string.IsNullOrEmpty(tag.TagText))
            {
                _tagRepository.DeleteTag(tag.TagText);
            }
        }

        private async Task SaveChannel()
        {
            IsWorking = true;
            CloseText = "Working...";
            IsChannelEnabled = false;

            var parsedId = await _youtubeService.GetChannelId(ChannelId);
            var existchanel = CatalogModel.All.Items.FirstOrDefault(x => x.Id == parsedId);
            if (existchanel != null)
            {
                _popupController.Hide();
                CatalogModel.SelectedEntry = existchanel;
                return;
            }

            var channel = await _youtubeService.GetChannelAsync(parsedId, ChannelTitle);

            channel.Tags.AddRange(All.Items.Where(y => y.IsEnabled).Select(TagModel.ToTag));
            CatalogModel.All.AddOrUpdate(channel);
            CatalogModel.SelectedEntry = channel;
            _popupController.Hide();

            var res = await _channelRepository.AddChannel(channel);
            if (res > 0)
            {
                await _appLogRepository.SetStatus(AppStatus.ChannelAdd, $"Add channel:{channel.Id}:{channel.Title}");
            }
        }

        private async Task SaveTag()
        {
            foreach (TagModel tag in All.Items.Where(x => !x.IsSaved))
            {
                if (All.Items.Count(x => x.TagText == tag.TagText) == 1)
                {
                    tag.IsSaved = true;
                    tag.Id = await _tagRepository.Add(tag.TagText);
                }
            }
        }

        #endregion
    }
}
