using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.Input;
using System.Windows;
using System.Diagnostics;
using System.Security.Principal;

namespace UnifiVideoExporter
{
    public class MainWindowViewModel : NotifyPropertyAndErrorInfoBase
    {
        private string _controllerAddress = string.Empty;
        private string _username = string.Empty;
        private string _password = string.Empty;
        private string _ffmpegPath = string.Empty;
        private string _selectedCamera;
        private double _snapshotInterval = 60;
        private double _framesPerSecond = FfmpegWrapper.FfmpegFps;
        private DateTime _startDate = DateTime.Today;
        private DateTime _endDate = DateTime.Today;
        private string _startTime = "08:00";
        private string _endTime = "17:00";
        private string _statusMessage = "";
        private bool _statusIsError = false;
        private bool _isDownloadFormEnabled = false;
        private string _log = "";
        private bool _windowLoaded = false;
        private bool _autoConnect = false;
        private bool _cleanupVideoFiles = true;
        private bool _enableVerboseLogs = false;
        private string _localVideoPath = "";
        private bool _sendToTimelapseForm = true;
        private bool _validateAfterDownloading = false;
        private bool _isCreatingTimelapse = false;
        private bool _isDownloadingVideo = false;
        private TimelapseBuilder _timelapseBuilder;

        public string _EncryptedUserName { get; set; }
        public string _EncryptedPassword { get; set; }
        [JsonIgnore]
        public bool _settingsChanged = false;
        
        #region observable properties
        public string ControllerAddress
        {
            get => _controllerAddress;
            set
            {
                _settingsChanged = _windowLoaded && _controllerAddress != value;
                _controllerAddress = value;
                OnPropertyChanged(nameof(ControllerAddress));
                OnPropertyChanged(nameof(CanConnect));
                ValidateProperty(nameof(ControllerAddress), value);
            }
        }

        [JsonIgnore]
        public string Username
        {
            get => _username;
            set
            {
                _settingsChanged = _windowLoaded && _username != value;
                _username = value;
                OnPropertyChanged(nameof(Username));
                OnPropertyChanged(nameof(CanConnect));
                ValidateProperty(nameof(Username), value);
            }
        }

        [JsonIgnore]
        public string Password
        {
            get => _password;
            set
            {
                _settingsChanged = _windowLoaded && _password != value;
                _password = value;
                OnPropertyChanged(nameof(Password));
                OnPropertyChanged(nameof(CanConnect));
                ValidateProperty(nameof(Password), value);
            }
        }

        public string FfmpegPath
        {
            get => _ffmpegPath;
            set
            {
                _settingsChanged = _windowLoaded && _ffmpegPath != value;
                _ffmpegPath = value;
                OnPropertyChanged(nameof(FfmpegPath));
                ValidateProperty(nameof(FfmpegPath), value);
            }
        }

        [JsonIgnore]
        public string SelectedCamera
        {
            get => _selectedCamera;
            set
            {
                _settingsChanged = _windowLoaded && _selectedCamera != value;
                _selectedCamera = value;
                OnPropertyChanged(nameof(SelectedCamera));
                ValidateProperty(nameof(SelectedCamera), value);
            }
        }

        [JsonIgnore]
        public ObservableCollection<string> CameraList { get; set; }

        public double SnapshotInterval
        {
            get => _snapshotInterval;
            set
            {
                _settingsChanged = _windowLoaded && _snapshotInterval != value;
                _snapshotInterval = value;
                OnPropertyChanged(nameof(SnapshotInterval));
                ValidateProperty(nameof(SnapshotInterval), value);
            }
        }

        public double FramesPerSecond
        {
            get => _framesPerSecond;
            set
            {
                _settingsChanged = _windowLoaded && _framesPerSecond != value;
                _framesPerSecond = value;
                OnPropertyChanged(nameof(FramesPerSecond));
                ValidateProperty(nameof(FramesPerSecond), value);
            }
        }

        public DateTime StartDate
        {
            get => _startDate;
            set
            {
                _settingsChanged = _windowLoaded && _startDate != value;
                _startDate = value;
                OnPropertyChanged(nameof(StartDate));
                ValidateProperty(nameof(StartDate), value);
                ValidateProperty(nameof(EndDate), EndDate); // Revalidate EndDate
            }
        }

        public DateTime EndDate
        {
            get => _endDate;
            set
            {
                _settingsChanged = _windowLoaded && _endDate != value;
                _endDate = value;
                OnPropertyChanged(nameof(EndDate));
                ValidateProperty(nameof(EndDate), value);
            }
        }

        public string StartTime
        {
            get => _startTime;
            set
            {
                _settingsChanged = _windowLoaded && _startTime != value;
                _startTime = value;
                OnPropertyChanged(nameof(StartTime));
                ValidateProperty(nameof(StartTime), value);
                ValidateProperty(nameof(EndTime), EndTime); // Revalidate EndTime
            }
        }

        public string EndTime
        {
            get => _endTime;
            set
            {
                _settingsChanged = _windowLoaded && _endTime != value;
                _endTime = value;
                OnPropertyChanged(nameof(EndTime));
                ValidateProperty(nameof(EndTime), value);
            }
        }

        [JsonIgnore]
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(nameof(StatusMessage)); }
        }

        [JsonIgnore]
        public bool StatusIsError
        {
            get => _statusIsError;
            set { _statusIsError = value; OnPropertyChanged(nameof(StatusIsError)); }
        }

        [JsonIgnore]
        public bool IsDownloadFormEnabled
        {
            get => _isDownloadFormEnabled;
            set { _isDownloadFormEnabled = value; OnPropertyChanged(nameof(IsDownloadFormEnabled)); }
        }

        [JsonIgnore]
        public string Log
        {
            get => _log;
            set
            {
                _log = value;
                OnPropertyChanged(nameof(Log));
            }
        }

        public bool AutoConnect
        {
            get => _autoConnect;
            set
            {
                _settingsChanged = _windowLoaded && _autoConnect != value;
                _autoConnect = value;
                OnPropertyChanged(nameof(AutoConnect));
            }
        }

        public bool CleanupVideoFiles
        {
            get => _cleanupVideoFiles;
            set
            {
                _settingsChanged = _windowLoaded && _cleanupVideoFiles != value;
                _cleanupVideoFiles = value;
                OnPropertyChanged(nameof(CleanupVideoFiles));
            }
        }

        public bool EnableVerboseLogs
        {
            get => _enableVerboseLogs;
            set
            {
                _settingsChanged = _windowLoaded && _enableVerboseLogs != value;
                _enableVerboseLogs = value;
                OnPropertyChanged(nameof(EnableVerboseLogs));
            }
        }

        public string LocalVideoPath
        {
            get => _localVideoPath;
            set
            {
                _localVideoPath = value;
                OnPropertyChanged(nameof(LocalVideoPath));
                ValidateProperty(nameof(LocalVideoPath), value);
                OpenTimelapseFolderCommand.NotifyCanExecuteChanged();
            }
        }

        [JsonIgnore]
        public bool IsCreatingTimelapse
        {
            get => _isCreatingTimelapse;
            set
            {
                _isCreatingTimelapse = value;
                OnPropertyChanged(nameof(IsCreatingTimelapse));
                CreateTimelapseCommand.NotifyCanExecuteChanged();
            }
        }

        public bool SendToTimelapseForm
        {
            get => _sendToTimelapseForm;
            set
            {
                _sendToTimelapseForm = value;
                OnPropertyChanged(nameof(SendToTimelapseForm));
            }
        }

        public bool ValidateAfterDownloading
        {
            get => _validateAfterDownloading;
            set
            {
                _validateAfterDownloading = value;
                OnPropertyChanged(nameof(ValidateAfterDownloading));
            }
        }

        [JsonIgnore]
        public bool IsDownloadingVideo
        {
            get => _isDownloadingVideo;
            set
            {
                _isDownloadingVideo = value;
                OnPropertyChanged(nameof(IsDownloadingVideo));
                DownloadUnifiVideoCommand.NotifyCanExecuteChanged();
            }
        }

        #endregion

        #region validation

        private void ValidateProperty(string propertyName, object value)
        {
            var errors = new List<string>();

            switch (propertyName)
            {
                case nameof(ControllerAddress):
                    if (string.IsNullOrEmpty((string)value) || !Uri.TryCreate((string)value, UriKind.Absolute, out _))
                        errors.Add("Must be a valid absolute URI (e.g., https://192.168.1.1:7443).");
                    break;
                case nameof(LocalVideoPath):
                    if (string.IsNullOrEmpty((string)value) || !Directory.Exists((string)value))
                        errors.Add("Local video path does not exist at the specified path.");
                    break;
                case nameof(Username):
                    if (string.IsNullOrEmpty((string)value))
                        errors.Add("Username is required.");
                    break;

                case nameof(Password):
                    if (string.IsNullOrEmpty((string)value))
                        errors.Add("Password is required.");
                    break;
                case nameof(FfmpegPath):
                    if (string.IsNullOrEmpty((string)value))
                        errors.Add("FFmpeg path is required.");
                    else if (!File.Exists((string)value))
                        errors.Add("FFmpeg executable does not exist at the specified path.");
                    break;

                case nameof(SelectedCamera):
                    if (value == null && IsDownloadFormEnabled)
                        errors.Add("A camera must be selected.");
                    break;

                case nameof(SnapshotInterval):
                    if ((double)value <= 0)
                        errors.Add("Snapshot interval must be a positive number.");
                    break;
                case nameof(FramesPerSecond):
                    if ((double)value <= 0)
                        errors.Add("Frames per second must be a positive number.");
                    break;

                case nameof(StartDate):
                    if ((DateTime)value == default)
                        errors.Add("Start date is required.");
                    break;

                case nameof(EndDate):
                    if ((DateTime)value == default)
                        errors.Add("End date is required.");
                    else if ((DateTime)value < StartDate)
                        errors.Add("End date must be on or after start date.");
                    break;

                case nameof(StartTime):
                    if (!Regex.IsMatch((string)value, @"^([0-1]?[0-9]|2[0-3]):[0-5][0-9]$"))
                        errors.Add("Start time must be in HH:mm format.");
                    break;

                case nameof(EndTime):
                    if (!Regex.IsMatch((string)value, @"^([0-1]?[0-9]|2[0-3]):[0-5][0-9]$"))
                        errors.Add("End time must be in HH:mm format.");
                    else if (Regex.IsMatch((string)value, @"^([0-1]?[0-9]|2[0-3]):[0-5][0-9]$") &&
                             Regex.IsMatch(StartTime, @"^([0-1]?[0-9]|2[0-3]):[0-5][0-9]$") &&
                             TimeSpan.Parse((string)value) <= TimeSpan.Parse(StartTime))
                        errors.Add("End time must be after start time.");
                    break;
            }

            if (errors.Any())
                AddErrorRange(propertyName, errors);
            else
                ClearErrors(propertyName);
            ConnectCommand.NotifyCanExecuteChanged();
            DownloadUnifiVideoCommand.NotifyCanExecuteChanged();
            CreateTimelapseCommand.NotifyCanExecuteChanged();
        }

        #endregion

        [JsonIgnore]
        public AsyncRelayCommand ConnectCommand { get; }
        [JsonIgnore]
        public AsyncRelayCommand CreateTimelapseCommand { get; }
        [JsonIgnore]
        public AsyncRelayCommand DownloadUnifiVideoCommand { get; }
        [JsonIgnore]
        public RelayCommand OpenTimelapseFolderCommand { get; }
        [JsonIgnore]
        public RelayCommand OpenTaskSchedulerCommand { get; }

        public MainWindowViewModel()
        {
            CameraList = new ObservableCollection<string>();
            ConnectCommand = new AsyncRelayCommand(ConnectAsync, CanConnect);
            OpenTimelapseFolderCommand = new RelayCommand(() =>
            {
                var psi = new ProcessStartInfo();
                psi.FileName = LocalVideoPath;
                psi.UseShellExecute = true;
                Process.Start(psi);
            },CanOpenTimelapseFolder);
            OpenTaskSchedulerCommand = new RelayCommand(() =>
            {
                bool isAdmin = false;
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
                if (!isAdmin)
                {
                    MessageBox.Show("Please restart the application as administrator.");
                    return;
                }
                string settingsJson = JsonConvert.SerializeObject(this, Formatting.Indented);
                var scheduleTaskWindow = new ScheduleTaskWindow(this);
                scheduleTaskWindow.ShowDialog();
            }, CanOpenTimelapseFolder);

            //
            // Note: we pass AsyncRelayCommandOptions.AllowConcurrentExecutions because we're
            // overloading the same button to mean "star this" and "cancel this"
            //
            CreateTimelapseCommand = new AsyncRelayCommand(
                CreateTimelapseAsync, CanCreateTimelapse, AsyncRelayCommandOptions.AllowConcurrentExecutions);
            DownloadUnifiVideoCommand = new AsyncRelayCommand(
                DownloadUnifiVideoAsync, CanDownloadUnifiVideo, AsyncRelayCommandOptions.AllowConcurrentExecutions);
            //
            // Connect form
            //
            ValidateProperty(nameof(ControllerAddress), ControllerAddress);
            ValidateProperty(nameof(Username), Username);
            ValidateProperty(nameof(Password), Password);
            //
            // Download form
            //
            ValidateProperty(nameof(SelectedCamera), SelectedCamera);
            ValidateProperty(nameof(StartDate), StartDate);
            ValidateProperty(nameof(EndDate), EndDate);
            ValidateProperty(nameof(StartTime), StartTime);
            ValidateProperty(nameof(EndTime), EndTime);
            //
            // Timelapse video form
            //
            ValidateProperty(nameof(LocalVideoPath), LocalVideoPath);
            ValidateProperty(nameof(FfmpegPath), FfmpegPath);            
            ValidateProperty(nameof(SnapshotInterval), SnapshotInterval);
            ValidateProperty(nameof(FramesPerSecond), FramesPerSecond);

            _timelapseBuilder = new TimelapseBuilder();
            _timelapseBuilder.StatusCallback = (statusMessage, isError) =>
            {
                UpdateStatusMessage(statusMessage, isError);
            };
            _timelapseBuilder.VerboseCallback = (verboseMessage) =>
            {
                if (EnableVerboseLogs)
                {
                    Log += $"{verboseMessage}\n";
                }
            };
        }

        public void SetWindowLoaded()
        {
            _windowLoaded = true;
        }

        public bool CanShutdown()
        {
            return _timelapseBuilder.CanShutdown();
        }

        public async Task Shutdown()
        {
            await _timelapseBuilder.Shutdown();
            Application.Current.Shutdown();
        }

        private bool CanConnect()
        {
            return !_timelapseBuilder._isConnected && 
                !PropertyHasErrors(nameof(ControllerAddress)) &&
                !PropertyHasErrors(nameof(Username)) &&
                !PropertyHasErrors(nameof(Password));
        }

        private bool CanCreateTimelapse()
        {
            //
            // The button is overloaded - it can be create timelapse or cancel timelapse
            // in progress - so account for both situations
            //
            return IsCreatingTimelapse || (!PropertyHasErrors(nameof(LocalVideoPath)) &&
                !PropertyHasErrors(nameof(FfmpegPath)) &&
                !PropertyHasErrors(nameof(SnapshotInterval)));
        }

        private bool CanDownloadUnifiVideo()
        {
            //
            // The button is overloaded - it can be start download or cancel download
            //
            return IsDownloadingVideo || (IsDownloadFormEnabled && !PropertyHasErrors(nameof(SelectedCamera)) &&
                !PropertyHasErrors(nameof(StartDate)) && !PropertyHasErrors(nameof(EndDate)) &&
                !PropertyHasErrors(nameof(StartTime)) && !PropertyHasErrors(nameof(EndTime)));
        }

        private bool CanOpenTimelapseFolder()
        {
            return !PropertyHasErrors(nameof(LocalVideoPath));
        }

        private async Task ConnectAsync()
        {
            if (!await _timelapseBuilder.ConnectAsync(ControllerAddress, Username, Password))
            {
                IsDownloadFormEnabled = false;
                DownloadUnifiVideoCommand.NotifyCanExecuteChanged();
                return;
            }
            CameraList.Clear();
            var cameras = await _timelapseBuilder.GetCameraListAsync();
            if (cameras == null || cameras.Length == 0)
            {
                IsDownloadFormEnabled = false;
                DownloadUnifiVideoCommand.NotifyCanExecuteChanged();
                UpdateStatusMessage("No cameras found", IsError: true);
                return;
            }
            foreach (var camera in cameras)
            {
                CameraList.Add(camera);
            }
            if (string.IsNullOrEmpty(SelectedCamera))
            {
                SelectedCamera = cameras[0];
            }
            IsDownloadFormEnabled = true;
            DownloadUnifiVideoCommand.NotifyCanExecuteChanged();
        }

        private async Task DownloadUnifiVideoAsync()
        {
            IsDownloadingVideo = true;
            _timelapseBuilder.NewJobFolder();
            var success = await _timelapseBuilder.DownloadUnifiVideoAsync(
                FfmpegPath, SelectedCamera, StartDate, EndDate, StartTime, EndTime, ValidateAfterDownloading);
            IsDownloadingVideo = false;
            if (success && SendToTimelapseForm)
            {
                LocalVideoPath = _timelapseBuilder._jobFolder;
                Log += $"Job folder {LocalVideoPath} set in timelapse form\n";
            }
        }

        private async Task CreateTimelapseAsync()
        {
            IsCreatingTimelapse = true;
            _ = await _timelapseBuilder.CreateTimelapseAsync(
                FfmpegPath, LocalVideoPath, null, SnapshotInterval, FramesPerSecond, CleanupVideoFiles);
            IsCreatingTimelapse = false;
        }

        private void UpdateStatusMessage(string Text, bool IsError = false)
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                StatusIsError = IsError;
                StatusMessage = Text;
            }));
        }
    }
}