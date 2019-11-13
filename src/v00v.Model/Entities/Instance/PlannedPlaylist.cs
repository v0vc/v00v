using System;
using v00v.Model.Enums;

namespace v00v.Model.Entities.Instance
{
    public class PlannedPlaylist : Playlist
    {
        #region Constructors

        static PlannedPlaylist()
        {
            //byte[] imageBytes =
            //    Convert.FromBase64String(
            //                             "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAACXBIWXMAAA7EAAAOxAGVKw4bAAACxUlEQVRYhcWXy2tVVxTGf/twCSFcLqGEEEIQlDSIAykSdCIOfEwdlCAdlBL8A2xpi5N21oEjERGdiC8EcWBmzqQEn9VAi6WGYouIrdhWtPhqVRR/Ds66GpNrss9NjN/s7L3W96191l5nrZPIhFoD1gIbgFXAANAV2/8DfwA/AePAuZTS8xzelCHcC3wObAV6gSvABHAVuAN0AA3gQ2A1sBK4DRwEdqeUbmedsIVwTf1Sva/+qW5X+zP8BsL2Rvh+rRZVxfvU0+p/QdbZxgE61C/Uh8HVl+u4TL2m/qIOVRVuwTcUXNfVwbmM+8LwrNqdQY6aE0S3ej64W6cxcn5W/VltzMla+nyn7si0bcSbOB8VNcNge+Rr9tf0ps8h9UgF+8HQ+Hb6Rn9sbMslC78j6tGKPtvicg8ANMvjK8ra3VeFLPyrlVip8XdoUkSJbQV25X695oPQ2AWMqp0FsB6oA8fb4Kt6+iaOhebGAtgEXEwp3WmTrDJSSv8CF4ANBWVjmVgs8Sm4CAwXwBLg9/cQwDVgSUGZi3vvIYB7QL1G+xepiVXqzgy7yZTSwWlrRQ14RNnP28EpoBtYzuuDtDrQC+DJtLUG8KgG3ASWtqOeUjoMHG7HNzRvFpRj1HCbJPPBMHAZ9WP1cW4HXAio9dAcaT48VEcXMYDPoiHVmwv7Yw6Yb0XkiKP+qB6YujioPlU/XYQAPlGfzRj31J3qPzGGvyvxXvWvlt8NtUudVMfVjncg3hHck2rX24yG1Lvq2EIGEeJjwT37pK0Oh+H3as8CiPcE110173ujLld/jXxtyRm9W3CgjgTHVXVFVYK6uidu7KW4va1z96ZfZ9heCt+9r+q9BXJ+TlcA3wAjlA3lDPAD8BvwIMwalA1pDbAO6AROADtSSldm458zgCmBfABspvw9/wjo53XnewHcAi5TdsiTMXbNiZf5+zt1Lc3n1wAAAABJRU5ErkJggg==");

            Instance = new PlannedPlaylist
            {
                IsStatePlaylist = true,
                Title = "Planned",
                Order = -1,
                Id = "-1",
                Thumbnail =
                    Convert.FromBase64String(
                                             "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAACXBIWXMAAA7EAAAOxAGVKw4bAAADNElEQVRYhbWXTWtdVRSGn3WJl3IJsYQSHIRQA5YQpJQoUuoHtX5QdGBRHCmKIv0BWiQDKfQHlFoEQRyIOBAHtTioUBHqt6AVQy22OGktfqG2aY2phjZ5HNxz7b6759xzk5B3tM7e71rvWvvss/c6QZ9Q1wE7gPuALcAYsL6Y/gs4C8wAx4API+LffmPXCY+qB9RZ+8cl9aA6uhrhprpXnV+GcI55dZ/arNKJCvEx4BBwe8n0BeAL4DRwqRi7EdgEbAVGSnyOA49HxNl+Kp9Ufy6p5qj6kDqQ8VO7oW5XD6mLmf+v6q114mMl4j+q91fwn1T3VMxtVU+VJLGxSrypfp05fKlu6JHsrLqgbqngDKpHspjflO4J2xsuFx+sWCzUZxPuiz14zZIk9uakIXUuW/bSyisSmK7httRvE/68xSfaKDi7gbTaZyLiz15BE9/cvg4RcRl4GrhaDLWAFzrZNdQzSXbv1Qh3/J4rW1J1g/qo+rw6mfm8lvjMqq3Obk2xfaUJqBNZMWOZz0SmtatB+3zv4Dfgk34SoHvZb1DvAT4HNhZjJyPiXOoQEaeBE8nQAw3gtmTgs4hY6jOBFA8CR4Hh4vkn4IkK7keJPTVA+1br4LsViAPckdgzwMMR8UsF91RijzS4dqVC+1pdDd4H7u4hDnAxsRs9P59l4lXgkYj4u4Z3NX0YoLvq1jIEZ2g3IQPA/oh4uU+/rtN1gPaGmSqeb+5XPSK+Wg4/wS2JfaEBfJ8M3LWCgMvFtsQ+gbozOxwm1kq5OCUXEq2nUNfZ3e+9soYJTCc6C3YuPNsNZAf/qONrID6snk903k4nRwvhDo6ZtV6rFEd9J4m/qG7OSfuyvXDQpN9bZQIvZbFfLyM17W4aVPerKz6sisqns5hn1PVVDuPqH5nDEbWs1a4TH1LfzGLNqVN1jlMlSZxX96hDfQi31N1e313PqTtyftWPySbgMDCZTV0GPgA+BX6g/ZOyRPsaHgfuBHbSfcEBnAMei4jjdQWkSQwWG/GKK8ei+oY6XK9YnciE+pbdJ1gdrqiHrXvfVLyCikRGgF3AvcBm4CautWVLwO/ASeBj4N2anuB//Ae0xoVIPWpetAAAAABJRU5ErkJggg=="),
                State = WatchState.Planned
            };
        }

        #endregion

        #region Static Properties

        public static PlannedPlaylist Instance { get; }

        #endregion
    }
}
