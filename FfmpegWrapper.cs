
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;

namespace UnifiVideoExporter
{
    internal static class FfmpegWrapper
    {
        public static readonly int FfmpegFps = 30;

        internal static async Task<(int, string)> FfmpegExtractFramesAsync(
            string FfmpegPath, 
            string VideoFile, 
            double Duration, 
            double SnapshotInterval, 
            string OutputFileNameFormat, 
            Action<string>? StatusCallback,
            Action<string, TraceLevel>? VerboseCallback,
            CancellationToken Token
            )
        {
            return await FfmpegRunAsync(
                FfmpegPath,
                $"-i \"{VideoFile}\" -vf \"fps=1/{SnapshotInterval}\" -f image2 \"{OutputFileNameFormat}\"",
                Duration,
                (status) => StatusCallback?.Invoke($"Extracting frames from video: {status:F1}%"),
                VerboseCallback,
                Token);
        }

        internal static async Task<(int, string)> FfmpegBuildTimelapseVideoAsync(
            string FfmpegPath, 
            string InputFileList, 
            string OutputFilePath, 
            double Duration, 
            double FramesPerSecond,
            Action<string>? StatusCallback,
            Action<string, TraceLevel>? VerboseCallback,
            CancellationToken Token
            )
        {
            return await FfmpegRunAsync(
                FfmpegPath,
                $"-r {FramesPerSecond} -f concat -safe 0 -i \"{InputFileList}\" -c:v libx264 -preset fast "+
                $"-crf 23 -vsync vfr -vf fps={FramesPerSecond} -pix_fmt yuv420p -use_wallclock_as_timestamps 1 "+
                $"-fps_mode vfr \"{OutputFilePath}\"",
                Duration,
                (status) => StatusCallback?.Invoke($"Building timelapse video: {status:F1}%"),
                VerboseCallback,
                Token);
        }

        internal static async Task<(int, string)> FfmpegValidateVideoAsync(
            string FfmpegPath, 
            string VideoFilePath, 
            double Duration, 
            Action<string> StatusCallback,
            Action<string, TraceLevel>? VerboseCallback,
            CancellationToken Token
            )
        {
            return await FfmpegRunAsync(
                FfmpegPath, 
                $"-v error -i \"{VideoFilePath}\" -f null -", 
                Duration,
                (status) => StatusCallback?.Invoke($"Validating video: {status:F1}%"),
                VerboseCallback,
                Token);
        }

        internal static async Task<double> FfmpegGetVideoDurationAsync(
            string FfmpegPath,
            string VideoFilePath,
            Action<string, TraceLevel>? VerboseCallback,
            CancellationToken Token
            )
        {
            //
            // This command requires ffprobe.exe, which is always distributed side-by-side with ffmpeg.exe
            //
            var ffprobePath = Path.Combine(Path.GetDirectoryName(FfmpegPath)!, "ffprobe.exe");
            if (!File.Exists(ffprobePath))
            {
                throw new Exception($"Unable to locate ffprobe at {ffprobePath}");
            }
            var result = await FfprobeRunAsync(
                ffprobePath,
                $"-v error -show_entries format=duration -of json \"{VideoFilePath}\"",
                1, // Dummy duration, as probe is quick
                VerboseCallback,
                Token);
            if (result.Item1 != 0 || string.IsNullOrEmpty(result.Item2))
            {
                if (string.IsNullOrEmpty(result.Item2))
                {
                    throw new Exception("ffprobe produced no output");
                }
                throw new Exception($"Ffprobe failed: {result.Item2}");
            }
            string output = result.Item2;
            var json = JObject.Parse(output);
            double duration = json["format"]?["duration"]?.Value<double>() ?? 0;
            return duration;
        }

        private static async Task<(int, string)> FfmpegRunAsync(
            string FfmpegPath,
            string Args,
            double Duration,
            Action<double>? ProgressCallback,
            Action<string, TraceLevel>? VerboseCallback,
            CancellationToken Token)
        {
            VerboseCallback?.Invoke($"Executing {FfmpegPath} {Args}", TraceLevel.Verbose);
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = FfmpegPath,
                    Arguments = Args + " -y -progress -", // important: using `pipe:2` instead of `-` causes hangs in Windows task scheduler
                    UseShellExecute = false,
                    RedirectStandardOutput = false,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    VerboseCallback?.Invoke(e.Data, TraceLevel.Verbose);

                    /* NB: This format should be used if we ever change back to " -progress pipe:2"
                    if (e.Data.StartsWith("out_time_ms="))
                    {
                        if (long.TryParse(e.Data.Split('=')[1], out long outTimeMs))
                        {
                            double progress = Math.Min((double)outTimeMs / 1000000 / Duration * 100, 100);
                            ProgressCallback?.Invoke(progress);
                        }
                    }
                    */
                    if (e.Data.Contains("time="))
                    {
                        // Extract time value (e.g., "00:59:00.00")
                        var timeMatch = Regex.Match(e.Data, @"time=(\d{2}:\d{2}:\d{2}\.\d{2})");
                        if (timeMatch.Success && TimeSpan.TryParse(timeMatch.Groups[1].Value, out TimeSpan time))
                        {
                            double outTimeMs = time.TotalMilliseconds;
                            double progress = Math.Min(outTimeMs / 1000 / Duration * 100, 100);
                            ProgressCallback?.Invoke(progress);
                        }
                    }
                }
            };

            return await RunProcessAsync(process, true, false, VerboseCallback, Token);
        }

        private static async Task<(int, string)> FfprobeRunAsync(
            string FfprobePath,
            string Args,
            double Duration,
            Action<string, TraceLevel>? VerboseCallback,
            CancellationToken Token
            )
        {
            VerboseCallback?.Invoke($"Executing {FfprobePath} {Args}", TraceLevel.Verbose);
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = FfprobePath,
                    Arguments = Args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = false,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };
            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    VerboseCallback?.Invoke(e.Data, TraceLevel.Verbose);
                }
            };
            return await RunProcessAsync(process, false, true, VerboseCallback, Token);
        }

        private static async Task<(int, string)> RunProcessAsync(
            Process Process,
            bool RedirectStderr,
            bool RedirectStdout,
            Action<string, TraceLevel>? VerboseCallback,
            CancellationToken Token
            )
        {
            var stderrBuffer = new StringBuilder();
            var stdoutBuffer = new StringBuilder();

            Process.Start();
            if (RedirectStderr)
            {
                Process.BeginErrorReadLine();
                Process.ErrorDataReceived += (s, e) =>
                {
                    stderrBuffer.Append(e.Data);
                };
            }
            if (RedirectStdout)
            {
                Process.BeginOutputReadLine();
                Process.OutputDataReceived += (s, e) =>
                {
                    stdoutBuffer.Append(e.Data);
                };
            }

            using (var cts = new CancellationTokenSource())
            {
                Token.Register(() =>
                {
                    try
                    {
                        if (!Process.HasExited)
                        {
                            VerboseCallback?.Invoke($"Killing process...", TraceLevel.Verbose);
                            Process.Kill();
                            VerboseCallback?.Invoke($"Process killed.", TraceLevel.Verbose);
                        }
                    }
                    catch (Exception) { }
                });

                var processTask = Task.Run(async () =>
                {
                    VerboseCallback?.Invoke($"Waiting for process to exit...", TraceLevel.Verbose);
                    await Task.Run(() => Process.WaitForExit(), Token);
                    VerboseCallback?.Invoke($"Process has exited.", TraceLevel.Verbose);
                    if (RedirectStderr)
                    {
                        Process.CancelErrorRead();
                    }
                    if (RedirectStdout)
                    {
                        Process.CancelOutputRead();
                    }
                    var exitCode = Process.ExitCode;
                    try
                    {
                        Process.Close(); // Explicitly close process resources
                    }
                    catch (Exception) { }
                    if (RedirectStderr)
                    {
                        return (exitCode, stderrBuffer.ToString());
                    }
                    if (RedirectStdout)
                    {
                        return (exitCode, stdoutBuffer.ToString());
                    }
                    return (exitCode, string.Empty);
                }, Token);

                await Task.WhenAny(processTask, Task.Delay(Timeout.Infinite, Token));
                return await processTask;
            }
        }
    }
}
