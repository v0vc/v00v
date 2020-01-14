using System;
using System.Collections.Generic;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using v00v.Model.Extensions;

namespace v00v.Model.Entities
{
    public class Comment : ViewModelBase
    {
        #region Static and Readonly Fields

        private readonly string _channelId;

        #endregion

        #region Fields

        private Bitmap _expandThumb;
        private bool _isExpanded;

        #endregion

        #region Constructors

        static Comment()
        {
            ReplyThumbnail =
                Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAACXBIWXMAAA7EAAAOxAGVKw4bAAABkUlEQVRYhe3WP0scQRzG8U+WQ1JKkJDS0iKvIaUECRIkhUU44QrzmlIG8i4sBCWIvV0qkXCR4xCrEMEkxc6R383t3a66tzY+8IPlmdn5PvNnh33mvwp8xB5eY0W7usEZvuAr/sTGFzjG347qODFRzrxLeAxRQP8R4JPq95R7HnWET/ipXb3CZ7wJ3h5cZqk2WgZHbWSsy8LsaW975ovGXimWCGukpwBNAmzhAkPsBH8bP1JtB38n9b1I79bqyvTJXM3az0PbOPjD4A+DPw7+eTbWasa6arICvfBczPGb9JmruhV4j5FyZrvB/5C8cXqeaDd5o/Ru1MwKNAnQpu61BUvVU4BC+acStbZEXj72DRyaPhgHWMfzhrVoFYvQbz2NHVmHMMjMu1b+GU7UN/uF5TWgvCxOHhhilMEHuK1550S4qF7i9IEh7gI/Tcwp9bCPb7jGr5qqClAFv039r9PY+xpe0XWq2s8c/hvv2oA1CdApvCpAp/BFATqBzwvQGbwqQKdwyn/BR4PDJr6nenvfQf4B1uo+0MmlnkoAAAAASUVORK5CYII=");
            LikeThumbnail =
                Convert.FromBase64String(
                                         "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAACXBIWXMAAA7EAAAOxAGVKw4bAAAB7ElEQVRYheXWT4hNURwH8M973aZJk6bpZWnxkoW1mCRFkuxsLCgbsRALUcRCForVY2OjrESzscFOSbKyoWHKnzRloqZkFKG8GYt7Xvd053lzL+++t/CtX93zveec7/f3u+eecxgyan2e7zo2duG3Y6HPWsuwAW0sdYmJPw2q99HAiTDfOxzHpT7OvSIa+CbN9ljgJmUVGK/awNkg9AVjgTsVuJdViyd4H8SuRvzDwF2u2sC+INTGusCN4Wfgd1Zt4GkQuhtxa2Tff1OV4rsjoW2DNpDgeRB5lHvXGISBM7JvvyX3biIyMI0nUbRkf4oa9mCkgOBbvAjPu3BfWoUbOJTrO47PPeaaxWE8qIWORTaKKziJI9IsRjGDzfia6zuCc8FgIt0hE2nm+7EKi7gmGOi2f+djGq+i9husLWA8j6Z0zSzhYxkDcUzpccAUwMGOgaTEoDncwm08+wfxGPUyBu7hdJ+EMwcl+v7qtzjqZQwsVmCgVAWqMFCqApXgvzVQX/YwJAx/DZTZiCZxIWq3cVN6DS+DUeyNib85CzrxXXrpXF1QvIHHsnvE0TLH8Tw+RO1mJDwfjMz1GJ/gPNbjBw7gDsUr0OqSTUt2+y0an7C1M0kNOxRbC7N43YVv4qJ0jayEBWnmMwX6Dga/ATdJp+DAyWFOAAAAAElFTkSuQmCC");
        }

        public Comment(string channelId)
        {
            _channelId = channelId;
        }

        #endregion

        #region Static Properties

        private static byte[] LikeThumbnail { get; }
        private static byte[] ReplyThumbnail { get; }

        #endregion

        #region Properties

        public string Author { get; set; }
        public string AuthorChannelId { get; set; }

        public SolidColorBrush BackgroundAuthor =>
            AuthorChannelId.Equals(_channelId, StringComparison.InvariantCultureIgnoreCase)
                ? new SolidColorBrush(Colors.PaleVioletRed)
                : new SolidColorBrush(Colors.PaleTurquoise);

        public SolidColorBrush BackgroundReply =>
            IsReply ? new SolidColorBrush(Colors.AliceBlue) : new SolidColorBrush(Colors.Transparent);

        public string CommentId { get; set; }
        public long CommentReplyCount { get; set; }
        public bool CopyTextUrlEnabled => !string.IsNullOrEmpty(TextUrl);
        public Bitmap ExpandDown { get; set; }

        public Bitmap ExpandThumb
        {
            get => _expandThumb;
            set => Update(ref _expandThumb, value);
        }

        public Bitmap ExpandUp { get; set; }
        public bool HasReply => CommentReplyCount > 0;

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                _isExpanded = value;
                ExpandThumb = _isExpanded ? ExpandUp : ExpandDown;
            }
        }

        public bool IsReply { get; set; }
        public long LikeCount { get; set; }
        public Bitmap LikeThumb => LikeThumbnail.CreateThumb();
        public int Order { get; set; }
        public HashSet<Comment> Replies { get; set; }
        public Bitmap ReplyThumb => ReplyThumbnail.CreateThumb();
        public string Text { get; set; }
        public string TextUrl { get; set; }
        public DateTime Timestamp { get; set; }
        public string TimestampAgo => Timestamp.TimeAgo();

        #endregion
    }
}
