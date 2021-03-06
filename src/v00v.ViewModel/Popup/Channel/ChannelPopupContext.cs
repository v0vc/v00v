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
            Action<Exception> handleException,
            Action<Model.Entities.Channel> updatePlList = null,
            Func<int> getMinOrder = null,
            Action<int> resortList = null) : this(AvaloniaLocator.Current.GetService<IPopupController>(),
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
            if (string.IsNullOrEmpty(searchText))
            {
                return x => true;
            }

            return x => x.Text.Contains(searchText, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Methods

        private Task AddChannel()
        {
            if (string.IsNullOrEmpty(ChannelId))
            {
                return Task.CompletedTask;
            }

            IsWorking = true;
            CloseText = "Working...";
            IsChannelEnabled = false;

            if (SetExisted(ChannelId))
            {
                IsWorking = false;
                return Task.CompletedTask;
            }

            return _youtubeService.GetChannelId(ChannelId).ContinueWith(x =>
            {
                if (string.IsNullOrEmpty(x.Result) || SetExisted(x.Result))
                {
                    IsWorking = false;
                    return;
                }

                _youtubeService.GetChannelAsync(x.Result, false, ChannelTitle).ContinueWith(y =>
                {
                    IsWorking = false;
                    if (y.Exception != null)
                    {
                        _popupController.Hide();
                        _setTitle?.Invoke($"{y.Exception.Message}");
                        return;
                    }

                    if (y.Result == null)
                    {
                        _popupController.Hide();
                        _setTitle?.Invoke("Banned channel: " + x.Result);
                        return;
                    }

                    y.Result.Tags.AddRange(All.Items.Where(z => z.IsEnabled));
                    y.Result.Order = _getMinOrder.Invoke() - 1;
                    _updateList?.Invoke(y.Result);
                    _popupController.Hide();
                    _setSelect?.Invoke(y.Result.Id);
                    Task.WhenAll(_channelRepository.AddChannel(y.Result),
                                 _appLogRepository.SetStatus(AppStatus.ChannelAdd, $"Add channel:{y.Result.Id}:{y.Result.Title}"))
                        .ContinueWith(done =>
                        {
                            _setTitle?.Invoke($"New channel: {y.Result.Title}. Saved {done.Result[0]} rows");
                        });
                });
            }, TaskScheduler.FromCurrentSynchronizationContext());
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

        private Task EditChannel(Model.Entities.Channel channel)
        {
            IsWorking = true;
            CloseText = "Working...";
            IsChannelEnabled = false;

            channel.Title = ChannelTitle.Trim();
            channel.Tags.Clear();
            channel.Tags.AddRange(All.Items.Where(y => y.IsEnabled));

            if (channel.IsNew)
            {
                return _youtubeService.AddPlaylists(channel).ContinueWith(done =>
                {
                    channel.IsNew = false;
                    Task.WhenAll(_channelRepository.SaveChannel(channel.Id, channel.Title, channel.Tags.Select(x => x.Id)),
                                 _appLogRepository.SetStatus(AppStatus.ChannelEdited, $"Edit channel:{channel.Id}:{channel.Title}"))
                        .ContinueWith(x =>
                        {
                            if (x.Status != TaskStatus.Faulted)
                            {
                                _setTitle?.Invoke($"Done: {channel.Title}. Saved {x.Result[0]} rows");
                            }

                            _updateList?.Invoke(channel);
                            _updatePlList?.Invoke(channel);
                            _resortList?.Invoke(x.Result[0]);
                            _popupController.Hide();
                        }).ContinueWith(x => { _setSelect?.Invoke(channel.Id); }, TaskScheduler.FromCurrentSynchronizationContext());
                });
            }

            return Task.WhenAll(_channelRepository.SaveChannel(channel.Id, channel.Title, channel.Tags.Select(x => x.Id)),
                                _appLogRepository.SetStatus(AppStatus.ChannelEdited, $"Edit channel:{channel.Id}:{channel.Title}"))
                .ContinueWith(x =>
                {
                    if (x.Status != TaskStatus.Faulted)
                    {
                        _setTitle?.Invoke($"Done: {channel.Title}. Saved {x.Result[0]} rows");
                    }

                    _updateList?.Invoke(channel);
                    _updatePlList?.Invoke(channel);
                    _popupController.Hide();
                }).ContinueWith(x => { _setSelect?.Invoke(channel.Id); }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private Task RemoveTag(Tag tag)
        {
            All.Remove(tag);
            if (tag.IsSaved && !string.IsNullOrEmpty(tag.Text))
            {
                return _tagRepository.DeleteTag(tag.Text).ContinueWith(x =>
                                                                       {
                                                                           _deleteNewTag?.Invoke(tag.Id);
                                                                       },
                                                                       TaskScheduler.FromCurrentSynchronizationContext());
            }
            return Task.CompletedTask;
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
