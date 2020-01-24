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

        private TimeSpan _dailyParserUpdateTime;
        private TimeSpan _dailySyncTime;
        private string _dbDir;
        private string _downloadDir;
        private string _downloadUrl;
        private bool _enableCustomDb;
        private bool _enableDailySchedule;
        private bool _enableParserUpdateSchedule;
        private bool _enableRepeatSchedule;
        private bool _isYoutubeLink;
        private int _repeatMin;
        private string _selectedFormat;
        private string _selectedHour;
        private string _selectedMinute;
        private string _selectedParserHour;
        private string _selectedParserMinute;
        private bool _showSettings;
        private bool _subsEnabled;
        private string _watchApp;
        private bool _withSubs;

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

            _enableParserUpdateSchedule = backupService.EnableParserUpdateSchedule;
            if (_enableParserUpdateSchedule)
            {
                ParserUpdateParsed = DateTime.TryParseExact(backupService.ParserUpdateSchedule,
                                                            "HH:mm",
                                                            CultureInfo.InvariantCulture,
                                                            DateTimeStyles.None,
                                                            out var dt);
                if (ParserUpdateParsed)
                {
                    _dailyParserUpdateTime = dt.TimeOfDay;
                    SelectedParserHour = $"{_dailyParserUpdateTime.Hours:D2}";
                    SelectedParserMinute = $"{_dailyParserUpdateTime.Minutes:D2}";
                }
            }

            _enableDailySchedule = backupService.EnableDailySchedule;
            if (_enableDailySchedule)
            {
                DailyParsed = DateTime.TryParseExact(backupService.DailySyncSchedule,
                                                     "HH:mm",
                                                     CultureInfo.InvariantCulture,
                                                     DateTimeStyles.None,
                                                     out var dt);
                if (DailyParsed)
                {
                    _dailySyncTime = dt.TimeOfDay;
                    SelectedHour = $"{_dailySyncTime.Hours:D2}";
                    SelectedMinute = $"{_dailySyncTime.Minutes:D2}";
                }
            }

            _enableRepeatSchedule = backupService.EnableRepeatSyncSchedule;
            if (_enableRepeatSchedule)
            {
                RepeatParsed = int.TryParse(backupService.RepeatSyncSchedule,
                                            NumberStyles.None,
                                            CultureInfo.InvariantCulture,
                                            out var min);
                if (RepeatParsed)
                {
                    _repeatMin = min;
                    RepeatMin = _repeatMin;
                }
            }

            this.WhenValueChanged(x => EnableRepeatSchedule).Subscribe(x =>
            {
                if (_isInited)
                {
                    backupService.SaveChanges(backupService.KeyEnableRepeatSyncSchedule, EnableRepeatSchedule.ToString());
                }
            });
            this.WhenValueChanged(x => EnableDailySchedule).Subscribe(x =>
            {
                if (_isInited)
                {
                    backupService.SaveChanges(backupService.KeyEnableDailySchedule, EnableDailySchedule.ToString());
                }
            });
            this.WhenValueChanged(x => EnableCustomDb).Subscribe(x =>
            {
                if (_isInited)
                {
                    backupService.SaveChanges(backupService.KeyEnableCustomDb, EnableCustomDb.ToString());
                    DbDir = !EnableCustomDb ? null : backupService.CustomDbPath;
                }
            });
            this.WhenValueChanged(x => EnableParserUpdateSchedule).Subscribe(x =>
            {
                if (_isInited)
                {
                    backupService.SaveChanges(backupService.KeyEnableParserUpdateSchedule, EnableParserUpdateSchedule.ToString());
                }
            });
            this.WhenValueChanged(x => RepeatMin).Subscribe(x =>
            {
                if (_isInited)
                {
                    backupService.SaveChanges(backupService.KeyRepeatSyncSchedule, RepeatMin.ToString());
                }
            });
            this.WhenValueChanged(x => SelectedHour).Subscribe(x =>
            {
                if (_isInited)
                {
                    backupService.SaveChanges(backupService.KeyDailySyncSchedule, $"{SelectedHour}:{SelectedMinute}");
                }
            });
            this.WhenValueChanged(x => SelectedMinute).Subscribe(x =>
            {
                if (_isInited)
                {
                    backupService.SaveChanges(backupService.KeyDailySyncSchedule, $"{SelectedHour}:{SelectedMinute}");
                }
            });
            this.WhenValueChanged(x => SelectedParserHour).Subscribe(x =>
            {
                if (_isInited)
                {
                    backupService.SaveChanges(backupService.KeyParserUpdateSchedule, $"{SelectedParserHour}:{SelectedParserMinute}");
                }
            });
            this.WhenValueChanged(x => SelectedParserMinute).Subscribe(x =>
            {
                if (_isInited)
                {
                    backupService.SaveChanges(backupService.KeyParserUpdateSchedule, $"{SelectedParserHour}:{SelectedParserMinute}");
                }
            });
            this.WhenValueChanged(x => DownloadDir).Subscribe(x =>
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
            this.WhenValueChanged(x => DbDir).Subscribe(x =>
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
            this.WhenValueChanged(x => WatchApp).Subscribe(x =>
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
            this.WhenValueChanged(x => SelectedFormat).Subscribe(x =>
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

            _isInited = true;
        }

        #endregion

        #region Properties

        public bool DailyParsed { get; }

        public TimeSpan DailyParserUpdateTime
        {
            get => _dailyParserUpdateTime;
            set => Update(ref _dailyParserUpdateTime, value);
        }

        public TimeSpan DailySyncTime
        {
            get => _dailySyncTime;
            set => Update(ref _dailySyncTime, value);
        }

        public string DbDir
        {
            get => _dbDir;
            set => Update(ref _dbDir, value);
        }

        public ICommand DownloadCommand { get; set; }

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

        public bool EnableDailySchedule
        {
            get => _enableDailySchedule;
            set => Update(ref _enableDailySchedule, value);
        }

        public bool EnableParserUpdateSchedule
        {
            get => _enableParserUpdateSchedule;
            set => Update(ref _enableParserUpdateSchedule, value);
        }

        public bool EnableRepeatSchedule
        {
            get => _enableRepeatSchedule;
            set => Update(ref _enableRepeatSchedule, value);
        }

        public IEnumerable<string> Formats { get; }

        public IEnumerable<string> Hours { get; }

        public bool IsYoutubeLink
        {
            get => _isYoutubeLink;
            set => Update(ref _isYoutubeLink, value);
        }

        public IEnumerable<string> Minutes { get; }

        public bool ParserUpdateParsed { get; }

        public int RepeatMin
        {
            get => _repeatMin;
            set => Update(ref _repeatMin, value);
        }

        public bool RepeatParsed { get; }

        public string SelectedFormat
        {
            get => _selectedFormat;
            set => Update(ref _selectedFormat, value);
        }

        public string SelectedHour
        {
            get => _selectedHour;
            set => Update(ref _selectedHour, value);
        }

        public string SelectedMinute
        {
            get => _selectedMinute;
            set => Update(ref _selectedMinute, value);
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

        public bool ShowSettings
        {
            get => _showSettings;
            set => Update(ref _showSettings, value);
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

            using (var process = new Process { StartInfo = pInfo })
            {
                process.Start();
                process.WaitForExit();
                process.Close();
            }
        }

        private async Task DownloadItem()
        {
            if (string.IsNullOrWhiteSpace(DownloadUrl) || !DownloadUrl.CheckUrlValid())
            {
                return;
            }

            await Task.Run(() =>
            {
                using (var process = Process.Start(YouParser,
                                                   IsYoutubeLink
                                                       ? MakeParam(SelectedFormat)
                                                       : $"-o \"{DownloadDir}\\%(title)s.%(ext)s\" \"{DownloadUrl}\" {YouParam}"))
                {
                    process?.Close();
                }
            }).ContinueWith(x => DownloadUrl = null);
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
