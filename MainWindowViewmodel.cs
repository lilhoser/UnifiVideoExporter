using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using System.Security.Principal;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UnifiVideoExporter
{
    using MessageBox = System.Windows.MessageBox;
    using Application = System.Windows.Application;

    public partial class MainWindowViewModel : ObservableValidator
    {
        private TimelapseBuilder _timelapseBuilder;

        #region observable properties

        [ObservableProperty]
        private MainWindowSettings settings;

        [ObservableProperty]
        private string statusMessage;

        [ObservableProperty]
        private bool statusIsError;

        [ObservableProperty]
        private bool isDownloadFormEnabled;
        partial void OnIsDownloadFormEnabledChanged(bool value)
        {
            DownloadUnifiVideoCommand.NotifyCanExecuteChanged();
        }

        [ObservableProperty]
        private string log;

        [ObservableProperty]
        private bool isCreatingTimelapse;
        partial void OnIsCreatingTimelapseChanged(bool value)
        {
            CreateTimelapseCommand.NotifyCanExecuteChanged();
        }

        [ObservableProperty]
        private bool isDownloadingVideo;
        partial void OnIsDownloadingVideoChanged(bool value)
        {
            DownloadUnifiVideoCommand.NotifyCanExecuteChanged();
        }

        public ObservableCollection<string> CameraList { get; set; }
        #endregion

        #region commands
        public AsyncRelayCommand ConnectCommand { get; }
        public AsyncRelayCommand CreateTimelapseCommand { get; }
        public AsyncRelayCommand DownloadUnifiVideoCommand { get; }
        public RelayCommand OpenTimelapseFolderCommand { get; }
        public RelayCommand OpenTaskSchedulerCommand { get; }
        #endregion

        public MainWindowViewModel()
        {
            CameraList = new ObservableCollection<string>();
            ConnectCommand = new AsyncRelayCommand(ConnectAsync, CanConnect);
            OpenTimelapseFolderCommand = new RelayCommand(() =>
            {
                var psi = new ProcessStartInfo();
                psi.FileName = Settings!.LocalVideoPath;
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

            _timelapseBuilder = new TimelapseBuilder();
            _timelapseBuilder.StatusCallback = (statusMessage, isError) =>
            {
                UpdateStatusMessage(statusMessage, isError);
            };
            _timelapseBuilder.VerboseCallback = (verboseMessage, level) =>
            {
                switch (level)
                {
                    case TraceLevel.Warning:
                    case TraceLevel.Error:
                    case TraceLevel.Info:
                        {
                            Log += $"{verboseMessage}\n";
                            break;
                        }
                    case TraceLevel.Verbose:
                        {
                            if (Settings!.EnableVerboseLogs)
                            {
                                Log += $"{verboseMessage}\n";
                            }
                            break;
                        }
                    default:
                        {
                            break;
                        }
                }
            };
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

        public void NotifyCanExecuteChanged()
        {
            ConnectCommand.NotifyCanExecuteChanged();
            DownloadUnifiVideoCommand.NotifyCanExecuteChanged();
            CreateTimelapseCommand.NotifyCanExecuteChanged();
            OpenTimelapseFolderCommand.NotifyCanExecuteChanged();
            OpenTaskSchedulerCommand.NotifyCanExecuteChanged();
        }

        private bool CanConnect()
        {
            if (Settings == null)
            {
                return false;
            }
            return !_timelapseBuilder._isConnected && 
                !Settings.GetErrors(nameof(Settings.ControllerAddress)).Any() &&
                !Settings.GetErrors(nameof(Settings.UserName)).Any() &&
                !Settings.GetErrors(nameof(Settings.Password)).Any();
        }

        private bool CanCreateTimelapse()
        {
            if (Settings == null)
            {
                return false;
            }
            //
            // The button is overloaded - it can be create timelapse or cancel timelapse
            // in progress - so account for both situations
            //
            return IsCreatingTimelapse || (
                !Settings.GetErrors(nameof(Settings.LocalVideoPath)).Any() &&
                !Settings.GetErrors(nameof(Settings.FfmpegPath)).Any() &&
                !Settings.GetErrors(nameof(Settings.SnapshotInterval)).Any());
        }

        private bool CanDownloadUnifiVideo()
        {
            if (Settings == null)
            {
                return false;
            }
            //
            // The button is overloaded - it can be start download or cancel download
            //
            return IsDownloadingVideo || (IsDownloadFormEnabled && 
                !Settings.GetErrors(nameof(Settings.SelectedCamera)).Any() &&
                !Settings.GetErrors(nameof(Settings.StartDate)).Any() && 
                !Settings.GetErrors(nameof(Settings.EndDate)).Any() &&
                !Settings.GetErrors(nameof(Settings.StartTime)).Any() &&
                !Settings.GetErrors(nameof(Settings.EndTime)).Any());
        }

        private bool CanOpenTimelapseFolder()
        {
            return Settings != null;
        }

        private async Task ConnectAsync()
        {
            if (!await _timelapseBuilder.ConnectAsync(
                Settings.ControllerAddress, Settings.UserName, Settings.Password))
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
            if (string.IsNullOrEmpty(Settings.SelectedCamera))
            {
                Settings.SelectedCamera = cameras[0];
            }
            IsDownloadFormEnabled = true;
            DownloadUnifiVideoCommand.NotifyCanExecuteChanged();
        }

        private async Task DownloadUnifiVideoAsync()
        {
            //
            // This function is re-entrant in the sense that a first invocation starts
            // a download and a subsequent invocation cancels it.
            //
            if (IsDownloadingVideo)
            {
                _timelapseBuilder.CancelDownload();
                return;
            }

            IsDownloadingVideo = true;
            _timelapseBuilder.NewJobFolder();
            var success = await _timelapseBuilder.DownloadUnifiVideoAsync(
                Settings.FfmpegPath,
                Settings.SelectedCamera,
                Settings.StartDate,
                Settings.EndDate,
                Settings.StartTime,
                Settings.EndTime,
                Settings.ValidateAfterDownloading);
            IsDownloadingVideo = false;
            if (success && Settings.SendToTimelapseForm)
            {
                Settings.LocalVideoPath = _timelapseBuilder._jobFolder;
                Log += $"Job folder {Settings.LocalVideoPath} set in timelapse form\n";
            }
        }

        private async Task CreateTimelapseAsync()
        {
            //
            // This function is re-entrant in the sense that a first invocation starts
            // a timelapse operation and a subsequent invocation cancels it.
            //
            if (IsCreatingTimelapse)
            {
                _timelapseBuilder.CancelTimelapseCreation();
                return;
            }
            IsCreatingTimelapse = true;
            _ = await _timelapseBuilder.CreateTimelapseAsync(
                Settings.FfmpegPath,
                Settings.LocalVideoPath,
                _timelapseBuilder._jobFolder,
                Settings.SnapshotInterval,
                Settings.FramesPerSecond,
                Settings.CleanupVideoFiles,
                Settings.SelectedCamera);
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