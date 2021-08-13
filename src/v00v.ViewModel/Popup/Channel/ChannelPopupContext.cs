using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using v00v.Model.Entities;
using v00v.Services.ContentProvider;
using v00v.Services.Persistence;

namespace v00v.ViewModel.Popup.Channel
{
    public class ChannelPopupContext : PopupContext
    {
        #region Static and Readonly Fields

        private readonly Action<Tag> _addNewTag;
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
            Action<Exception> handleException,
            Action<Model.Entities.Channel> updatePlList = null,
            Func<int> getMinOrder = null,
            Action<int> resortList = null) : this(AvaloniaLocator.Current.GetService<IPopupController>(),
                                                  AvaloniaLocator.Current.GetService<ITagRepository>(),
                                                  AvaloniaLocator.Current.GetService<IChannelRepository>(),
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

            ContextId = 0;
            All = new SourceList<Tag>();
            if (channel?.Tags.Count > 0)
            {
                var ids = channel.Tags.Select(x => x.Id);
                Parallel.ForEach(alltags,
                                 tag =>
                                 {
                                     tag.IsEnabled = ids.Contains(tag.Id);
                                     tag.IsRemovable = false;
                                 });
            }
            else
            {
                Parallel.ForEach(alltags.Where(x => x.IsEnabled || x.IsRemovable),
                                 tag =>
                                 {
                                     tag.IsEnabled = false;
                                     tag.IsRemovable = false;
                                 });
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
            AddTagCommand = ReactiveCommand.Create(AddTag, null, RxApp.MainThreadScheduler);
            SaveTagCommand = ReactiveCommand.Create(SaveTag, null, RxApp.MainThreadScheduler);
            CloseChannelCommand = channel == null
                ? ReactiveCommand.CreateFromTask(AddChannel, null, RxApp.MainThreadScheduler)
                : ReactiveCommand.CreateFromTask(() => EditChannel(channel), null, RxApp.MainThreadScheduler);
            CloseChannelCommand.ThrownExceptions.Subscribe(handleException);
        }

        private ChannelPopupContext(IPopupController popupController,
            ITagRepository tagRepository,
            IChannelRepository channelRepository,
            IYoutubeService youtubeService)
        {
            _popupController = popupController;
            _tagRepository = tagRepository;
            _channelRepository = channelRepository;
            _youtubeService = youtubeService;
        }

        #endregion

        #region Properties

        public ICommand AddTagCommand { get; }
        public SourceList<Tag> All { get; }
        public string ChannelId { get; set; }
        public string ChannelTitle { get; set; }
        public ReactiveCommand<Unit, Unit> CloseChannelCommand { get; }

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
        public ICommand SaveTagCommand { get; }

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
            return string.IsNullOrEmpty(searchText) ? _ => true : x => x.Text.Contains(searchText, StringComparison.OrdinalIgnoreCase);
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
                IsWorking = false;
                return;
            }

            var channelId = await _youtubeService.GetChannelId(ChannelId);
            if (string.IsNullOrEmpty(channelId) || SetExisted(channelId))
            {
                IsWorking = false;
                return;
            }

            try
            {
                var channel = await _youtubeService.GetChannelAsync(channelId, false, ChannelTitle);
                if (channel == null)
                {
                    _setTitle?.Invoke("Quota exceeded or banned channel");
                    return;
                }

                channel.Tags.AddRange(All.Items.Where(z => z.IsEnabled));
                channel.Order = _getMinOrder.Invoke() - 1;
                _updateList?.Invoke(channel);
                _setSelect?.Invoke(channel.Id);
                var rows = await _channelRepository.AddChannel(channel);
                _setTitle?.Invoke($"Added new channel: {channel.Title}. Saved {rows} rows");
            }
            catch (Exception ex)
            {
                _setTitle?.Invoke($"{ex.Message}");
            }
            finally
            {
                _popupController.Hide();
                IsWorking = false;
            }
        }

        private void AddTag()
        {
            var tag = new Tag
            {
                Text = string.Empty,
                IsEditable = true,
                IsRemovable = true,
                RemoveCommand = ReactiveCommand.CreateFromTask((Tag x) => RemoveTag(x), null, RxApp.MainThreadScheduler)
            };
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

            var rows = await _channelRepository.SaveChannel(channel.Id, channel.Title, channel.Tags.Select(x => x.Id));
            IsWorking = false;
            _setTitle?.Invoke($"Done: {channel.Title}. Saved {rows} rows");
            _updateList?.Invoke(channel);
            _updatePlList?.Invoke(channel);
            if (channel.IsNew)
            {
                await _youtubeService.AddPlaylists(channel);
                channel.IsNew = false;
                _resortList?.Invoke(rows);
            }

            _popupController.Hide();
            _setSelect?.Invoke(channel.Id);
        }

        private async Task RemoveTag(Tag tag)
        {
            All.Remove(tag);
            if (tag.IsSaved && !string.IsNullOrEmpty(tag.Text))
            {
                await _tagRepository.DeleteTag(tag.Text);
                _deleteNewTag?.Invoke(tag.Id);
            }
        }

        private void SaveTag()
        {
            Parallel.ForEach(All.Items.Where(x => !x.IsSaved),
                             async tag =>
                             {
                                 if (All.Items.Count(x => x.Text == tag.Text) == 1)
                                 {
                                     tag.IsSaved = true;
                                     tag.Id = await _tagRepository.Add(tag.Text);
                                     _addNewTag?.Invoke(tag);
                                 }
                             });
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
