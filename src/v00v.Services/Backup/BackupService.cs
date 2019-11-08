using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Newtonsoft.Json;
using v00v.Model.BackupEntities;
using v00v.Model.Entities;
using v00v.Model.Enums;
using v00v.Services.ContentProvider;
using v00v.Services.Persistence;

namespace v00v.Services.Backup
{
    public class BackupService : IBackupService
    {
        #region Static and Readonly Fields

        private readonly IChannelRepository _channelRepository;
        private readonly IConfigurationRoot _configuration;
        private readonly IItemRepository _itemRepository;
        private readonly IYoutubeService _syncService;

        #endregion

        #region Constructors

        public BackupService(IConfigurationRoot configuration,
            IYoutubeService syncService,
            IItemRepository itemRepository,
            IChannelRepository channelRepository)
        {
            _configuration = configuration;
            _itemRepository = itemRepository;
            _syncService = syncService;
            _channelRepository = channelRepository;
        }

        #endregion

        #region Methods

        public async Task<int> Backup(IEnumerable<Channel> entries)
        {
            if (!CheckJsonBackup())
            {
                return 0;
            }

            var bcp = new BackupAll
            {
                Items = entries.Select(channel => new BackupItem
                {
                    ChannelId = channel.Id,
                    ChannelTitle = channel.Title.Trim(),
                    Tags = channel.Tags.Select(x => x.Id)
                }),
                ItemsState = await _itemRepository.GetItemsState()
            };
            var fileName = GetBackupName();
            var tempFileName = fileName + ".new";
            string res = string.Empty;
            var ss = Task.Factory.StartNew(() =>
            {
                res = JsonConvert.SerializeObject(bcp, Formatting.Indented);
            });
            await ss;
            File.WriteAllText(tempFileName, res);
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }

            File.Move(tempFileName, fileName);
            return bcp.Items.Count();
        }

        public async Task<RestoreResult> Restore(IEnumerable<string> existChannels,
            bool isFast,
            Action<string> setTitle,
            Action<Channel> updateList)
        {
            var res = new RestoreResult { Channels = 0, Planned = 0, Watched = 0 };
            if (!CheckJsonBackup())
            {
                return res;
            }

            var fileName = GetBackupName();
            BackupAll backup = null;
            using (var r = new StreamReader(fileName))
            {
                string json = r.ReadToEnd();
                var ss = Task.Factory.StartNew(() =>
                {
                    backup = JsonConvert.DeserializeObject<BackupAll>(json);
                });
                await ss;
            }

            if (backup == null)
            {
                return res;
            }

            if (isFast)
            {
                setTitle.Invoke("Restore channels..");
                List<Task<Channel>> tasks = backup.Items.Where(x => !existChannels.Contains(x.ChannelId))
                    .Select(item => _syncService.GetChannelAsync(item.ChannelId, item.ChannelTitle)).ToList();

                await Task.WhenAll(tasks);

                var channels = new List<Channel>();
                foreach (Task<Channel> task in tasks)
                {
                    var ch = task.Result;
                    BackupItem rr = backup.Items.FirstOrDefault(x => x.ChannelId == ch.Id);
                    if (rr == null)
                    {
                        continue;
                    }

                    ch.Tags.AddRange(rr.Tags.Select(x => new Tag { Id = x }));
                    channels.Add(ch);
                    updateList.Invoke(ch);
                }

                var rows = await _channelRepository.AddChannels(channels);
                res.Channels = channels.Count;
                //await Console.Out.WriteLineAsync($"Saved {rows} rows!");
            }
            else
            {
                res.Channels = await RestoreOneByOne(backup.Items.Where(x => !existChannels.Contains(x.ChannelId)), setTitle, updateList);
            }

            await Task.WhenAll(backup.ItemsState.Select(x => _itemRepository.UpdateItemsWatchState(x.Key, x.Value)));

            var planned = _channelRepository.GetChannelStateCount(WatchState.Planned);
            var watched = _channelRepository.GetChannelStateCount(WatchState.Watched);
            await Task.WhenAll(planned, watched);

            var uplanned = planned.Result.Select(x => _channelRepository.UpdatePlannedCount(x.Key, x.Value));
            var uwatched = watched.Result.Select(x => _channelRepository.UpdateWatchedCount(x.Key, x.Value));

            await Task.WhenAll(uplanned.Union(uwatched));

            res.Planned = planned.Result.Sum(x => x.Value);
            res.Watched = watched.Result.Sum(x => x.Value);

            return res;
        }

        private bool CheckJsonBackup()
        {
            if (_configuration.Providers.Count() == 2)
            {
                return true;
            }

            //Console.Out.WriteLine("Please, enable backup/restore service (backup.json)");
            return false;
        }

        private string GetBackupName()
        {
            var prov = (FileConfigurationProvider)_configuration.Providers.Last();
            return Path.Combine(((PhysicalFileProvider)prov.Source.FileProvider).Root, prov.Source.Path);
        }

        private async Task<int> RestoreOneByOne(IEnumerable<BackupItem> lst, Action<string> setTitle, Action<Channel> updateList)
        {
            var res = 0;
            while (true)
            {
                var err = new List<BackupItem>();
                foreach (BackupItem item in lst)
                {
                    try
                    {
                        //await Console.Out.WriteLineAsync($"Start restoring {item.ChannelId}..");
                        setTitle.Invoke($"Restore {item.ChannelTitle}..");
                        var channel = await _syncService.GetChannelAsync(item.ChannelId, item.ChannelTitle);
                        if (channel == null)
                        {
                            // banned channel
                            continue;
                        }

                        channel.Tags.AddRange(item.Tags.Select(x => new Tag { Id = x }));
                        //await Console.Out.WriteLineAsync($"Restored {channel.Title}, now saving..");
                        var rows = await _channelRepository.AddChannel(channel);
                        //await Console.Out.WriteLineAsync($"Saved {rows}!");
                        updateList.Invoke(channel);
                        res++;
                    }
                    catch
                    {
                        err.Add(item);
                        //await Console.Out.WriteLineAsync(e.Message);
                    }
                }

                if (err.Count > 0)
                {
                    lst = err;
                    continue;
                }

                break;
            }

            return res;
        }

        #endregion
    }
}
