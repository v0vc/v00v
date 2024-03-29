﻿using System.Collections.Generic;

namespace v00v.Services.Database.Models
{
    public sealed class Tag
    {
        #region Properties

        public IEnumerable<ChannelTag> Channels { get; } = new List<ChannelTag>();
        public int Id { get; set; }
        public string Text { get; set; }

        #endregion
    }
}
