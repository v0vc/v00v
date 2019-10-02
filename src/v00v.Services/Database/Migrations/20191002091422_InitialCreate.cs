using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace v00v.Services.Database.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppLogs",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AppId = table.Column<string>(nullable: true),
                    AppStatus = table.Column<byte>(nullable: false),
                    Comment = table.Column<string>(nullable: true),
                    Timestamp = table.Column<DateTimeOffset>(nullable: false, defaultValueSql: "datetime('now','localtime')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sites",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Cookie = table.Column<string>(nullable: true),
                    Login = table.Column<string>(nullable: true),
                    Pass = table.Column<string>(nullable: true),
                    Title = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sites", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Text = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Channels",
                columns: table => new
                {
                    Id = table.Column<string>(nullable: false),
                    Count = table.Column<int>(nullable: false),
                    ItemsCount = table.Column<long>(nullable: false),
                    SiteId = table.Column<int>(nullable: false),
                    SubsCount = table.Column<long>(nullable: false),
                    SubsCountDiff = table.Column<long>(nullable: false),
                    SubTitle = table.Column<string>(nullable: true),
                    Sync = table.Column<byte>(nullable: false),
                    Thumbnail = table.Column<byte[]>(nullable: true),
                    Timestamp = table.Column<DateTimeOffset>(nullable: false),
                    Title = table.Column<string>(nullable: true),
                    ViewCount = table.Column<long>(nullable: false),
                    ViewCountDiff = table.Column<long>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Channels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Channels_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChannelTags",
                columns: table => new
                {
                    ChannelId = table.Column<string>(nullable: false),
                    TagId = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelTags", x => new { x.ChannelId, x.TagId });
                    table.ForeignKey(
                        name: "FK_ChannelTags_Channels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "Channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChannelTags_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Items",
                columns: table => new
                {
                    Id = table.Column<string>(nullable: false),
                    ChannelId = table.Column<string>(nullable: true),
                    Comments = table.Column<long>(nullable: false),
                    Description = table.Column<string>(nullable: true),
                    DislikeCount = table.Column<long>(nullable: false),
                    Duration = table.Column<int>(nullable: false),
                    FileName = table.Column<string>(nullable: true),
                    LikeCount = table.Column<long>(nullable: false),
                    SyncState = table.Column<byte>(nullable: false),
                    Thumbnail = table.Column<byte[]>(nullable: true),
                    Timestamp = table.Column<DateTimeOffset>(nullable: false),
                    Title = table.Column<string>(nullable: true),
                    ViewCount = table.Column<long>(nullable: false),
                    ViewDiff = table.Column<long>(nullable: false),
                    WatchState = table.Column<byte>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Items_Channels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "Channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Playlists",
                columns: table => new
                {
                    Id = table.Column<string>(nullable: false),
                    ChannelId = table.Column<string>(nullable: true),
                    Count = table.Column<int>(nullable: false),
                    SubTitle = table.Column<string>(nullable: true),
                    SyncState = table.Column<byte>(nullable: false),
                    Thumbnail = table.Column<byte[]>(nullable: true),
                    Title = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Playlists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Playlists_Channels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "Channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemPlaylists",
                columns: table => new
                {
                    ItemId = table.Column<string>(nullable: false),
                    PlaylistId = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemPlaylists", x => new { x.ItemId, x.PlaylistId });
                    table.ForeignKey(
                        name: "FK_ItemPlaylists_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemPlaylists_Playlists_PlaylistId",
                        column: x => x.PlaylistId,
                        principalTable: "Playlists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Sites",
                columns: new[] { "Id", "Cookie", "Login", "Pass", "Title" },
                values: new object[] { 1, null, null, null, "youtube.com" });

            migrationBuilder.InsertData(
                table: "Tags",
                columns: new[] { "Id", "Text" },
                values: new object[] { 26, "оффроад" });

            migrationBuilder.InsertData(
                table: "Tags",
                columns: new[] { "Id", "Text" },
                values: new object[] { 25, "эмиграция" });

            migrationBuilder.InsertData(
                table: "Tags",
                columns: new[] { "Id", "Text" },
                values: new object[] { 24, "политика" });

            migrationBuilder.InsertData(
                table: "Tags",
                columns: new[] { "Id", "Text" },
                values: new object[] { 23, "сплав" });

            migrationBuilder.InsertData(
                table: "Tags",
                columns: new[] { "Id", "Text" },
                values: new object[] { 22, "дубна" });

            migrationBuilder.InsertData(
                table: "Tags",
                columns: new[] { "Id", "Text" },
                values: new object[] { 21, "тренировки" });

            migrationBuilder.InsertData(
                table: "Tags",
                columns: new[] { "Id", "Text" },
                values: new object[] { 20, "путешествия" });

            migrationBuilder.InsertData(
                table: "Tags",
                columns: new[] { "Id", "Text" },
                values: new object[] { 19, "игры" });

            migrationBuilder.InsertData(
                table: "Tags",
                columns: new[] { "Id", "Text" },
                values: new object[] { 18, "обучение" });

            migrationBuilder.InsertData(
                table: "Tags",
                columns: new[] { "Id", "Text" },
                values: new object[] { 17, "история" });

            migrationBuilder.InsertData(
                table: "Tags",
                columns: new[] { "Id", "Text" },
                values: new object[] { 16, "ремонт" });

            migrationBuilder.InsertData(
                table: "Tags",
                columns: new[] { "Id", "Text" },
                values: new object[] { 15, "diy" });

            migrationBuilder.InsertData(
                table: "Tags",
                columns: new[] { "Id", "Text" },
                values: new object[] { 27, "мультики" });

            migrationBuilder.InsertData(
                table: "Tags",
                columns: new[] { "Id", "Text" },
                values: new object[] { 14, "религия" });

            migrationBuilder.InsertData(
                table: "Tags",
                columns: new[] { "Id", "Text" },
                values: new object[] { 12, "интервью" });

            migrationBuilder.InsertData(
                table: "Tags",
                columns: new[] { "Id", "Text" },
                values: new object[] { 11, "природа" });

            migrationBuilder.InsertData(
                table: "Tags",
                columns: new[] { "Id", "Text" },
                values: new object[] { 10, "вело" });

            migrationBuilder.InsertData(
                table: "Tags",
                columns: new[] { "Id", "Text" },
                values: new object[] { 9, "танцы" });

            migrationBuilder.InsertData(
                table: "Tags",
                columns: new[] { "Id", "Text" },
                values: new object[] { 8, "техника" });

            migrationBuilder.InsertData(
                table: "Tags",
                columns: new[] { "Id", "Text" },
                values: new object[] { 7, "еда" });

            migrationBuilder.InsertData(
                table: "Tags",
                columns: new[] { "Id", "Text" },
                values: new object[] { 6, "новости" });

            migrationBuilder.InsertData(
                table: "Tags",
                columns: new[] { "Id", "Text" },
                values: new object[] { 5, "наука" });

            migrationBuilder.InsertData(
                table: "Tags",
                columns: new[] { "Id", "Text" },
                values: new object[] { 4, "мото" });

            migrationBuilder.InsertData(
                table: "Tags",
                columns: new[] { "Id", "Text" },
                values: new object[] { 3, "смешное" });

            migrationBuilder.InsertData(
                table: "Tags",
                columns: new[] { "Id", "Text" },
                values: new object[] { 2, "музыка" });

            migrationBuilder.InsertData(
                table: "Tags",
                columns: new[] { "Id", "Text" },
                values: new object[] { 1, "авто" });

            migrationBuilder.InsertData(
                table: "Tags",
                columns: new[] { "Id", "Text" },
                values: new object[] { 13, "ревью" });

            migrationBuilder.InsertData(
                table: "Tags",
                columns: new[] { "Id", "Text" },
                values: new object[] { 28, "электротранспорт" });

            migrationBuilder.InsertData(
                table: "Tags",
                columns: new[] { "Id", "Text" },
                values: new object[] { 29, "рыбалка" });

            migrationBuilder.CreateIndex(
                name: "IX_Channels_SiteId",
                table: "Channels",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_ChannelTags_TagId",
                table: "ChannelTags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemPlaylists_PlaylistId",
                table: "ItemPlaylists",
                column: "PlaylistId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_ChannelId",
                table: "Items",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_Playlists_ChannelId",
                table: "Playlists",
                column: "ChannelId");
                
			migrationBuilder.Sql("CREATE TRIGGER IF NOT EXISTS trig_viewdiff BEFORE UPDATE ON Items BEGIN UPDATE Items SET ViewDiff = (NEW.ViewCount - OLD.ViewCount) WHERE Id = NEW.Id;END");
            migrationBuilder.Sql("CREATE TRIGGER IF NOT EXISTS trig_subdiff BEFORE UPDATE ON Channels BEGIN UPDATE Channels SET SubsCountDiff = (NEW.SubsCount - OLD.SubsCount) WHERE Id = NEW.Id;UPDATE Channels SET ViewCountDiff = (NEW.ViewCount - OLD.ViewCount) WHERE Id = NEW.Id;END");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
			migrationBuilder.Sql("DROP TRIGGER IF EXISTS trig_viewdiff");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trig_subdiff");
            
            migrationBuilder.DropTable(
                name: "AppLogs");

            migrationBuilder.DropTable(
                name: "ChannelTags");

            migrationBuilder.DropTable(
                name: "ItemPlaylists");

            migrationBuilder.DropTable(
                name: "Tags");

            migrationBuilder.DropTable(
                name: "Items");

            migrationBuilder.DropTable(
                name: "Playlists");

            migrationBuilder.DropTable(
                name: "Channels");

            migrationBuilder.DropTable(
                name: "Sites");
        }
    }
}
