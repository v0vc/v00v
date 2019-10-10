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

        public async Task Backup(IEnumerable<Channel> entries)
        {
            if (!CheckJsonBackup())
            {
                return;
            }

            var bcp = new BackupAll
            {
                Items = entries.Where(x => !x.IsStateChannel).Select(channel => new BackupItem
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
        }

        public async Task<List<Channel>> Restore(IEnumerable<string> existChannels, bool isFast)
        {
            var channels = new List<Channel>();
            if (!CheckJsonBackup())
            {
                return channels;
            }

            var fileName = GetBackupName();
            BackupAll res = null;
            using (var r = new StreamReader(fileName))
            {
                string json = r.ReadToEnd();
                var ss = Task.Factory.StartNew(() =>
                {
                    res = JsonConvert.DeserializeObject<BackupAll>(json);
                });
                await ss;
            }

            if (res == null)
            {
                //await Console.Out.WriteLineAsync("No backup, bye");
                return channels;
            }

            //await
            //    Console.Out.WriteLineAsync($"Backup has {res.Items.Count()} channels... and {res.ItemsState.Count} items state, start restoring..");
            if (isFast)
            {
                List<Task<Channel>> tasks = res.Items.Where(x => !existChannels.Contains(x.ChannelId))
                    .Select(item => _syncService.GetChannelAsync(item.ChannelId, item.ChannelTitle)).ToList();

                await Task.WhenAll(tasks);

                foreach (Task<Channel> task in tasks)
                {
                    var ch = task.Result;
                    BackupItem rr = res.Items.FirstOrDefault(x => x.ChannelId == ch.Id);
                    if (rr == null)
                    {
                        continue;
                    }

                    ch.Tags.AddRange(rr.Tags.Select(x => new Tag { Id = x }));
                    channels.Add(ch);
                }

                //await Console.Out.WriteLineAsync($"Restored {channels.Count} channels, now saving..");
                var rows = await _channelRepository.AddChannels(channels);
                //await Console.Out.WriteLineAsync($"Saved {rows} rows!");
            }
            else
            {
                channels.AddRange(await RestoreOneByOne(res.Items.Where(x => !existChannels.Contains(x.ChannelId))));
            }

            foreach ((string key, byte value) in res.ItemsState)
            {
                //await Console.Out.WriteLineAsync($"Set {key}...{value}");
                await _itemRepository.UpdateItemsWatchState(key, value);
            }

            //await Console.Out.WriteLineAsync("Restore finished");
            return channels;
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
            var filename = Path.Combine(((PhysicalFileProvider)prov.Source.FileProvider).Root, prov.Source.Path);
            //Console.Out.WriteLine($"Backup location:{filename}");
            return filename;
        }

        private async Task<List<Channel>> RestoreOneByOne(IEnumerable<BackupItem> lst)
        {
            var channels = new List<Channel>();
            while (true)
            {
                var err = new List<BackupItem>();
                foreach (BackupItem item in lst)
                {
                    try
                    {
                        //await Console.Out.WriteLineAsync($"Start restoring {item.ChannelId}..");
                        var channel = await _syncService.GetChannelAsync(item.ChannelId, item.ChannelTitle);
                        channel.Tags.AddRange(item.Tags.Select(x => new Tag { Id = x }));
                        //await Console.Out.WriteLineAsync($"Restored {channel.Title}, now saving..");
                        var rows = await _channelRepository.AddChannel(channel);
                        //await Console.Out.WriteLineAsync($"Saved {rows}!");
                        channels.Add(channel);
                    }
                    catch (Exception e)
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

            return channels;
        }

        #endregion
    }
}