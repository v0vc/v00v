﻿// <auto-generated />

using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace v00v.Services.Database.Migrations
{
    [DbContext(typeof(VideoContext))]
    partial class VideoContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "2.2.6-servicing-10079");

            modelBuilder.Entity("v0v.Services.Db.Models.AppLog", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("AppId");

                    b.Property<byte>("AppStatus");

                    b.Property<string>("Comment");

                    b.Property<DateTimeOffset>("Timestamp")
                        .ValueGeneratedOnAdd()
                        .HasDefaultValueSql("datetime('now','localtime')");

                    b.HasKey("Id");

                    b.ToTable("AppLogs");
                });

            modelBuilder.Entity("v0v.Services.Db.Models.Channel", b =>
                {
                    b.Property<string>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<int>("Count");

                    b.Property<long>("ItemsCount");

                    b.Property<int>("SiteId");

                    b.Property<string>("SubTitle");

                    b.Property<long>("SubsCount");

                    b.Property<long>("SubsCountDiff");

                    b.Property<byte>("Sync");

                    b.Property<byte[]>("Thumbnail");

                    b.Property<DateTimeOffset>("Timestamp");

                    b.Property<string>("Title");

                    b.Property<long>("ViewCount");

                    b.Property<long>("ViewCountDiff");

                    b.HasKey("Id");

                    b.HasIndex("SiteId");

                    b.ToTable("Channels");
                });

            modelBuilder.Entity("v0v.Services.Db.Models.ChannelTag", b =>
                {
                    b.Property<string>("ChannelId");

                    b.Property<int>("TagId");

                    b.HasKey("ChannelId", "TagId");

                    b.HasIndex("TagId");

                    b.ToTable("ChannelTags");
                });

            modelBuilder.Entity("v0v.Services.Db.Models.Item", b =>
                {
                    b.Property<string>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("ChannelId");

                    b.Property<long>("Comments");

                    b.Property<string>("Description");

                    b.Property<long>("DislikeCount");

                    b.Property<int>("Duration");

                    b.Property<string>("FileName");

                    b.Property<long>("LikeCount");

                    b.Property<byte>("SyncState");

                    b.Property<byte[]>("Thumbnail");

                    b.Property<DateTimeOffset>("Timestamp");

                    b.Property<string>("Title");

                    b.Property<long>("ViewCount");

                    b.Property<long>("ViewDiff");

                    b.Property<byte>("WatchState");

                    b.HasKey("Id");

                    b.HasIndex("ChannelId");

                    b.ToTable("Items");
                });

            modelBuilder.Entity("v0v.Services.Db.Models.ItemPlaylist", b =>
                {
                    b.Property<string>("ItemId");

                    b.Property<string>("PlaylistId");

                    b.HasKey("ItemId", "PlaylistId");

                    b.HasIndex("PlaylistId");

                    b.ToTable("ItemPlaylists");
                });

            modelBuilder.Entity("v0v.Services.Db.Models.Playlist", b =>
                {
                    b.Property<string>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("ChannelId");

                    b.Property<int>("Count");

                    b.Property<string>("SubTitle");

                    b.Property<byte>("SyncState");

                    b.Property<byte[]>("Thumbnail");

                    b.Property<string>("Title");

                    b.HasKey("Id");

                    b.HasIndex("ChannelId");

                    b.ToTable("Playlists");
                });

            modelBuilder.Entity("v0v.Services.Db.Models.Site", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("Cookie");

                    b.Property<string>("Login");

                    b.Property<string>("Pass");

                    b.Property<string>("Title");

                    b.HasKey("Id");

                    b.ToTable("Sites");

                    b.HasData(
                        new
                        {
                            Id = 1,
                            Title = "youtube.com"
                        });
                });

            modelBuilder.Entity("v0v.Services.Db.Models.Tag", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("Text");

                    b.HasKey("Id");

                    b.ToTable("Tags");

                    b.HasData(
                        new
                        {
                            Id = 1,
                            Text = "авто"
                        },
                        new
                        {
                            Id = 2,
                            Text = "музыка"
                        },
                        new
                        {
                            Id = 3,
                            Text = "смешное"
                        },
                        new
                        {
                            Id = 4,
                            Text = "мото"
                        },
                        new
                        {
                            Id = 5,
                            Text = "наука"
                        },
                        new
                        {
                            Id = 6,
                            Text = "новости"
                        },
                        new
                        {
                            Id = 7,
                            Text = "еда"
                        },
                        new
                        {
                            Id = 8,
                            Text = "техника"
                        },
                        new
                        {
                            Id = 9,
                            Text = "танцы"
                        },
                        new
                        {
                            Id = 10,
                            Text = "вело"
                        },
                        new
                        {
                            Id = 11,
                            Text = "природа"
                        },
                        new
                        {
                            Id = 12,
                            Text = "интервью"
                        },
                        new
                        {
                            Id = 13,
                            Text = "ревью"
                        },
                        new
                        {
                            Id = 14,
                            Text = "религия"
                        },
                        new
                        {
                            Id = 15,
                            Text = "diy"
                        },
                        new
                        {
                            Id = 16,
                            Text = "ремонт"
                        },
                        new
                        {
                            Id = 17,
                            Text = "история"
                        },
                        new
                        {
                            Id = 18,
                            Text = "обучение"
                        },
                        new
                        {
                            Id = 19,
                            Text = "игры"
                        },
                        new
                        {
                            Id = 20,
                            Text = "путешествия"
                        },
                        new
                        {
                            Id = 21,
                            Text = "тренировки"
                        },
                        new
                        {
                            Id = 22,
                            Text = "дубна"
                        },
                        new
                        {
                            Id = 23,
                            Text = "сплав"
                        },
                        new
                        {
                            Id = 24,
                            Text = "политика"
                        },
                        new
                        {
                            Id = 25,
                            Text = "эмиграция"
                        },
                        new
                        {
                            Id = 26,
                            Text = "оффроад"
                        },
                        new
                        {
                            Id = 27,
                            Text = "мультики"
                        },
                        new
                        {
                            Id = 28,
                            Text = "электротранспорт"
                        });
                });

            modelBuilder.Entity("v0v.Services.Db.Models.Channel", b =>
                {
                    b.HasOne("v0v.Services.Db.Models.Site", "Site")
                        .WithMany("Channels")
                        .HasForeignKey("SiteId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("v0v.Services.Db.Models.ChannelTag", b =>
                {
                    b.HasOne("v0v.Services.Db.Models.Channel")
                        .WithMany("Tags")
                        .HasForeignKey("ChannelId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.HasOne("v0v.Services.Db.Models.Tag")
                        .WithMany("Channels")
                        .HasForeignKey("TagId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("v0v.Services.Db.Models.Item", b =>
                {
                    b.HasOne("v0v.Services.Db.Models.Channel", "Channel")
                        .WithMany("Items")
                        .HasForeignKey("ChannelId");
                });

            modelBuilder.Entity("v0v.Services.Db.Models.ItemPlaylist", b =>
                {
                    b.HasOne("v0v.Services.Db.Models.Item")
                        .WithMany("Playlists")
                        .HasForeignKey("ItemId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.HasOne("v0v.Services.Db.Models.Playlist")
                        .WithMany("Items")
                        .HasForeignKey("PlaylistId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("v0v.Services.Db.Models.Playlist", b =>
                {
                    b.HasOne("v0v.Services.Db.Models.Channel", "Channel")
                        .WithMany("Playlists")
                        .HasForeignKey("ChannelId");
                });
#pragma warning restore 612, 618
        }
    }
}
