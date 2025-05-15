using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.IO;

namespace UnifiVideoExporter
{
    public partial class Settings : ObservableValidator
    {
        #region observable properties
        [ObservableProperty]
        [NotifyDataErrorInfo]
        [CustomValidation(typeof(Settings), nameof(ValidateProperty))]
        private string controllerAddress;

        [property: JsonIgnore]
        [ObservableProperty]
        [NotifyDataErrorInfo]
        [CustomValidation(typeof(Settings), nameof(ValidateProperty))]
        private string userName;
        partial void OnUserNameChanged(string value)
        {
            CredentialHelper.SetEncryptedCredentials(this);
        }

        [property: JsonIgnore]
        [ObservableProperty]
        [NotifyDataErrorInfo]
        [CustomValidation(typeof(Settings), nameof(ValidateProperty))]
        private string password;
        partial void OnPasswordChanged(string value)
        {
            CredentialHelper.SetEncryptedCredentials(this);
        }

        [ObservableProperty]
        [NotifyDataErrorInfo]
        [CustomValidation(typeof(Settings), nameof(ValidateProperty))]
        private string ffmpegPath;

        [ObservableProperty]
        [NotifyDataErrorInfo]
        [CustomValidation(typeof(Settings), nameof(ValidateProperty))]
        private string selectedCamera;

        [ObservableProperty]
        [NotifyDataErrorInfo]
        [CustomValidation(typeof(Settings), nameof(ValidateProperty))]
        private double snapshotInterval = 60.0;

        [ObservableProperty]
        [NotifyDataErrorInfo]
        [CustomValidation(typeof(Settings), nameof(ValidateProperty))]
        private double framesPerSecond = FfmpegWrapper.FfmpegFps;

        [ObservableProperty]
        [NotifyDataErrorInfo]
        [CustomValidation(typeof(Settings), nameof(ValidateProperty))]
        private DateTime startDate = DateTime.Today;

        [ObservableProperty]
        [NotifyDataErrorInfo]
        [CustomValidation(typeof(Settings), nameof(ValidateProperty))]
        private DateTime endDate = DateTime.Today;

        [ObservableProperty]
        [NotifyDataErrorInfo]
        [CustomValidation(typeof(Settings), nameof(ValidateProperty))]
        private string startTime = "08:00";

        [ObservableProperty]
        [NotifyDataErrorInfo]
        [CustomValidation(typeof(Settings), nameof(ValidateProperty))]
        private string endTime = "09:00";

        [ObservableProperty]
        [NotifyDataErrorInfo]
        [CustomValidation(typeof(Settings), nameof(ValidateProperty))]
        private string encryptedUserName;

        [ObservableProperty]
        [NotifyDataErrorInfo]
        [CustomValidation(typeof(Settings), nameof(ValidateProperty))]
        private string encryptedPassword;

        #endregion

        #region validation
        public static ValidationResult ValidateProperty(object value, ValidationContext context)
        {
            var settings = context.ObjectInstance as Settings;
            var propertyName = context.MemberName!;
            if (!settings!.IsValid(propertyName, value, out string message))
            {
                return new ValidationResult(message);
            }
            return ValidationResult.Success!;
        }

        public bool IsValid(string propertyName, object value, out string ErrorMessage)
        {
            ErrorMessage = string.Empty;
            switch (propertyName)
            {
                case nameof(ControllerAddress):
                    if (string.IsNullOrEmpty((string)value) || !Uri.TryCreate((string)value, UriKind.Absolute, out _))
                        ErrorMessage = "Must be a valid absolute URI (e.g., https://192.168.1.1:7443).";
                    break;
                case nameof(UserName):
                    if (string.IsNullOrEmpty((string)value))
                        ErrorMessage = "Username is required.";
                    break;
                case nameof(Password):
                    if (string.IsNullOrEmpty((string)value))
                        ErrorMessage = "Password is required.";
                    break;
                case nameof(EncryptedUserName):
                    if (string.IsNullOrEmpty((string)value))
                        ErrorMessage = "Encrypted user name is required.";
                    break;
                case nameof(EncryptedPassword):
                    if (string.IsNullOrEmpty((string)value))
                        ErrorMessage = "Encrypted password is required.";
                    break;
                case nameof(FfmpegPath):
                    if (string.IsNullOrEmpty((string)value))
                        ErrorMessage = "FFmpeg path is required.";
                    else if (!File.Exists((string)value))
                        ErrorMessage = "FFmpeg executable does not exist at the specified path.";
                    break;
                case nameof(SelectedCamera):
                    if (value == null)
                        ErrorMessage = "A camera must be selected.";
                    break;
                case nameof(SnapshotInterval):
                    if ((double)value <= 0)
                        ErrorMessage = "Snapshot interval must be a positive number.";
                    break;
                case nameof(FramesPerSecond):
                    if ((double)value <= 0)
                        ErrorMessage = "Frames per second must be a positive number.";
                    break;
                case nameof(StartDate):
                    if ((DateTime)value == default)
                        ErrorMessage = "Start date is required.";
                    break;
                case nameof(EndDate):
                    if ((DateTime)value == default)
                        ErrorMessage = "End date is required.";
                    else if ((DateTime)value < StartDate)
                        ErrorMessage = "End date must be on or after start date.";
                    break;
                case nameof(StartTime):
                    if (!TimeSpan.TryParse((string)value, out _))
                        ErrorMessage = "Start time must be in HH:mm format.";
                    break;
                case nameof(EndTime):
                    if (!TimeSpan.TryParse((string)value, out _))
                        ErrorMessage = "End time must be in HH:mm format.";
                    else if (TimeSpan.TryParse((string)value, out var endtime) &&
                             TimeSpan.TryParse(StartTime, out var starttime) &&
                             endtime <= starttime)
                        ErrorMessage = "End time must be after start time.";
                    break;
                default:
                    ErrorMessage = $"Unknown property {propertyName}";
                    break;
            }
            return string.IsNullOrEmpty(ErrorMessage);
        }
        #endregion

        public Settings()
        {
        }
    }

    public partial class MainWindowSettings : Settings
    {
        #region observable properties
        [ObservableProperty]
        private bool autoConnect = true;

        [ObservableProperty]
        private bool cleanupVideoFiles = true;

        [ObservableProperty]
        private bool enableVerboseLogs = false;

        [ObservableProperty]
        private bool sendToTimelapseForm = true;

        [ObservableProperty]
        private bool validateAfterDownloading = false;

        [ObservableProperty]
        [NotifyDataErrorInfo]
        [CustomValidation(typeof(MainWindowSettings), nameof(ValidateProperty))]
        private string localVideoPath;
        #endregion

        #region validation
        public static ValidationResult ValidateProperty(object value, ValidationContext context)
        {
            var settings = context.ObjectInstance as MainWindowSettings;
            var propertyName = context.MemberName!;
            if (!settings!.IsValid(propertyName, value, out string message))
            {
                return new ValidationResult(message);
            }
            return ValidationResult.Success!;
        }

        public bool IsValid(string propertyName, object value, out string ErrorMessage)
        {
            ErrorMessage = string.Empty;
            if (value == null)
            {
                return false;
            }
            switch (propertyName)
            {
                case nameof(LocalVideoPath):
                    if (string.IsNullOrEmpty((string)value) || !Directory.Exists((string)value))
                        ErrorMessage = "Local video path does not exist at the specified path.";
                        break;
                default:
                        ErrorMessage = $"Unknown property {propertyName}";
                        break;
            }
            return string.IsNullOrEmpty(ErrorMessage);
        }
        #endregion

        public MainWindowSettings()
        {
            
        }

        public void ValidateAll()
        {
            ValidateAllProperties();
        }
    }

    public partial class ConsoleSettings : Settings
    {
        #region observable properties
        [ObservableProperty]
        [NotifyDataErrorInfo]
        [CustomValidation(typeof(ConsoleSettings), nameof(ValidateProperty))]
        private string taskName = "UnifiVideoExporterTimelapse";

        [ObservableProperty]
        [NotifyDataErrorInfo]
        [CustomValidation(typeof(ConsoleSettings), nameof(ValidateProperty))]
        private TaskFrequency taskFrequency = TaskFrequency.Daily;

        [ObservableProperty]
        private bool weekdaysOnly = false;

        [ObservableProperty]
        [NotifyDataErrorInfo]
        [CustomValidation(typeof(ConsoleSettings), nameof(ValidateProperty))]
        private string outputVideoLocation = string.Empty;

        [ObservableProperty]
        [CustomValidation(typeof(ConsoleSettings), nameof(ValidateProperty))]
        private string taskSettingsFile;

        [ObservableProperty]
        [CustomValidation(typeof(ConsoleSettings), nameof(ValidateProperty))]
        private string taskStartTime = "09:00";

        #endregion

        #region validation
        public static ValidationResult ValidateProperty(object value, ValidationContext context)
        {
            var settings = context.ObjectInstance as ConsoleSettings;
            var propertyName = context.MemberName!;
            if (!settings!.IsValid(propertyName, value, out string message))
            {
                return new ValidationResult(message);
            }
            return ValidationResult.Success!;
        }

        public bool IsValid(string propertyName, object value, out string ErrorMessage)
        {
            ErrorMessage = string.Empty;
            if (value == null)
            {
                return false;
            }
            switch (propertyName)
            {
                case nameof(TaskName):
                    if (string.IsNullOrEmpty((string)value))
                        ErrorMessage = "Task name is invalid";
                    else if (ScheduleTaskViewModel.TaskExists((string)value))
                        ErrorMessage = $"Task {value} already exists";
                    break;
                case nameof(TaskFrequency):
                    var freq = $"{value}";
                    if (!Enum.TryParse(typeof(TaskFrequency), freq, out _))
                        ErrorMessage = $"Task frequency {value} is invalid";
                    break;
                case nameof(OutputVideoLocation):
                    if (string.IsNullOrEmpty((string)value))
                        ErrorMessage = "Output video location is required";
                    else if (!Directory.Exists((string)value))
                        ErrorMessage = $"Output video location {value} does not exist.";
                    break;
                case nameof(TaskSettingsFile):
                    if (string.IsNullOrEmpty((string)value))
                        ErrorMessage = "Task settings file is required";
                    break;
                case nameof(TaskStartTime):
                    if (string.IsNullOrEmpty((string)value))
                        ErrorMessage = "Task start time is required";
                    else if (!TimeSpan.TryParse((string)value, out _))
                        ErrorMessage = "Task start time must be in the format HH:mm";
                        break;
                default:
                    ErrorMessage = $"Unknown property {propertyName}";
                    break;
            }
            return string.IsNullOrEmpty(ErrorMessage);
        }
        #endregion

        public ConsoleSettings()
        {
        }

        public void ValidateAll()
        {
            ValidateAllProperties();
        }
    }

    public static class SettingsHelper
    {
        public static readonly string s_WorkingDirectory = Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData), "UnifiVideoExporter");

        #region MainWindowViewModel extension methods

        public static void LoadSettings(this MainWindowViewModel ViewModel)
        {
            try
            {
                string settingsDir = s_WorkingDirectory;
                string settingsPath = Path.Combine(settingsDir, "settings.json");
                if (!Directory.Exists(settingsDir))
                {
                    Directory.CreateDirectory(settingsDir);
                }
                if (!File.Exists(settingsPath))
                {
                    ViewModel.Settings = new MainWindowSettings();
                    string defaultJson = JsonConvert.SerializeObject(ViewModel.Settings, Formatting.Indented);
                    File.WriteAllText(settingsPath, defaultJson);
                }
                else
                {
                    string json = File.ReadAllText(settingsPath);
                    ViewModel.Settings = (MainWindowSettings)JsonConvert.DeserializeObject(
                        json, typeof(MainWindowSettings))!;
                    CredentialHelper.SetUnencryptedCredentials(ViewModel.Settings);
                }
                ViewModel.Settings.ValidateAll();
                ViewModel.NotifyCanExecuteChanged();
                ViewModel.Settings.PropertyChanged += (sender, args) =>
                {
                    ViewModel.NotifyCanExecuteChanged();
                };
            }
            catch (Exception ex)
            {
                ViewModel.Log += $"Error loading settings: {ex.Message}\n";
            }
        }

        public static void SaveSettings(this MainWindowViewModel ViewModel)
        {
            if (ViewModel.HasErrors)
            {
                return;
            }

            try
            {
                string settingsDir = Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData), "UnifiVideoExporter");
                Directory.CreateDirectory(settingsDir);
                string settingsPath = Path.Combine(settingsDir, "settings.json");
                string json = JsonConvert.SerializeObject(ViewModel.Settings, Formatting.Indented);
                File.WriteAllText(settingsPath, json);
            }
            catch (Exception ex)
            {
                ViewModel.Log += $"Error saving settings: {ex.Message}\n";
            }
        }

        #endregion
    }
}
