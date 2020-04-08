using v00v.Model.Enums;

namespace v00v.Model.SyncEntities
{
    public struct SyncPrivacy
    {
        #region Properties

        public string ChannelId { get; set; }
        public string ChannelTitle { get; set; }
        public SyncState Status { get; set; }

        #endregion
    }
}
