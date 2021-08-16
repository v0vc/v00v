using System;
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

        public async Task<SyncDiff> Sync(bool parallel,
            bool syncPls,
            List<Channel> channels,
            Action<string> setLog,
            Action<string> setTitle)
        {
            var chCount = channels.Count - 1;
            setLog?.Invoke(channels.Count <= 2
                               ? $"Working: {channels.Last().Title}.."
                               : $"Working channels: {chCount}, parallel: {parallel}, playlist: {syncPls}");

            var channelStructs = _channelRepository.GetChannelsStructYield(syncPls, channels.Count == 2 ? channels.Last().Id : null);
            var unl = new List<string>();
            var diffs = new List<ChannelDiff>();
            if (parallel)
            {
                try
                {
                    var resDiff = await Task.WhenAll(channelStructs.Select(x => _youtubeService.GetChannelDiffAsync(x, syncPls, setLog)));
                    diffs.AddRange(resDiff);
                    unl.AddRange(diffs.SelectMany(y => y.UnlistedItems).Distinct());
                }
                catch (Exception ex)
                {
                    setLog?.Invoke($"{ex.Message}");
                    return null;
                }
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
                        unl.AddRange(diff.UnlistedItems);
                        setLog?.Invoke($"{diff.ChannelId} ok, {cur} of {chCount}");
                        setTitle?.Invoke($"Working channels...{cur} of {chCount}");
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

            if (unl.Count > 0)
            {
                res.NoUnlistedAgain.AddRange(diffs.SelectMany(y => y.UploadedIds).Where(z => unl.Contains(z)));
            }

            res.DeletedItems.AddRange(diffs.SelectMany(x => x.DeletedItems));

            res.UnlistedItems.AddRange(diffs.SelectMany(x => x.UnlistedItems).Except(res.NoUnlistedAgain));

            if (!res.Items.IsEmpty)
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
            var rows = _channelRepository.StoreDiff(res);
            await Task.WhenAll(rows).ContinueWith(_ =>
            {
                setLog?.Invoke(rows.IsCompletedSuccessfully ? $"Saved {rows.GetAwaiter().GetResult()} rows." :
                               rows.Exception == null ? "An saving error occurred." : $"An saving error occurred {rows.Exception.Message}.");
            });
            return res;
        }

        #endregion
    }
}
