using System;
using System.Windows.Input;
using Avalonia;
using ReactiveUI;
using v00v.Model.Extensions;

namespace v00v.ViewModel.Popup
{
    public class PopupModel
    {
        #region Static and Readonly Fields

        private readonly IPopupController _popupController;

        #endregion

        #region Fields

        private bool _expanded;

        #endregion

        #region Constructors

        public PopupModel(PopupContext context) : this(AvaloniaLocator.Current.GetService<IPopupController>())
        {
            if (context == null)
            {
                IsVisible = false;
            }
            else
            {
                IsVisible = true;
                Contexts = new[] { context };
                Context = context;
                CloseCommand = ReactiveCommand.Create(_popupController.Hide, null, RxApp.MainThreadScheduler);
                ExpandCommand = ReactiveCommand.Create(() => ExpandPopup(context), null, RxApp.MainThreadScheduler);
                if (_popupController.ExpandUp == null)
                {
                    _popupController.ExpandUp = Convert.FromBase64String(_popupController.ExpandUpPopup).CreateThumb();
                }

                if (_popupController.ExpandDown == null)
                {
                    _popupController.ExpandDown = Convert.FromBase64String(_popupController.ExpandDownPopup).CreateThumb();
                }

                context.ExpandThumb = _popupController.ExpandUp;
            }
        }

        private PopupModel(IPopupController popupController)
        {
            _popupController = popupController;
        }

        #endregion

        #region Properties

        public ICommand CloseCommand { get; }
        public PopupContext Context { get; }
        public PopupContext[] Contexts { get; }
        public ICommand ExpandCommand { get; }
        public bool IsVisible { get; } = true;

        #endregion

        #region Methods

        private void ExpandPopup(PopupContext context)
        {
            if (context == null)
            {
                return;
            }

            context.CurrentWidth = _expanded ? _popupController.MinWidth : _popupController.MaxWidth;
            context.CurrentHeight = _expanded ? _popupController.MinHeight : _popupController.MaxHeight;
            context.ExpandThumb = _expanded ? _popupController.ExpandUp : _popupController.ExpandDown;
            _expanded = !_expanded;
        }

        #endregion
    }
}
