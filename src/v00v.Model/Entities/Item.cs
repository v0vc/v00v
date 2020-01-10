using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using v00v.Model.Enums;
using v00v.Model.Extensions;

namespace v00v.Model.Entities
{
    public class Item : ViewModelBase
    {
        #region Fields

        private bool _downloaded;
        private bool _isWorking;
        private Regex _numRegex;
        private double _percentage;
        private Process _proc;
        private Action<string> _setLog;
        private SyncState _syncState;
        private WatchState _watchState;

        #endregion

        #region Properties

        public string ChannelId { get; set; }

        public string ChannelTitle { get; set; }

        public long Comments { get; set; }

        public string Description { get; set; }

        public long DislikeCount { get; set; }

        public bool Downloaded
        {
            get => _downloaded;
            set => Update(ref _downloaded, value);
        }

        public int Duration { get; set; }

        public string DurationAgo => Timestamp.TimeAgo();

        public string DurationString => IntTostrTime(Duration);

        public string FileName { get; set; }

        public string Id { get; set; }

        public bool IsWorking
        {
            get => _isWorking;
            set => Update(ref _isWorking, value);
        }

        public IBitmap LargeThumb { get; set; }

        public long LikeCount { get; set; }

        public string Link => $"https://www.youtube.com/watch?v={Id}";
        public double OpacityThumb => WatchState == WatchState.Notset ? 1 : 0.6;

        public double Percentage
        {
            get => _percentage;
            set => Update(ref _percentage, value);
        }

        public bool Planned => WatchState == WatchState.Planned;

        public string SaveDir { get; set; }

        public SyncState SyncState
        {
            get => _syncState;
            set => Update(ref _syncState, value);
        }

        public IEnumerable<int> Tags { get; set; }

        public IBitmap Thumb => Thumbnail.CreateThumb();
        public string ThumbLink => $"http://img.youtube.com/vi/{Id}/0.jpg";

        public byte[] Thumbnail { get; set; }

        public string ThumbnailLink { get; set; }

        public DateTime Timestamp { get; set; }

        public string Title { get; set; }

        public long ViewCount { get; set; }

        public long ViewDiff { get; set; }
        public bool Watched => WatchState == WatchState.Watched;

        public WatchState WatchState
        {
            get => _watchState;
            set => Update(ref _watchState, value);
        }

        public bool WatchStateSet => WatchState == WatchState.Planned || WatchState == WatchState.Watched;

        #endregion

        #region Static Methods

        private static string IntTostrTime(int duration)
        {
            var t = TimeSpan.FromSeconds(duration);
            return t.Days > 0 ? $"{t.Days:D2}:{t.Hours:D2}:{t.Minutes:D2}:{t.Seconds:D2}" :
                t.Hours > 0 ? $"{t.Hours:D2}:{t.Minutes:D2}:{t.Seconds:D2}" : $"{t.Minutes:D2}:{t.Seconds:D2}";
        }

        #endregion

        #region Methods

        public async Task<bool> Download(string youdl, string youparam, string par, bool skip, Action<string> setLog)
        {
            if (_numRegex == null)
            {
                _numRegex = new Regex(@"[0-9][0-9]{0,2}\.[0-9]%", RegexOptions.Compiled);
            }

            if (_setLog == null)
            {
                _setLog = setLog;
            }

            IsWorking = true;
            var startInfo = new ProcessStartInfo(youdl, MakeParam(par, youparam))
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                ErrorDialog = false,
                CreateNoWindow = true,
                //StandardOutputEncoding = Encoding.UTF8,
                //StandardErrorEncoding = Encoding.UTF8,
                //StandardInputEncoding = Encoding.UTF8
            };

            await Task.Run(() =>
            {
                _proc = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
                _proc.OutputDataReceived += OutputDataReceived;
                _proc.Start();
                _proc.StandardInput.Close();
                _proc.BeginOutputReadLine();
                _proc.BeginErrorReadLine();
                _proc.WaitForExit();
            }).ContinueWith(x => HandleDownload(skip));

            return Downloaded;
        }

        public void RunItem(string mpcpath, string basedir)
        {
            var param = Downloaded && FileName != null ? $"\"{Path.Combine(basedir, ChannelId, FileName)}\" /play" : $"{Link} /play";
            var startInfo = new ProcessStartInfo(mpcpath, param)
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                RedirectStandardInput = false,
                ErrorDialog = false,
                CreateNoWindow = true,
            };

            using (var proc = new Process { StartInfo = startInfo, EnableRaisingEvents = false })
            {
                proc.Start();
                proc.Close();
            }
        }

        private double GetPercentFromYoudlOutput(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return 0;
            }

            var match = _numRegex.Match(input);
            return !match.Success ? 0 :
                double.TryParse(match.Value.TrimEnd('%'), NumberStyles.Any, CultureInfo.InvariantCulture, out var res) ? res : 0;
        }

        private void HandleDownload(bool skip)
        {
            IsWorking = false;
            var fn = new DirectoryInfo(SaveDir).GetFiles($"{Id}.*").FirstOrDefault();
            if (fn != null)
            {
                Downloaded = true;
                try
                {
                    var fileName =
                        $"{Title.RemoveInvalidChars().Replace('"', ' ').Replace('\'', ' ').Replace('?', ' ').Replace('/', ' ').Trim()}{fn.Extension}"
                            .FilterWhiteSpaces();

                    var fulname = Path.Combine(SaveDir, fileName);

                    if (File.Exists(fulname))
                    {
                        File.Delete(fulname);
                    }

                    File.Move(fn.FullName, fulname);
                    if (!skip)
                    {
                        FileName = fileName;
                    }
                }
                catch (Exception e)
                {
                    FileName = fn.Name;
                    _setLog?.Invoke(e.Message);
                }
            }
            else
            {
                Downloaded = false;
            }

            _proc.OutputDataReceived -= OutputDataReceived;
            _proc.Dispose();
        }

        private string MakeParam(string par, string youParam)
        {
            var param = string.Empty;
            var basePar = $"{SaveDir}\\{Id}.%(ext)s\" \"{Link}\" {youParam}";
            switch (par)
            {
                case "simple":
                    param = $"-f best, -o \"{basePar}";
                    break;
                case "hd":
                    param = $"-f bestvideo+bestaudio, -o \"{basePar}";
                    break;
                case "video":
                    param = $"-f bestvideo, -o \"{basePar}";
                    break;
                case "audio":
                    param = $"--extract-audio -o \"{basePar} --audio-format mp3";
                    break;
                case "subs":
                    param = $"-o \"{basePar} --all-subs --skip-download";
                    break;
            }

            return param;
        }

        #endregion

        #region Event Handling

        private void OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e?.Data == null)
            {
                return;
            }

            _setLog?.Invoke(e.Data);

            if (e.Data.StartsWith("[download]"))
            {
                Percentage = GetPercentFromYoudlOutput(e.Data);
            }
        }

        #endregion
    }
}
