using System;
using v00v.Model.Enums;

namespace v00v.Model.Entities.Instance
{
    public class WatchedPlaylist : Playlist
    {
        #region Constructors

        static WatchedPlaylist()
        {
            byte[] imageBytes =
                Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAACXBIWXMAAA7EAAAOxAGVKw4bAAABo0lEQVRYhcXWsWoUURQG4MO6LIsECRZLCBYiqSSEICJiJRIsRHyAfQYLC7EQCVhYpRCx9AGsxEawskoVgoiFhBBELCSFVQgiIuGz2Lt4M84muLtz85fDYb5/lplzN+KEgjbi1Anh1yLiTUQstArDXTyLiPWIuBgR30vii/hkkD3cKYn38SPh37BUCm5hzd/s4HwpvItXGb6F+VL4LNYzfLsk3sOHDP+Mc6XwuexNh11cKIX3Kvg+lkvhZ/Exww9wuxQ+gw2H87AU3sbbCv4aza94BF5U8C+YbRxPBe5V8N+4OskNT+MmVtA9ZvZ6AvOsToLfMjgkhtnGwojZufR959lEe1y8X/M0wxIzldkW3lXmfmFxLDzd9FENPsyTyuyDmpnHY+PZje+PKLCHM2lmKT1tni10Ji6QgKcjStxFx+EDZpgbU8FTgQ7e1yCbWK25/nJqeFbikvoXsnptX1Pnu3+3W12a2/WYx88j8K+OWVTTKHHUr9BvFE8FLo/ANxQ86fJ/Nwd4rrIZmy4x/PR2sVIMzgpcMdj7veJ4KtAy7gn3H/kDSGd+FNBB6wgAAAAASUVORK5CYII=");

            Instance = new WatchedPlaylist
            {
                IsStatePlaylist = true,
                Title = "Watched",
                Order = 0,
                Id = "0",
                Thumbnail = imageBytes,
                State = WatchState.Watched
            };
        }

        #endregion

        #region Static Properties

        public static WatchedPlaylist Instance { get; }

        #endregion
    }
}
