using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using DynamicData.Binding;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using v00v.Model;
using v00v.Model.Extensions;
using v00v.Services.Backup;
using v00v.Services.ContentProvider;

namespace v00v.ViewModel.Startup
{
    public class StartupModel : ViewModelBase, IStartupModel
    {
        #region Constants

        private const string AppSettings = "AppSettings";
        private const string KeyBaseDir = "BaseDir";
        private const string KeyEnableDailySchedule = "EnableDailySchedule";
        private const string KeyEnableRepeatSchedule = "EnableRepeatSchedule";
        private const string KeyRepeatSchedule = "RepeatSchedule(min)";
        private const string KeyTimeSchedule = "TimeSchedule(HH:mm)";
        private const string KeyWatchApp = "WatchApp";
        private const string KeyYouParam = "YouParam";
        private const string KeyYouParser = "YouParser";

        #endregion

        #region Static and Readonly Fields

        private readonly IBackupService _backupService;
        private readonly bool _isInited;

        #endregion

        #region Fields

        private string _baseDir;
        private TimeSpan _dailySyncTime;
        private string _downloadUrl;
        private bool _enableDailySchedule;
        private bool _enableRepeatSchedule;
        private bool _isYoutubeLink;
        private int _repeatMin;
        private string _selectedFormat;
        private string _selectedHour;
        private string _selectedMinute;
        private bool _showSettings;
        private bool _subsEnabled;
        private string _watchApp;
        private bool _withSubs;

        #endregion

        #region Constructors

        public StartupModel(IConfiguration configuration, IBackupService backupService, IYoutubeService youtubeService)
        {
            _backupService = backupService;

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

            _baseDir = configuration.GetValue<string>($"{AppSettings}:{KeyBaseDir}");
            _watchApp = configuration.GetValue<string>($"{AppSettings}:{KeyWatchApp}");
            YouParser = configuration.GetValue<string>($"{AppSettings}:{KeyYouParser}");
            YouParam = configuration.GetValue<string>($"{AppSettings}:{KeyYouParam}");

            _enableDailySchedule = configuration.GetValue<bool>($"{AppSettings}:{KeyEnableDailySchedule}");
            if (_enableDailySchedule)
            {
                DailyParsed = DateTime.TryParseExact(configuration.GetValue<string>($"{AppSettings}:{KeyTimeSchedule}"),
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

            _enableRepeatSchedule = configuration.GetValue<bool>($"{AppSettings}:{KeyEnableRepeatSchedule}");
            if (_enableRepeatSchedule)
            {
                RepeatParsed = int.TryParse(configuration.GetValue<string>($"{AppSettings}:{KeyRepeatSchedule}"),
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
                    SaveChanges(KeyEnableRepeatSchedule, EnableRepeatSchedule.ToString());
                }
            });
            this.WhenValueChanged(x => EnableDailySchedule).Subscribe(x =>
            {
                if (_isInited)
                {
                    SaveChanges(KeyEnableDailySchedule, EnableDailySchedule.ToString());
                }
            });
            this.WhenValueChanged(x => RepeatMin).Subscribe(x =>
            {
                if (_isInited)
                {
                    SaveChanges(KeyRepeatSchedule, RepeatMin.ToString());
                }
            });
            this.WhenValueChanged(x => SelectedHour).Subscribe(x =>
            {
                if (_isInited)
                {
                    SaveChanges(KeyTimeSchedule, $"{SelectedHour}:{SelectedMinute}");
                }
            });
            this.WhenValueChanged(x => SelectedMinute).Subscribe(x =>
            {
                if (_isInited)
                {
                    SaveChanges(KeyTimeSchedule, $"{SelectedHour}:{SelectedMinute}");
                }
            });
            this.WhenValueChanged(x => BaseDir).Subscribe(x =>
            {
                if (_isInited)
                {
                    var path = BaseDir.Trim();
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        var dir = new DirectoryInfo(path);
                        if (dir.Exists)
                        {
                            SaveChanges(KeyBaseDir, path);
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
                            SaveChanges(KeyWatchApp, path);
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

        public string BaseDir
        {
            get => _baseDir;
            set => Update(ref _baseDir, value);
        }

        public bool DailyParsed { get; }

        public TimeSpan DailySyncTime
        {
            get => _dailySyncTime;
            set => Update(ref _dailySyncTime, value);
        }

        public ICommand DownloadCommand { get; set; }

        public string DownloadUrl
        {
            get => _downloadUrl;
            set => Update(ref _downloadUrl, value);
        }

        public bool EnableDailySchedule
        {
            get => _enableDailySchedule;
            set => Update(ref _enableDailySchedule, value);
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

        private async Task DownloadItem()
        {
            if (string.IsNullOrWhiteSpace(DownloadUrl) || !DownloadUrl.CheckUrlValid())
            {
                return;
            }

            var param = IsYoutubeLink ? MakeParam(SelectedFormat) : $"-o \"{BaseDir}\\%(title)s.%(ext)s\" \"{DownloadUrl}\" {YouParam}";

            await Task.Run(() =>
            {
                using (var process = Process.Start(YouParser, param))
                {
                    process?.Close();
                }
            }).ConfigureAwait(false);
        }

        private string MakeParam(string par)
        {
            string param = null;
            var basePar = $" -o \"{BaseDir}\\%(title)s.%(ext)s\" \"{DownloadUrl}\" {YouParam}";

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

        private void SaveChanges(string key, string value)
        {
            var file = _backupService.GetSettingsName();
            dynamic jsonObj = JsonConvert.DeserializeObject(File.ReadAllText(file));
            jsonObj[AppSettings][key] = value;
            File.WriteAllText(file, JsonConvert.SerializeObject(jsonObj, Formatting.Indented));
        }

        #endregion
    }
}
