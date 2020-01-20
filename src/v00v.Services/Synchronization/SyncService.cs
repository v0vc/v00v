﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using v00v.Model.Entities;
using v00v.Model.SyncEntities;
using v00v.Services.ContentProvider;
using v00v.Services.Persistence;

namespace v00v.Services.Synchronization
{
    public class SyncService : ISyncService
    {
        #region Static and Readonly Fields

        private readonly IChannelRepository _channelRepository;
        private readonly IYoutubeService _youtubeService;

        #endregion

        #region Constructors

        public SyncService(IYoutubeService youtubeService, IChannelRepository channelRepository)
        {
            _youtubeService = youtubeService;
            _channelRepository = channelRepository;
        }

        #endregion

        #region Methods

        public async Task<SyncDiff> Sync(bool parallel, bool syncPls, List<Channel> channels, Action<string> setLog)
        {
            setLog?.Invoke(channels.Count <= 2
                               ? $"Working: {channels.Last().Title}.."
                               : $"Working channels: {channels.Count - 1}, parallel: {parallel}");

            var unl = new List<string>();
            IEnumerable<ChannelStruct> channelStructs = null;
            await Task.Run(() =>
            {
                channelStructs = _channelRepository.GetChannelsStructYield(syncPls, channels.Count == 2 ? channels.Last().Id : null);
                unl.AddRange(channelStructs.SelectMany(x => x.Items.Where(y => y.Item2 == 2 || y.Item2 == 3).Select(z => z.Item1)));
            });

            var diffs = new List<ChannelDiff>();
            if (parallel)
            {
                var pdiff = new List<Task<ChannelDiff>>();
                pdiff.AddRange(channelStructs.Select(x => _youtubeService.GetChannelDiffAsync(x, syncPls, setLog)));
                var tasks = Task.WhenAll(pdiff);
                await tasks.ContinueWith(x =>
                {
                    if (tasks.Exception != null)
                    {
                        setLog?.Invoke($"{tasks.Exception.Message}");
                    }
                    else
                    {
                        diffs.AddRange(pdiff.Select(diff => diff.Result));
                    }
                });
            }
            else
            {
                var cur = 1;
                foreach (var x in channelStructs)
                {
                    try
                    {
                        var diff = await _youtubeService.GetChannelDiffAsync(x, syncPls, setLog);
                        diffs.Add(diff);
                        setLog?.Invoke($"{diff.ChannelId} ok, {cur} of {channels.Count - 1}");
                        cur++;
                    }
                    catch (Exception e)
                    {
                        setLog?.Invoke($"Error: {x.ChannelId}, {e.Message}");
                    }
                }
            }

            var res = new SyncDiff(syncPls);

            res.ErrorSyncChannels.AddRange(diffs.Where(x => x.Faulted).Select(x => x.ChannelId));

            if (res.ErrorSyncChannels.Count > 0)
            {
                setLog?.Invoke($"Banned channel(s): {string.Join(", ", res.ErrorSyncChannels)}");
            }

            res.Channels = diffs.ToDictionary(x => x.ChannelId,
                                              y => new ChannelStats
                                              {
                                                  ViewCount = y.ViewCount, SubsCount = y.SubsCount, Description = y.Description
                                              });

            Parallel.ForEach(diffs.ToImmutableDictionary(z => new Tuple<string, string>(z.ChannelId, z.ChannelTitle), z => z.AddedItems),
                             pair =>
                             {
                                 var ((item1, item2), value) = pair;
                                 Parallel.ForEach(value,
                                                  privacy =>
                                                  {
                                                      res.Items.TryAdd(privacy.Id,
                                                                       new SyncPrivacy
                                                                       {
                                                                           ChannelId = item1,
                                                                           ChannelTitle = item2,
                                                                           Status = privacy.Status
                                                                       });
                                                  });
                             });

            res.NoUnlistedAgain.AddRange(diffs.SelectMany(x => x.UploadedIds).Where(x => unl.Contains(x)));

            res.DeletedItems.AddRange(diffs.SelectMany(x => x.DeletedItems));

            res.UnlistedItems.AddRange(diffs.SelectMany(x => x.UnlistedItems));

            if (res.Items.Count > 0)
            {
                res.NewItems.AddRange(await _youtubeService.GetItems(res.Items.ToDictionary(entry => entry.Key, entry => entry.Value)));
                channels.First(x => x.IsStateChannel).Items.AddRange(res.NewItems);
                channels.First(x => x.IsStateChannel).Count += res.NewItems.Count;
            }

            setLog?.Invoke($"New items: {res.NewItems.Count}");

            if (syncPls)
            {
                res.NewPlaylists.AddRange(diffs.SelectMany(x => x.AddedPls).Select(x => x.Key));
                setLog?.Invoke(res.NewPlaylists.Count > 0
                                   ? $"New playlists: {res.NewPlaylists.Count} - {string.Join(", ", res.NewPlaylists.Select(x => x.Title))}"
                                   : $"New playlists: {res.NewPlaylists.Count}");

                if (res.NewPlaylists.Count > 0)
                {
                    await _youtubeService.FillThumbs(res.NewPlaylists);
                    Parallel.ForEach(res.NewPlaylists,
                                     playlist =>
                                     {
                                         playlist.Items.AddRange(diffs.SelectMany(x => x.AddedPls).First(x => x.Key.Id == playlist.Id)
                                                                     .Value.Select(x => x.Id));
                                     });
                }

                res.DeletedPlaylists.AddRange(diffs.SelectMany(x => x.DeletedPls));
                setLog?.Invoke(res.DeletedPlaylists.Count > 0
                                   ? $"Deleted playlists: {res.DeletedPlaylists.Count} - {string.Join(", ", res.DeletedPlaylists)}"
                                   : $"Deleted playlists: {res.DeletedPlaylists.Count}");

                res.ExistPlaylists = diffs.SelectMany(x => x.ExistPls).ToDictionary(x => x.Key, y => y.Value);
                setLog?.Invoke($"Existed playlists: {res.ExistPlaylists.Count}");
            }

            Parallel.ForEach(res.Items.GroupBy(x => x.Value.ChannelId),
                             pair =>
                             {
                                 var chitems = res.NewItems.Where(x => pair.Select(y => y.Key).Contains(x.Id)).ToHashSet();
                                 res.Channels[pair.Key].ItemsCount = chitems.Count;
                                 res.Channels[pair.Key].Timestamp = chitems.OrderByDescending(x => x.Timestamp).First().Timestamp;
                                 var ch = channels.FirstOrDefault(x => x.Id == pair.Key);
                                 if (ch != null)
                                 {
                                     ch.Count += chitems.Count;
                                     if (ch.Loaded)
                                     {
                                         ch.Items.AddRange(chitems);
                                     }

                                     Parallel.ForEach(chitems,
                                                      x =>
                                                      {
                                                          x.Tags = ch.Tags.Select(y => y.Id);
                                                      });
                                 }
                             });

            setLog?.Invoke("Saving to db..");
            await Task.Run(() =>
            {
                var rows = _channelRepository.StoreDiff(res);
                rows.ContinueWith(x =>
                {
                    if (rows.IsCompletedSuccessfully)
                    {
                        setLog?.Invoke($"Saved {rows.Result} rows!");
                    }
                    else
                    {
                        setLog?.Invoke(rows.Exception == null ? "Save error" : $"Save error {rows.Exception.Message}");
                    }
                });
            });

            return res;
        }

        #endregion
    }
}
