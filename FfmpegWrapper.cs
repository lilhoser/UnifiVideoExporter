
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.IO;
using System.Text;

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
            Action<string>? VerboseCallback,
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
            Action<string>? VerboseCallback,
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
            Action<string>? VerboseCallback,
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
            Action<string>? VerboseCallback,
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
                null, // No progress needed
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
            Action<string>? VerboseCallback,
            CancellationToken Token
            )
        {
            VerboseCallback?.Invoke($"Executing {FfmpegPath} {Args}");
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = FfmpegPath,
                    Arguments = Args + " -progress pipe:2",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };
            //
            // ffmpeg writes diagnostic content to stderr and actual video content to stdout
            // for progress reporting, we need stderr output
            //
            var stderrBuffer = new StringBuilder();
            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    VerboseCallback?.Invoke(e.Data);
                    stderrBuffer.AppendLine(e.Data);
                    if (e.Data.StartsWith("out_time_ms="))
                    {
                        if (long.TryParse(e.Data.Split('=')[1], out long outTimeMs))
                        {
                            double progress = Math.Min((double)outTimeMs / 1000000 / Duration * 100, 100);
                            ProgressCallback?.Invoke(progress);
                        }
                    }
                }
            };
            process.Start();
            process.BeginErrorReadLine();

            using (var cts = new CancellationTokenSource())
            {
                Token.Register(() =>
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            VerboseCallback?.Invoke($"Killing process...");
                            process.Kill();
                            VerboseCallback?.Invoke($"Process killed.");
                        }
                    }
                    catch (Exception) { }
                });

                return await Task.Run(() =>
                {
                    VerboseCallback?.Invoke($"Waiting for process to exit...");
                    process.WaitForExit();
                    VerboseCallback?.Invoke($"Process has exited.");
                    process.CancelErrorRead();
                    return (process.ExitCode, stderrBuffer.ToString());
                },
                Token);
            }
        }

        private static async Task<(int, string)> FfprobeRunAsync(
            string FfprobePath,
            string Args,
            double Duration,
            Action<double>? ProgressCallback,
            Action<string>? VerboseCallback,
            CancellationToken Token
            )
        {
            VerboseCallback?.Invoke($"Executing {FfprobePath} {Args}");
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = FfprobePath,
                    Arguments = Args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };
            var stdoutBuffer = new StringBuilder();
            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    stdoutBuffer.AppendLine(e.Data);
                    VerboseCallback?.Invoke(e.Data);
                }
            };
            process.Start();
            process.BeginOutputReadLine();

            using (var cts = new CancellationTokenSource())
            {
                Token.Register(() =>
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            VerboseCallback?.Invoke($"Killing process...");
                            process.Kill();
                            VerboseCallback?.Invoke($"Process killed.");
                        }
                    }
                    catch (Exception) { }
                });

                return await Task.Run(() =>
                {
                    VerboseCallback?.Invoke($"Waiting for process to exit...");
                    process.WaitForExit();
                    VerboseCallback?.Invoke($"Process has exited.");
                    process.CancelOutputRead();
                    return (process.ExitCode, stdoutBuffer.ToString());
                },
                Token);
            }
        }
    }
}
