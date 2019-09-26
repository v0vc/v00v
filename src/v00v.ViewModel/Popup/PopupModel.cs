using v00v.ViewModel.Core;

namespace v00v.ViewModel.Popup
{
    public class PopupModel
    {
        #region Constructors

        private PopupModel(PopupContext context)
        {
            Contexts = new[] { context };
            Context = context;
        }

        private PopupModel()
        {
        }

        #endregion

        #region Properties

        public PopupContext Context { get; }
        public PopupContext[] Contexts { get; }
        public bool IsVisible { get; set; } = true;

        #endregion

        #region Static Methods

        public static PopupModel Hidden()
        {
            return new PopupModel { IsVisible = false };
        }

        public static PopupModel NoHidden(PopupContext context, IPopupController popupController)
        {
            return new PopupModel(context) { IsVisible = true, Context = { CloseCommand = new Command(x => popupController.Hide()) } };
        }

        #endregion
    }
}
