using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using v00v.Model.Entities;
using v00v.Model.Extensions;
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

        //#region Static Methods

        //private static void Log(string text)
        //{
        //    Console.WriteLine($"{DateTime.Now}:{text}");
        //}

        //#endregion

        #region Methods

        public async Task<SyncDiff> Sync(bool syncPls, IReadOnlyCollection<Channel> channels)
        {
            var channelStructs = await _channelRepository.GetChannelsStruct(syncPls);

            if (channelStructs.Count == 0)
            {
                //Log("nothing to sync");
                return null;
            }

            var res = new SyncDiff(syncPls);

            //Log($"channels:{channelStructs.Count}, start sync..");

            try
            {
                List<Task<ChannelDiff>> diffs = channelStructs.Select(x => _youtubeService.GetChannelDiffAsync(x, syncPls)).ToList();

                await Task.WhenAll(diffs);

                res.ErrorSyncChannels.AddRange(diffs.Where(x => x.Result.Faulted).Select(x => x.Result.ChannelId));

                res.Channels = diffs.ToDictionary(x => x.Result.ChannelId,
                                                  y => new ChannelStats
                                                  {
                                                      ViewCount = y.Result.ViewCount,
                                                      SubsCount = y.Result.SubsCount,
                                                      Description = y.Result.Description
                                                  });

                foreach (Task<ChannelDiff> task in diffs)
                {
                    foreach (ItemPrivacy item in task.Result.AddedItems)
                    {
                        res.Items.Add(item.Id,
                                      new SyncPrivacy
                                      {
                                          ChannelId = task.Result.ChannelId,
                                          ChannelTitle = channelStructs.First(x => x.ChannelId == task.Result.ChannelId).ChannelTitle,
                                          Status = item.Status
                                      });
                    }
                }

                //Log($"new items:{res.Items.Count}");

                if (res.Items.Count > 0)
                {
                    res.NewItems.AddRange(await _youtubeService.GetItems(res.Items));
                }

                if (syncPls)
                {
                    res.NewPlaylists.AddRange(diffs.Where(x => !x.Result.Faulted).SelectMany(x => x.Result.AddedPls).Select(x => x.Key));
                    //Log($"new playlist:{res.NewPlaylists.Count}");
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
                    //Log($"deleted playlist:{res.DeletedPlaylists.Count}");
                    res.ExistPlaylists = diffs.Where(x => !x.Result.Faulted).SelectMany(x => x.Result.ExistPls)
                        .ToDictionary(x => x.Key, y => y.Value);
                    //Log($"existed playlist:{res.ExistPlaylists.Count}");
                }

                foreach (IGrouping<string, KeyValuePair<string, SyncPrivacy>> pair in res.Items.GroupBy(x => x.Value.ChannelId))
                {
                    var chitems = res.NewItems.Where(x => pair.Select(y => y.Key).Contains(x.Id)).ToHashSet();
                    res.Channels[pair.Key].ItemsCount = chitems.Count;
                    res.Channels[pair.Key].Timestamp = chitems.OrderByDescending(x => x.Timestamp).First().Timestamp;
                    var ch = channels?.First(x => x.Id == pair.Key);
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
                    var stateChannel = channels?.First(x => x.IsStateChannel);
                    if (stateChannel != null)
                    {
                        stateChannel.Items.AddRange(res.NewItems);
                        stateChannel.Count += res.NewItems.Count;
                    }
                }

                if (res.TrueDiff)
                {
                    //Log("save to db..");
                    int rows = await _channelRepository.StoreDiff(res);
                    //Log($"saved {rows} rows");
                }
                else
                {
                    //Log("nothing to save");
                }

                return res;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                if (e.InnerException != null)
                {
                    Console.WriteLine(e.InnerException.Message);
                }

                return null;
            }
        }

        #endregion
    }
}
