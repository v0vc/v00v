using System;
using System.Collections.Generic;
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
            setLog?.Invoke(channels.Count == 1
                               ? $"Working: {channels.First().Title}.."
                               : $"Working channels: {channels.Count - 1}, parallel: {parallel}");

            var unl = new List<string>();
            IEnumerable<ChannelStruct> channelStructs = null;
            //channelStructs = await _channelRepository.GetChannelsStruct(syncPls, channels.Count == 2 ? channels.Last().Id : null);
            await Task.Run(() =>
            {
                channelStructs = _channelRepository.GetChannelsStructYield(syncPls, channels.Count == 2 ? channels.Last().Id : null);
                unl.AddRange(channelStructs.SelectMany(x => x.UnlistedItems));
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
                var err = new List<ChannelStruct>();
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
                        err.Add(x);
                        setLog?.Invoke($"Add to second try: {x.ChannelId}, {e.Message}");
                    }
                }

                if (err.Count > 0)
                {
                    setLog?.Invoke($"Second try: {err.Count}");
                    foreach (var cs in err)
                    {
                        try
                        {
                            var diff = await _youtubeService.GetChannelDiffAsync(cs, syncPls, setLog);
                            diffs.Add(diff);
                            setLog?.Invoke($"{diff.ChannelId} ok");
                        }
                        catch (Exception e)
                        {
                            setLog?.Invoke($"Second try fail: {e.Message}");
                        }
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

            foreach (((string item1, string item2), var value) in
                diffs.ToDictionary(z => new Tuple<string, string>(z.ChannelId, z.ChannelTitle), z => z.AddedItems))
            {
                foreach (var privacy in value)
                {
                    res.Items.Add(privacy.Id, new SyncPrivacy { ChannelId = item1, ChannelTitle = item2, Status = privacy.Status });
                }
            }

            res.NoUnlistedAgain.AddRange(diffs.SelectMany(x => x.UploadedIds).Where(x => unl.Contains(x)));

            res.DeletedItems.AddRange(diffs.SelectMany(x => x.DeletedItems));

            if (res.Items.Count > 0)
            {
                res.NewItems.AddRange(await _youtubeService.GetItems(res.Items));
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
                    foreach (var playlist in res.NewPlaylists)
                    {
                        playlist.Items.AddRange(diffs.SelectMany(x => x.AddedPls).First(x => x.Key.Id == playlist.Id).Value
                                                    .Select(x => x.Id));
                    }
                }

                res.DeletedPlaylists.AddRange(diffs.SelectMany(x => x.DeletedPls));
                setLog?.Invoke(res.DeletedPlaylists.Count > 0
                                   ? $"Deleted playlists: {res.DeletedPlaylists.Count} - {string.Join(", ", res.DeletedPlaylists)}"
                                   : $"Deleted playlists: {res.DeletedPlaylists.Count}");

                res.ExistPlaylists = diffs.SelectMany(x => x.ExistPls).ToDictionary(x => x.Key, y => y.Value);
                setLog?.Invoke($"Existed playlists: {res.ExistPlaylists.Count}");
            }

            foreach (var pair in res.Items.GroupBy(x => x.Value.ChannelId))
            {
                var chitems = res.NewItems.Where(x => pair.Select(y => y.Key).Contains(x.Id)).ToHashSet();
                res.Channels[pair.Key].ItemsCount = chitems.Count;
                res.Channels[pair.Key].Timestamp = chitems.OrderByDescending(x => x.Timestamp).First().Timestamp;
                var ch = channels.FirstOrDefault(x => x.Id == pair.Key);
                if (ch == null)
                {
                    continue;
                }

                ch.Count += chitems.Count;
                if (ch.Loaded)
                {
                    ch.Items.AddRange(chitems);
                }
            }

            if (res.NewItems.Count > 0)
            {
                var stateChannel = channels.First(x => x.IsStateChannel);
                if (stateChannel != null)
                {
                    stateChannel.Items.AddRange(res.NewItems);
                    stateChannel.Count += res.NewItems.Count;
                }
            }

            setLog?.Invoke("Saving to db..");
            var rows = _channelRepository.StoreDiff(res);
            await rows.ContinueWith(x =>
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

            return res;
        }

        #endregion
    }
}
