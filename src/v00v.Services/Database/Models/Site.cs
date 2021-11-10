using System.Collections.Generic;

namespace v00v.Services.Database.Models
{
    public sealed class Site
    {
        #region Properties

        public ICollection<Channel> Channels { get; } = new List<Channel>();

        public string Cookie { get; set; }

        public int Id { get; set; }

        public string Login { get; set; }

        public string Pass { get; set; }

        public string Title { get; set; }

        #endregion
    }
}
