using System;

namespace v00v.Model.Entities.Instance
{
    public class PopularPlaylist : Playlist
    {
        #region Static Properties

        public static PopularPlaylist Instance =>
            new()
            {
                IsStatePlaylist = true,
                ThumbSize = 25,
                Title = " Popular",
                Order = 2,
                Id = "-4",
                Thumbnail =
                    Convert.FromBase64String(
                                             "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAACXBIWXMAAA7EAAAOxAGVKw4bAAADn0lEQVRYhbWXTWhdVRDHf+fxCCGEUIpkEUoJQUopilCCCxGJUQS7KKWIiFQQF6KgYFVEECvFRelCiogLKS6KZlHBr0UsJYhoDBaFllLRVI3SopTQxo/aWENNfi7OuX2nN+/dd1vrbN47M/OfmTPnzJy5gZqkNoBR4A7gFmAY6AP+Af4CfgW+BmaAz0IIf9e13c3xkLpH/cX69Kd6QN3czX6ocNwPvAQ8AfRmohXgNDBL3HUT6AfWAxuAnpLuB8DOEMLpq9n1qDqX7WhZnVJ3qIMVuD51i/qWejHD/6E+UNf5VnUxA0/VSWUbO0PqGyn4gl5WK0H3qEtJeUl9rBJQL5Ax9UwWxO5OiiPqb9klGq9hfF2d7Kjr1e+zIO4rK6B+kp33lhpGe9STKVM31tAfUc8mH2fVG3Lhtiy6vd2MJcyjGWaiJmZrhnklF0wn5hm1r4ahHvVUZuySurFmEJPZMQ+gDmeGdlUAUQfV29TXkv5x401X/Vi9O9lrVNgZy/ztQH0kY6zaRXI8oS64mrYZL+Jiib+oztimZ6gNW1VxAPXVtFiwTcklQJ7ur9Q3U+CFznjKyuF0HBob0YYOWXg/6RxDPVgYrkjbxizqKdvck5SpPUnnkuVSu1J3X9Kbz6OZ6QTIgigepA/byJ9NsiV1exdbRaALDeB84q+tAoUQZoF703KszXHdmX6fCSG8V2Ur83W+AcylxUi71JaomX5/AJrqw+rT6lrgu5JOFd2Ufn9sAl+mRQ8wBnxUA9gPfAsUHfBF4Jv0/+YqzynY0bQ8gtprfC5V3+0C3lsqtxPqoRLviy42nsp0by2YryfGshWPi62mM2ecDRqJf7v6aZKdrMD3Zxf52OV7ZHytLmaC3g4GBo0DR9tzNnbJji0524CrKkV9PhMetKKdXgsZZ4uCJldVkbHjHc6U9l8nx8W5F5PRKTuNdeqAsdUWtO4/Oh+01WlV59VN3UDFUSwbJ+NrdbzL1oSlcSJadT+aJeAa4PG0PBpCuNDG+CZgHfAzsYuuEMf2YWAzcBcwzpXj+dvAkyGE36uiHrA1mKjeX5I31d22Xrs6NK2OdUnY5TI8ngEnbD21GEvvRA2Hy8Y5cZ81R/mgjgDTwFDivQM8RGy324lHkhs7CrxA/C5cAzSAC8QjmQ0hnKvjON/9ZLaDeXW/sRLKqV4wllOdx+aqAuj20fmT+pw6cF0dJwrqg8BOoJjTi4/Pz4FDwJEQwsr/4RzgX8ythoAiBb9uAAAAAElFTkSuQmCC"),
                Countries = new[] { " ", "RU", "US", "CA", "FR", "DE", "IT", "JP", "UA", "SG" }
            };

        #endregion
    }
}
