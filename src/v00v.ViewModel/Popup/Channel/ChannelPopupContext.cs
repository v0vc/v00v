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

namespace v00v.ViewModel.Popup.Channel
{
    public class ChannelPopupContext : PopupContext
    {
        #region Static and Readonly Fields

        private readonly IReadOnlyCollection<Model.Entities.Channel> _allChannels;
        private readonly IAppLogRepository _appLogRepository;
        private readonly IChannelRepository _channelRepository;
        private readonly ReadOnlyObservableCollection<TagModel> _entries;
        private readonly IPopupController _popupController;
        private readonly Action<Model.Entities.Channel> _setSelect;
        private readonly Action<string> _setTitle;
        private readonly ITagRepository _tagRepository;
        private readonly Action<Model.Entities.Channel> _updateList;
        private readonly IYoutubeService _youtubeService;

        #endregion

        #region Fields

        private string _closeText;
        private string _filterTag;
        private TagModel _selectedTag;

        #endregion

        #region Constructors

        public ChannelPopupContext(Model.Entities.Channel channel,
            IReadOnlyCollection<Model.Entities.Channel> allChannels,
            Action<string> setTitle,
            Action<Model.Entities.Channel> updateList,
            Action<Model.Entities.Channel> setSelect) : this(AvaloniaLocator.Current.GetService<IPopupController>(),
                                                             AvaloniaLocator.Current.GetService<ITagRepository>(),
                                                             AvaloniaLocator.Current.GetService<IChannelRepository>(),
                                                             AvaloniaLocator.Current.GetService<IAppLogRepository>(),
                                                             AvaloniaLocator.Current.GetService<IYoutubeService>())
        {
            _allChannels = allChannels;
            _setTitle = setTitle;
            _updateList = updateList;
            _setSelect = setSelect;
            All = new SourceList<TagModel>();
            All.AddRange(_tagRepository.GetTags(true).GetAwaiter().GetResult().Select(x => TagModel.FromDbTag(x, channel?.Tags)));
            All.Connect().Filter(this.WhenValueChanged(t => t.FilterTag).Select(BuildSearchFilter)).Bind(out _entries).DisposeMany()
                .Subscribe();
            CloseText = channel == null ? "Add" : "Save";
            Title = channel == null ? "Add" : $"Edit: {channel.Title}";
            ChannelId = channel?.Id;
            ChannelTitle = channel?.Title;
            IsChannelEnabled = channel == null;
            if (channel != null && channel.SubTitle == null)
            {
                channel.SubTitle = _channelRepository.GetChannelSubtitle(channel.Id).GetAwaiter().GetResult();
            }

            SubTitle = channel?.SubTitle;
            AddTagCommand = new Command(x => AddTag());
            SaveTagCommand = new Command(async x => await SaveTag());
            CloseChannelCommand = channel == null
                ? new Command(async x => await AddChannel())
                : new Command(async x => await EditChannel(channel));
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
        }

        #endregion

        #region Properties

        public ICommand AddTagCommand { get; set; }

        public SourceList<TagModel> All { get; }

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

        public string SubTitle { get; set; }

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

        private async Task AddChannel()
        {
            if (string.IsNullOrEmpty(ChannelId))
            {
                return;
            }

            IsWorking = true;
            CloseText = "Working...";
            IsChannelEnabled = false;

            if (SetExisted(ChannelId))
            {
                return;
            }

            var parsedId = await _youtubeService.GetChannelId(ChannelId);
            if (SetExisted(parsedId))
            {
                return;
            }

            var task = _youtubeService.GetChannelAsync(parsedId, ChannelTitle);
            await Task.WhenAll(task).ContinueWith(async done =>
            {
                if (task.Exception != null)
                {
                    _setTitle?.Invoke("Error: " + task.Exception.Message);
                    return;
                }

                if (task.Result == null)
                {
                    _setTitle?.Invoke("Banned channel: " + parsedId);
                    return;
                }

                task.Result.Tags.AddRange(All.Items.Where(y => y.IsEnabled).Select(TagModel.ToTag));
                var task1 = _channelRepository.AddChannel(task.Result);
                var task2 = _appLogRepository.SetStatus(AppStatus.ChannelAdd, $"Add channel:{task.Result.Id}:{task.Result.Title}");
                await Task.WhenAll(task1, task2);

                IsWorking = false;
                _popupController.Hide();
            });

            _setTitle?.Invoke("New channel: " + task.Result.Title);
            _updateList?.Invoke(task.Result);
            _setSelect?.Invoke(task.Result);
        }

        private void AddTag()
        {
            var tag = new TagModel { TagText = string.Empty, IsEditable = true, IsRemovable = true };
            tag.RemoveCommand = new Command(async x => await RemoveTag(tag));
            All.Add(tag);
            SelectedTag = tag;
        }

        private async Task EditChannel(Model.Entities.Channel channel)
        {
            IsWorking = true;
            CloseText = "Working...";
            IsChannelEnabled = false;

            channel.Title = ChannelTitle.Trim();
            channel.Tags.Clear();
            channel.Tags.AddRange(All.Items.Where(y => y.IsEnabled).Select(TagModel.ToTag));

            _updateList?.Invoke(channel);
            _popupController.Hide();

            var task1 = _channelRepository.SaveChannel(ChannelId, channel.Title, channel.Tags.Select(x => x.Id));
            var task2 = _appLogRepository.SetStatus(AppStatus.ChannelEdited, $"Edit channel:{channel.Id}:{channel.Title}");
            await Task.WhenAll(task1, task2);
        }

        private async Task RemoveTag(TagModel tag)
        {
            All.Remove(tag);
            if (tag.IsSaved && !string.IsNullOrEmpty(tag.TagText))
            {
                await _tagRepository.DeleteTag(tag.TagText);
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

        private bool SetExisted(string channelId)
        {
            var existchanel = _allChannels.FirstOrDefault(x => x.Id == channelId);
            if (existchanel == null)
            {
                return false;
            }

            _popupController.Hide();
            _setTitle?.Invoke("Already exist: " + existchanel.Title);
            _setSelect?.Invoke(existchanel);
            return true;
        }

        #endregion
    }
}
