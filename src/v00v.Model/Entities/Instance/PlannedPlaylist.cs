using System;
using v00v.Model.Enums;

namespace v00v.Model.Entities.Instance
{
    public class PlannedPlaylist : Playlist
    {
        #region Constructors

        static PlannedPlaylist()
        {
            byte[] imageBytes =
                Convert.FromBase64String(
                                         "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAACXBIWXMAAA7EAAAOxAGVKw4bAAACxUlEQVRYhcWXy2tVVxTGf/twCSFcLqGEEEIQlDSIAykSdCIOfEwdlCAdlBL8A2xpi5N21oEjERGdiC8EcWBmzqQEn9VAi6WGYouIrdhWtPhqVRR/Ds66GpNrss9NjN/s7L3W96191l5nrZPIhFoD1gIbgFXAANAV2/8DfwA/AePAuZTS8xzelCHcC3wObAV6gSvABHAVuAN0AA3gQ2A1sBK4DRwEdqeUbmedsIVwTf1Sva/+qW5X+zP8BsL2Rvh+rRZVxfvU0+p/QdbZxgE61C/Uh8HVl+u4TL2m/qIOVRVuwTcUXNfVwbmM+8LwrNqdQY6aE0S3ej64W6cxcn5W/VltzMla+nyn7si0bcSbOB8VNcNge+Rr9tf0ps8h9UgF+8HQ+Hb6Rn9sbMslC78j6tGKPtvicg8ANMvjK8ra3VeFLPyrlVip8XdoUkSJbQV25X695oPQ2AWMqp0FsB6oA8fb4Kt6+iaOhebGAtgEXEwp3WmTrDJSSv8CF4ANBWVjmVgs8Sm4CAwXwBLg9/cQwDVgSUGZi3vvIYB7QL1G+xepiVXqzgy7yZTSwWlrRQ14RNnP28EpoBtYzuuDtDrQC+DJtLUG8KgG3ASWtqOeUjoMHG7HNzRvFpRj1HCbJPPBMHAZ9WP1cW4HXAio9dAcaT48VEcXMYDPoiHVmwv7Yw6Yb0XkiKP+qB6YujioPlU/XYQAPlGfzRj31J3qPzGGvyvxXvWvlt8NtUudVMfVjncg3hHck2rX24yG1Lvq2EIGEeJjwT37pK0Oh+H3as8CiPcE110173ujLld/jXxtyRm9W3CgjgTHVXVFVYK6uidu7KW4va1z96ZfZ9heCt+9r+q9BXJ+TlcA3wAjlA3lDPAD8BvwIMwalA1pDbAO6AROADtSSldm458zgCmBfABspvw9/wjo53XnewHcAi5TdsiTMXbNiZf5+zt1Lc3n1wAAAABJRU5ErkJggg==");

            Instance = new PlannedPlaylist
            {
                IsStatePlaylist = true,
                Title = "Planned",
                Order = -1,
                Id = "-1",
                Thumbnail = imageBytes,
                State = WatchState.Planned
            };
        }

        #endregion

        #region Static Properties

        public static PlannedPlaylist Instance { get; }

        #endregion
    }
}
