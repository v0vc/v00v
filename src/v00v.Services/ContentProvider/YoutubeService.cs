using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using Avalonia;
using Avalonia.Media.Imaging;
using Newtonsoft.Json.Linq;
using v00v.Model.Entities;
using v00v.Model.Entities.Instance;
using v00v.Model.Enums;
using v00v.Model.Extensions;
using v00v.Model.SyncEntities;

namespace v00v.Services.ContentProvider
{
    public class YoutubeService : IYoutubeService
    {
        #region Constants

        private const int ItemsPerPage = 50;

        private const string Key = "AIzaSyDfdgAVDXbepYVGivfbgkknu0kYRbC2XwI";

        //private const string Key = "AIzaSyATbiQHQc5byekwpTWuUKbDdIsSURiYhZc";
        private const string PrintType = "prettyPrint=false";
        private const string Url = "https://www.googleapis.com/youtube/v3/";
        private const string YouChannel = "channel";
        private const string YouRegex = @"youtu(?:\.be|be\.com)/(?:.*v(?:/|=)|(?:.*/)?)([a-zA-Z0-9-_]+)";
        private const string YouUser = "user";

        #endregion

        #region Static Methods

        private static async Task<JArray> GetAll(string zap)
        {
            string rawzap = zap;

            var res = new JArray();

            object pagetoken;

            do
            {
                var record = await GetJsonObjectAsync(new Uri(zap)).ConfigureAwait(false);

                res.Add(record);

                pagetoken = record.SelectToken("nextPageToken");

                zap = rawzap + $"&pageToken={pagetoken}";
            }
            while (pagetoken != null);

            return res;
        }

        private static async Task<string> GetChannelIdByUserNameNetAsync(string username)
        {
            var zap = $"{Url}channels?&forUsername={username}&key={Key}&part=snippet&fields=items(id)&{PrintType}";

            return (await GetJsonObjectAsync(new Uri(zap))).SelectToken("items[0].id")?.Value<string>();
        }

        private static async Task<JObject> GetJsonObjectAsync(Uri uri)
        {
            using (var client = new HttpClient())
            {
                return JObject.Parse(await client.GetStringAsync(uri).ConfigureAwait(false));
            }
        }

        private static SyncState GetState(string res)
        {
            switch (res)
            {
                case "unlisted":
                    return SyncState.Unlisted;
                case "public":
                    return SyncState.Added;
                default:
                    return SyncState.Notset;
            }
        }

        #endregion

        #region Methods

        public async Task AddPlaylists(Channel channel)
        {
            if (channel.Playlists.Count != 1)
            {
                return;
            }

            var plu = channel.Playlists.First();
            var playlists =
                (await
                    GetAll($"{Url}playlists?&channelId={channel.Id}&key={Key}&part=snippet&fields=nextPageToken,items(id,snippet(title,channelId,thumbnails(default(url))))&maxResults={ItemsPerPage}&{PrintType}")
                ).SelectTokens("$..items.[*]").ToList();

            var plTasks = playlists.Select(x => x.SelectToken("id")?.Value<string>())
                .Select(pid => new Tuple<string, Task<JArray>>(pid,
                                                               GetAll($"{Url}playlistItems?&key={Key}&playlistId={pid}&part=snippet&order=date&fields=nextPageToken,items(snippet(resourceId(videoId)))&maxResults={ItemsPerPage}&{PrintType}")))
                .ToList();

            await Task.WhenAll(plTasks.Select(x => x.Item2));

            foreach (var plid in playlists)
            {
                var pid = plid.SelectToken("id")?.Value<string>();
                var vids = plTasks.First(x => x.Item1 == pid).Item2.Result.SelectTokens("$..items.[*]")
                    .Select(rec => rec.SelectToken("snippet.resourceId.videoId")?.Value<string>()).Where(x => x != null).Distinct()
                    .ToList();

                if (vids.Count == 0)
                {
                    // empty pl
                    continue;
                }

                var plc = new Playlist
                {
                    Id = pid,
                    ChannelId = channel.Id,
                    Title = plid.SelectToken("snippet.title")?.Value<string>(),
                    ThumbnailLink = plid.SelectToken("snippet.thumbnails.default.url")?.Value<string>()
                };

                plc.Items.AddRange(vids);
                channel.Playlists.Add(plc);
            }

            var unlisted = new List<string>();
            var unlistedTasks = channel.Playlists.Where(x => x.Id != plu.Id).SelectMany(x => x.Items).Distinct().Except(plu.Items)
                .ToList().Split()
                .Select(vid =>
                            GetJsonObjectAsync(new
                                                   Uri($"{Url}videos?id={string.Join(",", vid)}&key={Key}&part=snippet&fields=items(id,snippet(channelId))&{PrintType}")))
                .ToList();

            await Task.WhenAll(unlistedTasks);

            foreach (var task in unlistedTasks)
            {
                unlisted.AddRange(task.Result.SelectTokens("items.[*]")
                                      .Where(x => x.SelectToken("snippet.channelId")?.Value<string>() == channel.Id)
                                      .Select(x => x.SelectToken("id")?.Value<string>()).Where(x => x != null));
            }

            channel.Playlists.ForEach(x => x.Items.RemoveAll(y => !unlisted.Union(plu.Items).Contains(y)));

            if (unlisted.Count > 0)
            {
                var tasks = unlisted.Split()
                    .Select(vid =>
                                GetJsonObjectAsync(new
                                                       Uri($"{Url}videos?id={string.Join(",", vid)}&key={Key}&&part=snippet,contentDetails,statistics&fields=items(id,snippet(publishedAt,title,description,thumbnails(default(url))),contentDetails(duration),statistics(viewCount,commentCount,likeCount,dislikeCount))&{PrintType}")))
                    .ToList();

                await Task.WhenAll(tasks);

                foreach (var vid in tasks)
                {
                    channel.Items.AddRange(vid.Result.SelectTokens("items.[*]").Select(x => new Item
                    {
                        Id = x.SelectToken("id")?.Value<string>(),
                        ChannelId = channel.Id,
                        ChannelTitle = channel.Title,
                        Title =
                            x.SelectToken("snippet.title")
                                ?.Value<string>().RemoveNewLine()
                                .RemoveSpecialCharacters(),
                        Timestamp =
                            x.SelectToken("snippet.publishedAt")
                                ?.Value<DateTime?>()
                            ?? DateTime.MinValue,
                        Description =
                            x.SelectToken("snippet.description")
                                ?.Value<string>(),
                        ViewCount =
                            x.SelectToken("statistics.viewCount")
                                ?.Value<long?>() ?? 0,
                        Comments =
                            x.SelectToken("statistics.commentCount")
                                ?.Value<long?>() ?? 0,
                        LikeCount =
                            x.SelectToken("statistics.likeCount")
                                ?.Value<long?>() ?? 0,
                        DislikeCount =
                            x.SelectToken("statistics.dislikeCount")
                                ?.Value<long?>() ?? 0,
                        ThumbnailLink =
                            x.SelectToken("snippet.thumbnails.default.url")
                                ?.Value<string>(),
                        Duration =
                            x.SelectToken("contentDetails.duration")
                            != null
                                ? (int)XmlConvert
                                    .ToTimeSpan(x
                                                    .SelectToken("contentDetails.duration")
                                                    .Value<string
                                                    >())
                                    .TotalSeconds
                                : 0,
                        SyncState = SyncState.Unlisted
                    }));
                }
            }

            await FillThumbs(channel.Playlists.Where(x => x.Thumb == null).ToList());
            await FillThumbs(channel.Items.Where(x => x.Thumb == null).ToList());

            channel.ItemsCount = channel.Items.Count;
            channel.Playlists.ForEach(x => x.Count = x.Items.Count);
            channel.Timestamp = channel.Items.OrderByDescending(x => x.Timestamp).First().Timestamp;
            if (channel.Items.Any(x => x.SyncState == SyncState.Unlisted))
            {
                var unpl = UnlistedPlaylist.Instance;
                unpl.Id = channel.Id;
                unpl.Order = channel.Playlists.Count;
                unpl.Items.AddRange(channel.Items.Where(x => x.SyncState == SyncState.Unlisted).Select(x => x.Id));
                unpl.Count = unpl.Items.Count;
                channel.Playlists.Add(unpl);
            }
        }

        public async Task FillThumbs(IReadOnlyCollection<Playlist> items)
        {
            var itasks = items.Select(item => new Tuple<string, Task<byte[]>>(item.Id, GetStreamFromUrl(item.ThumbnailLink))).ToList();

            await Task.WhenAll(itasks.Select(x => x.Item2));

            foreach (var item in items)
            {
                item.Thumbnail = itasks.First(x => x.Item1 == item.Id).Item2.Result;
                item.ThumbnailLink = null;
            }
        }

        public async Task<Channel> GetChannelAsync(string channelId, bool withoutPl, string channelTitle = null)
        {
            var record = await GetJsonObjectAsync(new Uri(channelTitle == null
                                                              ? $"{Url}channels?&id={channelId}&key={Key}&part=contentDetails,snippet,statistics&fields=items(contentDetails(relatedPlaylists),snippet(title,description,thumbnails(default(url))),statistics(viewCount,subscriberCount))&{PrintType}"
                                                              : $"{Url}channels?&id={channelId}&key={Key}&part=contentDetails,snippet,statistics&fields=items(contentDetails(relatedPlaylists),snippet(description,thumbnails(default(url))),statistics(viewCount,subscriberCount))&{PrintType}"));

            if (!record.SelectToken("items").Any())
            {
                // banned channel
                return null;
            }

            // channel
            var channel = new Channel
            {
                Id = channelId,
                Title = channelTitle ?? record.SelectToken("items[0].snippet.title", false)?.Value<string>(),
                SubTitle = record.SelectToken("items[0].snippet.description", false)?.Value<string>().WordWrap(120),
                ViewCount = record.SelectToken("items[0].statistics.viewCount", false)?.Value<long>() ?? 0,
                SubsCount = record.SelectToken("items[0].statistics.subscriberCount", false)?.Value<long>() ?? 0,
                Thumbnail = await GetStreamFromUrl(record.SelectToken("items[0].snippet.thumbnails.default.url", false)
                                                       ?.Value<string>())
            };

            // uploads

            var upload =
                await
                    GetJsonObjectAsync(new
                                           Uri($"{Url}playlists?&id={record.SelectToken("items[0].contentDetails.relatedPlaylists").SelectToken("uploads")?.Value<string>()}&key={Key}&part=snippet&fields=items(id,snippet(title,thumbnails(default(url))))&{PrintType}"));

            var plu = new Playlist
            {
                Id = upload.SelectToken("items[0].id")?.Value<string>(),
                ChannelId = channel.Id,
                Title = upload.SelectToken("items[0].snippet.title")?.Value<string>(),
                ThumbnailLink = upload.SelectToken("items[0].snippet.thumbnails.default.url", false)?.Value<string>(),
            };

            var uploadsItems =
                (await
                    GetAll($"{Url}playlistItems?&key={Key}&playlistId={plu.Id}&part=snippet,contentDetails&order=date&fields=nextPageToken,items(snippet(publishedAt,channelId,title,description,thumbnails(default(url)),resourceId(videoId)),contentDetails(videoPublishedAt))&maxResults={ItemsPerPage}&{PrintType}")
                ).SelectTokens("$..items.[*]").Select(x => new Item
                {
                    Id = x.SelectToken("snippet.resourceId.videoId")?.Value<string>(),
                    ChannelTitle = channel.Title,
                    Description = x.SelectToken("snippet.description")?.Value<string>(),
                    ChannelId = x.SelectToken("snippet.channelId")?.Value<string>(),
                    Title =
                        x.SelectToken("snippet.title")?.Value<string>().RemoveNewLine()
                            .RemoveSpecialCharacters(),
                    Timestamp =
                        x.SelectToken("contentDetails.videoPublishedAt", false)
                            ?.Value<DateTime?>() ?? DateTime.MinValue,
                    ThumbnailLink = x.SelectToken("snippet.thumbnails.default.url", false)
                        .Value<string>()
                }).ToList();

            plu.Items.AddRange(uploadsItems.Select(x => x.Id));
            channel.Items.AddRange(uploadsItems);
            channel.Playlists.Add(plu);

            await SetItemsStatistic(channel, true, plu.Items);

            if (withoutPl)
            {
                await FillThumbs(channel.Items);
                await FillThumbs(channel.Playlists);
                return channel;
            }
            // end uploads

            var playlists =
                (await
                    GetAll($"{Url}playlists?&channelId={channel.Id}&key={Key}&part=snippet&fields=nextPageToken,items(id,snippet(title,channelId,thumbnails(default(url))))&maxResults={ItemsPerPage}&{PrintType}")
                ).SelectTokens("$..items.[*]").ToList();

            var plTasks = playlists.Select(x => x.SelectToken("id")?.Value<string>())
                .Select(pid => new Tuple<string, Task<JArray>>(pid,
                                                               GetAll($"{Url}playlistItems?&key={Key}&playlistId={pid}&part=snippet&order=date&fields=nextPageToken,items(snippet(resourceId(videoId)))&maxResults={ItemsPerPage}&{PrintType}")))
                .ToList();

            await Task.WhenAll(plTasks.Select(x => x.Item2));

            foreach (var plid in playlists)
            {
                var pid = plid.SelectToken("id")?.Value<string>();
                var vids = plTasks.First(x => x.Item1 == pid).Item2.Result.SelectTokens("$..items.[*]")
                    .Select(rec => rec.SelectToken("snippet.resourceId.videoId")?.Value<string>()).Where(x => x != null).Distinct()
                    .ToList();

                if (vids.Count == 0)
                {
                    // empty pl
                    continue;
                }

                var plc = new Playlist
                {
                    Id = pid,
                    ChannelId = channel.Id,
                    Title = plid.SelectToken("snippet.title")?.Value<string>(),
                    ThumbnailLink = plid.SelectToken("snippet.thumbnails.default.url")?.Value<string>()
                };

                plc.Items.AddRange(vids);
                channel.Playlists.Add(plc);
            }

            var unlisted = new List<string>();
            var unlistedTasks = channel.Playlists.Where(x => x.Id != plu.Id).SelectMany(x => x.Items).Distinct().Except(plu.Items)
                .ToList().Split()
                .Select(vid =>
                            GetJsonObjectAsync(new
                                                   Uri($"{Url}videos?id={string.Join(",", vid)}&key={Key}&part=snippet&fields=items(id,snippet(channelId))&{PrintType}")))
                .ToList();

            await Task.WhenAll(unlistedTasks);

            foreach (var task in unlistedTasks)
            {
                unlisted.AddRange(task.Result.SelectTokens("items.[*]")
                                      .Where(x => x.SelectToken("snippet.channelId")?.Value<string>() == channelId)
                                      .Select(x => x.SelectToken("id")?.Value<string>()).Where(x => x != null));
            }

            channel.Playlists.ForEach(x => x.Items.RemoveAll(y => !unlisted.Union(plu.Items).Contains(y)));

            if (unlisted.Count > 0)
            {
                var tasks = unlisted.Split()
                    .Select(vid =>
                                GetJsonObjectAsync(new
                                                       Uri($"{Url}videos?id={string.Join(",", vid)}&key={Key}&&part=snippet,contentDetails,statistics&fields=items(id,snippet(publishedAt,title,description,thumbnails(default(url))),contentDetails(duration),statistics(viewCount,commentCount,likeCount,dislikeCount))&{PrintType}")))
                    .ToList();

                await Task.WhenAll(tasks);

                foreach (var vid in tasks)
                {
                    channel.Items.AddRange(vid.Result.SelectTokens("items.[*]").Select(x => new Item
                    {
                        Id = x.SelectToken("id")?.Value<string>(),
                        ChannelId = channel.Id,
                        ChannelTitle = channel.Title,
                        Title =
                            x.SelectToken("snippet.title")
                                ?.Value<string>().RemoveNewLine()
                                .RemoveSpecialCharacters(),
                        Timestamp =
                            x.SelectToken("snippet.publishedAt")
                                ?.Value<DateTime?>()
                            ?? DateTime.MinValue,
                        Description =
                            x.SelectToken("snippet.description")
                                ?.Value<string>(),
                        ViewCount =
                            x.SelectToken("statistics.viewCount")
                                ?.Value<long?>() ?? 0,
                        Comments =
                            x.SelectToken("statistics.commentCount")
                                ?.Value<long?>() ?? 0,
                        LikeCount =
                            x.SelectToken("statistics.likeCount")
                                ?.Value<long?>() ?? 0,
                        DislikeCount =
                            x.SelectToken("statistics.dislikeCount")
                                ?.Value<long?>() ?? 0,
                        ThumbnailLink =
                            x.SelectToken("snippet.thumbnails.default.url")
                                ?.Value<string>(),
                        Duration =
                            x.SelectToken("contentDetails.duration")
                            != null
                                ? (int)XmlConvert
                                    .ToTimeSpan(x
                                                    .SelectToken("contentDetails.duration")
                                                    .Value<string
                                                    >())
                                    .TotalSeconds
                                : 0,
                        SyncState = SyncState.Unlisted
                    }));
                }
            }

            await FillThumbs(channel.Playlists);
            await FillThumbs(channel.Items);

            channel.ItemsCount = channel.Items.Count;
            channel.Playlists.ForEach(x => x.Count = x.Items.Count);
            channel.Timestamp = channel.Items.OrderByDescending(x => x.Timestamp).First().Timestamp;
            if (channel.Items.Any(x => x.SyncState == SyncState.Unlisted))
            {
                var unpl = UnlistedPlaylist.Instance;
                unpl.IsStatePlaylist = false;
                unpl.Id = channel.Id;
                unpl.Order = channel.Playlists.Count;
                unpl.Items.AddRange(channel.Items.Where(x => x.SyncState == SyncState.Unlisted).Select(x => x.Id));
                unpl.Count = unpl.Items.Count;
                channel.Playlists.Add(unpl);
            }

            return channel;
        }

        public async Task<ChannelDiff> GetChannelDiffAsync(ChannelStruct cs, bool syncPls, Action<string> setLog)
        {
            var diff = new ChannelDiff(cs.ChannelId, cs.ChannelTitle, syncPls);

            var record = await GetJsonObjectAsync(new Uri(syncPls
                                                              ? $"{Url}channels?&key={Key}&id={cs.ChannelId}&part=contentDetails,snippet,statistics&fields=items(contentDetails(relatedPlaylists),snippet(description),statistics(viewCount,subscriberCount))&{PrintType}"
                                                              : $"{Url}channels?&key={Key}&id={cs.ChannelId}&part=contentDetails,statistics&fields=items(contentDetails(relatedPlaylists),statistics(viewCount,subscriberCount))&{PrintType}"));

            if (!record.SelectToken("items").Any())
            {
                setLog?.Invoke($"Sync fail: {cs.ChannelTitle}");
                diff.Faulted = true;
                return diff;
            }

            diff.ViewCount = record.SelectToken("items[0].statistics.viewCount")?.Value<long>() ?? 0;
            diff.SubsCount = record.SelectToken("items[0].statistics.subscriberCount")?.Value<long>() ?? 0;

            if (syncPls)
            {
                diff.Description = record.SelectToken("items[0].snippet.description", false)?.Value<string>().WordWrap(120);
            }

            var upId = record.SelectToken("items[0].contentDetails.relatedPlaylists").SelectToken("uploads").Value<string>();

            var uploadvids =
                (await
                    GetAll($"{Url}playlistItems?&key={Key}&playlistId={upId}&part=snippet&order=date&fields=nextPageToken,items(snippet(resourceId(videoId)))&maxResults={ItemsPerPage}&{PrintType}")
                ).SelectTokens("$..items.[*]").Select(rec => rec.SelectToken("snippet.resourceId.videoId")?.Value<string>())
                .Where(x => x != null).Distinct().ToList();

            diff.UploadedIds.AddRange(uploadvids);
            diff.AddedItems.AddRange(uploadvids.Except(cs.Items).Select(x => new ItemPrivacy { Id = x, Status = SyncState.Added }));
            diff.DeletedItems.AddRange(cs.Items.Except(uploadvids));

            if (!syncPls)
            {
                setLog?.Invoke($"{diff.ChannelTitle}, added: {diff.AddedItems.Count}, deleted: {diff.DeletedItems.Count}");
                return diff;
            }

            var playlists =
                (await
                    GetAll($"{Url}playlists?&channelId={cs.ChannelId}&key={Key}&part=snippet&fields=nextPageToken,items(id,snippet(title,channelId,thumbnails(default(url))))&maxResults={ItemsPerPage}&{PrintType}")
                ).SelectTokens("$..items.[*]").ToList();

            var plTasks = playlists.Select(x => x.SelectToken("id")?.Value<string>())
                .Select(pid => new Tuple<string, Task<JArray>>(pid,
                                                               GetAll($"{Url}playlistItems?&key={Key}&playlistId={pid}&part=snippet,status&order=date&fields=nextPageToken,items(snippet(resourceId(videoId)),status(privacyStatus))&maxResults={ItemsPerPage}&{PrintType}")))
                .ToList();

            await Task.WhenAll(plTasks.Select(x => x.Item2));

            foreach (var plid in playlists)
            {
                var pid = plid.SelectToken("id")?.Value<string>();

                var vids = plTasks.First(x => x.Item1 == pid).Item2.Result.SelectTokens("$..items.[*]")
                    .Select(x => new
                    {
                        id = x.SelectToken("snippet.resourceId.videoId")?.Value<string>(),
                        status = x.SelectToken("status.privacyStatus")?.Value<string>()
                    }).GroupBy(x => x.id).ToDictionary(g => g.Key, g => g.FirstOrDefault()?.status);

                if (vids.Count == 0)
                {
                    // empty pl
                    continue;
                }

                if (pid != null && cs.Playlists.Contains(pid))
                {
                    diff.ExistPls.Add(pid, vids.Select(x => new ItemPrivacy { Id = x.Key, Status = GetState(x.Value) }).ToList());
                }
                else if (pid != null && !cs.Playlists.Contains(pid))
                {
                    diff.AddedPls.Add(new Playlist
                                      {
                                          Id = pid,
                                          ChannelId = cs.ChannelId,
                                          Title = plid.SelectToken("snippet.title")?.Value<string>(),
                                          ThumbnailLink = plid.SelectToken("snippet.thumbnails.default.url")?.Value<string>()
                                      },
                                      vids.Select(x => new ItemPrivacy { Id = x.Key, Status = GetState(x.Value) }).ToList());
                }
            }

            diff.ExistPls.Add(upId, uploadvids.Select(x => new ItemPrivacy { Id = x, Status = SyncState.Added }).ToList());
            diff.DeletedPls.AddRange(cs.Playlists.Where(z => z != upId)
                                         .Except(diff.ExistPls.Select(x => x.Key).Union(diff.AddedPls.Select(x => x.Key.Id))));

            var rawIds = diff.AddedPls.SelectMany(x => x.Value.Select(y => y.Id))
                .Union(diff.ExistPls.SelectMany(x => x.Value.Select(y => y.Id))).Except(diff.AddedItems.Select(x => x.Id).Union(cs.Items))
                .ToList();

            var unlistedTasks = rawIds.Split()
                .Select(vid =>
                            GetJsonObjectAsync(new
                                                   Uri($"{Url}videos?id={string.Join(",", vid)}&key={Key}&part=snippet&fields=items(id,snippet(channelId))&{PrintType}")))
                .ToList();

            await Task.WhenAll(unlistedTasks);

            var unlisted = new List<string>();
            foreach (var task in unlistedTasks)
            {
                unlisted.AddRange(task.Result.SelectTokens("items.[*]")
                                      .Where(x => x.SelectToken("snippet.channelId")?.Value<string>() == cs.ChannelId)
                                      .Select(x => x.SelectToken("id")?.Value<string>()).Where(x => x != null));
            }

            diff.AddedItems.AddRange(unlisted.Except(diff.AddedItems.Select(x => x.Id))
                                         .Select(y => new ItemPrivacy { Id = y, Status = SyncState.Unlisted }));

            foreach (var pair in diff.AddedPls)
            {
                pair.Value.RemoveAll(y => !unlisted.Union(cs.Items).Union(diff.AddedItems.Select(x => x.Id)).Contains(y.Id));
            }

            foreach (var pair in diff.ExistPls.Where(x => x.Key != upId))
            {
                pair.Value.RemoveAll(y => !unlisted.Union(cs.Items).Union(diff.AddedItems.Select(x => x.Id)).Contains(y.Id));
            }

            diff.DeletedItems.RemoveAll(x => diff.ExistPls.SelectMany(y => y.Value).Select(y => y.Id)
                                            .Union(diff.AddedPls.SelectMany(y => y.Value).Select(y => y.Id)).Contains(x));

            return diff;
        }

        public async Task<string> GetChannelId(string inputChannelLink)
        {
            var parsedChannelId = string.Empty;
            var sp = inputChannelLink.Split('/');
            if (sp.Length > 1)
            {
                if (sp.Contains(YouUser))
                {
                    var indexuser = Array.IndexOf(sp, YouUser);
                    if (indexuser < 0)
                    {
                        return string.Empty;
                    }

                    var user = sp[indexuser + 1];
                    parsedChannelId = await GetChannelIdByUserNameNetAsync(user);
                }
                else if (sp.Contains(YouChannel))
                {
                    var indexchannel = Array.IndexOf(sp, YouChannel);
                    if (indexchannel < 0)
                    {
                        return string.Empty;
                    }

                    parsedChannelId = sp[indexchannel + 1];
                    var appSp = parsedChannelId.Split('?');
                    if (appSp.Length > 1)
                    {
                        parsedChannelId = appSp[0];
                    }
                }
                else
                {
                    var regex = new Regex(YouRegex);
                    var match = regex.Match(inputChannelLink);
                    if (!match.Success)
                    {
                        return parsedChannelId;
                    }

                    var zap =
                        $"{Url}videos?&id={match.Groups[1].Value}&key={Key}&part=snippet&fields=items(snippet(channelId))&{PrintType}";

                    parsedChannelId = (await GetJsonObjectAsync(new Uri(zap))).SelectToken("items[0].snippet.channelId")?.Value<string>();
                }
            }
            else
            {
                parsedChannelId = await GetChannelIdByUserNameNetAsync(inputChannelLink) ?? inputChannelLink;
            }

            return parsedChannelId;
        }

        public async Task<List<Item>> GetItems(Dictionary<string, SyncPrivacy> privacyItems)
        {
            var tasks = privacyItems.Select(x => x.Key).ToList().Split()
                .Select(vid =>
                            $"{Url}videos?id={string.Join(",", vid)}&key={Key}&part=snippet,contentDetails,statistics&fields=items(id,snippet(publishedAt,title,description,thumbnails(default(url))),contentDetails(duration),statistics(viewCount,commentCount,likeCount,dislikeCount))&{PrintType}")
                .Select(zap => GetJsonObjectAsync(new Uri(zap))).ToList();

            await Task.WhenAll(tasks);

            var newItems = new List<Item>();

            foreach (var vid in tasks)
            {
                newItems.AddRange(vid.Result.SelectTokens("items.[*]").Where(x => x.SelectToken("id")?.Value<string>() != null)
                                      .Select(x => new Item
                                      {
                                          Id = x.SelectToken("id").Value<string>(),
                                          ChannelId = privacyItems[x.SelectToken("id").Value<string>()].ChannelId,
                                          ChannelTitle = privacyItems[x.SelectToken("id").Value<string>()].ChannelTitle,
                                          Title =
                                              x.SelectToken("snippet.title")?.Value<string>().RemoveNewLine()
                                                  .RemoveSpecialCharacters(),
                                          Timestamp =
                                              x.SelectToken("snippet.publishedAt")?.Value<DateTime?>() ?? DateTime.MinValue,
                                          Description = x.SelectToken("snippet.description")?.Value<string>(),
                                          ViewCount = x.SelectToken("statistics.viewCount")?.Value<long?>() ?? 0,
                                          Comments = x.SelectToken("statistics.commentCount")?.Value<long?>() ?? 0,
                                          LikeCount = x.SelectToken("statistics.likeCount")?.Value<long?>() ?? 0,
                                          DislikeCount = x.SelectToken("statistics.dislikeCount")?.Value<long?>() ?? 0,
                                          ThumbnailLink = x.SelectToken("snippet.thumbnails.default.url")?.Value<string>(),
                                          Duration = x.SelectToken("contentDetails.duration") != null
                                              ? (int)XmlConvert
                                                  .ToTimeSpan(x.SelectToken("contentDetails.duration").Value<string>())
                                                  .TotalSeconds
                                              : 0,
                                          SyncState = privacyItems[x.SelectToken("id").Value<string>()].Status
                                      }));
            }

            await FillThumbs(newItems);

            return newItems;
        }

        public async Task<List<Item>> GetPopularItems(string country, IEnumerable<string> existChannelsIds)
        {
            string zap =
                $"{Url}videos?chart=mostPopular&key={Key}&maxResults={ItemsPerPage}&regionCode={country}&safeSearch=none&part=snippet&fields=items(id,snippet(channelId))&{PrintType}";

            var res = await GetJsonObjectAsync(new Uri(zap)).ConfigureAwait(false);

            var ids = res.SelectTokens("items.[*]").Where(x => x.SelectToken("id")?.Value<string>() != null)
                .ToDictionary(y => y.SelectToken("id")?.Value<string>(),
                              y => new SyncPrivacy
                              {
                                  ChannelId = y.SelectToken("snippet.channelId")?.Value<string>(),
                                  Status = existChannelsIds.Contains(y.SelectToken("snippet.channelId")?.Value<string>())
                                      ? SyncState.Added
                                      : SyncState.Notset
                              });

            return await GetItems(ids);
        }

        public async Task<List<Channel>> GetRelatedChannelsAsync(string channelId, IEnumerable<string> existChannels)
        {
            string zap =
                $"{Url}channels?id={channelId}&key={Key}&part=brandingSettings&fields=items(brandingSettings(channel(featuredChannelsUrls)))&{PrintType}";

            var res = await GetJsonObjectAsync(new Uri(zap)).ConfigureAwait(false);

            if (!res.SelectToken("items").Any())
            {
                return null;
            }

            var tasks = res.SelectToken("items[0].brandingSettings.channel.featuredChannelsUrls").Select(x => x.Value<string>())
                .Where(x => !existChannels.Contains(x)).Select(item => GetChannelAsync(item, true)).ToList();

            var rrr = Task.WhenAll(tasks);
            return new List<Channel>(rrr.Result);
        }

        public async Task<List<Item>> GetSearchedItems(string searchText, IEnumerable<string> existChannelsIds, string region)
        {
            string zap =
                $"{Url}search?&q={searchText}&key={Key}&maxResults={ItemsPerPage}&regionCode={region}&safeSearch=none&part=snippet&fields=items(id(videoId),snippet(channelId))&{PrintType}";

            var res = await GetJsonObjectAsync(new Uri(zap)).ConfigureAwait(false);

            var ids = res.SelectTokens("items.[*]").Where(x => x.SelectToken("id.videoId")?.Value<string>() != null)
                .ToDictionary(y => y.SelectToken("id.videoId")?.Value<string>(),
                              y => new SyncPrivacy
                              {
                                  ChannelId = y.SelectToken("snippet.channelId")?.Value<string>(),
                                  Status = existChannelsIds.Contains(y.SelectToken("snippet.channelId")?.Value<string>())
                                      ? SyncState.Added
                                      : SyncState.Notset
                              });

            return await GetItems(ids);
        }

        public async Task<byte[]> GetStreamFromUrl(string dataurl)
        {
            if (string.IsNullOrEmpty(dataurl))
            {
                return new byte[0];
            }

            using (var wc = new WebClient())
            {
                var res = await wc.DownloadDataTaskAsync(dataurl);
                using (var ms = new MemoryStream(res))
                {
                    try
                    {
                        var bitmap = new Bitmap(ms);
                        return bitmap.Size != Size.Empty ? res : new byte[0];
                    }
                    catch
                    {
                        return new byte[0];
                    }
                }
            }
        }

        public Task<List<string>> GetVideoCommentsAsync(string itemlId, int maxResult)
        {
            //string zap =
            //$"{url}commentThreads?videoId={videoID}&key={key}&maxResults={itemsppage}&part=snippet&fields=nextPageToken,items(snippet(topLevelComment(snippet(authorDisplayName,textDisplay,authorChannelUrl,authorProfileImageUrl,publishedAt))))&{printType}";
            throw new NotImplementedException();
        }

        public async Task SetItemsStatistic(Channel channel, bool isDur, IEnumerable<string> ids = null)
        {
            if (ids == null)
            {
                ids = channel.Items.Select(x => x.Id);
            }

            var uploadTasks = ids.ToList().Split().Select(s => GetJsonObjectAsync(new Uri(isDur
                                                                                              ? $"{Url}videos?id={string.Join(",", s)}&key={Key}&part=contentDetails,statistics&fields=items(id,contentDetails(duration),statistics(viewCount,commentCount,likeCount,dislikeCount))&{PrintType}"
                                                                                              : $"{Url}videos?id={string.Join(",", s)}&key={Key}&part=snippet,statistics&fields=items(id,snippet(description),statistics(viewCount,commentCount,likeCount,dislikeCount))&{PrintType}")))
                .ToList();

            await Task.WhenAll(uploadTasks);

            var uploadDetails = new List<JToken>();
            foreach (var task in uploadTasks)
            {
                uploadDetails.AddRange(task.Result.SelectTokens("items.[*]"));
            }

            foreach (var rec in uploadDetails)
            {
                var item = channel.Items.FirstOrDefault(x => x.Id == rec.SelectToken("id")?.Value<string>());
                if (item == null)
                {
                    continue;
                }

                long vc = rec.SelectToken("statistics.viewCount")?.Value<long?>() ?? 0;
                if (vc > 0)
                {
                    item.ViewCount = vc;
                }

                long comm = rec.SelectToken("statistics.commentCount")?.Value<long?>() ?? 0;
                if (comm > 0)
                {
                    item.Comments = comm;
                }

                long like = rec.SelectToken("statistics.likeCount")?.Value<long?>() ?? 0;
                if (like > 0)
                {
                    item.LikeCount = like;
                }

                long dlike = rec.SelectToken("statistics.dislikeCount")?.Value<long?>() ?? 0;
                if (dlike > 0)
                {
                    item.DislikeCount = dlike;
                }

                var descr = rec.SelectToken("snippet.description")?.Value<string>();
                if (descr != null)
                {
                    item.Description = descr;
                }

                if (isDur)
                {
                    var dur = rec.SelectToken("contentDetails.duration");
                    item.Duration = dur != null ? (int)XmlConvert.ToTimeSpan(dur.Value<string>()).TotalSeconds : 0;
                }
            }
        }

        private async Task FillThumbs(IReadOnlyCollection<Item> items)
        {
            var itasks = items.Select(item => new Tuple<string, Task<byte[]>>(item.Id, GetStreamFromUrl(item.ThumbnailLink))).ToList();

            await Task.WhenAll(itasks.Select(x => x.Item2));

            foreach (var item in items)
            {
                item.Thumbnail = itasks.First(x => x.Item1 == item.Id).Item2.Result;
                item.ThumbnailLink = null;
            }
        }

        #endregion

        //private string Key => _keys[new Random().Next(0, _keys.Length)];
        //private readonly string[] _keys = { "AIzaSyDfdgAVDXbepYVGivfbgkknu0kYRbC2XwI", "AIzaSyATbiQHQc5byekwpTWuUKbDdIsSURiYhZc" };
    }
}
