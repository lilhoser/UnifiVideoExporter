
using System.IO;

namespace UnifiVideoExporter
{
    internal class TimelapseBuilder
    {
        private CancellationTokenSource? _createTimelapseCancellationTokenSource;
        private CancellationTokenSource? _downloadVideoCancellationTokenSource;
        private UnifiApiHelper? _apiHelper;
        private bool _isCreatingTimelapse = false;
        private bool _isDownloadingVideo = false;
        private int _activeTaskCount = 0;

        public Action<double>? ProgressCallback;
        public Action<string,bool>? StatusCallback;
        public Action<string>? VerboseCallback;
        public bool _isConnected { get; private set; } = false;
        public string _jobFolder { get; set; } = string.Empty;

        public TimelapseBuilder()
        {
            _jobFolder = Path.Combine(SettingsHelper.s_WorkingDirectory, Path.GetRandomFileName());
        }

        public bool CanShutdown()
        {
            return _activeTaskCount == 0;
        }

        public void NewJobFolder()
        {
            _jobFolder = Path.Combine(SettingsHelper.s_WorkingDirectory, Path.GetRandomFileName());
        }

        public async Task Shutdown()
        {
            _createTimelapseCancellationTokenSource?.Cancel();
            _downloadVideoCancellationTokenSource?.Cancel();
            while (_activeTaskCount > 0)
            {
                VerboseCallback?.Invoke($"Waiting for {_activeTaskCount} tasks to complete...");
                await Task.Delay(100); // Poll every 100ms
            }
        }

        public async Task<bool> ConnectAsync(string ControllerAddress, string Username, string Password)
        {
            if (_isConnected)
            {
                return true;
            }

            _apiHelper = new UnifiApiHelper(ControllerAddress);
            _apiHelper.StatusCallback = StatusCallback;
            _apiHelper.VerboseCallback = VerboseCallback;
            _isConnected = true;
            StatusCallback?.Invoke("Connecting...", false);
            Interlocked.Increment(ref _activeTaskCount);

            try
            {
                await _apiHelper.ConnectAsync(Username, Password);
                StatusCallback?.Invoke("Connected successfully.",false);
                VerboseCallback?.Invoke("Connected to UniFi Protect controller.");
                return true;
            }
            catch (Exception ex)
            {
                StatusCallback?.Invoke($"Connection failed", true);
                VerboseCallback?.Invoke($"Connection error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    VerboseCallback?.Invoke($"Details: {ex.InnerException.Message}");
                }
                _isConnected = false;
                return false;
            }
            finally
            {
                Interlocked.Decrement(ref _activeTaskCount);
            }
        }

        public async Task<string[]?> GetCameraListAsync()
        {
            if (!_isConnected)
            {
                VerboseCallback?.Invoke("Not connected to controller");
                return null;
            }
            Interlocked.Increment(ref _activeTaskCount);
            try
            {
                return await _apiHelper!.GetCameraListAsync();
            }
            catch (Exception ex)
            {
                StatusCallback?.Invoke($"Failed to retrieve camera list", true);
                VerboseCallback?.Invoke($"Exception retrieving camera list: {ex.Message}");
                return null;
            }
            finally
            {
                Interlocked.Decrement(ref _activeTaskCount);
            }
        }

        public async Task<string?> GetCameraIdAsync(string CameraName)
        {
            if (!_isConnected)
            {
                VerboseCallback?.Invoke("Not connected to controller");
                return null;
            }
            Interlocked.Increment(ref _activeTaskCount);
            try
            {
                return await _apiHelper!.GetCameraIdAsync(CameraName);
            }
            catch (Exception ex)
            {
                StatusCallback?.Invoke($"Failed to get camera ID", true);
                VerboseCallback?.Invoke($"Exception retrieving camera ID: {ex.Message}");
                return null;
            }
            finally
            {
                Interlocked.Decrement(ref _activeTaskCount);
            }
        }

        public async Task<bool> DownloadUnifiVideoAsync(
            string FfmpegPath,
            string CameraName,
            DateTime StartDate,
            DateTime EndDate,
            string StartTime,
            string EndTime,
            bool Validate=false
            )
        {
            if (_isDownloadingVideo)
            {
                //
                // This function is re-entrant in the sense that a first invocation starts
                // a download and a subsequent invocation cancels it.
                //
                _downloadVideoCancellationTokenSource?.Cancel();
                return true;
            }

            Interlocked.Increment(ref _activeTaskCount);
            _downloadVideoCancellationTokenSource = new CancellationTokenSource();
            StatusCallback?.Invoke("Downloading...", false);
            VerboseCallback?.Invoke($"Downloading video(s) from controller...");
            _isDownloadingVideo = true;

            try
            {
                var cameraId = await _apiHelper!.GetCameraIdAsync(CameraName);
                Directory.CreateDirectory(_jobFolder);
                VerboseCallback?.Invoke($"Using job folder {_jobFolder}");

                //
                // Process each date in the selected range.
                //
                int numDownloaded = 0, numFailed = 0;
                for (var date = StartDate; date <= EndDate; date = date.AddDays(1))
                {
                    string dateStr = date.ToString("yyyyMMdd");
                    if (!TimeSpan.TryParse(StartTime, out var startTimeSpan) || !TimeSpan.TryParse(EndTime, out var endTimeSpan))
                    {
                        throw new Exception($"Invalid time format for {date:yyyyMMdd}. Use HH:mm.");
                    }

                    var currentTime = new DateTime(date.Year, date.Month, date.Day, startTimeSpan.Hours, startTimeSpan.Minutes, 0);
                    var endOfDayTime = new DateTime(date.Year, date.Month, date.Day, endTimeSpan.Hours, endTimeSpan.Minutes, 0);

                    if (endOfDayTime < currentTime)
                    {
                        endOfDayTime = endOfDayTime.AddDays(1); // Handle time ranges crossing midnight
                    }

                    VerboseCallback?.Invoke($"Processing date {dateStr}...");

                    while (currentTime < endOfDayTime)
                    {
                        var segmentEndTime = currentTime.AddHours(1);
                        if (segmentEndTime > endOfDayTime)
                        {
                            segmentEndTime = endOfDayTime; // Last segment may be shorter than an hour
                        }

                        long startMs = (long)(currentTime.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
                        long endMs = (long)(segmentEndTime.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
                        var inputVideoFile = Path.Combine(_jobFolder, $"{dateStr}_{startMs}_{CameraName}.mp4");
                        VerboseCallback?.Invoke($"Downloading segment for {date:yyyyMMdd} from {currentTime:HH:mm} to {segmentEndTime:HH:mm}");
                        try
                        {
                            await _apiHelper.DownloadVideoAsync(
                                cameraId,
                                startMs,
                                endMs,
                                inputVideoFile,
                                _downloadVideoCancellationTokenSource.Token);
                            numDownloaded++;
                        }
                        catch (OperationCanceledException ex2)
                        {
                            //
                            // UniFi controller will abruptly cancel a download request if there is a problem
                            // with the recording; eg, the camera was offline. In these cases, we'll swallow
                            // the exception and continue on to the next segment. If the cancellation was from
                            // a user request, stop now.
                            //
                            if (ex2.CancellationToken == _downloadVideoCancellationTokenSource.Token)
                            {
                                throw;
                            }
                            VerboseCallback?.Invoke($"  ->Segment is corrupt or invalid, skipping.");
                            numFailed++;
                        }

                        _downloadVideoCancellationTokenSource.Token.ThrowIfCancellationRequested();

                        //
                        // For validation, duration is a function of the video length.
                        //
                        if (Validate)
                        {
                            var duration = (segmentEndTime - currentTime).TotalSeconds;
                            var result = await FfmpegWrapper.FfmpegValidateVideoAsync(
                                FfmpegPath,
                                inputVideoFile,
                                duration,
                                Progress => StatusCallback?.Invoke($"Validating video: {Progress:F1}%",false),
                                VerboseCallback,
                                _downloadVideoCancellationTokenSource.Token);
                            if (result.Item1 != 0)
                            {
                                StatusCallback?.Invoke("Validation failed", true);
                                VerboseCallback?.Invoke($"Video {inputVideoFile} could not be validated: {result.Item2}");
                                return false;
                            }
                        }
                        _downloadVideoCancellationTokenSource.Token.ThrowIfCancellationRequested();
                        currentTime = segmentEndTime;
                    }
                }

                VerboseCallback?.Invoke($"Video(s) downloaded ({numDownloaded} success, {numFailed} failed)");
                StatusCallback?.Invoke("Video(s) downloaded.",false);
                return true;
            }
            catch (OperationCanceledException)
            {
                StatusCallback?.Invoke($"Operation cancelled", true);
                VerboseCallback?.Invoke($"Operation cancelled");
                return false;
            }
            catch (Exception ex)
            {
                StatusCallback?.Invoke($"Download failed", true);
                VerboseCallback?.Invoke($"DownloadUnifiVideoAsync: {ex.Message}");
                VerboseCallback?.Invoke($"Make sure the date range is valid for your controller's storage capacity.");
                return false;
            }
            finally
            {
                _isDownloadingVideo = false;
                _downloadVideoCancellationTokenSource?.Dispose();
                _downloadVideoCancellationTokenSource = null;
                Interlocked.Decrement(ref _activeTaskCount);
            }
        }

        public async Task<bool> CreateTimelapseAsync(
            string FfmpegPath, 
            string? LocalVideoPath,
            string? OutputPath,
            double SnapshotInterval,
            double FramesPerSecond,
            bool CleanupVideoFiles
            )
        {
            if (_isCreatingTimelapse)
            {
                //
                // This function is re-entrant in the sense that a first invocation starts
                // a timelapse and a subsequent invocation cancels it.
                //
                _createTimelapseCancellationTokenSource?.Cancel();
                return true;
            }

            Interlocked.Increment(ref _activeTaskCount);
            _isCreatingTimelapse = true;
            _createTimelapseCancellationTokenSource = new CancellationTokenSource();

            StatusCallback?.Invoke("Creating timelapse video...", false);
            var inputVideos = new List<string>();
            string tempFramesDir = string.Empty;
            double duration;
            (int, string) result;
            var inputPath = LocalVideoPath;
            if (string.IsNullOrEmpty(inputPath))
            {
                inputPath = _jobFolder;
            }

            try
            {
                var videoExtensions = new[] { "*.mp4", "*.mov", "*.avi" };
                var files = videoExtensions.SelectMany(ext => Directory.GetFiles(inputPath, ext)).ToArray();
                if (files.Length == 0)
                {
                    VerboseCallback?.Invoke($"No video files found in path {inputPath}");
                    StatusCallback?.Invoke("No videos found", true);
                    return false;
                }
                inputVideos.AddRange(files);
                VerboseCallback?.Invoke($"Creating timelapse video from {files.Length} video(s) in path {inputPath}...");
                tempFramesDir = Path.Combine(_jobFolder, "TempFrames");
                Directory.CreateDirectory(tempFramesDir);

                //
                // For all input videos, extract frames on specified interval into PNG files.
                //
                foreach (var inputVideo in inputVideos)
                {
                    VerboseCallback?.Invoke($"Processing input video {inputVideo}...");

                    //
                    // Determine the video's duration which is needed for frame extraction progress reporting.
                    //
                    duration = await FfmpegWrapper.FfmpegGetVideoDurationAsync(
                        FfmpegPath,
                        inputVideo,
                        VerboseCallback,
                        _createTimelapseCancellationTokenSource.Token);
                    if (duration == 0)
                    {
                        StatusCallback?.Invoke("Unable to determine video duration", true);
                        VerboseCallback?.Invoke($"FfmpegGetVideoDurationAsync failed");
                        return false;
                    }
                    VerboseCallback?.Invoke($"Video duration is {duration:F1}...");

                    //
                    // Extract frames at specified interval and write them to PNGs
                    //
                    var name = Path.GetFileNameWithoutExtension(inputVideo);
                    var outputFileNameFormat = Path.Combine($"{tempFramesDir}", $"{name}_%06d.png");
                    result = await FfmpegWrapper.FfmpegExtractFramesAsync(
                        FfmpegPath,
                        inputVideo,
                        duration,
                        SnapshotInterval,
                        outputFileNameFormat,
                        Progress => StatusCallback?.Invoke($"Extracting frames from video: {Progress:F1}%",false),
                        VerboseCallback,
                        _createTimelapseCancellationTokenSource.Token);
                    if (result.Item1 != 0)
                    {
                        StatusCallback?.Invoke("Frame extraction failed", true);
                        VerboseCallback?.Invoke($"Frame extraction failed:\n{result.Item2}...");
                        return false;
                    }
                }

                //
                // Now use ffmpeg to glue together all PNGs into a timelapse video.
                //
                var outputLocation = string.IsNullOrEmpty(OutputPath) ? _jobFolder : OutputPath;
                string outputVideo = Path.Combine(outputLocation, "timelapse.mp4");
                //
                // Ffmpeg globbing support is lackluster, so use concat demux on a list of files.
                //              
                string[] pngFiles = Directory.GetFiles(tempFramesDir, "*.png").OrderBy(f => f).ToArray();
                string inputFileList = Path.Combine(tempFramesDir, "ffmpeg_input.txt");
                using (var writer = new StreamWriter(inputFileList))
                {
                    duration = (double)1 / FramesPerSecond;
                    foreach (string file in pngFiles)
                    {
                        writer.WriteLine($"file '{file.Replace("\\", "/")}'");
                        writer.WriteLine($"duration {duration}");
                    }
                }

                //
                // For building a timelapse video, duration is a function of the number of frames
                // in the video and the frame rate. Each png represents a single frame, so the
                // number of files in the frames dir represents the total frame count.
                //                
                int frameCount = pngFiles.Length;
                if (frameCount == 0)
                {
                    StatusCallback?.Invoke("No frames found", true);
                    VerboseCallback?.Invoke($"No frames (PNG files) found in {tempFramesDir}");
                    return false;
                }
                duration = (double)frameCount / FramesPerSecond;
                VerboseCallback?.Invoke($"Timelapse video: {frameCount} frames, duration {duration:F1} seconds at {FramesPerSecond} FPS");
                result = await FfmpegWrapper.FfmpegBuildTimelapseVideoAsync(
                    FfmpegPath,
                    inputFileList,
                    outputVideo,
                    duration,
                    FramesPerSecond,
                    Progress => StatusCallback?.Invoke($"Building timelapse video: {Progress:F1}%",false),
                    VerboseCallback,
                    _createTimelapseCancellationTokenSource.Token);
                if (result.Item1 != 0)
                {
                    StatusCallback?.Invoke($"Ffmpeg failed", true);
                    VerboseCallback?.Invoke(result.Item2);
                    return false;
                }
                StatusCallback?.Invoke("Timelapse created successfully.",false);
                VerboseCallback?.Invoke($"Timelapse video created at {outputVideo}");
                return true;
            }
            catch (OperationCanceledException)
            {
                StatusCallback?.Invoke($"Operation cancelled", true);
                VerboseCallback?.Invoke($"Operation cancelled");
                return false;
            }
            catch (Exception ex)
            {
                StatusCallback?.Invoke($"Timelapse creation failed", true);
                VerboseCallback?.Invoke($"CreateTimelapseAsync: {ex.Message}");
                return false;
            }
            finally
            {
                _isCreatingTimelapse = false;
                _createTimelapseCancellationTokenSource?.Dispose();
                _createTimelapseCancellationTokenSource = null;

                if (CleanupVideoFiles)
                {
                    if (Directory.Exists(tempFramesDir))
                    {
                        try
                        {
                            Directory.Delete(tempFramesDir, true);
                            VerboseCallback?.Invoke("Cleaned up temporary frames.");
                        }
                        catch (Exception ex)
                        {
                            VerboseCallback?.Invoke($"Warning: Failed to delete temporary frames: {ex.Message}");
                        }
                    }
                    foreach (var videoFile in inputVideos)
                    {
                        try
                        {
                            File.Delete(videoFile);
                            VerboseCallback?.Invoke($"Cleaned up video file {videoFile}");
                        }
                        catch (Exception ex)
                        {
                            VerboseCallback?.Invoke($"Failed to delete video file {videoFile}: {ex.Message}");
                        }
                    }
                }

                Interlocked.Decrement(ref _activeTaskCount);
            }
        }
    }
}
