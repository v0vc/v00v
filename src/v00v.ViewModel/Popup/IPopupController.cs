using System;
using System.Reactive.Subjects;

namespace v00v.ViewModel.Popup
{
    public interface IPopupController : IDisposable
    {
        #region Properties

        Subject<PopupContext> Trigger { get; }

        #endregion

        #region Methods

        void Hide();

        void Show(PopupContext context);

        #endregion
    }
}
