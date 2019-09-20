using System;
using System.Collections.Generic;
using System.Linq;

namespace v00v.Model.SyncEntities
{
    public class ChannelStruct
    {
        #region Properties

        public string ChannelId { get; set; }
        public string ChannelTitle { get; set; }
        public HashSet<ItemPrivacy> Items { get; set; }
        public IEnumerable<string> PlIds => Pls?.Select(x => x.Item1);
        public HashSet<Tuple<string, List<string>>> Pls { private get; set; }

        #endregion
    }
}
