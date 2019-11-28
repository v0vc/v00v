using System;

namespace v00v.Model.Entities.Instance
{
    public class UnlistedPlaylist : Playlist
    {
        #region Static Properties

        public static UnlistedPlaylist Instance =>
            new UnlistedPlaylist
            {
                IsStatePlaylist = true,
                ThumbSize = 25,
                Title = " Unlisted",
                Order = 1,
                Id = "-2",
                Thumbnail =
                    Convert.FromBase64String(
                                             "iVBORw0KGgoAAAANSUhEUgAAABgAAAAYCAYAAADgdz34AAAACXBIWXMAAA7EAAAOxAGVKw4bAAACFElEQVRIie2UMWhTURSG/1NCyFBKCOIgDqEEkSIiHaRgEBSH4ODkEEGkODiJoBSHDq5ORRzEycHJ0cHBqkgpUVxEcKhSp4LGUorYVhxszf859AZeYmJbHLr0wOPde89//nPO/+470p7ttsV2gUBOUj5t1yPi938nACpALSJOSCpLGkyuNUkLkt5Iei7pU8Q2awVyts/anrXdso1tANrr7AO0bL8GzgH5rciHgcfAL3ZotjeAJ0ClH/kY8DkTQNe6afsecB9o9sGQfNVu8jO2V7NSAA3b47aXbDeAUgZfBF7aXgbGgdku6VaBWhs80q4okf8EJoECUE/HYz06Hk2+S0DB9oTtHxnVFm0fFfCiS8srgCTJ9s10PNgjQSFVO5n2sn25S67GgKTDbQCwEhGlzJX7kpKN9Phsh9L7qySlmKKkFUlOnBXZPp80vAVcA+bbets+kOSbAfZnqt8HPAMWgYMJWwTmbN9IEs8C9Y6SbA8B74GpTFd1YAOYt30HmAI+Ai3gYltO4DYwBxR7dNuh7XHgm+0JNseDgJPAU9tN281U/enkywFXge+2q/8kT10IqNleAh4Cw+ksZ7tku5RIBZRtPwCW2fyT/+LrO0CAY8DdiDgiaVrSDLAQEQLKkk5FRA34EBHXI+JtL56tht2ApKqkC5JGgSFJiog14J2kRxHxKiLcj2Mn4zqvznG9vt3YPdtd+wNmpsv207JHxQAAAABJRU5ErkJggg==")
            };

        #endregion
    }
}
