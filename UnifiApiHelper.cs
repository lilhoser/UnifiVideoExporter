using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Net.Security;
using System.Text.RegularExpressions;
using System.Text;
using System.Collections;
using System.IO;
using System.Diagnostics;

namespace UnifiVideoExporter
{
    internal class UnifiVideoDownloadTimeoutException : Exception
    {
        public UnifiVideoDownloadTimeoutException()
        {
        }

        public UnifiVideoDownloadTimeoutException(string message)
            : base(message)
        {
        }
    }

    internal class UnifiApiHelper
    {
        private readonly HttpClient _httpClient;
        private readonly string _AuthEndpoint = $"/api/auth/login";
        private readonly string _CamerasEndpoint = $"/proxy/protect/api/cameras";
        private readonly string _ExportEndpoint = $"/proxy/protect/api/video/export";
        private string _authToken = string.Empty;
        private bool _authenticated = false;
        private readonly string _controllerAddress = string.Empty;

        public Action<string, bool>? StatusCallback;
        public Action<string, TraceLevel>? VerboseCallback;

        public UnifiApiHelper(string ControllerAddress)
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) =>
                {
                    if (sslPolicyErrors == SslPolicyErrors.None)
                        return true;
                    // Allow connections despite name mismatch for IP-based URLs and chain errors
                    // due to self-signing.
                    return sslPolicyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch) ||
                    sslPolicyErrors.HasFlag(SslPolicyErrors.RemoteCertificateChainErrors);
                },
            };
            _httpClient = new HttpClient(handler);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "UnifiVideoExporter");
            _controllerAddress = ControllerAddress;
        }

        internal async Task ConnectAsync(string Username, string Password)
        {
            if (_authenticated)
            {
                throw new Exception("Client already authenticated");
            }

            //
            // Note: for reference, see https://github.com/uilibs/uiprotect/blob/main/src/uiprotect/api.py
            //
            var payload = new
            {
                username = Username,
                password = Password,
                rememberMe = true,
            };
            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_controllerAddress}{_AuthEndpoint}")
            {
                Content = content
            };
            request.Headers.Add("Accept", "application/json");
            var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();
            VerboseCallback?.Invoke($"Authentication response: Status {response.StatusCode}, Body: {responseBody}", TraceLevel.Verbose);
            VerboseCallback?.Invoke("Response headers:", TraceLevel.Verbose);
            foreach (var header in response.Headers)
            {
                VerboseCallback?.Invoke($"{header.Key}: {string.Join(", ", header.Value)}", TraceLevel.Verbose);
            }

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    throw new Exception("Unauthorized: Check your username and password, or ensure the account has API access permissions.");
                }
                throw new Exception($"HTTP {response.StatusCode}: {responseBody}");
            }

            var json = JObject.Parse(responseBody);
            if (json.ContainsKey("accessToken"))
            {
                _authToken = json["accessToken"]!.ToString();
            }

            if (string.IsNullOrEmpty(_authToken))
            {
                var sessionToken = string.Empty;
                if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
                {
                    foreach (var cookie in cookies)
                    {
                        var match = Regex.Match(cookie, @"TOKEN=([^;]+)");
                        if (match.Success)
                        {
                            sessionToken = match.Groups[1].Value;
                            VerboseCallback?.Invoke($"Found session token in Set-Cookie: TOKEN={sessionToken}", TraceLevel.Verbose);
                            break;
                        }
                    }
                }
                if (string.IsNullOrEmpty(sessionToken))
                {
                    throw new Exception("No access token or session cookie received. Check the response body and headers in the log.");
                }
                //
                // Cookie-based authentication; HttpClient will handle cookies automatically
                //
                VerboseCallback?.Invoke("Using cookie-based authentication (no Authorization header set).", TraceLevel.Verbose);
            }
            else
            {
                //
                // JWT-based authentication
                //
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authToken);
                VerboseCallback?.Invoke("Using JWT-based authentication with Bearer token.", TraceLevel.Verbose);
            }
            _authenticated = true;
        }

        internal async Task<string[]> GetCameraListAsync()
        {
            if (!_authenticated)
            {
                throw new Exception("Client not authenticated");
            }
            var response = await _httpClient.GetAsync($"{_controllerAddress}{_CamerasEndpoint}");
            response.EnsureSuccessStatusCode();
            var json = JArray.Parse(await response.Content.ReadAsStringAsync());
            var cameras = new ArrayList();
            foreach (var camera in json)
            {
                var name = camera["name"]?.ToString();
                if (!string.IsNullOrEmpty(name))
                {
                    cameras.Add(name);
                }
            }
            return (string[])cameras.ToArray(typeof(string));
        }

        internal async Task<string> GetCameraIdAsync(string cameraName)
        {
            if (!_authenticated)
            {
                throw new Exception("Client not authenticated");
            }
            var response = await _httpClient.GetAsync($"{_controllerAddress}{_CamerasEndpoint}");
            response.EnsureSuccessStatusCode();
            var json = JArray.Parse(await response.Content.ReadAsStringAsync());
            foreach (var camera in json)
            {
                if (camera["name"]?.ToString().Equals(cameraName, StringComparison.OrdinalIgnoreCase) == true)
                {
                    var id = camera["id"]?.ToString();
                    if (string.IsNullOrEmpty(id))
                    {
                        throw new Exception($"Camera {cameraName} has no ID");
                    }
                    return id;
                }
            }
            throw new Exception($"No camera named {cameraName} found");
        }

        internal async Task DownloadVideoAsync(
            string CameraId,
            long StartMs,
            long EndMs,
            string TargetOutputLocation,
            CancellationToken Token,
            int DownloadTimeoutSec = 30
            )
        {
            if (!_authenticated)
            {
                throw new Exception("Client not authenticated");
            }
            StatusCallback?.Invoke($"Issuing request for video export...", false);
            var getUrl = $"{_controllerAddress}{_ExportEndpoint}?camera={CameraId}&start={StartMs}&end={EndMs}&channel=0";
            var request = new HttpRequestMessage(HttpMethod.Get, getUrl);
            request.Headers.Add("Accept", "video/mp4");

            using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, Token))
            {
                response.EnsureSuccessStatusCode();

                if (response.Content.Headers.ContentType?.MediaType != "video/mp4")
                {
                    using (var stream = await response.Content.ReadAsStreamAsync(Token))
                    {
                        byte[] buffer = new byte[1024];
                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, Token);
                        string errorBody = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        throw new Exception($"Invalid content type: Expected video/mp4, got {response.Content.Headers.ContentType}. Partial Body: {errorBody}");
                    }
                }

                using (var stream = await response.Content.ReadAsStreamAsync(Token))
                using (var fileStream = new FileStream(TargetOutputLocation, FileMode.Create, FileAccess.Write))
                {
                    byte[] buffer = new byte[4 * 1024 * 1024]; // 4MB
                    long totalBytes = 0;
                    long? contentLength = response.Content.Headers.ContentLength;
                    double lastProgress = -1;

                    while (true)
                    {
                        int bytesRead = 0;
                        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Token);
                        cts.CancelAfter(TimeSpan.FromSeconds(DownloadTimeoutSec));
                        try
                        {
                            bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            if (cts.IsCancellationRequested)
                            {
                                var msg = "ReadAsync timed out after 30 seconds. Aborting download.";
                                StatusCallback?.Invoke(msg, true);
                                throw new UnifiVideoDownloadTimeoutException(msg);
                            }
                            throw; // user cancelled
                        }

                        if (bytesRead <= 0)
                            break;

                        await fileStream.WriteAsync(buffer, 0, bytesRead, Token);
                        totalBytes += bytesRead;

                        if (contentLength.HasValue)
                        {
                            double progress = (double)totalBytes / contentLength.Value * 100;
                            if (progress - lastProgress >= 1)
                            {
                                StatusCallback?.Invoke(
                                    $"Downloading video: {progress:F1}% ({totalBytes / (1024 * 1024)} MB of {contentLength.Value / (1024 * 1024)} MB)",
                                    false);
                                lastProgress = progress;
                            }
                        }
                        else
                        {
                            StatusCallback?.Invoke($"Downloading video: {totalBytes / (1024 * 1024)} MB downloaded", false);
                        }
                    }
                    StatusCallback?.Invoke($"Downloaded video: {TargetOutputLocation} ({totalBytes / (1024 * 1024)} MB)", false);
                }
            }
        }
    }
}
