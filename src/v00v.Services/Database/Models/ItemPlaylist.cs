using System.Collections.Generic;

namespace v00v.Services.Database.Models
{
    public class ItemPlaylist
    {
        #region Static Properties

        public static IEqualityComparer<ItemPlaylist> ItemIdPlaylistIdComparer { get; } = new ItemIdPlaylistIdEqualityComparer();

        #endregion

        #region Properties

        public string ItemId { get; set; }
        public string PlaylistId { get; set; }

        #endregion

        #region Nested type: ItemIdPlaylistIdEqualityComparer

        private sealed class ItemIdPlaylistIdEqualityComparer : IEqualityComparer<ItemPlaylist>
        {
            #region Methods

            public bool Equals(ItemPlaylist x, ItemPlaylist y)
            {
                if (ReferenceEquals(x, y))
                    return true;
                if (x is null)
                    return false;
                if (y is null)
                    return false;
                if (x.GetType() != y.GetType())
                    return false;
                return string.Equals(x.ItemId, y.ItemId) && string.Equals(x.PlaylistId, y.PlaylistId);
            }

            public int GetHashCode(ItemPlaylist obj)
            {
                unchecked
                {
                    return ((obj.ItemId != null ? obj.ItemId.GetHashCode() : 0) * 397)
                           ^ (obj.PlaylistId != null ? obj.PlaylistId.GetHashCode() : 0);
                }
            }

            #endregion
        }

        #endregion
    }
}
