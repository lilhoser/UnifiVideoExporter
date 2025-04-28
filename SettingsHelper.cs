using Newtonsoft.Json;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace UnifiVideoExporter
{
    internal static class SettingsHelper
    {
        public static readonly string s_WorkingDirectory = Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData), "UnifiVideoExporter");

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
                    ViewModel.ControllerAddress = "";
                    ViewModel.LocalVideoPath = "";
                    ViewModel.FfmpegPath = @"";
                    ViewModel.SnapshotInterval = 60.0;
                    ViewModel.StartDate = DateTime.Today;
                    ViewModel.EndDate = DateTime.Today;
                    ViewModel.StartTime = "08:00";
                    ViewModel.EndTime = "17:00";
                    ViewModel.AutoConnect = false;
                    ViewModel.CleanupVideoFiles = true;
                    ViewModel.EnableVerboseLogs = false;
                    ViewModel.SendToTimelapseForm = true;
                    ViewModel.ValidateAfterDownloading = false;
                    ViewModel.FramesPerSecond = FfmpegWrapper.FfmpegFps;
                    string defaultJson = JsonConvert.SerializeObject(ViewModel, Formatting.Indented);
                    File.WriteAllText(settingsPath, defaultJson);
                }
                else
                {
                    string json = File.ReadAllText(settingsPath);
                    var settings = (MainWindowViewModel)JsonConvert.DeserializeObject(
                        json, typeof(MainWindowViewModel))!;

                    if (!string.IsNullOrEmpty(settings._EncryptedUserName))
                    {
                        ViewModel._EncryptedUserName = settings._EncryptedUserName;
                        ViewModel.Username = Encoding.UTF8.GetString(ProtectedData.Unprotect(
                            Convert.FromBase64String(settings._EncryptedUserName), null, DataProtectionScope.CurrentUser));
                    }
                    if (!string.IsNullOrEmpty(settings._EncryptedPassword))
                    {
                        ViewModel._EncryptedPassword = settings._EncryptedPassword;
                        ViewModel.Password = Encoding.UTF8.GetString(ProtectedData.Unprotect(
                            Convert.FromBase64String(settings._EncryptedPassword), null, DataProtectionScope.CurrentUser));
                    }
                    ViewModel.ControllerAddress = settings.ControllerAddress;
                    ViewModel.LocalVideoPath = settings.LocalVideoPath;
                    ViewModel.FfmpegPath = settings.FfmpegPath;
                    ViewModel.SnapshotInterval = settings.SnapshotInterval;
                    ViewModel.FramesPerSecond = settings.FramesPerSecond;
                    ViewModel.StartDate = settings.StartDate;
                    ViewModel.EndDate = settings.EndDate;
                    ViewModel.StartTime = settings.StartTime;
                    ViewModel.EndTime = settings.EndTime;
                    ViewModel.SelectedCamera = settings.SelectedCamera;
                    ViewModel.AutoConnect = settings.AutoConnect;
                    ViewModel.CleanupVideoFiles = settings.CleanupVideoFiles;
                    ViewModel.EnableVerboseLogs = settings.EnableVerboseLogs;
                    ViewModel.SendToTimelapseForm = settings.SendToTimelapseForm;
                    ViewModel.ValidateAfterDownloading = settings.ValidateAfterDownloading;
                }
            }
            catch (Exception ex)
            {
                ViewModel.Log += $"Error loading settings: {ex.Message}\n";
            }
        }

        public static void SaveSettings(this MainWindowViewModel ViewModel)
        {
            if (!ViewModel._settingsChanged || ViewModel.HasErrors)
            {
                return;
            }

            try
            {
                string settingsDir = Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData), "UnifiVideoExporter");
                Directory.CreateDirectory(settingsDir);
                string settingsPath = Path.Combine(settingsDir, "settings.json");
                ViewModel._EncryptedUserName = Convert.ToBase64String(ProtectedData.Protect(
                    Encoding.UTF8.GetBytes(ViewModel.Username), null, DataProtectionScope.CurrentUser));
                ViewModel._EncryptedPassword = Convert.ToBase64String(ProtectedData.Protect(
                    Encoding.UTF8.GetBytes(ViewModel.Password), null, DataProtectionScope.CurrentUser));
                string json = JsonConvert.SerializeObject(ViewModel, Formatting.Indented);
                File.WriteAllText(settingsPath, json);
            }
            catch (Exception ex)
            {
                ViewModel.Log += $"Error saving settings: {ex.Message}\n";
            }
        }
    }
}
