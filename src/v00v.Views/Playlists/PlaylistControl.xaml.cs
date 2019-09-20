using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace v00v.Views.Playlists
{
    public class PlaylistControl : UserControl
    {
        #region Constructors

        public PlaylistControl()
        {
            AvaloniaXamlLoader.Load(this);
        }

        #endregion
    }
}
