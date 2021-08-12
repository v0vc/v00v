using System;
using v00v.Model.Enums;

namespace v00v.Model.Entities.Instance
{
    public class PlannedPlaylist : Playlist
    {
        #region Static Properties

        public static PlannedPlaylist Instance =>
            new()
            {
                IsStatePlaylist = true,
                ThumbSize = 25,
                Title = " Planned",
                Order = -1,
                Id = "-1",
                Thumbnail =
                    Convert.FromBase64String(
                                             "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAACXBIWXMAAA7EAAAOxAGVKw4bAAADNElEQVRYhbWXTWtdVRSGn3WJl3IJsYQSHIRQA5YQpJQoUuoHtX5QdGBRHCmKIv0BWiQDKfQHlFoEQRyIOBAHtTioUBHqt6AVQy22OGktfqG2aY2phjZ5HNxz7b6759xzk5B3tM7e71rvWvvss/c6QZ9Q1wE7gPuALcAYsL6Y/gs4C8wAx4API+LffmPXCY+qB9RZ+8cl9aA6uhrhprpXnV+GcI55dZ/arNKJCvEx4BBwe8n0BeAL4DRwqRi7EdgEbAVGSnyOA49HxNl+Kp9Ufy6p5qj6kDqQ8VO7oW5XD6mLmf+v6q114mMl4j+q91fwn1T3VMxtVU+VJLGxSrypfp05fKlu6JHsrLqgbqngDKpHspjflO4J2xsuFx+sWCzUZxPuiz14zZIk9uakIXUuW/bSyisSmK7httRvE/68xSfaKDi7gbTaZyLiz15BE9/cvg4RcRl4GrhaDLWAFzrZNdQzSXbv1Qh3/J4rW1J1g/qo+rw6mfm8lvjMqq3Obk2xfaUJqBNZMWOZz0SmtatB+3zv4Dfgk34SoHvZb1DvAT4HNhZjJyPiXOoQEaeBE8nQAw3gtmTgs4hY6jOBFA8CR4Hh4vkn4IkK7keJPTVA+1br4LsViAPckdgzwMMR8UsF91RijzS4dqVC+1pdDd4H7u4hDnAxsRs9P59l4lXgkYj4u4Z3NX0YoLvq1jIEZ2g3IQPA/oh4uU+/rtN1gPaGmSqeb+5XPSK+Wg4/wS2JfaEBfJ8M3LWCgMvFtsQ+gbozOxwm1kq5OCUXEq2nUNfZ3e+9soYJTCc6C3YuPNsNZAf/qONrID6snk903k4nRwvhDo6ZtV6rFEd9J4m/qG7OSfuyvXDQpN9bZQIvZbFfLyM17W4aVPerKz6sisqns5hn1PVVDuPqH5nDEbWs1a4TH1LfzGLNqVN1jlMlSZxX96hDfQi31N1e313PqTtyftWPySbgMDCZTV0GPgA+BX6g/ZOyRPsaHgfuBHbSfcEBnAMei4jjdQWkSQwWG/GKK8ei+oY6XK9YnciE+pbdJ1gdrqiHrXvfVLyCikRGgF3AvcBm4CautWVLwO/ASeBj4N2anuB//Ae0xoVIPWpetAAAAABJRU5ErkJggg=="),
                State = WatchState.Planned
            };

        #endregion
    }
}
