using System.Collections.Generic;

namespace v00v.Services.Database.Models
{
    public class Site
    {
        #region Properties

        public virtual ICollection<Channel> Channels { get; } = new List<Channel>();

        public string Cookie { get; set; }

        public int Id { get; set; }

        public string Login { get; set; }

        public string Pass { get; set; }

        public string Title { get; set; }

        #endregion
    }
}
