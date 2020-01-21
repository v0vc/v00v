using System;
using System.Reactive.Subjects;
using Avalonia.Media.Imaging;

namespace v00v.ViewModel.Popup
{
    public interface IPopupController : IDisposable
    {
        #region Properties

        Bitmap ExpandDown { get; set; }
        string ExpandDownPopup { get; }
        Bitmap ExpandUp { get; set; }
        string ExpandUpPopup { get; }
        int MaxDescrHeight { get; }
        int MaxHeight { get; }
        int MaxImageHeight { get; }
        int MaxImageWidth { get; }
        int MaxWidth { get; }
        int MinDescrHeight { get; }
        int MinHeight { get; }
        int MinImageHeight { get; }
        int MinImageWidth { get; }
        int MinWidth { get; }
        Subject<PopupContext> Trigger { get; }

        #endregion

        #region Methods

        void Hide();

        void Show(PopupContext context);

        #endregion
    }
}
