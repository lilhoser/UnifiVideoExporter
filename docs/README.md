# UnifiVideoExporter Documentation

UnifiVideoExporter is a Windows WPF application for exporting UniFi Protect recordings and turning those recordings, or other local video files, into timelapse MP4 files with FFmpeg.

The application has two main workflows:

- Connect to a UniFi Protect controller, download camera recordings, and optionally send the downloaded files into timelapse creation.
- Create a timelapse from local video files, either interactively or through a Windows Task Scheduler job created by the app.

## Documentation Map

- [Usage Guide](usage.md) explains prerequisites, the interactive UI, scheduled jobs, command-line mode, and practical tips.
- [Architecture and Design](architecture.md) describes the application structure, execution modes, data flow, persistence, and important source files.
- [Operations and Troubleshooting](operations.md) covers local files, logs, credentials, FFmpeg requirements, UniFi API failure modes, and scheduler behavior.

## Requirements

- Windows.
- .NET 9 Windows Desktop runtime or SDK.
- FFmpeg full build with both `ffmpeg.exe` and `ffprobe.exe` in the same directory.
- Network access to a UniFi Protect controller when exporting controller recordings.
- Administrator privileges only when creating or removing Windows scheduled tasks.

## Quick Start

1. Download and extract FFmpeg.
2. Run UnifiVideoExporter.
3. Enter the UniFi Protect controller URL, username, and password.
4. Select `ffmpeg.exe`.
5. Click `Connect`.
6. Select a camera, date range, and time range.
7. Click `Download`.
8. Use the downloaded folder as the timelapse input, or select another local folder of videos.
9. Set the snapshot interval and output FPS.
10. Click `Create Timelapse`.

Settings are stored under `%APPDATA%\UnifiVideoExporter`. Usernames and passwords are encrypted with Windows DPAPI for the current Windows user.

## Important Notes

- The UniFi Protect API used by this app is undocumented and can change.
- Use a dedicated local UniFi account instead of a primary admin account.
- Do not repeatedly retry bad credentials. UniFi account lockout behavior is not controlled by this app.
- Scheduled task credentials are encrypted for the Windows user that created them. Running the task as a different user can prevent credentials from decrypting.
- The app deletes input videos after timelapse creation when cleanup is enabled.

