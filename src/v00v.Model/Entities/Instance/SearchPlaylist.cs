using System;

namespace v00v.Model.Entities.Instance
{
    public class SearchPlaylist : Playlist
    {
        #region Static Properties

        public static SearchPlaylist Instance =>
            new()
            {
                IsStatePlaylist = true,
                ThumbSize = 25,
                Title = " Search",
                Order = 3,
                Id = "-3",
                Thumbnail =
                    Convert.FromBase64String(
                                             "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAACXBIWXMAAA7EAAAOxAGVKw4bAAACSklEQVRYhcWXPWgUURSFzwspliVIkFQWa1hC2EJCkCgioqCFsmUKBQttxMoilYWdhYVFKkmRykoUYqVYWIgoKgYRwR9EBE3YiKyJhIjGP+JnMS9wfe7uzJvMklNdmHfPOffenbtvnAoAMCDpqKRDkoYllSX9kLQg6bGkGefcyyK0QuEycBH4RjpuAtUixavAqwzCFivAkSLEK0AjIH8NnAX2ADVgDDgB3ALWzLmfQH0j4r3AE0P4G5gAejrk7A8MrwBDeQ1MBOLjGfPCrt3OI94LfDAk5yPz9wbjGIs1cNAkLwLlKIKEY8ZwTLadWxscMPF159xqrAFJV028L9bAsImf5RCXpOcmrsQaKJn4V04DXyxfrAHb8lLbU52xxfLFGnhn4l05Dew08VysgXsmHgf6cxg4aeL7UZl+D8yb1+hSZH7d5K4BO6IMeJJT/IvTGfNGgWWTNxMt7ol6gLeBicl2SwkQcJxk/6+jCWyTJJfDwAVJ51o8+iTpmqRZSUuS+iTVJB2TNGLOfZV02Dn3KKtgv69wFngRVD5PHBrA7phqKyT/860wTfKjPONb2gnfgSlga6jRdgQk16c7kgZbPF6QtN0598efLUmqK7kTDilZNquS5iQ9lHTDObcUU3k1pb1PM5Ol4L9F5Cu/K6nSIe9BUQZC8cGUygE+4l+hzRD/DIx2Q7wCvE8RXyb2CpVRfAB4syni3sBUhrZ3R9wb6DT3xa7MPDAQfuGsowmMpDNs3MB0C/EGUOu6uDfQB1z27W4CV4BOS6hQ/AVAafghdGdZBQAAAABJRU5ErkJggg==")
            };

        #endregion
    }
}
