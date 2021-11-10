using System;

namespace v00v.Services.Database.Models
{
    public class AppLog
    {
        #region Properties

        public string AppId { get; set; }
        public byte AppStatus { get; set; }
        public string Comment { get; set; }
        
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }

        #endregion
    }
}
