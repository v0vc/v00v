using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using ReactiveUI;

namespace v00v.Views.Controls
{
    public class ExtendedTextBox : TextBox, IStyleable
    {
        #region Static and Readonly Fields

        private static readonly Geometry CopyIcon =
            Geometry.Parse("M19,21H8V7H19M19,5H8A2,2 0 0,0 6,7V21A2,2 0 0,0 8,23H19A2,2 0 0,0 21,21V7A2,2 0 0,0 19,5M16,1H4A2,2 0 0,0 2,3V17H4V3H16V1Z");

        private static readonly Geometry PasteIcon =
            Geometry.Parse(@"M19,20H5V4H7V7H17V4H19M12,2A1,1 0 0,1 13,3A1,1 0 0,1 12,4A1,1 0 0,1 11,3A1,1 0 0,1 12,2M19,2H14.82C14.4,0.84
				13.3,0 12,0C10.7,0 9.6,0.84 9.18,2H5A2,2 0 0,0 3,4V20A2,2 0 0,0 5,22H19A2,2 0 0,0 21,20V4A2,2 0 0,0 19,2Z");

        #endregion

        #region Fields

        private MenuItem _pasteItem;
        private TextPresenter _presenter;

        #endregion

        #region Constructors

        public ExtendedTextBox()
        {
            Disposables = new CompositeDisposable();
            CopyCommand = ReactiveCommand.CreateFromTask(CopyAsync);
            PasteCommand = ReactiveCommand.CreateFromTask(PasteAsync);
            this.GetObservable(IsReadOnlyProperty).Subscribe(isReadOnly =>
            {
                if (ContextMenu is null)
                {
                    return;
                }

                var items = ContextMenu.Items as Avalonia.Controls.Controls;

                if (isReadOnly)
                {
                    if (items == null || !items.Contains(_pasteItem))
                    {
                        return;
                    }

                    items.Remove(_pasteItem);
                    _pasteItem = null;
                }
                else
                {
                    if (items == null || items.Contains(_pasteItem))
                    {
                        return;
                    }

                    CreatePasteItem();
                    items.Add(_pasteItem);
                }
            });
        }

        #endregion

        #region Properties

        protected virtual bool IsCopyEnabled => true;
        private ReactiveCommand<Unit, Unit> CopyCommand { get; }
        private CompositeDisposable Disposables { get; }
        private ReactiveCommand<Unit, string> PasteCommand { get; }

        Type IStyleable.StyleKey => typeof(TextBox);

        #endregion

        #region Static Methods

        private static DrawingPresenter GetCopyPresenter()
        {
            return new DrawingPresenter
            {
                Drawing = new GeometryDrawing { Brush = Brush.Parse("#22B14C"), Geometry = CopyIcon }, Width = 16, Height = 16
            };
        }

        private static DrawingPresenter GetPastePresenter()
        {
            return new DrawingPresenter
            {
                Drawing = new GeometryDrawing { Brush = Brush.Parse("#22B14C"), Geometry = PasteIcon }, Width = 16, Height = 16
            };
        }

        #endregion

        #region Methods

        protected virtual async Task CopyAsync()
        {
            var selection = GetSelection();

            if (string.IsNullOrWhiteSpace(selection))
            {
                selection = Text;
            }

            if (!string.IsNullOrWhiteSpace(selection))
            {
                await Avalonia.Application.Current.Clipboard.SetTextAsync(selection);
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);

            Disposables?.Dispose();
        }

        protected override void OnLostFocus(RoutedEventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (ContextMenu != null && ContextMenu.IsOpen)
                {
                    _presenter?.HideCaret();
                }
                else
                {
                    base.OnLostFocus(e);
                }
            });
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            _presenter = e.NameScope.Get<TextPresenter>("PART_TextPresenter");

            ContextMenu = new ContextMenu { DataContext = this, Items = new Avalonia.Controls.Controls() };

            Observable.FromEventPattern(ContextMenu, nameof(ContextMenu.MenuClosed)).ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => Focus()).DisposeWith(Disposables);

            var menuItems = (ContextMenu.Items as Avalonia.Controls.Controls);
            if (IsCopyEnabled)
            {
                menuItems?.Add(new MenuItem
                {
                    Header = "Copy",
                    Command = CopyCommand,
                    Icon = GetCopyPresenter()
                });
            }

            if (!IsReadOnly)
            {
                CreatePasteItem();
                menuItems?.Add(_pasteItem);
            }
        }

        private void CreatePasteItem()
        {
            if (_pasteItem != null)
            {
                return;
            }

            _pasteItem = new MenuItem
            {
                Header = "Paste",
                Command = PasteCommand,
                Icon = GetPastePresenter()
            };
        }

        private string GetSelection()
        {
            var text = Text;

            if (string.IsNullOrEmpty(text))
            {
                return "";
            }

            var selectionStart = SelectionStart;
            var selectionEnd = SelectionEnd;

            var start = Math.Min(selectionStart, selectionEnd);
            var end = Math.Max(selectionStart, selectionEnd);

            if (start == end || (Text?.Length ?? 0) < end)
            {
                return string.Empty;
            }

            return text[start..end];
        }

        private async Task<string> PasteAsync()
        {
            var text = await Avalonia.Application.Current.Clipboard.GetTextAsync();

            if (text is null)
            {
                return null;
            }

            OnTextInput(new TextInputEventArgs { Text = text });
            return text;
        }

        #endregion

        //private static readonly byte[] CopyIcon =
        //    Convert.FromBase64String(
        //                             "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAACXBIWXMAAA7EAAAOxAGVKw4bAAAC0ElEQVRYhbXWT4hURxAG8N8Owx5EFv+s6MGDhxiChyDiIYh6EUQvwXiUEGElGzyoIEpOHkRFFvEgJCCKLBFEQvAgegghCkbBwIYkSIgoCYYgIqK7Ciqyjrseut/Mm9nXO2/G8YPmvVevuurrruqu6tPAx9iDVZiLShxTmMR9jOBn7wFDeI3pNuMN9vXScR9W4yaqeI4fMY6asPqpqLsKa+P7YRzoFYkfhNU9w4pZ9OZq3qWRXhH4Nxr8roTuM80hOfauzqvCyuBxCf3J+Lwh7MCvvSBQje+1Evo1fCOcln58gQ1YEO1UcnrjOI2f2hl9Imzn0RIEzkcny3Bb+1MzjeOzGcyznppNMWJH1L+Ej6LsH/xv5g4O4BPsFcK8M+VjIjI9kpPNSQz4XGN1B9sQ/iOne0ZjsU3IQpARqEpv5yBG4/u9lMEcLrTMn0GinYEiZEn7SPuwTbZ8DwmJWfdbNRNT+DJh8HmLXjsU6QwJ+fJVRqBI6WUJ42XQugMZhvEKe4p2oIJziYmLOiQwig8wT1hsv5BHA9iNsSICZVEmBL9gXYusHxexCfuLCNSwMGHwaUJ+CWtKEFoshOVWJLCkKAeqwnEpwvaEfEC4jssi81lNhWBLQt6fkI/iSgeO64tOEbiakOev2/zO/Yn/EnMe4G7iX6UoBDWhwnWCE1if+HdSqAPFDHIE5nXotCeoCk3FVqHIfIu/u7CzXaNYtSJ1cuoERvCpkMljuCw0E0Xn/OuEnS1YXoLoroRdw8q15YNC7ziN67n510rMndZI+iPxeyITnMJvQqu1UuPqbK2Wefb5f+N4OOvam1FvgvLH8Hfpi6aIRH7uZx04byLQTT+QVbh3OTXZ3FfdELgTnx9iYxfzlwhJD3/1dWFgMJJYINT0s0J7lt2SRVleEUI2H9uwNMo3d+EfoZK9UC7zU+NQt84zrMD3QlM7UXI8EY5vvdi9BdjX3qxtlHCCAAAAAElFTkSuQmCC");
    }
}
