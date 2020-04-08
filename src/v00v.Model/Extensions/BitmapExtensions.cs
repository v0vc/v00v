using System.IO;
using Avalonia.Media.Imaging;

namespace v00v.Model.Extensions
{
    public static class BitmapExtensions
    {
        #region Static Methods

        public static Bitmap CreateThumb(this byte[] thumbnail)
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
