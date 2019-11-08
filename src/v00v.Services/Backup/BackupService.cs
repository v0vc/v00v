﻿using System;
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

        public async Task<int> Backup(IEnumerable<Channel> entries, Action<string> setLog)
        {
            if (!CheckJsonBackup(setLog))
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
            setLog?.Invoke($"Start backup {bcp.Items.Count()} channels..");
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
            setLog?.Invoke($"Done, saved to {fileName}");
            return bcp.Items.Count();
        }

        public async Task<RestoreResult> Restore(IEnumerable<string> existChannels,
            bool isFast,
            Action<string> setTitle,
            Action<Channel> updateList,
            Action<string> setLog)
        {
            var res = new RestoreResult { ChannelsCount = 0, PlannedCount = 0, WatchedCount = 0 };
            if (!CheckJsonBackup(setLog))
            {
                return res;
            }

            var fileName = GetBackupName();
            BackupAll backup;
            using (var r = new StreamReader(fileName))
            {
                string json = r.ReadToEnd();
                backup = JsonConvert.DeserializeObject<BackupAll>(json);
            }

            if (backup == null)
            {
                setLog?.Invoke("No backup");
                return res;
            }

            if (isFast)
            {
                setTitle?.Invoke("Working..");
                setLog?.Invoke("Parallel mode: ON");

                List<Task<Channel>> tasks = backup.Items.Where(x => !existChannels.Contains(x.ChannelId))
                    .Select(item => _syncService.GetChannelAsync(item.ChannelId, item.ChannelTitle)).ToList();

                setLog?.Invoke($"Total channels: {tasks.Count}, working..");

                var channels = new List<Channel>();
                await Task.WhenAll(tasks).ContinueWith(done =>
                {
                    foreach (Task<Channel> task in tasks)
                    {
                        BackupItem rr = backup.Items.FirstOrDefault(x => x.ChannelId == task.Result.Id);
                        if (rr == null)
                        {
                            continue;
                        }

                        task.Result.Tags.AddRange(rr.Tags.Select(x => new Tag { Id = x }));
                        channels.Add(task.Result);
                        //setLog?.Invoke($"Error channel {task.Result.Title}: {task.Exception.Message}");
                    }

                    var rows = _channelRepository.AddChannels(channels);
                    Task.WhenAll(rows).ContinueWith(r =>
                    {
                        setLog?.Invoke($"Saved {rows.Result} rows!");
                    });
                });
                res.ChannelsCount = channels.Count;
                channels.ForEach(updateList.Invoke);
            }
            else
            {
                setLog?.Invoke("Parallel mode: OFF");
                res.ChannelsCount = await RestoreOneByOne(backup.Items.Where(x => !existChannels.Contains(x.ChannelId)),
                                                          setTitle,
                                                          updateList,
                                                          setLog);
            }

            await Task.WhenAll(backup.ItemsState.Select(x => _itemRepository.UpdateItemsWatchState(x.Key, x.Value)));

            var planned = _channelRepository.GetChannelStateCount(WatchState.Planned);
            var watched = _channelRepository.GetChannelStateCount(WatchState.Watched);
            await Task.WhenAll(planned, watched);

            var uplanned = planned.Result.Select(x => _channelRepository.UpdatePlannedCount(x.Key, x.Value));
            var uwatched = watched.Result.Select(x => _channelRepository.UpdateWatchedCount(x.Key, x.Value));

            await Task.WhenAll(uplanned.Union(uwatched));

            res.PlannedCount = planned.Result.Sum(x => x.Value);
            res.WatchedCount = watched.Result.Sum(x => x.Value);
            setLog?.Invoke($"Total planned: {res.PlannedCount}");
            setLog?.Invoke($"Total watched: {res.WatchedCount}");
            return res;
        }

        private bool CheckJsonBackup(Action<string> setLog)
        {
            if (_configuration.Providers.Count() == 2)
            {
                return true;
            }

            setLog?.Invoke("Please, enable backup/restore service (backup.json)");
            return false;
        }

        private string GetBackupName()
        {
            var prov = (FileConfigurationProvider)_configuration.Providers.Last();
            return Path.Combine(((PhysicalFileProvider)prov.Source.FileProvider).Root, prov.Source.Path);
        }

        private async Task<int> RestoreOneByOne(IEnumerable<BackupItem> lst,
            Action<string> setTitle,
            Action<Channel> updateList,
            Action<string> setLog)
        {
            var res = 0;
            while (true)
            {
                var err = new List<BackupItem>();
                foreach (BackupItem item in lst)
                {
                    try
                    {
                        setLog?.Invoke($"Start restoring {item.ChannelTitle} - {item.ChannelId}..");
                        setTitle.Invoke($"Restoring {item.ChannelTitle}..");
                        var channel = await _syncService.GetChannelAsync(item.ChannelId, item.ChannelTitle);
                        if (channel == null)
                        {
                            setLog?.Invoke($"Banned {item.ChannelTitle} - {item.ChannelId}, skipping..");
                            continue;
                        }

                        channel.Tags.AddRange(item.Tags.Select(x => new Tag { Id = x }));
                        setLog?.Invoke($"Restored {channel.Title}, now saving..");
                        var rows = _channelRepository.AddChannel(channel);
                        await Task.WhenAll(rows).ContinueWith(r =>
                        {
                            setLog?.Invoke($"Saved {rows.Result} rows!");
                        });
                        updateList.Invoke(channel);
                        res++;
                    }
                    catch (Exception e)
                    {
                        err.Add(item);
                        setLog?.Invoke(e.Message);
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
