using Newtonsoft.Json;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;

namespace UnifiVideoExporter;
public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        //
        // We're being run via cmd line or scheduled task
        //
        if (e.Args.Length > 0 && e.Args.Contains("--settings"))
        {
            bool isScheduledTask = e.Args.Contains("--scheduledtask");
            int settingsIndex = Array.IndexOf(e.Args, "--settings") + 1;
            string settingsPath = settingsIndex < e.Args.Length ? e.Args[settingsIndex] : string.Empty;
            if (string.IsNullOrEmpty(settingsPath) || !File.Exists(settingsPath))
            {
                Shutdown(1);
            }

            //
            // Setup log folder structure
            //
            string logFilePath = string.Empty;
            try
            {
                string tasksDir = Path.Combine(SettingsHelper.s_WorkingDirectory, "tasks");
                Directory.CreateDirectory(tasksDir);
                string executionDir = Path.Combine(tasksDir, "executionLog");
                Directory.CreateDirectory(executionDir);
                string datePath = Path.Combine(executionDir, DateTime.Now.ToString("yyyy-MM-dd"));
                Directory.CreateDirectory(datePath);
                string logFileName = $"TaskLog_{DateTime.Now:yyyy-MM-dd_HHmmss}.log";
                logFilePath = Path.Combine(datePath, logFileName);
            }
            catch (Exception)
            {
                Shutdown(2);
            }

            using (var writer = new StreamWriter(logFilePath, append: true))
            {
                //
                // Instantiate a builder based on where the log info should go:
                //  (1) scheduled task - non-interactive, only need a verbose callback and
                //  contents are sent to a log.
                //  (2) from cmd line - interactive, status is written to console, verbose
                //  info sent to a log.
                //
                var builder = new TimelapseBuilder();
                builder.VerboseCallback = (verboseMessage) =>
                {
                    writer.WriteLine(verboseMessage);
                };

                if (!isScheduledTask)
                {
                    builder.StatusCallback = (statusMessage, isError) =>
                    {
                        Console.ForegroundColor = isError? ConsoleColor.Red : ConsoleColor.Green;
                        Console.WriteLine(statusMessage);
                        Console.ResetColor();
                        if (isError)
                        {
                            Console.WriteLine($"Log is at {logFilePath}");
                        }
                    };
                }

                try
                {
                    await ParseTaskSettings(builder, settingsPath);
                    Shutdown(0);
                }
                catch (Exception ex)
                {
                    writer.WriteLine(ex.Message);
                    Shutdown(3);
                }
            }            
        }
        else
        {
            base.OnStartup(e);
        }
    }

    private async Task ParseTaskSettings(TimelapseBuilder Builder, string SettingsFile)
    {
        string json = File.ReadAllText(SettingsFile);
        var taskInfo = (ScheduledTaskInfo)JsonConvert.DeserializeObject(json)!;

        //
        // Set date range relative to today
        //
        var today = DateTime.Today;
        switch (taskInfo.TaskFrequency)
        {
            case TaskFrequency.Daily: // capture the last day's video
                await HandleContiguousTask(Builder, taskInfo, today.AddDays(-1), today.AddDays(-1));
                break;
            case TaskFrequency.Weekly: // capture the last week's videos
                if (taskInfo.WeekdaysOnly)
                {
                    var lastMonday = today.AddDays(-((int)today.DayOfWeek + 7));
                    await HandleContiguousTask(Builder, taskInfo, lastMonday, lastMonday.AddDays(4));
                }
                else
                {
                    await HandleContiguousTask(Builder, taskInfo, today.AddDays(-7), today);
                }
                break;
            case TaskFrequency.Monthly: // capture the last month's videos
                await HandleMonthlyTask(Builder, taskInfo);
                break;
            default:
                throw new ArgumentException($"Invalid task frequency");
        }
    }

    private async Task HandleContiguousTask(
        TimelapseBuilder Builder,
        ScheduledTaskInfo TaskInfo,
        DateTime StartDate,
        DateTime EndDate
        )
    {
        var username = Encoding.UTF8.GetString(
            ProtectedData.Unprotect(Convert.FromBase64String(TaskInfo.EncryptedUsername), null, DataProtectionScope.CurrentUser));
        var password = Encoding.UTF8.GetString(
            ProtectedData.Unprotect(Convert.FromBase64String(TaskInfo.EncryptedPassword), null, DataProtectionScope.CurrentUser));
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            throw new Exception("Unable to decrypt UniFi credentials");
        }
        if (!await Builder.ConnectAsync(TaskInfo.ControllerAddress, username, password))
        {
            throw new Exception("Connection to UniFi controller failed");
        }
        if (!await Builder.DownloadUnifiVideoAsync(TaskInfo.FfmpegPath,
                TaskInfo.CameraName,
                StartDate,
                EndDate,
                TaskInfo.StartTime,
                TaskInfo.EndTime))
        {
            throw new Exception("Video download from UniFi controller failed");
        }
        if (!await Builder.CreateTimelapseAsync(TaskInfo.FfmpegPath,
                null,
                TaskInfo.OutputVideoLocation,
                TaskInfo.SnapshotInterval,
                TaskInfo.FramesPerSecond,
                true))                
        {
            throw new Exception("Timelapse video creation failed");
        }
    }

    private async Task HandleMonthlyTask(TimelapseBuilder Builder, ScheduledTaskInfo TaskInfo)
    {
        var username = Encoding.UTF8.GetString(
            ProtectedData.Unprotect(Convert.FromBase64String(TaskInfo.EncryptedUsername), null, DataProtectionScope.CurrentUser));
        var password = Encoding.UTF8.GetString(
            ProtectedData.Unprotect(Convert.FromBase64String(TaskInfo.EncryptedPassword), null, DataProtectionScope.CurrentUser));
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            throw new Exception("Unable to decrypt UniFi credentials");
        }
        if (!await Builder.ConnectAsync(TaskInfo.ControllerAddress, username, password))
        {
            throw new Exception("Connection to UniFi controller failed");
        }        

        var today = DateTime.Today;
        var firstOfMonth = today.AddMonths(-1).AddDays(-today.Day + 1);
        var lastOfMonth = firstOfMonth.AddMonths(1).AddDays(-1);
        for (var date = firstOfMonth; date <= lastOfMonth; date = date.AddDays(1))
        {
            if (TaskInfo.WeekdaysOnly && (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday))
            {
                continue;
            }
            if (!await Builder.DownloadUnifiVideoAsync(TaskInfo.FfmpegPath,
                TaskInfo.CameraName,
                date,
                date,
                TaskInfo.StartTime,
                TaskInfo.EndTime))
            {
                throw new Exception("Video download from UniFi controller failed");
            }
        }

        if (!await Builder.CreateTimelapseAsync(TaskInfo.FfmpegPath,
                null,
                TaskInfo.OutputVideoLocation,
                TaskInfo.SnapshotInterval,
                TaskInfo.FramesPerSecond,
                true))
        {
            throw new Exception("Timelapse video creation failed");
        }
    }

}

