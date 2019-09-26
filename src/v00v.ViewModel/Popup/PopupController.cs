using System.Reactive.Subjects;

namespace v00v.ViewModel.Popup
{
    public class PopupController : IPopupController
    {
        #region Properties

        public Subject<PopupContext> Trigger { get; } = new Subject<PopupContext>();

        #endregion

        #region Methods

        public void Dispose()
        {
            Trigger?.Dispose();
        }

        public void Hide()
        {
            Trigger.OnNext(null);
        }

        public void Show(PopupContext context)
        {
            Trigger.OnNext(context);
        }

        #endregion
    }
}
