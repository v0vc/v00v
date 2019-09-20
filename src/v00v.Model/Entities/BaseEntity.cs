using System.IO;
using Avalonia.Media.Imaging;

namespace v00v.Model.Entities
{
    public abstract class BaseEntity
    {
        #region Static Methods

        internal static Bitmap CreateThumb(byte[] thumbnail)
        {
            if (thumbnail == null || thumbnail.Length <= 0)
            {
                return null;
            }

            using (var ms = new MemoryStream(thumbnail))
            {
                return new Bitmap(ms);
            }
        }

        #endregion
    }
}
