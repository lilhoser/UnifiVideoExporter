using Newtonsoft.Json;
using System.IO;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace UnifiVideoExporter
{
    using Application = System.Windows.Application;

    public partial class App : Application
    {
        #region interop for console window management
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AllocConsole();

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);
        [DllImport("user32.dll")]
        private static extern bool EnableMenuItem(IntPtr hMenu, uint uIDEnableItem, uint uEnable);

        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;
        private const int GWL_STYLE = -16;
        private const int WS_MINIMIZEBOX = 0x20000;
        private const int SW_RESTORE = 9;
        private const uint SC_CLOSE = 0xF060;
        private const uint MF_BYCOMMAND = 0x00000000;
        private const uint MF_GRAYED = 0x00000001;
        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_MINIMIZE = 0xF020;
        #endregion

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            bool isCliMode = e.Args.Length > 0 && e.Args.Contains("--settings");

            if (isCliMode)
            {
                AllocConsole();
                Console.WriteLine("Running in CLI mode...");

                bool isScheduledTask = e.Args.Contains("--scheduledtask");
                int settingsIndex = Array.IndexOf(e.Args, "--settings") + 1;
                string settingsPath = settingsIndex < e.Args.Length ? e.Args[settingsIndex] : string.Empty;

                if (string.IsNullOrEmpty(settingsPath) || !File.Exists(settingsPath))
                {
                    Console.WriteLine("Invalid command line arguments: missing settings path");
                    Shutdown(1);
                    return;
                }

                // Hide console window for scheduled tasks
                if (isScheduledTask)
                {
                    IntPtr hWnd = GetConsoleWindow();
                    if (hWnd != IntPtr.Zero)
                    {
                        ShowWindow(hWnd, SW_HIDE);
                    }
                }

                // Run CLI mode with system tray icon for scheduled tasks
                await RunCliModeAsync(settingsPath, isScheduledTask);
            }
            else
            {
                var mainWindow = new MainWindow();
                mainWindow.Show();
            }
        }

        private async Task RunCliModeAsync(string settingsPath, bool isScheduledTask)
        {
            // Setup log folder structure
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
                return;
            }

            using (var writer = new StreamWriter(logFilePath, append: true))
            {
                var builder = new TimelapseBuilder();
                builder.VerboseCallback = (verboseMessage, level) =>
                {
                    writer.WriteLine(verboseMessage);
                };

                ConsoleSettings consoleSettings;
                try
                {
                    string json = File.ReadAllText(settingsPath);
                    consoleSettings = (ConsoleSettings)JsonConvert.DeserializeObject(json, typeof(ConsoleSettings))!;
                    CredentialHelper.SetUnencryptedCredentials(consoleSettings);
                }
                catch (Exception ex)
                {
                    writer.WriteLine($"Failed to parse console settings: {ex.Message}");
                    Shutdown(3);
                    return;
                }

                if (isScheduledTask)
                {
                    //
                    // Add system tray icon for scheduled tasks
                    //
                    using (NotifyIcon notifyIcon = new NotifyIcon())
                    {
                        notifyIcon.Icon = new Icon(GetType().Assembly.GetManifestResourceStream("UnifiVideoExporter.resources.u_logo.ico"));
                        notifyIcon.Text = "Unifi Video Exporter";
                        notifyIcon.Visible = true;

                        // Track console and balloon visibility
                        bool consoleWindowVisible = false;
                        IntPtr hWnd = GetConsoleWindow();

                        // Hook console window messages to detect minimize (instead, send to tray)
                        HwndSource source = HwndSource.FromHwnd(hWnd);
                        source?.AddHook((IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
                        {
                            if (msg == WM_SYSCOMMAND && wParam.ToInt32() == SC_MINIMIZE && hwnd == hWnd)
                            {
                                ShowWindow(hWnd, SW_HIDE);
                                notifyIcon.Visible = true;
                                handled = true;
                                consoleWindowVisible = false;
                            }
                            return IntPtr.Zero;
                        });

                        notifyIcon.DoubleClick += (s, e) =>
                        {
                            ShowWindow(hWnd, SW_RESTORE);
                            notifyIcon.Visible = false;
                            consoleWindowVisible = true;
                        };

                        // Modify window style to show only minimize button
                        int style = GetWindowLong(hWnd, GWL_STYLE);
                        style = (style & ~(0x10000 | 0x100000)) | WS_MINIMIZEBOX; // Remove maximize, close; keep minimize
                        SetWindowLong(hWnd, GWL_STYLE, style);

                        // Disable close in system menu
                        IntPtr hMenu = GetSystemMenu(hWnd, false);
                        if (hMenu != IntPtr.Zero)
                        {
                            EnableMenuItem(hMenu, SC_CLOSE, MF_BYCOMMAND | MF_GRAYED);
                        }

                        // Context menu for system tray
                        ContextMenuStrip menu = new ContextMenuStrip();
                        menu.Items.Add("Open", null, (s, e) =>
                        {
                            ShowWindow(hWnd, SW_SHOW);
                            consoleWindowVisible = true; // Stop balloons when console is shown
                        });
                        menu.Items.Add("Exit", null, async (s, e) =>
                        {
                            writer.WriteLine("User canceled the operation.");
                            await builder.Shutdown();
                            Shutdown(0);
                        });
                        notifyIcon.ContextMenuStrip = menu;

                        //
                        // We only show notification balloon for:
                        //  1) initial task start
                        //  2) task end
                        //  3) error
                        //
                        notifyIcon.BalloonTipTitle = "Unifi Video Exporter - Task started";
                        notifyIcon.BalloonTipText = "A timelapse task has started. Click icon to see progress...";
                        notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
                        notifyIcon.ShowBalloonTip(30000); // 30 seconds timeout

                        //
                        // Set StatusCallback to update balloon tip on error
                        //
                        builder.StatusCallback = (statusMessage, isError) =>
                        {
                            if (!consoleWindowVisible && isError)
                            {
                                //
                                // Only show the balloon notification if the console window is hidden
                                // and the message is an error.
                                //
                                notifyIcon.BalloonTipTitle = "Unifi Video Exporter - Task failed";
                                notifyIcon.BalloonTipText = statusMessage;
                                notifyIcon.BalloonTipIcon = ToolTipIcon.Error;
                                notifyIcon.ShowBalloonTip(30000); // 30 seconds timeout
                            }
                            else if (consoleWindowVisible)
                            {
                                //
                                // If the console window becomes visible, always write updates there.
                                //
                                Console.WriteLine(statusMessage);
                            }
                            writer.WriteLine($"[Status] {statusMessage}"); // Always log any updates
                        };

                        try
                        {
                            await RunTimelapseBuilder(builder, consoleSettings);
                            notifyIcon.BalloonTipTitle = "Unifi Video Exporter - Task finished";
                            notifyIcon.BalloonTipText = "A timelapse task has completed successfully.";
                            notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
                            notifyIcon.ShowBalloonTip(30000); // 30 seconds timeout
                            Shutdown(0);
                        }
                        catch (Exception ex)
                        {
                            writer.WriteLine(ex.Message);
                            notifyIcon.BalloonTipTitle = "Unifi Video Exporter - Task exception";
                            notifyIcon.BalloonTipText = ex.Message;
                            notifyIcon.BalloonTipIcon = ToolTipIcon.Error;
                            notifyIcon.ShowBalloonTip(30000); // Show error in balloon
                            Shutdown(4);
                        }
                        finally
                        {
                            notifyIcon.Visible = false; // Cleanup

                            try
                            {
                                Directory.Delete(builder._jobFolder, true);
                            }
                            catch (Exception) { }
                        }
                    }
                }
                else
                {
                    builder.StatusCallback = (statusMessage, isError) =>
                    {
                        Console.ForegroundColor = isError ? ConsoleColor.Red : ConsoleColor.Green;
                        Console.WriteLine(statusMessage);
                        Console.ResetColor();
                        if (isError)
                        {
                            Console.WriteLine($"Log is at {logFilePath}");
                        }
                    };

                    try
                    {
                        await RunTimelapseBuilder(builder, consoleSettings);
                        Shutdown(0);
                    }
                    catch (Exception ex)
                    {
                        writer.WriteLine(ex.Message);
                        Shutdown(3);
                    }
                    finally
                    {
                        try
                        {
                            Directory.Delete(builder._jobFolder, true);
                        }
                        catch (Exception) { }
                    }
                }
            }
        }

        private async Task RunTimelapseBuilder(TimelapseBuilder Builder, ConsoleSettings Settings)
        {
            //
            // Set date range relative to today
            //
            var today = DateTime.Today;
            switch (Settings.TaskFrequency)
            {
                case TaskFrequency.Daily: // capture the last day's video
                    await HandleContiguousTask(Builder, Settings, today.AddDays(-1), today.AddDays(-1));
                    break;
                case TaskFrequency.Weekly: // capture the last week's videos
                    if (Settings.WeekdaysOnly)
                    {
                        // M-F
                        var lastMonday = today.AddDays(-(((int)today.DayOfWeek + 5) % 7 + 1));
                        await HandleContiguousTask(Builder, Settings, lastMonday, lastMonday.AddDays(4));
                    }
                    else
                    {
                        // S-SA
                        var lastSunday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7 + 1));
                        await HandleContiguousTask(Builder, Settings, lastSunday, lastSunday.AddDays(6));
                    }
                    break;
                case TaskFrequency.Monthly: // capture the last month's videos
                    await HandleMonthlyTask(Builder, Settings);
                    break;
                default:
                    throw new ArgumentException($"Invalid task frequency");
            }
        }

        private async Task HandleContiguousTask(
            TimelapseBuilder Builder,
            ConsoleSettings Settings,
            DateTime StartDate,
            DateTime EndDate
            )
        {
            var username = Settings.UserName;
            var password = Settings.Password;
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                throw new Exception("Unable to decrypt UniFi credentials");
            }
            if (!await Builder.ConnectAsync(Settings.ControllerAddress, username, password))
            {
                throw new Exception("Connection to UniFi controller failed");
            }
            if (!await Builder.DownloadUnifiVideoAsync(Settings.FfmpegPath,
                    Settings.SelectedCamera,
                    StartDate,
                    EndDate,
                    Settings.StartTime,
                    Settings.EndTime))
            {
                throw new Exception("Video download from UniFi controller failed");
            }
            if (!await Builder.CreateTimelapseAsync(Settings.FfmpegPath,
                    Builder._jobFolder,
                    Settings.OutputVideoLocation,
                    Settings.SnapshotInterval,
                    Settings.FramesPerSecond,
                    true,
                    Settings.SelectedCamera))
            {
                throw new Exception("Timelapse video creation failed");
            }
        }

        private async Task HandleMonthlyTask(TimelapseBuilder Builder, ConsoleSettings Settings)
        {
            var username = Settings.UserName;
            var password = Settings.Password;
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                throw new Exception("Unable to decrypt UniFi credentials");
            }
            if (!await Builder.ConnectAsync(Settings.ControllerAddress, username, password))
            {
                throw new Exception("Connection to UniFi controller failed");
            }

            var today = DateTime.Today;
            var firstOfMonth = today.AddMonths(-1).AddDays(-today.Day + 1);
            var lastOfMonth = firstOfMonth.AddMonths(1).AddDays(-1);
            for (var date = firstOfMonth; date <= lastOfMonth; date = date.AddDays(1))
            {
                if (Settings.WeekdaysOnly && (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday))
                {
                    continue;
                }
                if (!await Builder.DownloadUnifiVideoAsync(Settings.FfmpegPath,
                    Settings.SelectedCamera,
                    date,
                    date,
                    Settings.StartTime,
                    Settings.EndTime))
                {   
                    throw new Exception("Video download from UniFi controller failed");
                }
            }

            if (!await Builder.CreateTimelapseAsync(Settings.FfmpegPath,
                    Builder._jobFolder,
                    Settings.OutputVideoLocation,
                    Settings.SnapshotInterval,
                    Settings.FramesPerSecond,
                    true,
                    Settings.SelectedCamera))
            {
                throw new Exception("Timelapse video creation failed");
            }
        }
    }
}