# Usage Guide

## Install and Build

The project targets `net9.0-windows` and uses WPF.

From the repository root:

```powershell
dotnet restore
dotnet build
```

Run from Visual Studio, or publish/build a Windows executable with the .NET SDK.

Download a Windows FFmpeg full build and remember the path to `ffmpeg.exe`. The app also requires `ffprobe.exe` in the same folder.

## Controller Account Recommendation

Use a dedicated local UniFi Protect account for this application. Avoid using a primary administrator account. The app cannot control UniFi lockout behavior, and repeated failed login attempts may lock the account.

## Interactive Export

1. Start the app normally.
2. Enter the UniFi Protect controller address as an absolute URL, for example `https://192.168.1.1`.
3. Enter the UniFi username and password.
4. Select `ffmpeg.exe`.
5. Click `Connect`.
6. Choose a camera from the loaded camera list.
7. Pick a start date, end date, and `HH:mm` time range.
8. Optionally enable `Validate after downloading`.
9. Optionally enable `Send to Timelapse Form When Downloaded`.
10. Click `Download`.

The app downloads one MP4 per one-hour segment into a generated job folder under `%APPDATA%\UnifiVideoExporter`.

If validation is enabled, FFmpeg checks each downloaded MP4. Validation is useful when first testing a controller or diagnosing corrupt downloads, but it adds time.

## Create a Timelapse

1. Select the folder containing input videos.
2. Select `ffmpeg.exe`.
3. Set `Snapshot Interval`.
4. Set `Frames per second`.
5. Decide whether `Cleanup Input Video(s)` should be enabled.
6. Click `Create Timelapse`.

The app processes `.mp4`, `.mov`, and `.avi` files in the selected folder. In the interactive UI, the final timelapse MP4 is written to the current builder job folder under `%APPDATA%\UnifiVideoExporter`. If the timelapse input came from a just-finished UniFi download, that job folder is also the input folder. If you select an unrelated local input folder, the `Open` button still opens the input folder, not necessarily the output folder.

The output filename is based on the selected camera name:

```text
<camera>_timelapse_<yyyy-MM-dd_HHmmss>.mp4
```

Characters that are invalid in Windows filenames are replaced with `_`.

## Snapshot Interval and FPS

Snapshot interval controls how often the app keeps a frame from the source videos.

Examples:

- `5` seconds keeps frequent frames and produces more visual detail.
- `15` seconds is a practical starting point for many camera timelapses.
- `60` seconds greatly compresses long footage.
- Larger intervals are better for very long, slow-changing scenes.

Frames per second controls playback speed and smoothness of the final MP4. Higher FPS usually looks smoother but can make the same frame count play faster unless more frames are extracted.

## Scheduled Tasks

Task Scheduler integration requires the app to run as administrator.

1. Connect to the controller in the main window.
2. Click `Task Scheduler...`.
3. Enter a task name.
4. Choose camera, frequency, start date, task start time, capture time range, output folder, FFmpeg path, snapshot interval, and FPS.
5. Click `Create Task`.

The app registers a Windows scheduled task that runs:

```text
UnifiVideoExporter.exe --settings "<task-settings-file>" --scheduledtask
```

Frequency behavior:

- Daily: captures the prior calendar day.
- Weekly: captures the prior week. With weekdays only, this means Monday through Friday; otherwise Sunday through Saturday.
- Monthly: captures the previous calendar month. With weekdays only, Saturdays and Sundays are skipped.

The scheduled task trigger behavior is separate from capture behavior:

- Daily tasks are implemented as weekly triggers. Without weekdays only they run every day. With weekdays only they run Tuesday through Saturday so each run captures the prior weekday.
- Weekly tasks run on Sunday.
- Monthly tasks run on day 1 of each month.

## Command-Line Mode

The executable can also be run manually with a settings file:

```powershell
UnifiVideoExporter.exe --settings "C:\path\to\task-settings.json"
```

Add `--scheduledtask` to use scheduled-task behavior such as hiding the console and showing the tray icon:

```powershell
UnifiVideoExporter.exe --settings "C:\path\to\task-settings.json" --scheduledtask
```

The settings file must serialize the `ConsoleSettings` model used by the app.

## Local Settings

Main window settings are loaded and saved automatically from:

```text
%APPDATA%\UnifiVideoExporter\settings.json
```

Scheduled task settings are stored under:

```text
%APPDATA%\UnifiVideoExporter\tasks
```

Credentials are stored encrypted, not in plain text, using Windows DPAPI for the current user.

## Practical Tips

- Start with a short date and time range to confirm controller access, camera selection, and FFmpeg configuration.
- Enable verbose logs when diagnosing controller authentication, download, or FFmpeg issues.
- Enable validation after downloading for first-time setup or unreliable camera footage.
- Keep cleanup disabled until you have verified the output and understand where the app writes temporary files.
- Use a local output folder for scheduled tasks. Network paths may fail depending on the Windows account used by the scheduled task.
- Make sure the scheduled task runs as the same Windows user who created the encrypted task settings.
