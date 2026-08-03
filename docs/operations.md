# Operations and Troubleshooting

## Logs

Interactive logs appear in the left pane of the main window. The `Enable Verbose Logs` checkbox controls whether verbose messages are appended.

Scheduled task logs are written to:

```text
%APPDATA%\UnifiVideoExporter\tasks\executionLog\<yyyy-MM-dd>\TaskLog_<yyyy-MM-dd_HHmmss>.log
```

The scheduled task runner logs status messages, verbose messages, parse failures, exceptions, and cancellation requests.

## Credentials

`CredentialHelper` encrypts usernames and passwords with Windows DPAPI using `DataProtectionScope.CurrentUser`.

Operational consequences:

- The encrypted values can only be decrypted by the same Windows user profile.
- Moving settings files to another machine or another Windows account will not preserve usable credentials.
- Scheduled tasks should run as the same Windows user that created the task settings.

The JSON settings files contain encrypted credential fields. The plain `UserName` and `Password` properties are marked `JsonIgnore` in the shared settings model.

## FFmpeg and FFprobe

The app asks for `ffmpeg.exe`, but it also uses `ffprobe.exe` from the same directory. If timelapse creation fails before frame extraction, confirm that both executables exist side by side.

Important operations:

```text
ffprobe: read video duration
ffmpeg: validate downloaded videos
ffmpeg: extract PNG frames
ffmpeg: build final H.264 MP4
```

FFmpeg progress is parsed from stderr lines containing `time=...`. Some FFmpeg output format changes could affect progress reporting without necessarily breaking the actual operation.

## UniFi Protect API

The app uses these endpoints:

```text
POST /api/auth/login
GET  /proxy/protect/api/cameras
GET  /proxy/protect/api/video/export
```

Authentication supports either an `accessToken` response or cookie-based session behavior.

The HTTP handler allows certificate name mismatch and certificate chain errors, which is common for local IP-based controller access and self-signed certificates.

## Download Behavior

Downloads are split into one-hour segments. Each segment becomes an MP4 file named with the date, Unix millisecond start time, and camera name.

On timeout, a segment is retried up to four times. HTTP 500 and 404 failures are treated as segment-level failures and the app continues. Operation cancellation stops the download.

Common causes of partial or failed downloads:

- The requested date is outside the controller retention window.
- The camera was offline for part of the range.
- The recording is corrupt or incomplete.
- The undocumented UniFi Protect API changed.
- The controller needs more time to prepare a large export.

## Timelapse Behavior

The timelapse builder searches the input folder for:

```text
*.mp4
*.mov
*.avi
```

For each input video, it extracts PNG frames into a `TempFrames` folder under the current job folder. It then writes a concat list and creates the final MP4.

Cleanup behavior:

- Temporary frames are deleted when cleanup is enabled.
- Input videos are also deleted when cleanup is enabled.
- In scheduled task mode, the whole job folder is deleted after the run.

Disable cleanup while testing.

## Cancellation and Shutdown

Download and timelapse operations have separate cancellation tokens. In the UI, pressing the active operation button a second time requests cancellation.

During application shutdown, `MainWindow` asks the view model whether active work is running. If work is active, shutdown is delayed while `TimelapseBuilder.Shutdown` cancels work and waits for active task counts to drain.

## Scheduled Task Details

Task creation uses the `TaskScheduler` NuGet package and registers tasks in the Windows root task folder by task name.

The task definition:

- Runs with highest privileges.
- Is enabled.
- Is not hidden.
- Calls the current app executable location.
- Passes the per-task settings file and `--scheduledtask`.

Removing a task from the scheduler window deletes the per-task settings file, removes the task from `tasks.json`, and deletes the Windows scheduled task if it exists.

## Exit Codes

Command-line mode uses explicit shutdown codes in several failure paths:

| Code | Meaning |
| --- | --- |
| `0` | Success or user-requested scheduled task exit. |
| `1` | Missing or invalid `--settings` path. |
| `2` | Failed to create scheduled task log folder or log file path. |
| `3` | Settings parse failure or non-scheduled task execution failure. |
| `4` | Scheduled task execution exception. |

## Troubleshooting Checklist

If connection fails:

- Confirm the controller address is an absolute URL.
- Confirm the account is local and has enough permissions.
- Stop retrying if credentials may be wrong.
- Enable verbose logs and inspect the authentication response.

If cameras do not load:

- Confirm authentication succeeded.
- Confirm the Protect application is installed and reachable on the controller.
- Confirm the account can view cameras in the Protect web UI.

If downloads fail:

- Test a smaller time range.
- Confirm recordings exist for the selected dates.
- Try enabling validation after downloading.
- Review verbose logs for HTTP status codes or timeout messages.

If timelapse creation fails:

- Confirm the input folder contains `.mp4`, `.mov`, or `.avi` files.
- Confirm `ffprobe.exe` is next to `ffmpeg.exe`.
- Disable cleanup and inspect the job folder.
- Run with verbose logs enabled to capture FFmpeg output.

If scheduled tasks fail:

- Confirm the task was created while running the app as administrator.
- Confirm the task runs as the same Windows user that created the encrypted settings.
- Confirm the configured output folder exists.
- Check the scheduled task log folder under `%APPDATA%\UnifiVideoExporter\tasks\executionLog`.

