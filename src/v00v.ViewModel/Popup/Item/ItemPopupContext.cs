using Avalonia.Media.Imaging;
using v00v.Model.Extensions;

namespace v00v.ViewModel.Popup.Item
{
    public class ItemPopupContext : PopupContext
    {
        #region Constructors

        public ItemPopupContext(Model.Entities.Item item)
        {
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
        }

        #endregion

        #region Properties

        public int DescrHeight { get; }
        public string Description { get; }
        public int ImageHeight { get; }
        public int ImageWidth { get; }
        public IBitmap Thumb { get; }

        #endregion
    }
}
