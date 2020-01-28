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
        private readonly IYoutubeService _youtubeService;

        #endregion

        #region Constructors

        public BackupService(IConfigurationRoot configuration,
            IYoutubeService syncService,
            IItemRepository itemRepository,
            IChannelRepository channelRepository)
        {
            _configuration = configuration;
            _itemRepository = itemRepository;
            _youtubeService = syncService;
            _channelRepository = channelRepository;
        }

        #endregion

        #region Properties

        public string AppSettings => "AppSettings";
        public bool CustomDbEnabled => _configuration.GetValue<bool>($"{AppSettings}:{KeyEnableCustomDb}");
        public string CustomDbPath => _configuration.GetValue<string>($"{AppSettings}:{KeyDbDir}");
        public string DailyBackupSchedule => _configuration.GetValue<string>($"{AppSettings}:{KeyDailyBackupSchedule}");
        public string DailyParserUpdateSchedule => _configuration.GetValue<string>($"{AppSettings}:{KeyDailyParserUpdateSchedule}");
        public string DailySyncSchedule => _configuration.GetValue<string>($"{AppSettings}:{KeyDailySyncSchedule}");
        public string DownloadDir => _configuration.GetValue<string>($"{AppSettings}:{KeyDownloadDir}");
        public bool EnableDailyBackupSchedule => _configuration.GetValue<bool>($"{AppSettings}:{KeyEnableDailyBackupSchedule}");

        public bool EnableDailyParserUpdateSchedule =>
            _configuration.GetValue<bool>($"{AppSettings}:{KeyEnableDailyParserUpdateSchedule}");

        public bool EnableDailySyncSchedule => _configuration.GetValue<bool>($"{AppSettings}:{KeyEnableDailySyncSchedule}");
        public bool EnableRepeatBackupSchedule => _configuration.GetValue<bool>($"{AppSettings}:{KeyEnableRepeatBackupSchedule}");

        public bool EnableRepeatParserUpdateSchedule =>
            _configuration.GetValue<bool>($"{AppSettings}:{KeyEnableRepeatParserUpdateSchedule}");

        public bool EnableRepeatSyncSchedule => _configuration.GetValue<bool>($"{AppSettings}:{KeyEnableRepeatSyncSchedule}");
        public string KeyDailyBackupSchedule => "DailyBackupSchedule";
        public string KeyDailyParserUpdateSchedule => "DailyParserUpdateSchedule";
        public string KeyDailySyncSchedule => "DailySyncSchedule";
        public string KeyDbDir => "DbDir";
        public string KeyDownloadDir => "DownloadDir";
        public string KeyEnableCustomDb => "EnableCustomDb";
        public string KeyEnableDailyBackupSchedule => "EnableDailyBackupSchedule";
        public string KeyEnableDailyParserUpdateSchedule => "EnableDailyParserUpdateSchedule";
        public string KeyEnableDailySyncSchedule => "EnableDailySyncSchedule";
        public string KeyEnableRepeatBackupSchedule => "EnableRepeatBackupSchedule";
        public string KeyEnableRepeatParserUpdateSchedule => "EnableRepeatParserUpdateSchedule";
        public string KeyEnableRepeatSyncSchedule => "EnableRepeatSyncSchedule";
        public string KeyRepeatBackupSchedule => "RepeatBackupSchedule";
        public string KeyRepeatParserUpdateSchedule => "RepeatParserUpdateSchedule";
        public string KeyRepeatSyncSchedule => "RepeatSyncSchedule";
        public string KeyWatchApp => "WatchApp";
        public string KeyYouParam => "YouParam";
        public string KeyYouParser => "YouParser";
        public string RepeatBackupSchedule => _configuration.GetValue<string>($"{AppSettings}:{KeyRepeatBackupSchedule}");
        public string RepeatParserUpdateSchedule => _configuration.GetValue<string>($"{AppSettings}:{KeyRepeatParserUpdateSchedule}");
        public string RepeatSyncSchedule => _configuration.GetValue<string>($"{AppSettings}:{KeyRepeatSyncSchedule}");
        public string UseSqlite { get; set; }
        public bool UseSqliteInit { get; set; } = false;
        public string WatchApp => _configuration.GetValue<string>($"{AppSettings}:{KeyWatchApp}");
        public string YouParam => _configuration.GetValue<string>($"{AppSettings}:{KeyYouParam}");
        public string YouParser => _configuration.GetValue<string>($"{AppSettings}:{KeyYouParser}");

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
            var res = string.Empty;
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
                backup = JsonConvert.DeserializeObject<BackupAll>(r.ReadToEnd());
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

                var tasks = backup.Items.Where(x => !existChannels.Contains(x.ChannelId))
                    .Select(item => _youtubeService.GetChannelAsync(item.ChannelId, false, item.ChannelTitle)).ToList();

                setLog?.Invoke($"Total channels: {tasks.Count}, working..");

                var channels = new List<Channel>();
                await Task.WhenAll(tasks).ContinueWith(done =>
                {
                    foreach (var task in tasks)
                    {
                        var rr = backup.Items.FirstOrDefault(x => x.ChannelId == task.Result.Id);
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

        public void SaveChanges(string key, string value)
        {
            var prov = (FileConfigurationProvider)_configuration.Providers.First();
            var file = Path.Combine(((PhysicalFileProvider)prov.Source.FileProvider).Root, prov.Source.Path);
            dynamic jsonObj = JsonConvert.DeserializeObject(File.ReadAllText(file));
            jsonObj[AppSettings][key] = value;
            File.WriteAllText(file, JsonConvert.SerializeObject(jsonObj, Formatting.Indented));
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
            string folder;
            if (CustomDbEnabled && !string.IsNullOrWhiteSpace(CustomDbPath))
            {
                var dir = new DirectoryInfo(CustomDbPath);
                folder = dir.Exists ? dir.FullName : ((PhysicalFileProvider)prov.Source.FileProvider).Root;
            }
            else
            {
                folder = ((PhysicalFileProvider)prov.Source.FileProvider).Root;
            }

            return Path.Combine(folder, prov.Source.Path);
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
                foreach (var item in lst)
                {
                    try
                    {
                        setLog?.Invoke($"Start restoring {item.ChannelTitle} - {item.ChannelId}..");
                        setTitle.Invoke($"Restoring {item.ChannelTitle}..");
                        var channel = await _youtubeService.GetChannelAsync(item.ChannelId, false, item.ChannelTitle);
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
