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
using v00v.Model.Entities;
using v00v.Model.Enums;
using v00v.Services.ContentProvider;
using v00v.Services.Persistence;

namespace v00v.ViewModel.Popup.Channel
{
    public class ChannelPopupContext : PopupContext
    {
        #region Static and Readonly Fields

        private readonly Action<Tag> _addNewTag;
        private readonly IAppLogRepository _appLogRepository;
        private readonly IChannelRepository _channelRepository;
        private readonly Action<int> _deleteNewTag;
        private readonly ReadOnlyObservableCollection<Tag> _entries;
        private readonly Func<IEnumerable<string>> _getExistId;
        private readonly Func<int> _getMinOrder;
        private readonly IPopupController _popupController;
        private readonly Action<int> _resortList;
        private readonly Action<string> _setSelect;
        private readonly Action<string> _setTitle;
        private readonly ITagRepository _tagRepository;
        private readonly Action<Model.Entities.Channel> _updateList;
        private readonly Action<Model.Entities.Channel> _updatePlList;
        private readonly IYoutubeService _youtubeService;

        #endregion

        #region Fields

        private string _closeText;
        private string _filterTag;
        private Tag _selectedTag;

        #endregion

        #region Constructors

        public ChannelPopupContext(Model.Entities.Channel channel,
            Func<IEnumerable<string>> getExistId,
            IReadOnlyCollection<Tag> alltags,
            Action<Tag> addNewTag,
            Action<int> deleteNewTag,
            Action<string> setTitle,
            Action<Model.Entities.Channel> updateList,
            Action<string> setSelect,
            Action<Model.Entities.Channel> updatePlList = null,
            Action<int> resortList = null,
            Func<int> getMinOrder = null) : this(AvaloniaLocator.Current.GetService<IPopupController>(),
                                                 AvaloniaLocator.Current.GetService<ITagRepository>(),
                                                 AvaloniaLocator.Current.GetService<IChannelRepository>(),
                                                 AvaloniaLocator.Current.GetService<IAppLogRepository>(),
                                                 AvaloniaLocator.Current.GetService<IYoutubeService>())
        {
            _getExistId = getExistId;
            _addNewTag = addNewTag;
            _deleteNewTag = deleteNewTag;
            _setTitle = setTitle;
            _updateList = updateList;
            _setSelect = setSelect;
            _updatePlList = updatePlList;
            _resortList = resortList;
            _getMinOrder = getMinOrder;

            All = new SourceList<Tag>();
            if (channel?.Tags.Count > 0)
            {
                foreach (Tag tag in alltags)
                {
                    tag.IsEnabled = channel.Tags.Select(x => x.Id).Contains(tag.Id);
                }
            }

            All.AddRange(alltags);
            All.Connect().Filter(this.WhenValueChanged(t => t.FilterTag).Select(BuildSearchFilter)).Bind(out _entries).DisposeMany()
                .Subscribe();

            CloseText = channel == null ? "Add" : "Save";
            Title = channel == null ? "Add" : $"Edit: {channel.Title}";
            ChannelId = channel?.Id;
            ChannelTitle = channel?.Title;
            IsChannelEnabled = channel == null;
            if (channel != null && channel.SubTitle == null)
            {
                channel.SubTitle = _channelRepository.GetChannelSubtitle(channel.Id);
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
        public SourceList<Tag> All { get; }
        public string ChannelId { get; set; }
        public string ChannelTitle { get; set; }
        public ICommand CloseChannelCommand { get; set; }

        public string CloseText
        {
            get => _closeText;
            set => Update(ref _closeText, value);
        }

        public IEnumerable<Tag> Entries => _entries;

        public string FilterTag
        {
            get => _filterTag;
            set => Update(ref _filterTag, value);
        }

        public bool IsChannelEnabled { get; set; }
        public ICommand SaveTagCommand { get; set; }

        public Tag SelectedTag
        {
            get => _selectedTag;
            set => Update(ref _selectedTag, value);
        }

        public string SubTitle { get; set; }

        #endregion

        #region Static Methods

        private static Func<Tag, bool> BuildSearchFilter(string searchText)
        {
            if (string.IsNullOrEmpty(searchText))
            {
                return x => true;
            }

            return x => x.Text.Contains(searchText, StringComparison.OrdinalIgnoreCase);
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

            var task = _youtubeService.GetChannelAsync(parsedId, false, ChannelTitle);
            await Task.WhenAll(task).ContinueWith(done =>
            {
                if (task.Status != TaskStatus.Faulted && task.Result == null)
                {
                    _setTitle?.Invoke("Banned channel: " + parsedId);
                }
            });

            if (task.Result == null)
            {
                return;
            }

            if (task.Status == TaskStatus.Faulted)
            {
                IsWorking = false;
                _popupController.Hide();
                _setTitle?.Invoke($"{task.Exception.Message}");
                return;
            }

            task.Result.Tags.AddRange(All.Items.Where(y => y.IsEnabled));
            task.Result.Order = _getMinOrder.Invoke() - 1;
            _updateList?.Invoke(task.Result);
            _setSelect?.Invoke(task.Result.Id);

            var task1 = _channelRepository.AddChannel(task.Result);
            var task2 = _appLogRepository.SetStatus(AppStatus.ChannelAdd, $"Add channel:{task.Result.Id}:{task.Result.Title}");
            await Task.WhenAll(task1, task2).ContinueWith(done =>
            {
                IsWorking = false;
                _popupController.Hide();
                if (task.Status != TaskStatus.Faulted && task.Result != null)
                {
                    _setTitle?.Invoke($"New channel: {task.Result.Title}. Saved {task1.Result} rows");
                }
            });
        }

        private void AddTag()
        {
            var tag = new Tag { Text = string.Empty, IsEditable = true, IsRemovable = true };
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
            channel.Tags.AddRange(All.Items.Where(y => y.IsEnabled));

            if (channel.IsNew)
            {
                var task = _youtubeService.AddPlaylists(channel);
                await Task.WhenAll(task).ContinueWith(done =>
                {
                    channel.IsNew = false;
                });
            }

            _popupController.Hide();

            var task1 = _channelRepository.SaveChannel(ChannelId, channel.Title, channel.Tags.Select(x => x.Id));
            var task2 = _appLogRepository.SetStatus(AppStatus.ChannelEdited, $"Edit channel:{channel.Id}:{channel.Title}");
            await Task.WhenAll(task1, task2);
            _updateList?.Invoke(channel);
            _updatePlList?.Invoke(channel);
            _resortList?.Invoke(task1.Result);
        }

        private async Task RemoveTag(Tag tag)
        {
            if (tag.IsSaved && !string.IsNullOrEmpty(tag.Text))
            {
                All.Remove(tag);
                await _tagRepository.DeleteTag(tag.Text);
                _deleteNewTag?.Invoke(tag.Id);
            }
        }

        private async Task SaveTag()
        {
            foreach (var tag in All.Items.Where(x => !x.IsSaved))
            {
                if (All.Items.Count(x => x.Text == tag.Text) == 1)
                {
                    tag.IsSaved = true;
                    tag.Id = await _tagRepository.Add(tag.Text);
                    _addNewTag?.Invoke(tag);
                }
            }
        }

        private bool SetExisted(string channelId)
        {
            if (!_getExistId.Invoke().Contains(channelId))
            {
                return false;
            }

            _popupController.Hide();
            _setTitle?.Invoke("Already exist: " + channelId);
            _setSelect?.Invoke(channelId);
            return true;
        }

        #endregion
    }
}
