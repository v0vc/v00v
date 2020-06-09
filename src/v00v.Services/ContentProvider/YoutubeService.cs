using System;
using System.Collections.Concurrent;
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
using v00v.Services.Backup;

namespace v00v.Services.ContentProvider
{
    public class YoutubeService : IYoutubeService
    {
        #region Constants

        private const string CommentDown =
            "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAACXBIWXMAAA7EAAAOxAGVKw4bAAAAoElEQVRYhe2WoQ6EQAwFZy8oFOZ+dQW/jGA9lwOBQVCyXQo1fclTm3QmK5om9JmATnj7Ad+GmaoswCp00Q77mKo1JARCIARCwF3gLJl9pUqV1vBxHUvNtRJjBUjbUfsTlhJquKVEM9xC4jb8joQZvEXCHK6ReAxeI/E4/EriNfiZxOvwo4QbPGKSBPT43QX/BMzA4CRQ3C8id4EOKI78sgGr+p2V3rS57wAAAABJRU5ErkJggg==";

        private const string CommentUp =
            "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAAwUlEQVRYR+3VvQrCMBSG4bdSFycXb7WDzjp4k15AB90VkUCFDGnOTwpZTtZAv6dfwskAPIAjfdZrAJ4B6N3AAdj1uQJ80x3ougLQ2sB5Ob+b9xxbACn8ugRfABfCC8jD/z/vQngApXA3wgqohbsQFoAm3IzQAizhJoQG4AlXIyRAS7gKUQNsES4i1gBbhlcRJcCUTbjShB2Fsfup7Kdhdc/3pTtQ+tYbWEOk8L3lXQhANBANRAPRgKeBWRjFJ8so/gFrRzjXxWFROwAAAABJRU5ErkJggg==";

        private const string HrefRegex = @"<a\s+(?:[^>]*?\s+)?href=([""'])(.*?)\1>";
        private const int ItemsPerPage = 50;
        private const string PrintType = "prettyPrint=false";
        private const string Url = "https://www.googleapis.com/youtube/v3/";
        private const string YouChannel = "channel";
        private const string YouRegex = @"youtu(?:\.be|be\.com)/(?:.*v(?:/|=)|(?:.*/)?)([a-zA-Z0-9-_]+)";
        private const string YouUser = "user";

        #endregion

        #region Fields

        private bool _inited;

        private string _key;

        #endregion

        #region Properties

        public string ChannelLink => "https://www.youtube.com/channel/";
        public string ItemLink => "https://www.youtube.com/watch?v=";
        public string PlaylistLink => "https://www.youtube.com/playlist?list=";

        #endregion

        #region Static Methods

        private static async Task<JArray> GetAll(string zap)
        {
            var rawzap = zap;
            var res = new JArray();
            object pagetoken;
            do
            {
                var record = await GetJsonObjectAsync(new Uri(zap));
                if (record == null)
                {
                    break;
                }

                res.Add(record);
                pagetoken = record.SelectToken("nextPageToken");
                if (pagetoken != null)
                {
                    zap = $"{rawzap}&pageToken={pagetoken}";
                }
            }
            while (pagetoken != null);

            return res;
        }

        private static string GetCommentText(JToken x, string levelName, Regex hrefRegex)
        {
            return hrefRegex.Replace(x.SelectToken(levelName)?.Value<string>().Replace("&quot;", @"""").Replace("<br />", " ")
                                         .Replace("</a>", " ").Replace("<b>", string.Empty).Replace("</b>", string.Empty)
                                         .Replace("&gt;", ">").Replace("&lt;", "<").Replace("&#39;", "'").RemoveSpecialCharacters()
                                     ?? string.Empty,
                                     string.Empty);
        }

        private static async Task<JObject> GetJsonObjectAsync(Uri uri)
        {
            using (var client = new HttpClient())
            {
                using (var response = await client.GetAsync(uri))
                {
                    return !response.IsSuccessStatusCode ? null : JObject.Parse(await response.Content.ReadAsStringAsync());
                }
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

        private static Item MakeItem(JToken x, string channelId, string cTitle)
        {
            return new Item
            {
                Id = x.SelectToken("id")?.Value<string>(),
                ChannelId = channelId,
                ChannelTitle = cTitle,
                Title = x.SelectToken("snippet.title")?.Value<string>().RemoveNewLine().RemoveSpecialCharacters(),
                Timestamp = x.SelectToken("snippet.publishedAt")?.Value<DateTime?>() ?? DateTime.MinValue,
                Description = x.SelectToken("snippet.description")?.Value<string>(),
                ViewCount = x.SelectToken("statistics.viewCount")?.Value<long?>() ?? 0,
                Comments = x.SelectToken("statistics.commentCount")?.Value<long?>() ?? 0,
                LikeCount = x.SelectToken("statistics.likeCount")?.Value<long?>() ?? 0,
                DislikeCount = x.SelectToken("statistics.dislikeCount")?.Value<long?>() ?? 0,
                ThumbnailLink = x.SelectToken("snippet.thumbnails.default.url")?.Value<string>(),
                Duration = x.SelectToken("contentDetails.duration") != null
                    ? (int)XmlConvert.ToTimeSpan(x.SelectToken("contentDetails.duration").Value<string>()).TotalSeconds
                    : 0,
                SyncState = SyncState.Unlisted
            };
        }

        private static Item MakeItem(JToken x, string cTitle)
        {
            return new Item
            {
                Id = x.SelectToken("snippet.resourceId.videoId")?.Value<string>(),
                ChannelTitle = cTitle,
                Description = x.SelectToken("snippet.description")?.Value<string>(),
                ChannelId = x.SelectToken("snippet.channelId")?.Value<string>(),
                Title = x.SelectToken("snippet.title")?.Value<string>().RemoveNewLine().RemoveSpecialCharacters(),
                Timestamp = x.SelectToken("contentDetails.videoPublishedAt", false)?.Value<DateTime?>() ?? DateTime.MinValue,
                ThumbnailLink = x.SelectToken("snippet.thumbnails.default.url", false).Value<string>()
            };
        }

        #endregion

        #region Methods

        public async Task AddPlaylists(Channel channel)
        {
            InitKey();

            if (channel.Playlists.Count != 1)
            {
                return;
            }

            var plu = channel.Playlists.First();

            channel.Items.AddRange((await
                                       GetAll($"{Url}playlistItems?&key={_key}&playlistId={plu.Id}&part=snippet,contentDetails&order=date&fields=nextPageToken,items(snippet(publishedAt,channelId,title,description,thumbnails(default(url)),resourceId(videoId)),contentDetails(videoPublishedAt))&maxResults={ItemsPerPage}&{PrintType}")
                                   ).SelectTokens("$..items.[*]").Select(x => MakeItem(x, channel.Title)).Skip(ItemsPerPage));

            plu.Items.Clear();

            plu.Items.AddRange(channel.Items.Select(x => x.Id));

            await SetItemsStatistic(channel, true, plu.Items);

            var cId = channel.Id;

            var playlists =
                (await
                    GetAll($"{Url}playlists?&channelId={channel.Id}&key={_key}&part=snippet&fields=nextPageToken,items(id,snippet(title,channelId,thumbnails(default(url))))&maxResults={ItemsPerPage}&{PrintType}")
                ).SelectTokens("$..items.[*]").ToHashSet();

            var plTasks = playlists.Select(x => x.SelectToken("id")?.Value<string>())
                .Select(pid => new Tuple<string, Task<JArray>>(pid,
                                                               GetAll($"{Url}playlistItems?&key={_key}&playlistId={pid}&part=snippet&order=date&fields=nextPageToken,items(snippet(resourceId(videoId)))&maxResults={ItemsPerPage}&{PrintType}")))
                .ToHashSet();

            await Task.WhenAll(plTasks.Select(x => x.Item2));

            var plBag = new ConcurrentBag<Playlist>();
            Parallel.ForEach(playlists,
                             plid =>
                             {
                                 var pid = plid.SelectToken("id")?.Value<string>();
                                 var vids = plTasks.First(x => x.Item1 == pid).Item2.Result.SelectTokens("$..items.[*]")
                                     .Select(rec => rec.SelectToken("snippet.resourceId.videoId")?.Value<string>()).Where(x => x != null)
                                     .Distinct().ToHashSet();

                                 if (vids.Count != 0)
                                 {
                                     // not empty pl
                                     var plc = new Playlist
                                     {
                                         Id = pid,
                                         ChannelId = channel.Id,
                                         Title = plid.SelectToken("snippet.title")?.Value<string>(),
                                         ThumbnailLink = plid.SelectToken("snippet.thumbnails.default.url")?.Value<string>()
                                     };

                                     plc.Items.AddRange(vids);
                                     plBag.Add(plc);
                                 }
                             });

            channel.Playlists.AddRange(plBag);

            var unlistedTasks = channel.Playlists.Where(x => x.Id != plu.Id).SelectMany(x => x.Items).Distinct().Except(plu.Items)
                .ToList().Split()
                .Select(vid =>
                            GetJsonObjectAsync(new
                                                   Uri($"{Url}videos?id={string.Join(",", vid)}&key={_key}&part=snippet&fields=items(id,snippet(channelId))&{PrintType}")))
                .ToHashSet();

            await Task.WhenAll(unlistedTasks);

            var unlisted = unlistedTasks.AsParallel().SelectMany(x => x.Result.SelectTokens("items.[*]")
                                                                     .Where(y => y.SelectToken("snippet.channelId")?.Value<string>()
                                                                                 == cId).Select(z => z.SelectToken("id")?.Value<string>())
                                                                     .Where(k => k != null)).ToList();
            Parallel.ForEach(channel.Playlists,
                             x =>
                             {
                                 x.Items.RemoveAll(y => !unlisted.Union(plu.Items).Contains(y));
                             });

            if (unlisted.Count > 0)
            {
                var tasks = unlisted.Split()
                    .Select(vid =>
                                GetJsonObjectAsync(new
                                                       Uri($"{Url}videos?id={string.Join(",", vid)}&key={_key}&&part=snippet,contentDetails,statistics&fields=items(id,snippet(publishedAt,title,description,thumbnails(default(url))),contentDetails(duration),statistics(viewCount,commentCount,likeCount,dislikeCount))&{PrintType}")))
                    .ToHashSet();

                await Task.WhenAll(tasks);

                var cTitle = channel.Title;
                channel.Items.AddRange(tasks.AsParallel()
                                           .SelectMany(i => i.Result.SelectTokens("items.[*]").Select(x => MakeItem(x, cId, cTitle))));
            }

            await Task.WhenAll(FillThumbs(channel.Items.Where(x => x.Thumb == null).ToHashSet()),
                               FillThumbs(channel.Playlists.Where(x => x.Thumb == null).ToHashSet()));

            channel.ItemsCount = channel.Items.Count;
            Parallel.ForEach(channel.Playlists,
                             x =>
                             {
                                 x.Count = x.Items.Count;
                             });
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
            var itasks = items.Select(item => new Tuple<string, Task<byte[]>>(item.Id, GetStreamFromUrl(item.ThumbnailLink))).ToHashSet();

            await Task.WhenAll(itasks.Select(x => x.Item2));

            Parallel.ForEach(items,
                             item =>
                             {
                                 item.Thumbnail = itasks.First(x => x.Item1 == item.Id).Item2.Result;
                                 item.ThumbnailLink = null;
                             });
        }

        public async Task<Channel> GetChannelAsync(string channelId, bool withoutPl, string channelTitle = null)
        {
            InitKey();

            var record = await GetJsonObjectAsync(new Uri(channelTitle == null
                                                              ? $"{Url}channels?&id={channelId}&key={_key}&part=contentDetails,snippet,statistics&fields=items(contentDetails(relatedPlaylists),snippet(title,description,thumbnails(default(url))),statistics(viewCount,subscriberCount))&{PrintType}"
                                                              : $"{Url}channels?&id={channelId}&key={_key}&part=contentDetails,snippet,statistics&fields=items(contentDetails(relatedPlaylists),snippet(description,thumbnails(default(url))),statistics(viewCount,subscriberCount))&{PrintType}"));

            if (record == null || !record.SelectToken("items").Any())
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
                                           Uri($"{Url}playlists?&id={record.SelectToken("items[0].contentDetails.relatedPlaylists").SelectToken("uploads")?.Value<string>()}&key={_key}&part=snippet&fields=items(id,snippet(title,thumbnails(default(url))))&{PrintType}"));

            var plu = new Playlist
            {
                Id = upload?.SelectToken("items[0].id")?.Value<string>(),
                ChannelId = channel.Id,
                Title = upload?.SelectToken("items[0].snippet.title")?.Value<string>(),
                ThumbnailLink = upload?.SelectToken("items[0].snippet.thumbnails.default.url", false)?.Value<string>(),
            };

            var itemms = withoutPl
                ? new JArray
                {
                    await
                        GetJsonObjectAsync(new
                                               Uri($"{Url}playlistItems?&key={_key}&playlistId={plu.Id}&part=snippet,contentDetails&order=date&fields=items(snippet(publishedAt,channelId,title,description,thumbnails(default(url)),resourceId(videoId)),contentDetails(videoPublishedAt))&maxResults={ItemsPerPage}&{PrintType}"))
                }
                : await
                    GetAll($"{Url}playlistItems?&key={_key}&playlistId={plu.Id}&part=snippet,contentDetails&order=date&fields=nextPageToken,items(snippet(publishedAt,channelId,title,description,thumbnails(default(url)),resourceId(videoId)),contentDetails(videoPublishedAt))&maxResults={ItemsPerPage}&{PrintType}");

            channel.Items.AddRange(itemms.SelectTokens("$..items.[*]").Select(x => MakeItem(x, channel.Title)));

            plu.Items.AddRange(channel.Items.Select(x => x.Id));

            channel.Playlists.Add(plu);

            await SetItemsStatistic(channel, true, plu.Items);

            if (withoutPl)
            {
                await Task.WhenAll(FillThumbs(channel.Items), FillThumbs(channel.Playlists));
                return channel;
            }
            // end uploads

            var playlists =
                (await
                    GetAll($"{Url}playlists?&channelId={channel.Id}&key={_key}&part=snippet&fields=nextPageToken,items(id,snippet(title,channelId,thumbnails(default(url))))&maxResults={ItemsPerPage}&{PrintType}")
                ).SelectTokens("$..items.[*]").ToHashSet();

            var plTasks = playlists.Select(x => x.SelectToken("id")?.Value<string>())
                .Select(pid => new Tuple<string, Task<JArray>>(pid,
                                                               GetAll($"{Url}playlistItems?&key={_key}&playlistId={pid}&part=snippet&order=date&fields=nextPageToken,items(snippet(resourceId(videoId)))&maxResults={ItemsPerPage}&{PrintType}")))
                .ToHashSet();

            await Task.WhenAll(plTasks.Select(x => x.Item2));

            var plBag = new ConcurrentBag<Playlist>();
            Parallel.ForEach(playlists,
                             plid =>
                             {
                                 var pid = plid.SelectToken("id")?.Value<string>();
                                 var vids = plTasks.First(x => x.Item1 == pid).Item2.Result.SelectTokens("$..items.[*]")
                                     .Select(rec => rec.SelectToken("snippet.resourceId.videoId")?.Value<string>()).Where(x => x != null)
                                     .Distinct().ToHashSet();

                                 if (vids.Count != 0)
                                 {
                                     // not empty pl
                                     var plc = new Playlist
                                     {
                                         Id = pid,
                                         ChannelId = channel.Id,
                                         Title = plid.SelectToken("snippet.title")?.Value<string>(),
                                         ThumbnailLink = plid.SelectToken("snippet.thumbnails.default.url")?.Value<string>()
                                     };

                                     plc.Items.AddRange(vids);
                                     plBag.Add(plc);
                                 }
                             });

            channel.Playlists.AddRange(plBag);

            var unlistedTasks = channel.Playlists.Where(x => x.Id != plu.Id).SelectMany(x => x.Items).Distinct().Except(plu.Items)
                .ToList().Split()
                .Select(vid =>
                            GetJsonObjectAsync(new
                                                   Uri($"{Url}videos?id={string.Join(",", vid)}&key={_key}&part=snippet&fields=items(id,snippet(channelId))&{PrintType}")))
                .ToHashSet();

            await Task.WhenAll(unlistedTasks);

            var unlisted = unlistedTasks.AsParallel().SelectMany(x => x.Result.SelectTokens("items.[*]")
                                                                     .Where(y => y.SelectToken("snippet.channelId")?.Value<string>()
                                                                                 == channelId)
                                                                     .Select(z => z.SelectToken("id")?.Value<string>())
                                                                     .Where(k => k != null)).ToList();

            Parallel.ForEach(channel.Playlists,
                             x =>
                             {
                                 x.Items.RemoveAll(y => !unlisted.Union(plu.Items).Contains(y));
                             });

            if (unlisted.Count > 0)
            {
                var tasks = unlisted.Split()
                    .Select(vid =>
                                GetJsonObjectAsync(new
                                                       Uri($"{Url}videos?id={string.Join(",", vid)}&key={_key}&&part=snippet,contentDetails,statistics&fields=items(id,snippet(publishedAt,title,description,thumbnails(default(url))),contentDetails(duration),statistics(viewCount,commentCount,likeCount,dislikeCount))&{PrintType}")))
                    .ToHashSet();

                await Task.WhenAll(tasks);

                var chTitle = channel.Title;
                channel.Items.AddRange(tasks.AsParallel()
                                           .SelectMany(i => i.Result.SelectTokens("items.[*]")
                                                           .Select(x => MakeItem(x, channelId, chTitle))));
            }

            await Task.WhenAll(FillThumbs(channel.Items), FillThumbs(channel.Playlists));

            channel.ItemsCount = channel.Items.Count;
            Parallel.ForEach(channel.Playlists,
                             x =>
                             {
                                 x.Count = x.Items.Count;
                             });
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
            InitKey();

            var diff = new ChannelDiff(cs.ChannelId, cs.ChannelTitle, syncPls);

            diff.UnlistedItems.AddRange(cs.Items.Where(y => y.Item2 == 2 || y.Item2 == 3).Select(k => k.Item1));

            var record = await GetJsonObjectAsync(new Uri(syncPls
                                                              ? $"{Url}channels?&key={_key}&id={cs.ChannelId}&part=contentDetails,snippet,statistics&fields=items(contentDetails(relatedPlaylists),snippet(description),statistics(viewCount,subscriberCount))&{PrintType}"
                                                              : $"{Url}channels?&key={_key}&id={cs.ChannelId}&part=contentDetails,statistics&fields=items(contentDetails(relatedPlaylists),statistics(viewCount,subscriberCount))&{PrintType}"));

            if (record == null || !record.SelectToken("items").Any())
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
                    GetAll($"{Url}playlistItems?&key={_key}&playlistId={upId}&part=snippet&order=date&fields=nextPageToken,items(snippet(resourceId(videoId)))&maxResults={ItemsPerPage}&{PrintType}")
                ).SelectTokens("$..items.[*]").Select(rec => rec.SelectToken("snippet.resourceId.videoId")?.Value<string>())
                .Where(x => x != null).Distinct().ToHashSet();

            diff.UploadedIds.AddRange(uploadvids);
            diff.AddedItems.AddRange(uploadvids.Except(cs.Items.Select(x => x.Item1))
                                         .Select(x => new ItemPrivacy { Id = x, Status = SyncState.Added }));
            diff.DeletedItems.AddRange(cs.Items.Select(x => x.Item1).Except(uploadvids));

            setLog?.Invoke($"{diff.ChannelTitle}, added: {diff.AddedItems.Count}, deleted: {diff.DeletedItems.Count}");

            if (!syncPls)
            {
                if (diff.DeletedItems.Count > 0)
                {
                    await GetTrueDeleted(diff);
                }

                return diff;
            }

            var playlists =
                (await
                    GetAll($"{Url}playlists?&channelId={cs.ChannelId}&key={_key}&part=snippet&fields=nextPageToken,items(id,snippet(title,channelId,thumbnails(default(url))))&maxResults={ItemsPerPage}&{PrintType}")
                ).SelectTokens("$..items.[*]").ToHashSet();

            var plTasks = playlists.Select(x => x.SelectToken("id")?.Value<string>())
                .Select(pid => new Tuple<string, Task<JArray>>(pid,
                                                               GetAll($"{Url}playlistItems?&key={_key}&playlistId={pid}&part=snippet,status&order=date&fields=nextPageToken,items(snippet(resourceId(videoId)),status(privacyStatus))&maxResults={ItemsPerPage}&{PrintType}")))
                .ToHashSet();

            await Task.WhenAll(plTasks.Select(x => x.Item2));

            Parallel.ForEach(playlists,
                             plid =>
                             {
                                 var pid = plid.SelectToken("id")?.Value<string>();

                                 var vids = plTasks.First(x => x.Item1 == pid).Item2.Result.SelectTokens("$..items.[*]")
                                     .Select(x => new
                                     {
                                         id = x.SelectToken("snippet.resourceId.videoId")?.Value<string>(),
                                         status = x.SelectToken("status.privacyStatus")?.Value<string>()
                                     }).GroupBy(x => x.id).ToDictionary(g => g.Key, g => g.FirstOrDefault()?.status);

                                 if (vids.Count != 0)
                                 {
                                     // not empty pl
                                     if (pid != null && cs.Playlists.Contains(pid))
                                     {
                                         diff.ExistPls.TryAdd(pid,
                                                              vids.Select(x => new ItemPrivacy { Id = x.Key, Status = GetState(x.Value) })
                                                                  .ToList());
                                     }
                                     else if (pid != null && !cs.Playlists.Contains(pid))
                                     {
                                         diff.AddedPls.TryAdd(new Playlist
                                                              {
                                                                  Id = pid,
                                                                  ChannelId = cs.ChannelId,
                                                                  Title = plid.SelectToken("snippet.title")?.Value<string>(),
                                                                  ThumbnailLink = plid.SelectToken("snippet.thumbnails.default.url")
                                                                      ?.Value<string>()
                                                              },
                                                              vids.Select(x => new ItemPrivacy { Id = x.Key, Status = GetState(x.Value) })
                                                                  .ToList());
                                     }
                                 }
                             });

            diff.ExistPls.TryAdd(upId, uploadvids.Select(x => new ItemPrivacy { Id = x, Status = SyncState.Added }).ToList());
            diff.DeletedPls.AddRange(cs.Playlists.Where(z => z != upId)
                                         .Except(diff.ExistPls.Select(x => x.Key).Union(diff.AddedPls.Select(x => x.Key.Id))));

            var unlistedTasks = diff.AddedPls.SelectMany(x => x.Value.Select(y => y.Id))
                .Union(diff.ExistPls.SelectMany(x => x.Value.Select(y => y.Id)))
                .Except(diff.AddedItems.Select(x => x.Id).Union(cs.Items.Select(x => x.Item1))).ToList().Split()
                .Select(vid =>
                            GetJsonObjectAsync(new
                                                   Uri($"{Url}videos?id={string.Join(",", vid)}&key={_key}&part=snippet&fields=items(id,snippet(channelId))&{PrintType}")))
                .ToHashSet();

            await Task.WhenAll(unlistedTasks);

            var cId = cs.ChannelId;
            var unlisted = unlistedTasks.AsParallel().SelectMany(x => x.Result?.SelectTokens("items.[*]")
                                                                     .Where(y => y.SelectToken("snippet.channelId")?.Value<string>()
                                                                                 == cId).Select(z => z.SelectToken("id")?.Value<string>())
                                                                     .Where(k => k != null)).ToHashSet();

            diff.AddedItems.AddRange(unlisted.Except(diff.AddedItems.Select(x => x.Id))
                                         .Select(y => new ItemPrivacy { Id = y, Status = SyncState.Unlisted }));

            var trueAdded = unlisted.Union(cs.Items.Select(x => x.Item1)).Union(diff.AddedItems.Select(x => x.Id));

            if (diff.AddedPls.Count > 0)
            {
                Parallel.ForEach(diff.AddedPls,
                                 pair =>
                                 {
                                     pair.Value.RemoveAll(y => !trueAdded.Contains(y.Id));
                                 });
            }

            if (diff.ExistPls.Count > 0)
            {
                Parallel.ForEach(diff.ExistPls.Where(x => x.Key != upId),
                                 pair =>
                                 {
                                     pair.Value.RemoveAll(y => !trueAdded.Contains(y.Id));
                                 });
            }

            if (diff.DeletedItems.Count > 0)
            {
                diff.DeletedItems.RemoveAll(x => diff.ExistPls.SelectMany(y => y.Value).Union(diff.AddedPls.SelectMany(y => y.Value))
                                                .Where(z => z.Status == SyncState.Added || z.Status == SyncState.Unlisted)
                                                .Select(y => y.Id).Contains(x));

                if (diff.DeletedItems.Count > 0)
                {
                    await GetTrueDeleted(diff);
                }
            }

            return diff;
        }

        public async Task<string> GetChannelId(string inputChannelLink)
        {
            InitKey();

            var sp = inputChannelLink.Split('/');
            if (sp.Length <= 1)
            {
                return await GetChannelIdByUserNameNetAsync(inputChannelLink) ?? inputChannelLink;
            }

            if (sp.Contains(YouUser))
            {
                var indexuser = Array.IndexOf(sp, YouUser);
                if (indexuser < 0)
                {
                    return string.Empty;
                }

                var user = sp[indexuser + 1];
                return await GetChannelIdByUserNameNetAsync(user);
            }

            if (!sp.Contains(YouChannel))
            {
                return IsYoutubeLink(inputChannelLink, out var videoId)
                    ? (await
                        GetJsonObjectAsync(new
                                               Uri($"{Url}videos?&id={videoId}&key={_key}&part=snippet&fields=items(snippet(channelId))&{PrintType}"))
                    )?.SelectToken("items[0].snippet.channelId")?.Value<string>()
                    : string.Empty;
            }

            var indexchannel = Array.IndexOf(sp, YouChannel);
            if (indexchannel < 0)
            {
                return string.Empty;
            }

            var appSp = sp[indexchannel + 1].Split('?');
            return appSp.Length >= 1 ? appSp[0] : string.Empty;
        }

        public async Task<List<Item>> GetItems(Dictionary<string, SyncPrivacy> privacyItems)
        {
            var tasks = privacyItems.Select(x => x.Key).ToList().Split()
                .Select(vid =>
                            $"{Url}videos?id={string.Join(",", vid)}&key={_key}&part=snippet,contentDetails,statistics&fields=items(id,snippet(publishedAt,title,description,thumbnails(default(url))),contentDetails(duration),statistics(viewCount,commentCount,likeCount,dislikeCount))&{PrintType}")
                .Select(zap => GetJsonObjectAsync(new Uri(zap))).ToHashSet();

            await Task.WhenAll(tasks);

            var newItems = tasks.AsParallel()
                .SelectMany(x => x.Result.SelectTokens("items.[*]").Where(y => y.SelectToken("id")?.Value<string>() != null))
                .Select(x => new Item
                {
                    Id = x.SelectToken("id").Value<string>(),
                    ChannelId = privacyItems[x.SelectToken("id").Value<string>()].ChannelId,
                    ChannelTitle = privacyItems[x.SelectToken("id").Value<string>()].ChannelTitle,
                    Title = x.SelectToken("snippet.title")?.Value<string>().RemoveNewLine().RemoveSpecialCharacters(),
                    Timestamp = x.SelectToken("snippet.publishedAt")?.Value<DateTime?>() ?? DateTime.MinValue,
                    Description = x.SelectToken("snippet.description")?.Value<string>(),
                    ViewCount = x.SelectToken("statistics.viewCount")?.Value<long?>() ?? 0,
                    Comments = x.SelectToken("statistics.commentCount")?.Value<long?>() ?? 0,
                    LikeCount = x.SelectToken("statistics.likeCount")?.Value<long?>() ?? 0,
                    DislikeCount = x.SelectToken("statistics.dislikeCount")?.Value<long?>() ?? 0,
                    ThumbnailLink = x.SelectToken("snippet.thumbnails.default.url")?.Value<string>(),
                    Duration = x.SelectToken("contentDetails.duration") != null
                        ? (int)XmlConvert.ToTimeSpan(x.SelectToken("contentDetails.duration").Value<string>()).TotalSeconds
                        : 0,
                    SyncState = privacyItems[x.SelectToken("id").Value<string>()].Status
                }).ToList();

            await FillThumbs(newItems);

            return newItems;
        }

        public async Task<List<Item>> GetPopularItems(string country, IEnumerable<string> existChannelsIds)
        {
            InitKey();

            var ids =
                (await
                    GetJsonObjectAsync(new
                                           Uri($"{Url}videos?chart=mostPopular&key={_key}&maxResults={ItemsPerPage}&regionCode={country}&safeSearch=none&part=snippet&fields=items(id,snippet(channelId))&{PrintType}"))
                )?.SelectTokens("items.[*]").Where(x => x.SelectToken("id")?.Value<string>() != null)
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

        public string GetPreviewThumbLink(string itemId)
        {
            return $"http://img.youtube.com/vi/{itemId}/0.jpg";
        }

        public async Task<Channel[]> GetRelatedChannelsAsync(string channelId, IEnumerable<string> existChannels)
        {
            InitKey();

            var res =
                await
                    GetJsonObjectAsync(new
                                           Uri($"{Url}channels?id={channelId}&key={_key}&part=brandingSettings&fields=items(brandingSettings(channel(featuredChannelsUrls)))&{PrintType}"));

            if (res == null || !res.SelectToken("items").Any())
            {
                return null;
            }

            return await Task.WhenAll(res.SelectToken("items[0].brandingSettings.channel.featuredChannelsUrls")
                                          .Select(x => x.Value<string>()).Where(x => !existChannels.Contains(x))
                                          .Select(chId => GetChannelAsync(chId, true)));
        }

        public async Task<HashSet<Comment>> GetReplyCommentsAsync(string commentId, string channelId)
        {
            InitKey();

            var record =
                await
                    GetAll($"{Url}comments?parentId={commentId}&key={_key}&part=snippet,id&fields=nextPageToken,items(id,snippet(authorDisplayName,textDisplay,likeCount,publishedAt,authorChannelId))&maxResults={ItemsPerPage}&{PrintType}");

            if (record.Count == 0)
            {
                // deleted video
                return new HashSet<Comment>(0);
            }

            var hrefRegex = new Regex(HrefRegex, RegexOptions.Compiled);
            return record.SelectTokens("$..items.[*]").Select(x => new Comment(channelId)
            {
                CommentId = x.SelectToken("id")?.Value<string>(),
                Author =
                    x.SelectToken("snippet.authorDisplayName")?.Value<string>()
                        .RemoveSpecialCharacters(),
                AuthorChannelId =
                    x.SelectToken("snippet.authorChannelId.value")?.Value<string>(),
                Text = GetCommentText(x, "snippet.textDisplay", hrefRegex),
                TextUrl = hrefRegex
                    .Match(x.SelectToken("snippet.textDisplay")?.Value<string>()
                           ?? string.Empty).Value.Replace("<a href=", string.Empty)
                    .Replace(">", string.Empty).Trim('"'),
                LikeCount = x.SelectToken("snippet.likeCount")?.Value<long>() ?? 0,
                Timestamp = x.SelectToken("snippet.publishedAt", false)
                                ?.Value<DateTime?>() ?? DateTime.MinValue,
                IsReply = true
            }).Reverse().ToHashSet();
        }

        public async Task<List<Item>> GetSearchedItems(string searchText, IEnumerable<string> existChannelsIds, string region)
        {
            InitKey();

            var ids =
                (await
                    GetJsonObjectAsync(new
                                           Uri($"{Url}search?&q={searchText}&key={_key}&maxResults={ItemsPerPage}&regionCode={region}&safeSearch=none&part=snippet&fields=items(id(videoId),snippet(channelId))&{PrintType}"))
                )?.SelectTokens("items.[*]").Where(x => x.SelectToken("id.videoId")?.Value<string>() != null)
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

        public async Task<IEnumerable<Comment>> GetVideoCommentsAsync(string itemlId, string channelId)
        {
            InitKey();

            var record =
                await
                    GetAll($"{Url}commentThreads?videoId={itemlId}&key={_key}&part=id,snippet&fields=nextPageToken,items(id,snippet(topLevelComment(snippet(authorChannelId,authorDisplayName,textDisplay,likeCount,publishedAt)),totalReplyCount))&maxResults={ItemsPerPage}&{PrintType}");

            if (record.Count == 0)
            {
                // deleted video
                return new Comment[0];
            }

            var hrefRegex = new Regex(HrefRegex, RegexOptions.Compiled);
            return record.SelectTokens("$..items.[*]").Select(x => new Comment(channelId)
            {
                CommentId = x.SelectToken("id")?.Value<string>(),
                Author =
                    channelId
                        .Equals(x
                                    .SelectToken("snippet.topLevelComment.snippet.authorChannelId.value")
                                    ?.Value<string>(),
                                StringComparison.InvariantCultureIgnoreCase)
                        ? $" {x.SelectToken("snippet.topLevelComment.snippet.authorDisplayName")?.Value<string>().RemoveSpecialCharacters()}"
                        : x
                            .SelectToken("snippet.topLevelComment.snippet.authorDisplayName")
                            ?.Value<string>().RemoveSpecialCharacters().Trim(),
                AuthorChannelId =
                    x.SelectToken("snippet.topLevelComment.snippet.authorChannelId.value")
                        ?.Value<string>(),
                Text =
                    GetCommentText(x,
                                   "snippet.topLevelComment.snippet.textDisplay",
                                   hrefRegex),
                TextUrl =
                    hrefRegex
                        .Match(x.SelectToken("snippet.topLevelComment.snippet.textDisplay")
                                   ?.Value<string>() ?? string.Empty).Value
                        .Replace("<a href=", string.Empty)
                        .Replace(">", string.Empty).Trim('"'),
                CommentReplyCount =
                    x.SelectToken("snippet.totalReplyCount")?.Value<long>() ?? 0,
                LikeCount =
                    x.SelectToken("snippet.topLevelComment.snippet.likeCount")
                        ?.Value<long>() ?? 0,
                Timestamp =
                    x.SelectToken("snippet.topLevelComment.snippet.publishedAt",
                                  false)?.Value<DateTime?>() ?? DateTime.MinValue,
                IsReply = false,
                ExpandDown =
                    (x.SelectToken("snippet.totalReplyCount")?.Value<long>() ?? 0)
                    > 0
                        ? Convert.FromBase64String(CommentDown).CreateThumb()
                        : null,
                ExpandUp =
                    (x.SelectToken("snippet.totalReplyCount")?.Value<long>() ?? 0)
                    > 0
                        ? Convert.FromBase64String(CommentUp).CreateThumb()
                        : null
            });
        }

        public bool IsYoutubeLink(string link, out string videoId)
        {
            var regex = new Regex(YouRegex, RegexOptions.None);
            var match = regex.Match(link);
            var res = match.Success;
            videoId = res ? match.Groups[1].Value : null;
            return res;
        }

        public async Task SetItemsStatistic(Channel channel, bool isDur, IEnumerable<string> ids = null)
        {
            InitKey();

            if (ids == null)
            {
                ids = channel.Items.Select(x => x.Id);
            }

            var uploadTasks = ids.ToList().Split().Select(s => GetJsonObjectAsync(new Uri(isDur
                                                                                              ? $"{Url}videos?id={string.Join(",", s)}&key={_key}&part=contentDetails,statistics&fields=items(id,contentDetails(duration),statistics(viewCount,commentCount,likeCount,dislikeCount))&{PrintType}"
                                                                                              : $"{Url}videos?id={string.Join(",", s)}&key={_key}&part=snippet,statistics&fields=items(id,snippet(description),statistics(viewCount,commentCount,likeCount,dislikeCount))&{PrintType}")))
                .ToHashSet();

            await Task.WhenAll(uploadTasks);

            Parallel.ForEach(uploadTasks.AsParallel().SelectMany(x => x.Result.SelectTokens("items.[*]")),
                             rec =>
                             {
                                 var item = channel.Items.FirstOrDefault(x => x.Id == rec.SelectToken("id")?.Value<string>());
                                 if (item != null)
                                 {
                                     item.ViewCount = rec.SelectToken("statistics.viewCount")?.Value<long?>() ?? 0;
                                     item.Comments = rec.SelectToken("statistics.commentCount")?.Value<long?>() ?? 0;
                                     item.LikeCount = rec.SelectToken("statistics.likeCount")?.Value<long?>() ?? 0;
                                     item.DislikeCount = rec.SelectToken("statistics.dislikeCount")?.Value<long?>() ?? 0;
                                     item.Description = rec.SelectToken("snippet.description")?.Value<string>();
                                     if (isDur)
                                     {
                                         var dur = rec.SelectToken("contentDetails.duration");
                                         item.Duration = dur != null ? (int)XmlConvert.ToTimeSpan(dur.Value<string>()).TotalSeconds : 0;
                                     }
                                 }
                             });
        }

        public async Task SetItemsStatistic(List<Item> items)
        {
            InitKey();

            var uploadTasks = items.Select(x => x.Id).ToList().Split()
                .Select(s =>
                            GetJsonObjectAsync(new
                                                   Uri($"{Url}videos?id={string.Join(",", s)}&key={_key}&part=snippet,statistics&fields=items(id,snippet(description),statistics(viewCount,commentCount,likeCount,dislikeCount))&{PrintType}")))
                .ToHashSet();

            await Task.WhenAll(uploadTasks);

            Parallel.ForEach(uploadTasks.AsParallel().SelectMany(x => x.Result.SelectTokens("items.[*]")),
                             rec =>
                             {
                                 var item = items.FirstOrDefault(x => x.Id == rec.SelectToken("id")?.Value<string>());
                                 if (item != null)
                                 {
                                     item.ViewCount = rec.SelectToken("statistics.viewCount")?.Value<long?>() ?? 0;
                                     item.Comments = rec.SelectToken("statistics.commentCount")?.Value<long?>() ?? 0;
                                     item.LikeCount = rec.SelectToken("statistics.likeCount")?.Value<long?>() ?? 0;
                                     item.DislikeCount = rec.SelectToken("statistics.dislikeCount")?.Value<long?>() ?? 0;
                                     item.Description = rec.SelectToken("snippet.description")?.Value<string>();
                                 }
                             });
        }

        private async Task FillThumbs(IReadOnlyCollection<Item> items)
        {
            var itasks = items.Select(item => new Tuple<string, Task<byte[]>>(item.Id, GetStreamFromUrl(item.ThumbnailLink))).ToHashSet();

            await Task.WhenAll(itasks.Select(x => x.Item2));

            Parallel.ForEach(items,
                             item =>
                             {
                                 item.Thumbnail = itasks.First(x => x.Item1 == item.Id).Item2.Result;
                                 item.ThumbnailLink = null;
                             });
        }

        private async Task<string> GetChannelIdByUserNameNetAsync(string username)
        {
            return (await
                    GetJsonObjectAsync(new
                                           Uri($"{Url}channels?&forUsername={username}&key={_key}&part=snippet&fields=items(id)&{PrintType}"))
                )?.SelectToken("items[0].id")?.Value<string>();
        }

        private async Task GetTrueDeleted(ChannelDiff diff)
        {
            var nonPlunl = diff.DeletedItems.Split()
                .Select(id =>
                            GetJsonObjectAsync(new
                                                   Uri($"{Url}videos?id={string.Join(",", id)}&key={_key}&part=id&fields=items(id)&{PrintType}")))
                .ToHashSet();

            await Task.WhenAll(nonPlunl);

            diff.UnlistedItems.AddRange(nonPlunl.AsParallel()
                                            .SelectMany(x => x.Result?.SelectTokens("items.[*]")
                                                            .Select(z => z.SelectToken("id")?.Value<string>())));

            diff.DeletedItems.RemoveAll(x => diff.UnlistedItems.Contains(x));
        }

        private void InitKey()
        {
            if (_inited)
            {
                return;
            }

            _key = AvaloniaLocator.Current.GetService<IBackupService>().YouApiKey;
            _inited = true;
        }

        #endregion
    }
}
