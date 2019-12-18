using System;

namespace v00v.Model.Entities
{
    public class Comment
    {
        #region Properties

        public string Author { get; set; }
        public long CommentReplyCount { get; set; }
        public long LikeCount { get; set; }
        public string Text { get; set; }
        public DateTime Timestamp { get; set; }

        #endregion
    }
}
