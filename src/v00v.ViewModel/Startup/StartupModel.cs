using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using DynamicData.Binding;
using v00v.Model;
using v00v.Model.Extensions;
using v00v.Services.Backup;
using v00v.Services.ContentProvider;

namespace v00v.ViewModel.Startup
{
    public class StartupModel : ViewModelBase, IStartupModel
    {
        #region Static and Readonly Fields

        private readonly bool _isInited;

        #endregion

        #region Fields

        private string _dbDir;
        private string _downloadDir;
        private string _downloadUrl;
        private bool _enableCustomDb;
        private bool _enableDailyBackupSchedule;
        private bool _enableDailyParserUpdateSchedule;
        private bool _enableDailySyncSchedule;
        private bool _enableRepeatBackupSchedule;
        private bool _enableRepeatParserUpdateSchedule;
        private bool _enableRepeatSyncSchedule;
        private bool _isYoutubeLink;
        private int _repeatBackupMin;
        private int _repeatParserMin;
        private int _repeatSyncMin;
        private string _selectedBackupHour;
        private string _selectedBackupMinute;
        private string _selectedFormat;
        private string _selectedParserHour;
        private string _selectedParserMinute;
        private string _selectedSyncHour;
        private string _selectedSyncMinute;
        private bool _subsEnabled;
        private string _watchApp;
        private bool _withSubs;
        private string _youApiKey;

        #endregion

        #region Constructors

        public StartupModel(IBackupService backupService, IYoutubeService youtubeService)
        {
            Hours = new[]
            {
                "00", "01", "02", "03", "04", "05", "06", "07", "08", "09", "10", "11", "12", "13", "14", "15", "16", "17", "18",
                "19", "20", "21", "22", "23", "24"
            };
            Minutes = new[]
            {
                "00", "01", "02", "03", "04", "05", "06", "07", "08", "09", "10", "11", "12", "13", "14", "15", "16", "17", "18",
                "19", "20", "21", "22", "23", "24", "25", "26", "27", "28", "29", "30", "31", "32", "33", "34", "35", "36", "37",
                "38", "39", "40", "41", "42", "43", "44", "45", "46", "47", "48", "49", "50", "51", "52", "53", "54", "55", "56",
                "57", "58", "59"
            };
            Formats = new[] { "480p", "720p", "HD", "Audio only", "Video only", "Subtitles only" };

            DownloadCommand = new Command(async () => await DownloadItem());

            _downloadDir = backupService.DownloadDir;
            _enableCustomDb = backupService.CustomDbEnabled;
            if (_enableCustomDb)
            {
                _dbDir = backupService.CustomDbPath;
            }

            _watchApp = backupService.WatchApp;
            YouParser = backupService.YouParser;
            YouParam = backupService.YouParam;
            YouApiKey = backupService.YouApiKey;

            _enableDailyParserUpdateSchedule = backupService.EnableDailyParserUpdateSchedule;
            if (_enableDailyParserUpdateSchedule)
            {
                DailyParserUpdateParsed = DateTime.TryParseExact(backupService.DailyParserUpdateSchedule,
                                                                 "HH:mm",
                                                                 CultureInfo.InvariantCulture,
                                                                 DateTimeStyles.None,
                                                                 out var dt);
                if (DailyParserUpdateParsed)
                {
                    DailyParserUpdateTime = dt.TimeOfDay;
                    SelectedParserHour = $"{DailyParserUpdateTime.Hours:D2}";
                    SelectedParserMinute = $"{DailyParserUpdateTime.Minutes:D2}";
                }
            }

            _enableDailySyncSchedule = backupService.EnableDailySyncSchedule;
            if (_enableDailySyncSchedule)
            {
                DailySyncParsed = DateTime.TryParseExact(backupService.DailySyncSchedule,
                                                         "HH:mm",
                                                         CultureInfo.InvariantCulture,
                                                         DateTimeStyles.None,
                                                         out var dt);
                if (DailySyncParsed)
                {
                    DailySyncTime = dt.TimeOfDay;
                    SelectedSyncHour = $"{DailySyncTime.Hours:D2}";
                    SelectedSyncMinute = $"{DailySyncTime.Minutes:D2}";
                }
            }

            _enableDailyBackupSchedule = backupService.EnableDailyBackupSchedule;
            if (_enableDailyBackupSchedule)
            {
                DailyBackupParsed = DateTime.TryParseExact(backupService.DailyBackupSchedule,
                                                           "HH:mm",
                                                           CultureInfo.InvariantCulture,
                                                           DateTimeStyles.None,
                                                           out var dt);
                if (DailyBackupParsed)
                {
                    DailyBackupTime = dt.TimeOfDay;
                    SelectedBackupHour = $"{DailyBackupTime.Hours:D2}";
                    SelectedBackupMinute = $"{DailyBackupTime.Minutes:D2}";
                }
            }

            _enableRepeatSyncSchedule = backupService.EnableRepeatSyncSchedule;
            if (_enableRepeatSyncSchedule)
            {
                RepeatSyncParsed = int.TryParse(backupService.RepeatSyncSchedule,
                                                NumberStyles.None,
                                                CultureInfo.InvariantCulture,
                                                out var min);
                if (RepeatSyncParsed && min >= 1)
                {
                    _repeatSyncMin = min;
                }
            }

            _enableRepeatParserUpdateSchedule = backupService.EnableRepeatParserUpdateSchedule;
            if (_enableRepeatParserUpdateSchedule)
            {
                RepeatParserUpdateParsed = int.TryParse(backupService.RepeatParserUpdateSchedule,
                                                        NumberStyles.None,
                                                        CultureInfo.InvariantCulture,
                                                        out var min);
                if (RepeatParserUpdateParsed && min >= 1)
                {
                    _repeatParserMin = min;
                }
            }

            _enableRepeatBackupSchedule = backupService.EnableRepeatBackupSchedule;
            if (_enableRepeatBackupSchedule)
            {
                RepeatBackupParsed = int.TryParse(backupService.RepeatBackupSchedule,
                                                  NumberStyles.None,
                                                  CultureInfo.InvariantCulture,
                                                  out var min);
                if (RepeatBackupParsed && min >= 1)
                {
                    _repeatBackupMin = min;
                }
            }

            this.WhenValueChanged(x => EnableCustomDb).Subscribe(_ =>
            {
                if (_isInited)
                {
                    backupService.SaveChanges(backupService.KeyEnableCustomDb, EnableCustomDb.ToString());
                    DbDir = !EnableCustomDb ? null : backupService.CustomDbPath;
                }
            });
            this.WhenValueChanged(x => EnableRepeatSyncSchedule).Subscribe(_ =>
            {
                if (_isInited)
                {
                    backupService.SaveChanges(backupService.KeyEnableRepeatSyncSchedule, EnableRepeatSyncSchedule.ToString());
                }
            });
            this.WhenValueChanged(x => EnableDailySyncSchedule).Subscribe(_ =>
            {
                if (_isInited)
                {
                    backupService.SaveChanges(backupService.KeyEnableDailySyncSchedule, EnableDailySyncSchedule.ToString());
                }
            });
            this.WhenValueChanged(x => EnableDailyParserUpdateSchedule).Subscribe(_ =>
            {
                if (_isInited)
                {
                    backupService.SaveChanges(backupService.KeyEnableDailyParserUpdateSchedule,
                                              EnableDailyParserUpdateSchedule.ToString());
                }
            });
            this.WhenValueChanged(x => EnableRepeatParserUpdateSchedule).Subscribe(_ =>
            {
                if (_isInited)
                {
                    backupService.SaveChanges(backupService.KeyEnableRepeatParserUpdateSchedule,
                                              EnableRepeatParserUpdateSchedule.ToString());
                }
            });
            this.WhenValueChanged(x => EnableDailyBackupSchedule).Subscribe(_ =>
            {
                if (_isInited)
                {
                    backupService.SaveChanges(backupService.KeyEnableDailyBackupSchedule, EnableDailyBackupSchedule.ToString());
                }
            });
            this.WhenValueChanged(x => EnableRepeatBackupSchedule).Subscribe(_ =>
            {
                if (_isInited)
                {
                    backupService.SaveChanges(backupService.KeyEnableRepeatBackupSchedule, EnableRepeatBackupSchedule.ToString());
                }
            });
            this.WhenValueChanged(x => RepeatSyncMin).Subscribe(_ =>
            {
                if (_isInited)
                {
                    backupService.SaveChanges(backupService.KeyRepeatSyncSchedule, RepeatSyncMin.ToString());
                }
            });
            this.WhenValueChanged(x => RepeatParserMin).Subscribe(_ =>
            {
                if (_isInited)
                {
                    backupService.SaveChanges(backupService.KeyRepeatParserUpdateSchedule, RepeatParserMin.ToString());
                }
            });
            this.WhenValueChanged(x => RepeatBackupMin).Subscribe(_ =>
            {
                if (_isInited)
                {
                    backupService.SaveChanges(backupService.KeyRepeatBackupSchedule, RepeatBackupMin.ToString());
                }
            });
            this.WhenValueChanged(x => SelectedSyncHour).Subscribe(_ =>
            {
                if (_isInited)
                {
                    backupService.SaveChanges(backupService.KeyDailySyncSchedule, $"{SelectedSyncHour}:{SelectedSyncMinute}");
                }
            });
            this.WhenValueChanged(x => SelectedSyncMinute).Subscribe(_ =>
            {
                if (_isInited)
                {
                    backupService.SaveChanges(backupService.KeyDailySyncSchedule, $"{SelectedSyncHour}:{SelectedSyncMinute}");
                }
            });
            this.WhenValueChanged(x => SelectedParserHour).Subscribe(_ =>
            {
                if (_isInited)
                {
                    backupService.SaveChanges(backupService.KeyDailyParserUpdateSchedule, $"{SelectedParserHour}:{SelectedParserMinute}");
                }
            });
            this.WhenValueChanged(x => SelectedParserMinute).Subscribe(_ =>
            {
                if (_isInited)
                {
                    backupService.SaveChanges(backupService.KeyDailyParserUpdateSchedule, $"{SelectedParserHour}:{SelectedParserMinute}");
                }
            });
            this.WhenValueChanged(x => SelectedBackupHour).Subscribe(_ =>
            {
                if (_isInited)
                {
                    backupService.SaveChanges(backupService.KeyDailyBackupSchedule, $"{SelectedBackupHour}:{SelectedBackupMinute}");
                }
            });
            this.WhenValueChanged(x => SelectedBackupMinute).Subscribe(_ =>
            {
                if (_isInited)
                {
                    backupService.SaveChanges(backupService.KeyDailyBackupSchedule, $"{SelectedBackupHour}:{SelectedBackupMinute}");
                }
            });
            this.WhenValueChanged(x => DownloadDir).Subscribe(_ =>
            {
                if (_isInited)
                {
                    var path = DownloadDir.Trim();
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        var dir = new DirectoryInfo(path);
                        if (dir.Exists)
                        {
                            backupService.SaveChanges(backupService.KeyDownloadDir, path);
                        }
                    }
                }
            });
            this.WhenValueChanged(x => DbDir).Subscribe(_ =>
            {
                if (_isInited)
                {
                    var path = DbDir?.Trim();
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        var dir = new DirectoryInfo(path);
                        if (dir.Exists)
                        {
                            backupService.SaveChanges(backupService.KeyDbDir, path);
                        }
                    }
                }
            });
            this.WhenValueChanged(x => WatchApp).Subscribe(_ =>
            {
                if (_isInited)
                {
                    var path = WatchApp.Trim();
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        var fn = new FileInfo(path);
                        if (fn.Exists)
                        {
                            backupService.SaveChanges(backupService.KeyWatchApp, path);
                        }
                    }
                }
            });
            this.WhenValueChanged(x => DownloadUrl).Subscribe(x =>
            {
                if (_isInited)
                {
                    IsYoutubeLink = !string.IsNullOrWhiteSpace(DownloadUrl) && youtubeService.IsYoutubeLink(DownloadUrl, out _);
                    if (IsYoutubeLink)
                    {
                        SelectedFormat = "720p";
                    }
                }
            });
            this.WhenValueChanged(x => SelectedFormat).Subscribe(_ =>
            {
                if (_isInited)
                {
                    SubsEnabled = SelectedFormat != "Subtitles only";
                    if (!SubsEnabled)
                    {
                        WithSubs = false;
                    }
                }
            });
            this.WhenValueChanged(x => YouApiKey).Subscribe(_ =>
            {
                if (_isInited)
                {
                    backupService.SaveChanges(backupService.KeyYouApiKey, $"{YouApiKey}");
                }
            });

            _isInited = true;
        }

        #endregion

        #region Properties

        public bool DailyBackupParsed { get; }

        public TimeSpan DailyBackupTime { get; set; }

        public bool DailyParserUpdateParsed { get; }

        public TimeSpan DailyParserUpdateTime { get; set; }

        public bool DailySyncParsed { get; }

        public TimeSpan DailySyncTime { get; set; }

        public string DbDir
        {
            get => _dbDir;
            set => Update(ref _dbDir, value);
        }

        public ICommand DownloadCommand { get; }

        public string DownloadDir
        {
            get => _downloadDir;
            set => Update(ref _downloadDir, value);
        }

        public string DownloadUrl
        {
            get => _downloadUrl;
            set => Update(ref _downloadUrl, value);
        }

        public bool EnableCustomDb
        {
            get => _enableCustomDb;
            set => Update(ref _enableCustomDb, value);
        }

        public bool EnableDailyBackupSchedule
        {
            get => _enableDailyBackupSchedule;
            set => Update(ref _enableDailyBackupSchedule, value);
        }

        public bool EnableDailyParserUpdateSchedule
        {
            get => _enableDailyParserUpdateSchedule;
            set => Update(ref _enableDailyParserUpdateSchedule, value);
        }

        public bool EnableDailySyncSchedule
        {
            get => _enableDailySyncSchedule;
            set => Update(ref _enableDailySyncSchedule, value);
        }

        public bool EnableRepeatBackupSchedule
        {
            get => _enableRepeatBackupSchedule;
            set => Update(ref _enableRepeatBackupSchedule, value);
        }

        public bool EnableRepeatParserUpdateSchedule
        {
            get => _enableRepeatParserUpdateSchedule;
            set => Update(ref _enableRepeatParserUpdateSchedule, value);
        }

        public bool EnableRepeatSyncSchedule
        {
            get => _enableRepeatSyncSchedule;
            set => Update(ref _enableRepeatSyncSchedule, value);
        }

        public IEnumerable<string> Formats { get; }

        public IEnumerable<string> Hours { get; }

        public bool IsYoutubeLink
        {
            get => _isYoutubeLink;
            set => Update(ref _isYoutubeLink, value);
        }

        public IEnumerable<string> Minutes { get; }

        public int RepeatBackupMin
        {
            get => _repeatBackupMin;
            set => Update(ref _repeatBackupMin, value);
        }

        public bool RepeatBackupParsed { get; }

        public int RepeatParserMin
        {
            get => _repeatParserMin;
            set => Update(ref _repeatParserMin, value);
        }

        public bool RepeatParserUpdateParsed { get; }

        public int RepeatSyncMin
        {
            get => _repeatSyncMin;
            set => Update(ref _repeatSyncMin, value);
        }

        public bool RepeatSyncParsed { get; }

        public string SelectedBackupHour
        {
            get => _selectedBackupHour;
            set => Update(ref _selectedBackupHour, value);
        }

        public string SelectedBackupMinute
        {
            get => _selectedBackupMinute;
            set => Update(ref _selectedBackupMinute, value);
        }

        public string SelectedFormat
        {
            get => _selectedFormat;
            set => Update(ref _selectedFormat, value);
        }

        public string SelectedParserHour
        {
            get => _selectedParserHour;
            set => Update(ref _selectedParserHour, value);
        }

        public string SelectedParserMinute
        {
            get => _selectedParserMinute;
            set => Update(ref _selectedParserMinute, value);
        }

        public string SelectedSyncHour
        {
            get => _selectedSyncHour;
            set => Update(ref _selectedSyncHour, value);
        }

        public string SelectedSyncMinute
        {
            get => _selectedSyncMinute;
            set => Update(ref _selectedSyncMinute, value);
        }

        public bool SubsEnabled
        {
            get => _subsEnabled;
            set => Update(ref _subsEnabled, value);
        }

        public string WatchApp
        {
            get => _watchApp;
            set => Update(ref _watchApp, value);
        }

        public bool WithSubs
        {
            get => _withSubs;
            set => Update(ref _withSubs, value);
        }

        public string YouApiKey
        {
            get => _youApiKey;
            set => Update(ref _youApiKey, value);
        }

        public string YouParam { get; }

        public string YouParser { get; }

        #endregion

        #region Methods

        public void UpdateParser(int i)
        {
            var pInfo = new ProcessStartInfo
            {
                FileName = "choco.exe",
                Arguments = $"upgrade {YouParser}",
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = true,
                Verb = "runas",
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = pInfo };
            process.Start();
            process.WaitForExit();
            process.Close();
        }

        private Task DownloadItem()
        {
            return string.IsNullOrWhiteSpace(DownloadUrl) || !DownloadUrl.CheckUrlValid()
                ? Task.CompletedTask
                : Task.Run(() =>
                {
                    using var process = Process.Start(YouParser,
                                                      IsYoutubeLink
                                                          ? MakeParam(SelectedFormat)
                                                          : $"-o \"{DownloadDir}\\%(title)s.%(ext)s\" \"{DownloadUrl}\" {YouParam}");
                    process?.Close();
                }).ContinueWith(_ => { return DownloadUrl = null; });
        }

        private string MakeParam(string par)
        {
            string param = null;
            var basePar = $" -o \"{DownloadDir}\\%(title)s.%(ext)s\" \"{DownloadUrl}\" {YouParam}";

            switch (par)
            {
                case "480p":
                    param = $"-f bestvideo[height<=480]+bestaudio/best[height<=480],{basePar}";
                    break;
                case "720p":
                    param = $"-f best,{basePar}";
                    break;
                case "HD":
                    param = $"-f bestvideo+bestaudio,{basePar}";
                    break;
                case "Video only":
                    param = $"-f bestvideo,{basePar}";
                    break;
                case "Audio only":
                    param = $"{basePar} --extract-audio --audio-format mp3";
                    break;
                case "Subtitles only":
                    param = $"{basePar} --all-subs --skip-download";
                    break;
            }

            if (SubsEnabled && WithSubs && par != "Subtitles only")
            {
                param += " --all-subs";
            }

            return param;
        }

        #endregion
    }
}
