using System.IO;
using Avalonia;
using Microsoft.EntityFrameworkCore;
using v00v.Services.Backup;
using v00v.Services.Database.Models;

namespace v00v.Services.Database
{
    public sealed class VideoContext : DbContext
    {
        #region Properties

        public DbSet<AppLog> AppLogs { get; set; }
        public DbSet<Channel> Channels { get; set; }
        public DbSet<ChannelTag> ChannelTags { get; set; }
        public DbSet<ItemPlaylist> ItemPlaylists { get; set; }
        public DbSet<Item> Items { get; set; }
        public DbSet<Playlist> Playlists { get; set; }
        public DbSet<Site> Sites { get; set; }
        public DbSet<Tag> Tags { get; set; }

        #endregion

        #region Methods

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            //optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            //optionsBuilder.EnableSensitiveDataLogging();
            //optionsBuilder.UseLazyLoadingProxies();

            var backupservice = AvaloniaLocator.Current.GetService<IBackupService>();
            if (!backupservice.UseSqliteInit)
            {
                if (backupservice.CustomDbEnabled)
                {
                    var dir = new DirectoryInfo(backupservice.CustomDbPath);
                    backupservice.UseSqlite = dir.Exists ? $"Data Source={dir.FullName}\\data.db" : "Data Source=data.db";
                }
                else
                {
                    backupservice.UseSqlite = "Data Source=data.db";
                }

                backupservice.UseSqliteInit = true;
            }

            optionsBuilder.UseSqlite(backupservice.UseSqlite);
            //optionsBuilder.UseSqlite("Data Source=data.db" /*, x => x.SuppressForeignKeyEnforcement()*/);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ChannelTag>().HasKey(t => new { t.ChannelId, t.TagId });
            modelBuilder.Entity<ItemPlaylist>().HasKey(t => new { t.ItemId, t.PlaylistId });
            modelBuilder.Entity<Playlist>().Ignore(b => b.ThumbnailLink);
            modelBuilder.Entity<Item>().Ignore(b => b.ThumbnailLink);

            modelBuilder.Entity<AppLog>().Property(x => x.Timestamp).HasDefaultValueSql("datetime('now','localtime')");
            modelBuilder.Entity<Tag>().HasData(new Tag { Id = 1, Text = "авто" });
            modelBuilder.Entity<Tag>().HasData(new Tag { Id = 2, Text = "музыка" });
            modelBuilder.Entity<Tag>().HasData(new Tag { Id = 3, Text = "смешное" });
            modelBuilder.Entity<Tag>().HasData(new Tag { Id = 4, Text = "мото" });
            modelBuilder.Entity<Tag>().HasData(new Tag { Id = 5, Text = "наука" });
            modelBuilder.Entity<Tag>().HasData(new Tag { Id = 6, Text = "новости" });
            modelBuilder.Entity<Tag>().HasData(new Tag { Id = 7, Text = "еда" });
            modelBuilder.Entity<Tag>().HasData(new Tag { Id = 8, Text = "техника" });
            modelBuilder.Entity<Tag>().HasData(new Tag { Id = 9, Text = "танцы" });
            modelBuilder.Entity<Tag>().HasData(new Tag { Id = 10, Text = "вело" });
            modelBuilder.Entity<Tag>().HasData(new Tag { Id = 11, Text = "природа" });
            modelBuilder.Entity<Tag>().HasData(new Tag { Id = 12, Text = "интервью" });
            modelBuilder.Entity<Tag>().HasData(new Tag { Id = 13, Text = "ревью" });
            modelBuilder.Entity<Tag>().HasData(new Tag { Id = 14, Text = "религия" });
            modelBuilder.Entity<Tag>().HasData(new Tag { Id = 15, Text = "diy" });
            modelBuilder.Entity<Tag>().HasData(new Tag { Id = 16, Text = "ремонт" });
            modelBuilder.Entity<Tag>().HasData(new Tag { Id = 17, Text = "история" });
            modelBuilder.Entity<Tag>().HasData(new Tag { Id = 18, Text = "обучение" });
            modelBuilder.Entity<Tag>().HasData(new Tag { Id = 19, Text = "игры" });
            modelBuilder.Entity<Tag>().HasData(new Tag { Id = 20, Text = "путешествия" });
            modelBuilder.Entity<Tag>().HasData(new Tag { Id = 21, Text = "тренировки" });
            modelBuilder.Entity<Tag>().HasData(new Tag { Id = 22, Text = "дубна" });
            modelBuilder.Entity<Tag>().HasData(new Tag { Id = 23, Text = "сплав" });
            modelBuilder.Entity<Tag>().HasData(new Tag { Id = 24, Text = "политика" });
            modelBuilder.Entity<Tag>().HasData(new Tag { Id = 25, Text = "эмиграция" });
            modelBuilder.Entity<Tag>().HasData(new Tag { Id = 26, Text = "оффроад" });
            modelBuilder.Entity<Tag>().HasData(new Tag { Id = 27, Text = "мультики" });
            modelBuilder.Entity<Tag>().HasData(new Tag { Id = 28, Text = "электротранспорт" });
            modelBuilder.Entity<Tag>().HasData(new Tag { Id = 29, Text = "рыбалка" });

            modelBuilder.Entity<Site>().HasData(new Site { Id = 1, Title = "youtube.com" });
        }

        #endregion
    }
}
