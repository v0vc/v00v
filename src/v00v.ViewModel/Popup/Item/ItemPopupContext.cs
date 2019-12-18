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
        private readonly IYoutubeService _youtubeService;

        #endregion

        #region Fields

        private CommentSort _commentSort;
        private Comment _selectedComment;
        private byte _selectedTab;
        private bool _working;

        #endregion

        #region Constructors

        public ItemPopupContext(Model.Entities.Item item) : this(AvaloniaLocator.Current.GetService<IYoutubeService>(),
                                                                 AvaloniaLocator.Current.GetService<IItemRepository>())
        {
            _item = item;
            Description = item.Description.WordWrap(75);
            if (item.LargeThumb == null)
            {
                Thumb = item.Thumb;
                ImageWidth = 120;
                ImageHeight = 90;
                DescrHeight = 410;
            }
            else
            {
                Thumb = item.LargeThumb;
                ImageWidth = 480;
                ImageHeight = 360;
                DescrHeight = 140;
            }

            Title = item.ChannelTitle;

            All = new SourceCache<Comment, string>(x => x.CommentId);
            All.Connect().Sort(GetCommentSorter(), SortOptimisations.ComparesImmutableValuesOnly, 25).ObserveOn(RxApp.MainThreadScheduler)
                .Bind(out _comments).DisposeMany().Subscribe();
            this.WhenValueChanged(x => x.SelectedTab).Where(x => x == 1).InvokeCommand(LoadCommentsCommand);
        }

        private ItemPopupContext(IYoutubeService youtubeService, IItemRepository itemRepository)
        {
            _youtubeService = youtubeService;
            _itemRepository = itemRepository;
            LoadCommentsCommand = ReactiveCommand.CreateFromTask((byte tab) => LoadComments(tab), null, RxApp.MainThreadScheduler);
            LoadRepliesCommand = ReactiveCommand.CreateFromTask((Comment c) => LoadReplies(c), null, RxApp.MainThreadScheduler);
            SetSortCommand = ReactiveCommand.Create((string par) => SetSort(par), null, RxApp.MainThreadScheduler);
        }

        #endregion

        #region Properties

        public SourceCache<Comment, string> All { get; }
        public IEnumerable<Comment> Comments => _comments;

        public CommentSort CommentSort
        {
            get => _commentSort;
            set => Update(ref _commentSort, value);
        }

        public int DescrHeight { get; }
        public string Description { get; }
        public int ImageHeight { get; }
        public int ImageWidth { get; }
        public ICommand LoadCommentsCommand { get; }
        public ICommand LoadRepliesCommand { get; }

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

        public bool Working
        {
            get => _working;
            set => Update(ref _working, value);
        }

        #endregion

        #region Methods

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

            All.AddOrUpdate(comment.Replies);
            CommentSort = CommentSort.Order;
            comment.IsExpanded = true;
        }

        private async Task LoadComments(byte selectedTab)
        {
            if (selectedTab == 0 || _comments.Count > 0)
            {
                return;
            }

            Working = true;
            var oldTitle = Title;
            Title += ", loading comments..";
            All.AddOrUpdate(await _youtubeService.GetVideoCommentsAsync(_item.Id, _item.ChannelId));
            SetOrder();
            Title = oldTitle;

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

                    All.RemoveKeys(comment.Replies.Select(x => x.CommentId));
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
            All.Remove(_comments.Where(x => x.IsReply));
            foreach (var comment in _comments.Where(x => x.IsExpanded))
            {
                comment.IsExpanded = false;
            }

            CommentSort = (CommentSort)Enum.Parse(typeof(CommentSort), par);
            SetOrder();
        }

        #endregion
    }
}
