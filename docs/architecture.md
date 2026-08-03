# Architecture and Design

## Overview

UnifiVideoExporter is a single-project .NET 9 WPF application. The UI follows a lightweight MVVM structure using CommunityToolkit.Mvvm, with code-behind limited mostly to WPF-specific event handling such as file pickers, folder pickers, password box updates, log buttons, and window shutdown.

The central workflow object is `TimelapseBuilder`. Both the interactive UI and command-line scheduled task mode use this same class to connect to UniFi Protect, download recordings, extract frames with FFmpeg, and assemble the final timelapse video.

## Execution Modes

The app has two startup paths in `App.xaml.cs`.

Interactive mode is used when the process starts without `--settings`. It opens `MainWindow`, loads `%APPDATA%\UnifiVideoExporter\settings.json`, optionally auto-connects to the controller, and waits for user actions.

Command-line mode is used when the process starts with `--settings <path>`. The app reads a serialized `ConsoleSettings` JSON file and runs without the main window. If `--scheduledtask` is also present, it hides the console window and creates a tray icon with open and exit actions.

## Major Components

`MainWindow.xaml` and `MainWindow.xaml.cs` define the interactive UI. The window contains a log pane, a UniFi export form, a timelapse creation form, and a task scheduler button.

`MainWindowViewModel.cs` owns the interactive state and commands. It exposes connection, download, timelapse creation, open-folder, and task scheduler commands. It also bridges `TimelapseBuilder` status and verbose callbacks into the UI log and status message.

`TimelapseBuilder.cs` is the application service layer. It manages connection state, per-job working folders, cancellation tokens, active task counts, UniFi downloads, optional validation, frame extraction, timelapse assembly, and cleanup.

`UnifiApiHelper.cs` handles HTTP communication with UniFi Protect. It authenticates at `/api/auth/login`, retrieves cameras from `/proxy/protect/api/cameras`, and downloads MP4 exports from `/proxy/protect/api/video/export`.

`FfmpegWrapper.cs` contains process wrappers for `ffmpeg.exe` and `ffprobe.exe`. It validates videos, reads durations, extracts PNG frames, and builds the final MP4.

`SettingsHelper.cs` defines settings models, validation rules, and persistence for the main window settings. It stores app data under `%APPDATA%\UnifiVideoExporter`.

`ScheduleTaskViewModel.cs` creates and removes Windows scheduled tasks. It serializes `ConsoleSettings` files under `%APPDATA%\UnifiVideoExporter\tasks`, records known tasks in `tasks.json`, and registers Windows Task Scheduler entries that call the app with `--settings "<file>" --scheduledtask`.

`CredentialHelper.cs` encrypts and decrypts usernames and passwords with Windows DPAPI using `DataProtectionScope.CurrentUser`.

## Data Flow

Interactive UniFi export flow:

1. User enters controller URL and credentials.
2. `MainWindowViewModel.ConnectCommand` calls `TimelapseBuilder.ConnectAsync`.
3. `TimelapseBuilder` creates `UnifiApiHelper` and authenticates.
4. Cameras are loaded through `GetCameraListAsync` and shown in the UI.
5. User selects camera, dates, and time range.
6. `DownloadUnifiVideoCommand` creates a new job folder and calls `DownloadUnifiVideoAsync`.
7. `TimelapseBuilder` resolves the camera ID, splits the requested time range into one-hour segments, and downloads each segment as MP4.
8. If validation is enabled, each MP4 is checked with FFmpeg.
9. If `Send to Timelapse Form When Downloaded` is enabled, the job folder becomes the local timelapse input folder.

Timelapse creation flow:

1. User selects a folder containing `.mp4`, `.mov`, or `.avi` files.
2. `CreateTimelapseCommand` calls `TimelapseBuilder.CreateTimelapseAsync`.
3. For each input video, `ffprobe.exe` reads duration.
4. `ffmpeg.exe` extracts PNG frames at the configured snapshot interval into a temporary frame directory.
5. The app writes an FFmpeg concat input list.
6. `ffmpeg.exe` encodes the final MP4 with H.264 and yuv420p pixel format.
7. Temporary frames and input videos are deleted if cleanup is enabled.

Scheduled task flow:

1. User opens the scheduler window as administrator.
2. The scheduler form is initialized from the main window settings and selected camera list.
3. Creating a task writes a `ConsoleSettings` JSON file.
4. A Windows scheduled task is registered to launch `UnifiVideoExporter.exe --settings "<settings-file>" --scheduledtask`.
5. On execution, command-line mode chooses dates based on frequency:
   - Daily exports the prior calendar day.
   - Weekly exports the prior week. With weekdays only, it exports Monday through Friday; otherwise it exports Sunday through Saturday.
   - Monthly exports the previous calendar month. With weekdays only, Saturdays and Sundays are skipped.
6. After downloads complete, the scheduled run creates a timelapse in the configured output folder and removes the temporary job folder.

## Persistence

The working directory is:

```text
%APPDATA%\UnifiVideoExporter
```

Main settings are stored in:

```text
%APPDATA%\UnifiVideoExporter\settings.json
```

Scheduled task metadata is stored in:

```text
%APPDATA%\UnifiVideoExporter\tasks.json
```

Per-task console settings are stored in:

```text
%APPDATA%\UnifiVideoExporter\tasks\<guid>.json
```

Scheduled task logs are stored in date folders:

```text
%APPDATA%\UnifiVideoExporter\tasks\executionLog\<yyyy-MM-dd>\TaskLog_<yyyy-MM-dd_HHmmss>.log
```

Temporary job folders are random directory names under `%APPDATA%\UnifiVideoExporter`.

## Design Choices and Constraints

The app is Windows-only because it uses WPF, Windows Forms tray APIs, Windows DPAPI, and Windows Task Scheduler.

The UniFi API helper accepts self-signed certificate chain errors and certificate name mismatches. This is intended for local controller URLs and IP-based access, but it also means controller identity is not strictly verified.

Large UniFi exports are split into one-hour segments. This limits controller-side failures and lets the app continue past segments that are missing, corrupt, unavailable, or outside the controller retention window.

Commands are cancellable. The UI overloads `Download` and `Create Timelapse` buttons so a second click cancels the active operation.

FFmpeg progress is inferred from stderr output lines that include `time=...`. `ffprobe.exe` is expected to sit next to `ffmpeg.exe`.

Settings validation is implemented in the settings classes through `ObservableValidator` and `CustomValidation` attributes. WPF validation templates display validation errors next to controls.

## Source File Index

| File | Responsibility |
| --- | --- |
| `App.xaml.cs` | Startup mode selection, scheduled task CLI runner, tray icon integration, scheduled date range logic. |
| `MainWindow.xaml` | Main interactive UI layout. |
| `MainWindow.xaml.cs` | UI event handlers for file/folder dialogs, password box, log buttons, shutdown, and auto-connect. |
| `MainWindowViewModel.cs` | Main UI state, commands, command eligibility, status and log callback handling. |
| `TimelapseBuilder.cs` | Shared workflow engine for connection, download, validation, timelapse creation, cancellation, cleanup. |
| `UnifiApiHelper.cs` | UniFi Protect authentication, camera lookup, and video export downloads. |
| `FfmpegWrapper.cs` | FFmpeg and ffprobe process execution. |
| `SettingsHelper.cs` | Settings models, validation, local settings load/save. |
| `ScheduleTaskViewModel.cs` | Scheduled task settings, task creation/removal, task metadata persistence. |
| `ScheduleTaskWindow.xaml` | Scheduled task UI. |
| `ScheduleTaskWindow.xaml.cs` | Scheduler dialog event handlers. |
| `CredentialHelper.cs` | DPAPI encryption and decryption. |
| `ValidationHelper.cs` | WPF validation support helpers. |
| `PasswordBoxHelper.cs` | PasswordBox binding helper. |
| `Converters.cs` | UI value converters. |

