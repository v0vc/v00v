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

        public async Task<SyncDiff> Sync(bool parallel, bool syncPls, IReadOnlyCollection<Channel> channels, Action<string> setLog)
        {
            var channelStructs =
                await _channelRepository.GetChannelsStruct(syncPls, channels.Where(x => !x.IsNew).Select(x => x.Id).ToHashSet());

            setLog?.Invoke(channelStructs.Count == 1
                               ? $"Start sync: {channelStructs.First().ChannelTitle}"
                               : $"Start sync channels: {channelStructs.Count}, parallel: {parallel}");

            var diffs = channelStructs.Select(x => _youtubeService.GetChannelDiffAsync(x, syncPls, setLog)).ToHashSet();

            if (parallel)
            {
                //IEnumerable<IEnumerable<Task<ChannelDiff>>> result = diffs.Split(99);
                //foreach (IEnumerable<Task<ChannelDiff>> enumerable in result)
                //{
                //    Task<ChannelDiff[]> tasks = Task.WhenAll(enumerable);

                //    await tasks.ContinueWith(x =>
                //    {
                //        Thread.Sleep(TimeSpan.FromSeconds(1));
                //        if (tasks.Exception != null)
                //        {
                //            setLog?.Invoke($"{tasks.Exception.Message}");
                //        }
                //    });
                //}
                Task<ChannelDiff[]> tasks = Task.WhenAll(diffs);

                await tasks.ContinueWith(x =>
                {
                    if (tasks.Exception != null)
                    {
                        setLog?.Invoke($"{tasks.Exception.Message}");
                    }
                });
            }
            else
            {
                var err = new List<Task<ChannelDiff>>();
                foreach (Task<ChannelDiff> diff in diffs)
                {
                    try
                    {
                        await diff;
                        setLog?.Invoke($"{diff.Status}");
                    }
                    catch (Exception e)
                    {
                        err.Add(diff);
                        setLog?.Invoke($"Add to second try: {diff.Result.ChannelId}, {e.Message}");
                    }
                }

                if (err.Count > 0)
                {
                    setLog?.Invoke($"Second try: {err.Count}");
                    foreach (Task<ChannelDiff> diff in diffs)
                    {
                        try
                        {
                            await diff;
                        }
                        catch (Exception e)
                        {
                            setLog?.Invoke($"Second try fail: {e.Message}");
                        }
                    }
                }
            }

            var res = new SyncDiff(syncPls);

            res.ErrorSyncChannels.AddRange(diffs.Where(x => x.Result.Faulted).Select(x => x.Result.ChannelId));

            if (res.ErrorSyncChannels.Count > 0)
            {
                setLog?.Invoke($"Banned channel(s): {string.Join(", ", res.ErrorSyncChannels)}");
            }

            res.Channels = diffs.Where(x => !x.IsFaulted).ToDictionary(x => x.Result.ChannelId,
                                                                       y => new ChannelStats
                                                                       {
                                                                           ViewCount = y.Result.ViewCount,
                                                                           SubsCount = y.Result.SubsCount,
                                                                           Description = y.Result.Description
                                                                       });

            foreach (((string item1, string item2), List<ItemPrivacy> value) in diffs.Where(x => x.Status == TaskStatus.RanToCompletion)
                .ToDictionary(z => new Tuple<string, string>(z.Result.ChannelId, z.Result.ChannelTitle), z => z.Result.AddedItems))
            {
                foreach (ItemPrivacy privacy in value)
                {
                    res.Items.Add(privacy.Id, new SyncPrivacy { ChannelId = item1, ChannelTitle = item2, Status = privacy.Status });
                }
            }

            res.NoUnlistedAgain.AddRange(diffs.SelectMany(x => x.Result.UploadedIds)
                                             .Where(x => channelStructs.SelectMany(y => y.UnlistedItems).Contains(x)));

            if (res.Items.Count > 0)
            {
                res.NewItems.AddRange(await _youtubeService.GetItems(res.Items));
            }

            setLog?.Invoke($"New items: {res.NewItems.Count}");

            if (syncPls)
            {
                res.NewPlaylists.AddRange(diffs.Where(x => !x.Result.Faulted).SelectMany(x => x.Result.AddedPls).Select(x => x.Key));
                setLog?.Invoke(res.NewPlaylists.Count > 0
                                   ? $"New playlists: {res.NewPlaylists.Count} - {string.Join(", ", res.NewPlaylists.Select(x => x.Title))}"
                                   : $"New playlists: {res.NewPlaylists.Count}");
                if (res.NewPlaylists.Count > 0)
                {
                    await _youtubeService.FillThumbs(res.NewPlaylists);
                    foreach (Playlist playlist in res.NewPlaylists)
                    {
                        playlist.Items.AddRange(diffs.Where(x => !x.Result.Faulted).SelectMany(x => x.Result.AddedPls)
                                                    .First(x => x.Key.Id == playlist.Id).Value.Select(x => x.Id));
                    }
                }

                res.DeletedPlaylists.AddRange(diffs.Where(x => !x.Result.Faulted).SelectMany(x => x.Result.DeletedPls));
                setLog?.Invoke(res.DeletedPlaylists.Count > 0
                                   ? $"Deleted playlists: {res.DeletedPlaylists.Count} - {string.Join(", ", res.DeletedPlaylists)}"
                                   : $"Deleted playlists: {res.DeletedPlaylists.Count}");

                res.ExistPlaylists = diffs.Where(x => !x.Result.Faulted).SelectMany(x => x.Result.ExistPls)
                    .ToDictionary(x => x.Key, y => y.Value);
                setLog?.Invoke($"Existed playlists: {res.ExistPlaylists.Count}");
            }

            foreach (IGrouping<string, KeyValuePair<string, SyncPrivacy>> pair in res.Items.GroupBy(x => x.Value.ChannelId))
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
                setLog?.Invoke($"Saved {rows.Result} rows!");
            });

            return res;
        }

        #endregion
    }
}
