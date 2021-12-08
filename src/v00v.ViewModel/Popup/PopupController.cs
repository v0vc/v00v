using System.Reactive.Subjects;
using Avalonia.Media.Imaging;

namespace v00v.ViewModel.Popup
{
    public class PopupController : IPopupController
    {
        #region Properties

        public Bitmap ExpandDown { get; set; }
        public string ExpandDownPopup => "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAACXBIWXMAAA7EAAAOxAGVKw4bAAAA5klEQVRYhcWXywrCMBBFr+Laz+w67kM/2q2UupBADZl0Hnf0QjZlmnNoHk2A7ywAKvQphtrTLAA2ADuAVQnfM+CtzSTKoS4FPpMoXU04VYCPJHo4bQhWhcQIThPQSEiNGo8EPVaJlFgkQrkKz5/RjiORZvtPhsAKpwp44DQBL5wiEIGHBS4AbsE+XoF3H0E2AP+Xox1m/gr3CFDhVgE63CIwhEv/gozcszq2DIHmtJ0qkCLhWYZUCWnCaQ66KQLH2X4mYbkGqgRGS02S2PC5FNEEZptML0GDNwHNDtckqHAo4S21h78Byeo6cqKc5TEAAAAASUVORK5CYII=";
        public Bitmap ExpandUp { get; set; }
        public string ExpandUpPopup => "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAACXBIWXMAAA7EAAAOxAGVKw4bAAACHElEQVRYhe3XMWgUURAG4M8zWAQJIiFIkBgVgoighVgEC0uxEBGxsLAQSWVne51YWAcJFlYWkkLExpDqCGJAJKBoIyKiIkkKWQUDRk1i8XbJZS97792ZMj+8Yt/9M//c7Lx5szswjsvoEbCKv/iF7/iMtzlvQRr6cBNHMYRB9Db5X8VvzMJXrCWssURxuJDoM4NvCcSH1jOUivGtCmCyC/ECE7EAasL7aIeXQk10g+cppFgN/MHZLsRPYCniO4P3EdIafuBYB+ID+JjgNyOkOKViP2FfgvguPEv0+QUeNW1M4gaWKwxe5ALtUFV4K6iXfp+BW1qP2ijmKxyNthHfmwuVbZZwsYlXHNEJ6Md1rUdtCHMlR3Ob8MqYKtnM4+QmvCvYH/GlV8hMkf7BmIGQhUZu8xrDCTZRjKDWAb8m/OveGHEb29hGCmrCMewUA1shvgfTQlNpCF0zhj48tn637E4xuKS10QzjjY1tdSYhgCclm6pueF6epXpOvNcUxCnVl9GRNuKHKmwWcbqJdzvfv0tIU0G8j6uqJ5kp7Vtyj5ClzWyXhav+TtNeg/SB5J1QEzH0S5uyiiHHhwRipn3qyxiRNu5nxIfSFd0NpWdUT1YbAqgqtmLVuxAvMBYLoCZ+zx9M4FThcAppMRJl+Yimopg1/7sGinWtA/FziT6znTiOA8LneHkt4BWe4oHwgZKCTBjff+bPRR8o+5/9B/p2dKkZN7nwAAAAAElFTkSuQmCC";
        public int MaxDescrHeight => 140;
        public int MaxHeight => 703;
        public int MaxImageHeight => 360;
        public int MaxImageWidth => 480;
        public int MaxWidth => 680;
        public int MinDescrHeight => 410;
        public int MinHeight => 503;
        public int MinImageHeight => 90;
        public int MinImageWidth => 120;
        public int MinWidth => 480;

        public Subject<PopupContext> Trigger { get; } = new();

        #endregion

        #region Methods

        public void Dispose() => Trigger?.Dispose();
        public void Hide() => Trigger.OnNext(null);
        public void Show(PopupContext context) => Trigger.OnNext(context);

        #endregion
    }
}
