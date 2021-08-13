using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Media.Imaging;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using v00v.Model.Entities;
using v00v.Model.Enums;
using v00v.Model.Extensions;
using v00v.Services.ContentProvider;
using v00v.Services.Persistence;

namespace v00v.ViewModel.Popup.Item
{
    public class ItemPopupContext : PopupContext
    {
        #region Static and Readonly Fields

        private readonly ReadOnlyObservableCollection<Comment> _comments;
        private readonly Model.Entities.Item _item;
        private readonly IItemRepository _itemRepository;
        private readonly IPopupController _popupController;
        private readonly IYoutubeService _youtubeService;

        #endregion

        #region Fields

        private CommentSort _commentSort;
        private string _searchText;
        private Comment _selectedComment;
        private byte _selectedTab;
        private string _watermark = " Search..";
        private bool _working;

        #endregion

        #region Constructors

        public ItemPopupContext(Model.Entities.Item item) : this(AvaloniaLocator.Current.GetService<IYoutubeService>(),
                                                                 AvaloniaLocator.Current.GetService<IItemRepository>(),
                                                                 AvaloniaLocator.Current.GetService<IPopupController>())
        {
            ContextId = 1;
            CurrentWidth = _popupController.MinWidth;
            CurrentHeight = _popupController.MinHeight;
            _item = item;
            Description = item.Description.WordWrap(75);
            if (item.LargeThumb == null)
            {
                Thumb = item.Thumb;
                ImageWidth = _popupController.MinImageWidth;
                ImageHeight = _popupController.MinImageHeight;
                DescrHeight = _popupController.MinDescrHeight;
            }
            else
            {
                Thumb = item.LargeThumb;
                ImageWidth = _popupController.MaxImageWidth;
                ImageHeight = _popupController.MaxImageHeight;
                DescrHeight = _popupController.MaxDescrHeight;
            }

            Title = item.ChannelTitle;
            All = new SourceList<Comment>();
            All.Connect().Filter(this.WhenValueChanged(t => t.SearchText).Select(BuildFilter)).Sort(GetCommentSorter())
                .ObserveOn(RxApp.MainThreadScheduler).Bind(out _comments).DisposeMany().Subscribe();
            this.WhenValueChanged(x => x.SelectedTab).InvokeCommand(LoadCommentsCommand);
        }

        private ItemPopupContext(IYoutubeService youtubeService, IItemRepository itemRepository, IPopupController popupController)
        {
            _youtubeService = youtubeService;
            _itemRepository = itemRepository;
            _popupController = popupController;
            LoadCommentsCommand = ReactiveCommand.CreateFromTask((byte tab) => LoadComments(tab), null, RxApp.MainThreadScheduler);
            LoadRepliesCommand = ReactiveCommand.CreateFromTask((Comment c) => LoadReplies(c), null, RxApp.MainThreadScheduler);
            SetSortCommand = ReactiveCommand.Create((string par) => SetSort(par), null, RxApp.MainThreadScheduler);
            CopyItemCommand = ReactiveCommand.CreateFromTask((string par) => CopyItem(par), null, RxApp.MainThreadScheduler);
        }

        #endregion

        #region Properties

        public SourceList<Comment> All { get; }
        public IEnumerable<Comment> Comments => _comments;

        public CommentSort CommentSort
        {
            get => _commentSort;
            set => Update(ref _commentSort, value);
        }

        public ICommand CopyItemCommand { get; }

        public int DescrHeight { get; }
        public string Description { get; }
        public int ImageHeight { get; }
        public int ImageWidth { get; }
        public ICommand LoadCommentsCommand { get; }
        public ICommand LoadRepliesCommand { get; }

        public string SearchText
        {
            get => _searchText;
            set => Update(ref _searchText, value);
        }

        public Comment SelectedComment
        {
            get => _selectedComment;
            set => Update(ref _selectedComment, value);
        }

        public byte SelectedTab
        {
            get => _selectedTab;
            set => Update(ref _selectedTab, value);
        }

        public ICommand SetSortCommand { get; }

        public IBitmap Thumb { get; }

        public string Watermark
        {
            get => _watermark;
            set => Update(ref _watermark, value);
        }

        public bool Working
        {
            get => _working;
            set => Update(ref _working, value);
        }

        #endregion

        #region Static Methods

        private static Func<Comment, bool> BuildFilter(string searchText)
        {
            return string.IsNullOrWhiteSpace(searchText)
                ? _ => true
                : x => x.Text.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                       || x.Author.Contains(searchText, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Methods

        private Task CopyItem(string par)
        {
            if (SelectedComment == null)
            {
                return Task.CompletedTask;
            }

            string res = null;
            switch (par)
            {
                case "link":
                    res = $"{_youtubeService.ChannelLink}{SelectedComment.AuthorChannelId}";
                    break;
                case "text":
                    res = SelectedComment.Text;
                    break;
                case "url":
                    res = SelectedComment.TextUrl;
                    break;
            }

            return !string.IsNullOrEmpty(res) ? Application.Current.Clipboard.SetTextAsync(res) : Task.CompletedTask;
        }

        private IObservable<SortExpressionComparer<Comment>> GetCommentSorter()
        {
            return this.WhenValueChanged(x => x.CommentSort).Select(x =>
            {
                switch (x)
                {
                    case CommentSort.ReplyCount:
                        return SortExpressionComparer<Comment>.Descending(t => t.CommentReplyCount);
                    case CommentSort.LikeCount:
                        return SortExpressionComparer<Comment>.Descending(t => t.LikeCount);
                    case CommentSort.TimeStamp:
                        return SortExpressionComparer<Comment>.Descending(t => t.Timestamp);
                    case CommentSort.Author:
                        return SortExpressionComparer<Comment>.Ascending(t => t.Author);
                    case CommentSort.Order:
                        return SortExpressionComparer<Comment>.Ascending(t => t.Order);
                    default:
                        return SortExpressionComparer<Comment>.Descending(t => t.Timestamp);
                }
            });
        }

        private void InsertReplies(Comment comment)
        {
            var i = comment.Order + 1;
            foreach (var comm in comment.Replies)
            {
                comm.Order = i;
                i++;
            }

            foreach (var parcomm in _comments.Skip(comment.Order + 1))
            {
                parcomm.Order += comment.Replies.Count;
            }

            All.AddRange(comment.Replies);
            if (CommentSort != CommentSort.Order)
            {
                CommentSort = CommentSort.Order;
            }

            comment.IsExpanded = true;
        }

        private async Task LoadComments(byte selectedTab)
        {
            if (selectedTab == 0)
            {
                CanExpanded = false;
                return;
            }

            if (_comments.Count > 0)
            {
                CanExpanded = true;
                return;
            }

            CanExpanded = true;
            Working = true;
            var oldTitle = Title;
            Title += ", loading comments..";

            All.AddRange(await _youtubeService.GetVideoCommentsAsync(_item.Id, _item.ChannelId));
            Title = oldTitle;
            if (!_comments.Any())
            {
                Watermark = " No comments...";
                return;
            }

            SetOrder();

            if (_item.Comments != _comments.Count)
            {
                _item.Comments = _comments.Count;
                await _itemRepository.SetItemCommentsCount(_item.Id, _comments.Count);
            }

            Working = false;
        }

        private async Task LoadReplies(Comment comment)
        {
            if (comment.IsReply)
            {
                return;
            }

            if (comment.Replies == null)
            {
                comment.Replies = await _youtubeService.GetReplyCommentsAsync(comment.CommentId, _item.ChannelId);
                InsertReplies(comment);
            }

            else
            {
                if (comment.IsExpanded)
                {
                    foreach (var comm in _comments.Skip(comment.Order + comment.Replies.Count + 1))
                    {
                        comm.Order -= comment.Replies.Count;
                    }

                    All.RemoveMany(comment.Replies);
                    if (CommentSort != CommentSort.Order)
                    {
                        CommentSort = CommentSort.Order;
                    }

                    comment.IsExpanded = false;
                }
                else
                {
                    InsertReplies(comment);
                }
            }
        }

        private void SetOrder()
        {
            var i = 0;
            foreach (var comment in _comments)
            {
                if (comment.CommentReplyCount > 0 && comment.ExpandThumb == null)
                {
                    comment.ExpandThumb = comment.ExpandDown;
                }

                comment.Order = i;
                i++;
            }
        }

        private void SetSort(string par)
        {
            All.RemoveMany(_comments.Where(x => x.IsReply));
            SearchText = null;
            Parallel.ForEach(_comments.Where(x => x.IsExpanded),
                             comment =>
                             {
                                 comment.IsExpanded = false;
                             });
            CommentSort = (CommentSort)Enum.Parse(typeof(CommentSort), par);
            SetOrder();
        }

        #endregion
    }
}
