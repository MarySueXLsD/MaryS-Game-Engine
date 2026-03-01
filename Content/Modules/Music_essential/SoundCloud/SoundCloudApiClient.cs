using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using MarySGameEngine.Modules.Music_essential.Audio;

namespace MarySGameEngine.Modules.Music_essential.SoundCloud
{
    /// <summary>
    /// SoundCloud API client: OAuth (PKCE), /me/tracks, and stream URL resolution.
    /// </summary>
    public class SoundCloudApiClient
    {
        private static readonly HttpClient HttpClient = new HttpClient();
        private const string TokenEndpoint = "https://secure.soundcloud.com/oauth/token";
        private const string AuthorizeEndpoint = "https://secure.soundcloud.com/authorize";
        private const string ApiBase = "https://api.soundcloud.com";
        private const string ApiV2Base = "https://api-v2.soundcloud.com";

        private string _userAccessToken;
        private string _userRefreshToken;
        private DateTime _tokenExpiry = DateTime.MinValue;
        private string _clientAccessToken;
        private DateTime _clientTokenExpiry = DateTime.MinValue;
        private readonly string _tokenFilePath;
        private readonly object _tokenLock = new object();

        public SoundCloudApiClient()
        {
            _tokenFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MarySGameEngine", "soundcloud_tokens.json");
            LoadStoredTokens();
        }

        public bool HasUserToken => !string.IsNullOrEmpty(_userAccessToken) && _tokenExpiry > DateTime.UtcNow.AddMinutes(2);

        /// <summary>Start OAuth PKCE flow: open browser and wait for callback. Returns access token or null on cancel/error.</summary>
        public async Task<string> SignInWithSoundCloudAsync(Action<string> log = null)
        {
            string codeVerifier = GenerateCodeVerifier();
            string codeChallenge = ComputeCodeChallenge(codeVerifier);
            string state = Guid.NewGuid().ToString("N");

            var listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{SoundCloudConfig.CallbackPort}/");
            listener.Start();
            try
            {
                var authUrl = $"{AuthorizeEndpoint}?client_id={Uri.EscapeDataString(SoundCloudConfig.ClientId)}"
                    + $"&redirect_uri={Uri.EscapeDataString(SoundCloudConfig.RedirectUri)}"
                    + "&response_type=code"
                    + $"&code_challenge={Uri.EscapeDataString(codeChallenge)}"
                    + "&code_challenge_method=S256"
                    + $"&state={Uri.EscapeDataString(state)}";
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = authUrl, UseShellExecute = true }); }
                catch (Exception ex) { log?.Invoke($"Could not open browser: {ex.Message}"); return null; }

                string code = null;
                string returnedState = null;
                var getContextTask = listener.GetContextAsync();
                using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5)))
                {
                    var done = await Task.WhenAny(getContextTask, Task.Delay(Timeout.Infinite, cts.Token));
                    if (done != getContextTask) { log?.Invoke("Sign-in timed out."); return null; }
                }

                var context = await getContextTask;
                try
                {
                    returnedState = context.Request.QueryString["state"];
                    code = context.Request.QueryString["code"];
                    var response = context.Response;
                    response.StatusCode = 200;
                    response.ContentType = "text/html";
                    var body = "<html><body><h2>Signed in successfully. You can close this window.</h2></body></html>";
                    var buf = Encoding.UTF8.GetBytes(body);
                    response.ContentLength64 = buf.Length;
                    await response.OutputStream.WriteAsync(buf);
                    response.OutputStream.Close();
                }
                finally { context.Response.Close(); }

                if (string.IsNullOrEmpty(code) || returnedState != state)
                {
                    log?.Invoke("Invalid callback (state or code missing).");
                    return null;
                }

                var token = await ExchangeCodeForTokenAsync(code, codeVerifier, log);
                if (token != null)
                {
                    lock (_tokenLock)
                    {
                        _userAccessToken = token.AccessToken;
                        _userRefreshToken = token.RefreshToken ?? _userRefreshToken;
                        _tokenExpiry = DateTime.UtcNow.AddSeconds(token.ExpiresIn);
                        SaveTokens();
                    }
                    return _userAccessToken;
                }
            }
            finally { listener.Stop(); }
            return null;
        }

        public void SignOut()
        {
            lock (_tokenLock)
            {
                _userAccessToken = null;
                _userRefreshToken = null;
                _tokenExpiry = DateTime.MinValue;
                try { if (File.Exists(_tokenFilePath)) File.Delete(_tokenFilePath); } catch { }
            }
        }

        private async Task<SoundCloudTokenResponse> ExchangeCodeForTokenAsync(string code, string codeVerifier, Action<string> log)
        {
            var body = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = SoundCloudConfig.ClientId,
                ["client_secret"] = SoundCloudConfig.ClientSecret,
                ["redirect_uri"] = SoundCloudConfig.RedirectUri,
                ["code"] = code,
                ["code_verifier"] = codeVerifier
            };
            return await PostTokenAsync(body, log);
        }

        /// <summary>Refresh user token. Call when HasUserToken is false but we have refresh token.</summary>
        public async Task<bool> RefreshUserTokenAsync(Action<string> log = null)
        {
            string refresh = null;
            lock (_tokenLock) { refresh = _userRefreshToken; }
            if (string.IsNullOrEmpty(refresh)) return false;
            var body = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = SoundCloudConfig.ClientId,
                ["client_secret"] = SoundCloudConfig.ClientSecret,
                ["refresh_token"] = refresh
            };
            var token = await PostTokenAsync(body, log);
            if (token == null) return false;
            lock (_tokenLock)
            {
                _userAccessToken = token.AccessToken;
                _userRefreshToken = token.RefreshToken ?? _userRefreshToken;
                _tokenExpiry = DateTime.UtcNow.AddSeconds(token.ExpiresIn);
                SaveTokens();
            }
            return true;
        }

        private async Task<SoundCloudTokenResponse> PostTokenAsync(Dictionary<string, string> body, Action<string> log)
        {
            var content = new FormUrlEncodedContent(body);
            try
            {
                var response = await HttpClient.PostAsync(TokenEndpoint, content);
                var json = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode) { log?.Invoke($"Token error: {response.StatusCode} {json}"); return null; }
                return JsonSerializer.Deserialize<SoundCloudTokenResponse>(json);
            }
            catch (Exception ex) { log?.Invoke($"Token request failed: {ex.Message}"); return null; }
        }

        /// <summary>Get current user's tracks. Requires user OAuth token.</summary>
        public async Task<List<SoundCloudTrack>> GetMyTracksAsync(Action<string> log = null)
        {
            var list = new List<SoundCloudTrack>();
            try
            {
                string token = null;
                lock (_tokenLock) { token = _userAccessToken; }
                if (string.IsNullOrEmpty(token)) { log?.Invoke("Not signed in."); return list; }
                if (_tokenExpiry <= DateTime.UtcNow.AddMinutes(2) && !(await RefreshUserTokenAsync(log))) { log?.Invoke("Session expired. Please sign in again."); return list; }
                lock (_tokenLock) { token = _userAccessToken; }

                var url = $"{ApiBase}/me/tracks?limit=200&linked_partitioning=1";
                while (!string.IsNullOrEmpty(url))
                {
                    using var req = new HttpRequestMessage(HttpMethod.Get, url);
                    req.Headers.Add("Authorization", "OAuth " + token);
                    req.Headers.Add("Accept", "application/json; charset=utf-8");
                    var response = await HttpClient.SendAsync(req);
                    var json = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode) { log?.Invoke($"API error: {response.StatusCode}"); break; }

                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    if (root.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in root.EnumerateArray())
                            try { list.Add(JsonSerializer.Deserialize<SoundCloudTrack>(item.GetRawText())); } catch { }
                        break;
                    }
                    if (root.TryGetProperty("collection", out var coll))
                    {
                        foreach (var item in coll.EnumerateArray())
                            try { list.Add(JsonSerializer.Deserialize<SoundCloudTrack>(item.GetRawText())); } catch { }
                        url = root.TryGetProperty("next_href", out var next) ? next.GetString() : null;
                    }
                    else
                        break;
                }
            }
            catch (Exception ex) { log?.Invoke($"My tracks failed: {ex.Message}"); }
            return list;
        }

        /// <summary>Get current user's liked tracks. Requires user OAuth token. Uses /me/likes/tracks.</summary>
        public async Task<List<SoundCloudTrack>> GetMyLikesAsync(Action<string> log = null)
        {
            var list = new List<SoundCloudTrack>();
            try
            {
                string token = null;
                lock (_tokenLock) { token = _userAccessToken; }
                if (string.IsNullOrEmpty(token)) { log?.Invoke("Not signed in."); return list; }
                if (_tokenExpiry <= DateTime.UtcNow.AddMinutes(2) && !(await RefreshUserTokenAsync(log))) { log?.Invoke("Session expired."); return list; }
                lock (_tokenLock) { token = _userAccessToken; }

                var url = $"{ApiBase}/me/likes/tracks?limit=200&linked_partitioning=1";
                while (!string.IsNullOrEmpty(url))
                {
                    using var req = new HttpRequestMessage(HttpMethod.Get, url);
                    req.Headers.Add("Authorization", "OAuth " + token);
                    req.Headers.Add("Accept", "application/json; charset=utf-8");
                    var response = await HttpClient.SendAsync(req);
                    var json = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode) { log?.Invoke($"Likes API error: {response.StatusCode}"); break; }
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    if (root.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in root.EnumerateArray())
                            try { if (item.ValueKind == JsonValueKind.Object) { var toParse = item.TryGetProperty("track", out var te) ? te : item; list.Add(ParseTrackFromJson(toParse)); } } catch { }
                        break;
                    }
                    if (root.TryGetProperty("collection", out var coll))
                    {
                        foreach (var item in coll.EnumerateArray())
                            try { if (item.ValueKind == JsonValueKind.Object) { var toParse = item.TryGetProperty("track", out var te) ? te : item; list.Add(ParseTrackFromJson(toParse)); } } catch { }
                        url = root.TryGetProperty("next_href", out var next) ? next.GetString() : null;
                    }
                    else
                        break;
                }
            }
            catch (Exception ex) { log?.Invoke($"Likes failed: {ex.Message}"); }
            return list;
        }

        /// <summary>Search tracks. When signed in uses v2 with user token; when not signed in tries client token then shows empty with message.</summary>
        public async Task<List<SoundCloudTrack>> SearchTracksAsync(string query, int limit = 50, Action<string> log = null)
        {
            if (string.IsNullOrWhiteSpace(query)) return new List<SoundCloudTrack>();
            try
            {
                string token = await GetTokenForPublicApiAsync(log);
                if (string.IsNullOrEmpty(token)) return new List<SoundCloudTrack>();
                var url = $"{ApiV2Base}/search/tracks?q={Uri.EscapeDataString(query.Trim())}&limit={limit}&client_id={Uri.EscapeDataString(SoundCloudConfig.ClientId)}";
                return await FetchTracksFromV2Async(url, token, log);
            }
            catch (Exception ex) { log?.Invoke($"Search failed: {ex.Message}"); return new List<SoundCloudTrack>(); }
        }

        /// <summary>Get a token for public API (v2 charts/search): user token when signed in, else client credentials. Refreshes if needed.</summary>
        private async Task<string> GetTokenForPublicApiAsync(Action<string> log)
        {
            lock (_tokenLock)
            {
                if (!string.IsNullOrEmpty(_userAccessToken) && _tokenExpiry > DateTime.UtcNow.AddMinutes(2))
                    return _userAccessToken;
            }
            if (await RefreshUserTokenAsync(log))
            {
                lock (_tokenLock) { if (!string.IsNullOrEmpty(_userAccessToken)) return _userAccessToken; }
            }
            await RefreshClientCredentialsTokenAsync(log);
            return _clientAccessToken;
        }

        /// <summary>Get popular/charts tracks. When signed in tries v2 then v1; when not signed in uses v1 only to avoid Forbidden.</summary>
        public async Task<List<SoundCloudTrack>> GetChartsAsync(int limit = 30, Action<string> log = null)
        {
            try
            {
                if (HasUserToken)
                {
                    string token = null;
                    lock (_tokenLock) { token = _userAccessToken; }
                    if (!string.IsNullOrEmpty(token))
                    {
                        var url = $"{ApiV2Base}/charts?kind=top&genre=soundcloud%3Agenres%3Aall-music&limit={limit}&client_id={Uri.EscapeDataString(SoundCloudConfig.ClientId)}";
                        var list = await FetchChartTracksFromV2Async(url, token, log);
                        if (list.Count > 0) return list;
                    }
                }
                return await GetDiscoverTracksV1Async(limit, log);
            }
            catch (Exception ex) { log?.Invoke($"Charts failed: {ex.Message}"); return new List<SoundCloudTrack>(); }
        }

        /// <summary>Fallback: get tracks from v1 API with client credentials (when v2 charts/search fail).</summary>
        private async Task<List<SoundCloudTrack>> GetDiscoverTracksV1Async(int limit = 20, Action<string> log = null)
        {
            var list = new List<SoundCloudTrack>();
            try
            {
                await RefreshClientCredentialsTokenAsync(log);
                if (string.IsNullOrEmpty(_clientAccessToken)) return list;
                var url = $"{ApiBase}/tracks?limit={limit}&linked_partitioning=1";
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Add("Authorization", "OAuth " + _clientAccessToken);
                req.Headers.Add("Accept", "application/json; charset=utf-8");
                var response = await HttpClient.SendAsync(req);
                var json = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode) return list;
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in root.EnumerateArray())
                        try { if (item.ValueKind == JsonValueKind.Object) list.Add(ParseTrackFromJson(item)); } catch { }
                    return list;
                }
                if (root.TryGetProperty("collection", out var coll))
                    foreach (var item in coll.EnumerateArray())
                        try { if (item.ValueKind == JsonValueKind.Object) list.Add(ParseTrackFromJson(item)); } catch { }
            }
            catch (Exception ex) { log?.Invoke($"Discover failed: {ex.Message}"); }
            return list;
        }

        /// <summary>Get current user's playlists (requires user OAuth).</summary>
        public async Task<List<SoundCloudPlaylist>> GetMyPlaylistsAsync(Action<string> log = null)
        {
            var list = new List<SoundCloudPlaylist>();
            try
            {
                string token = null;
                lock (_tokenLock) { token = _userAccessToken; }
                if (string.IsNullOrEmpty(token)) { log?.Invoke("Not signed in."); return list; }
                if (_tokenExpiry <= DateTime.UtcNow.AddMinutes(2) && !(await RefreshUserTokenAsync(log))) { log?.Invoke("Session expired."); return list; }
                lock (_tokenLock) { token = _userAccessToken; }

                var url = $"{ApiBase}/me/playlists?limit=50&linked_partitioning=1";
                while (!string.IsNullOrEmpty(url))
                {
                    using var req = new HttpRequestMessage(HttpMethod.Get, url);
                    req.Headers.Add("Authorization", "OAuth " + token);
                    req.Headers.Add("Accept", "application/json; charset=utf-8");
                    var response = await HttpClient.SendAsync(req);
                    var json = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode) { log?.Invoke($"API error: {response.StatusCode}"); break; }
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    if (root.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in root.EnumerateArray())
                            list.Add(ParsePlaylist(item));
                        break;
                    }
                    if (root.TryGetProperty("collection", out var coll))
                    {
                        foreach (var item in coll.EnumerateArray())
                            list.Add(ParsePlaylist(item));
                        url = root.TryGetProperty("next_href", out var next) ? next.GetString() : null;
                    }
                    else
                        break;
                }
            }
            catch (Exception ex) { log?.Invoke($"Playlists failed: {ex.Message}"); }
            return list;
        }

        private static SoundCloudPlaylist ParsePlaylist(JsonElement item)
        {
            var pl = new SoundCloudPlaylist();
            if (item.TryGetProperty("id", out var id)) pl.Id = id.GetInt64();
            if (item.TryGetProperty("title", out var t)) pl.Title = t.GetString();
            pl.Tracks = new List<SoundCloudTrack>();
            if (item.TryGetProperty("tracks", out var tracks) && tracks.ValueKind == JsonValueKind.Array)
            {
                foreach (var tr in tracks.EnumerateArray())
                {
                    if (tr.ValueKind != JsonValueKind.Object) continue;
                    try { pl.Tracks.Add(ParseTrackFromJson(tr)); }
                    catch { /* skip malformed track */ }
                }
            }
            return pl;
        }

        /// <summary>Fetch tracks from v2 search/tracks response. Uses provided token (user or client). On 403 clears client token.</summary>
        private async Task<List<SoundCloudTrack>> FetchTracksFromV2Async(string url, string token, Action<string> log)
        {
            var list = new List<SoundCloudTrack>();
            if (string.IsNullOrEmpty(token)) { log?.Invoke("Search: no token"); return list; }
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Add("Accept", "application/json");
                req.Headers.Add("Authorization", "OAuth " + token);
                req.Headers.TryAddWithoutValidation("User-Agent", "MarySGameEngine/1.0");
                var response = await HttpClient.SendAsync(req);
                var json = await response.Content.ReadAsStringAsync();
                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    _clientAccessToken = null;
                if (!response.IsSuccessStatusCode) { log?.Invoke($"Search error: {response.StatusCode}"); return list; }
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in root.EnumerateArray())
                    {
                        try { if (item.ValueKind == JsonValueKind.Object) list.Add(ParseTrackFromJson(item)); }
                        catch { /* skip */ }
                    }
                    return list;
                }
                if (root.TryGetProperty("collection", out var coll))
                {
                    foreach (var item in coll.EnumerateArray())
                    {
                        try { if (item.ValueKind == JsonValueKind.Object) list.Add(ParseTrackFromJson(item)); }
                        catch { /* skip */ }
                    }
                }
            }
            catch (Exception ex) { log?.Invoke($"Search failed: {ex.Message}"); }
            return list;
        }

        /// <summary>Fetch tracks from v2 charts response. Uses provided token (user or client). On 403 clears client token.</summary>
        private async Task<List<SoundCloudTrack>> FetchChartTracksFromV2Async(string url, string token, Action<string> log)
        {
            var list = new List<SoundCloudTrack>();
            if (string.IsNullOrEmpty(token)) { log?.Invoke("Charts: no token"); return list; }
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Add("Accept", "application/json");
                req.Headers.Add("Authorization", "OAuth " + token);
                req.Headers.TryAddWithoutValidation("User-Agent", "MarySGameEngine/1.0");
                var response = await HttpClient.SendAsync(req);
                var json = await response.Content.ReadAsStringAsync();
                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    _clientAccessToken = null;
                if (!response.IsSuccessStatusCode) { log?.Invoke($"Charts error: {response.StatusCode}"); return list; }
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("collection", out var coll))
                {
                    foreach (var item in coll.EnumerateArray())
                    {
                        try
                        {
                            if (item.TryGetProperty("track", out var trackEl))
                                list.Add(ParseTrackFromJson(trackEl));
                            else if (item.ValueKind == JsonValueKind.Object)
                                list.Add(ParseTrackFromJson(item));
                        }
                        catch { /* skip */ }
                    }
                }
            }
            catch (Exception ex) { log?.Invoke($"Charts failed: {ex.Message}"); }
            return list;
        }

        private static SoundCloudTrack ParseTrackFromJson(JsonElement el)
        {
            var t = new SoundCloudTrack();
            if (el.TryGetProperty("id", out var id)) t.Id = id.GetInt64();
            if (el.TryGetProperty("title", out var title)) t.Title = title.GetString();
            if (el.TryGetProperty("duration", out var d)) t.DurationMs = d.GetInt64();
            else if (el.TryGetProperty("full_duration", out var fd)) t.DurationMs = fd.GetInt64();
            if (el.TryGetProperty("stream_url", out var su)) t.StreamUrl = su.GetString();
            if (el.TryGetProperty("permalink_url", out var pu)) t.PermalinkUrl = pu.GetString();
            if (el.TryGetProperty("user", out var userEl))
            {
                t.User = new SoundCloudUser();
                if (userEl.TryGetProperty("username", out var un)) t.User.Username = un.GetString();
            }
            return t;
        }

        /// <summary>Try to get a playable stream URL from the /tracks/:id/stream or /streams API. Returns null on failure.</summary>
        private async Task<string> ResolveStreamUrlFromStreamsApiAsync(long trackId, string token, Action<string> log = null)
        {
            foreach (var pathSuffix in new[] { "stream", "streams" })
            {
                try
                {
                    var url = $"{ApiBase}/tracks/{trackId}/{pathSuffix}";
                    foreach (var authHeader in new[] { "OAuth ", "Bearer " })
                    {
                        using var req = new HttpRequestMessage(HttpMethod.Get, url);
                        req.Headers.Add("Authorization", authHeader + token);
                        req.Headers.Add("Accept", "application/json");
                        var response = await HttpClient.SendAsync(req);
                        var json = await response.Content.ReadAsStringAsync();
                        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized) continue;
                        if (!response.IsSuccessStatusCode) { log?.Invoke($"Stream(s) API error: {response.StatusCode}"); break; }
                        try
                        {
                            using var doc = JsonDocument.Parse(json);
                            var root = doc.RootElement;
                            if (root.ValueKind == JsonValueKind.Object)
                            {
                                foreach (var prop in new[] { "http_mp3_128_url", "url", "hls_mp3_128_url", "preview_mp3_128_url" })
                                {
                                    if (root.TryGetProperty(prop, out var urlEl))
                                    {
                                        var u = urlEl.GetString();
                                        if (!string.IsNullOrEmpty(u)) return u;
                                    }
                                }
                            }
                            else if (root.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var item in root.EnumerateArray())
                                {
                                    if (item.TryGetProperty("url", out var urlEl))
                                    {
                                        var u = urlEl.GetString();
                                        if (!string.IsNullOrEmpty(u)) return u;
                                    }
                                }
                            }
                        }
                        catch { /* ignore parse errors */ }
                    }
                }
                catch (Exception ex) { log?.Invoke($"Stream(s) API ({pathSuffix}): {ex.Message}"); }
            }
            return null;
        }

        /// <summary>Resolve stream URL for a track and download to a temp file. Never throws.</summary>
        public async Task<string> DownloadTrackToTempFileAsync(SoundCloudTrack track, Action<string> log = null)
        {
            try
            {
                if (track == null) { log?.Invoke("No track."); return null; }
                SoundCloudTrack resolved = track;
                if (string.IsNullOrEmpty(track.StreamUrl))
                {
                    resolved = await GetTrackByIdAsync(track.Id, log);
                    if (resolved == null) { log?.Invoke("Could not resolve track."); return null; }
                }

                string token = null;
                lock (_tokenLock) { token = _userAccessToken; }
                if (!string.IsNullOrEmpty(token) && _tokenExpiry <= DateTime.UtcNow.AddMinutes(2))
                    await RefreshUserTokenAsync(log);
                lock (_tokenLock) { token = _userAccessToken; }
                if (string.IsNullOrEmpty(token))
                {
                    if (_clientTokenExpiry <= DateTime.UtcNow.AddMinutes(2))
                        await RefreshClientCredentialsTokenAsync(log);
                    token = _clientAccessToken;
                }
                if (string.IsNullOrEmpty(token)) { log?.Invoke("Not authorized. Sign in and try again."); return null; }

                string streamUrl = resolved.StreamUrl;
                if (string.IsNullOrEmpty(streamUrl)) { log?.Invoke("Track has no stream URL."); return null; }
                if (!streamUrl.Contains("?")) streamUrl += "?";
                else streamUrl += "&";
                streamUrl += "client_id=" + Uri.EscapeDataString(SoundCloudConfig.ClientId);

                string tempPath = Path.Combine(Path.GetTempPath(), "MarySGameEngine", $"sc_{resolved.Id}.mp3");
                var dir = Path.GetDirectoryName(tempPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                string mediaUrl = null;
                foreach (var authHeader in new[] { "OAuth ", "Bearer " })
                {
                    using (var req = new HttpRequestMessage(HttpMethod.Get, streamUrl))
                    {
                        req.Headers.Add("Authorization", authHeader + token);
                        req.Headers.Add("Accept", "*/*");
                        var response = await HttpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
                        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                            continue;
                        if (!response.IsSuccessStatusCode) { log?.Invoke($"Stream API error: {response.StatusCode}"); continue; }

                        var contentType = response.Content?.Headers?.ContentType?.MediaType ?? "";
                        bool useBodyAsMedia = contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)
                            || contentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase)
                            || string.IsNullOrWhiteSpace(contentType);

                        if (useBodyAsMedia)
                        {
                            await using var fileStream = File.Create(tempPath);
                            await response.Content.CopyToAsync(fileStream);
                            return tempPath;
                        }

                        var json = await response.Content.ReadAsStringAsync();
                        try
                        {
                            using var doc = JsonDocument.Parse(json);
                            var root = doc.RootElement;
                            if (root.ValueKind == JsonValueKind.Object)
                            {
                                foreach (var prop in new[] { "url", "http_mp3_128_url", "hls_mp3_128_url", "preview_mp3_128_url" })
                                {
                                    if (root.TryGetProperty(prop, out var urlEl))
                                    {
                                        mediaUrl = urlEl.GetString();
                                        break;
                                    }
                                }
                            }
                            if (string.IsNullOrEmpty(mediaUrl) && response.RequestMessage?.RequestUri != null)
                                mediaUrl = response.RequestMessage.RequestUri.ToString();
                        }
                        catch
                        {
                            if (response.RequestMessage?.RequestUri != null)
                                mediaUrl = response.RequestMessage.RequestUri.ToString();
                        }
                        if (string.IsNullOrEmpty(mediaUrl))
                            log?.Invoke($"Stream response: Status={response.StatusCode}, Content-Type={contentType}, BodyLength={response.Content?.Headers?.ContentLength?.ToString() ?? "?"}");
                    }
                    if (!string.IsNullOrEmpty(mediaUrl)) break;
                }

                if (string.IsNullOrEmpty(mediaUrl))
                {
                    using (var req = new HttpRequestMessage(HttpMethod.Get, streamUrl))
                    {
                        req.Headers.Add("Accept", "*/*");
                        var response = await HttpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
                        if (response.IsSuccessStatusCode)
                        {
                            var contentType = response.Content?.Headers?.ContentType?.MediaType ?? "";
                            bool useBodyAsMedia = contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)
                                || contentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase)
                                || string.IsNullOrWhiteSpace(contentType);
                            if (useBodyAsMedia)
                            {
                                await using var fileStream = File.Create(tempPath);
                                await response.Content.CopyToAsync(fileStream);
                                return tempPath;
                            }
                            var json = await response.Content.ReadAsStringAsync();
                            try
                            {
                                using var doc = JsonDocument.Parse(json);
                                var root = doc.RootElement;
                                if (root.ValueKind == JsonValueKind.Object)
                                    foreach (var prop in new[] { "url", "http_mp3_128_url", "hls_mp3_128_url", "preview_mp3_128_url" })
                                    {
                                        if (root.TryGetProperty(prop, out var urlEl))
                                        {
                                            mediaUrl = urlEl.GetString();
                                            break;
                                        }
                                    }
                            }
                            catch { /* ignore */ }
                        }
                    }
                }

                if (string.IsNullOrEmpty(mediaUrl))
                {
                    mediaUrl = await ResolveStreamUrlFromStreamsApiAsync(resolved.Id, token, log);
                    if (!string.IsNullOrEmpty(mediaUrl)) log?.Invoke("Resolved stream URL via /tracks/{id}/streams.");
                }

                if (string.IsNullOrEmpty(mediaUrl)) { log?.Invoke("Could not get stream URL."); return null; }

                using (var req = new HttpRequestMessage(HttpMethod.Get, mediaUrl))
                {
                    req.Headers.Add("Accept", "*/*");
                    req.Headers.Add("Authorization", "OAuth " + token);
                    req.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                    var response = await HttpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
                    if (response.StatusCode == (System.Net.HttpStatusCode)429)
                    {
                        log?.Invoke("Download failed: Too many requests (429). Please wait a few minutes and try again.");
                        return null;
                    }
                    if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        log?.Invoke("Download failed: 403 Forbidden. Try signing in with SoundCloud (Settings → Sign in with SoundCloud).");
                        return null;
                    }
                    response.EnsureSuccessStatusCode();
                    await using var fileStream = File.Create(tempPath);
                    await response.Content.CopyToAsync(fileStream);
                }
                return tempPath;
            }
            catch (Exception ex) { log?.Invoke($"Download failed: {ex.Message}"); return null; }
        }

        /// <summary>Open a stream for the track so playback can start immediately while data is read. Caller must dispose the returned keepAlive when done playing.</summary>
        public async Task<(Stream stream, IDisposable keepAlive)> OpenTrackStreamAsync(SoundCloudTrack track, Action<string> log = null)
        {
            try
            {
                if (track == null) { log?.Invoke("No track."); return (null, null); }
                SoundCloudTrack resolved = track;
                if (string.IsNullOrEmpty(track.StreamUrl))
                {
                    resolved = await GetTrackByIdAsync(track.Id, log);
                    if (resolved == null) { log?.Invoke("Could not resolve track."); return (null, null); }
                }

                string token = null;
                lock (_tokenLock) { token = _userAccessToken; }
                if (!string.IsNullOrEmpty(token) && _tokenExpiry <= DateTime.UtcNow.AddMinutes(2))
                    await RefreshUserTokenAsync(log);
                lock (_tokenLock) { token = _userAccessToken; }
                if (string.IsNullOrEmpty(token))
                {
                    if (_clientTokenExpiry <= DateTime.UtcNow.AddMinutes(2))
                        await RefreshClientCredentialsTokenAsync(log);
                    token = _clientAccessToken;
                }
                if (string.IsNullOrEmpty(token)) { log?.Invoke("Not authorized. Sign in and try again."); return (null, null); }

                string streamUrl = resolved.StreamUrl;
                if (string.IsNullOrEmpty(streamUrl)) { log?.Invoke("Track has no stream URL."); return (null, null); }
                if (!streamUrl.Contains("?")) streamUrl += "?";
                else streamUrl += "&";
                streamUrl += "client_id=" + Uri.EscapeDataString(SoundCloudConfig.ClientId);

                string mediaUrl = null;
                foreach (var authHeader in new[] { "OAuth ", "Bearer " })
                {
                    using (var req = new HttpRequestMessage(HttpMethod.Get, streamUrl))
                    {
                        req.Headers.Add("Authorization", authHeader + token);
                        req.Headers.Add("Accept", "*/*");
                        var response = await HttpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
                        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                            continue;
                        if (!response.IsSuccessStatusCode) { log?.Invoke($"Stream API error: {response.StatusCode}"); continue; }

                        var contentType = response.Content?.Headers?.ContentType?.MediaType ?? "";
                        bool useBodyAsStream = contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)
                            || contentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase)
                            || string.IsNullOrWhiteSpace(contentType);

                        if (useBodyAsStream)
                        {
                            Stream bodyStream = await response.Content.ReadAsStreamAsync();
                            return (new ReadFullyStream(bodyStream), response);
                        }

                        var json = await response.Content.ReadAsStringAsync();
                        try
                        {
                            using var doc = JsonDocument.Parse(json);
                            var root = doc.RootElement;
                            if (root.ValueKind == JsonValueKind.Object)
                            {
                                foreach (var prop in new[] { "url", "http_mp3_128_url", "hls_mp3_128_url", "preview_mp3_128_url" })
                                {
                                    if (root.TryGetProperty(prop, out var urlEl))
                                    {
                                        mediaUrl = urlEl.GetString();
                                        break;
                                    }
                                }
                            }
                            if (string.IsNullOrEmpty(mediaUrl) && response.RequestMessage?.RequestUri != null)
                                mediaUrl = response.RequestMessage.RequestUri.ToString();
                        }
                        catch
                        {
                            if (response.RequestMessage?.RequestUri != null)
                                mediaUrl = response.RequestMessage.RequestUri.ToString();
                        }
                    }
                    if (!string.IsNullOrEmpty(mediaUrl)) break;
                }

                if (string.IsNullOrEmpty(mediaUrl))
                {
                    using (var req = new HttpRequestMessage(HttpMethod.Get, streamUrl))
                    {
                        req.Headers.Add("Accept", "*/*");
                        var response = await HttpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
                        if (response.IsSuccessStatusCode)
                        {
                            var contentType = response.Content?.Headers?.ContentType?.MediaType ?? "";
                            bool useBodyAsStream = contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)
                                || contentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase)
                                || string.IsNullOrWhiteSpace(contentType);
                            if (useBodyAsStream)
                            {
                                Stream bodyStream = await response.Content.ReadAsStreamAsync();
                                return (new ReadFullyStream(bodyStream), response);
                            }
                            var json = await response.Content.ReadAsStringAsync();
                            try
                            {
                                using var doc = JsonDocument.Parse(json);
                                var root = doc.RootElement;
                                if (root.ValueKind == JsonValueKind.Object)
                                    foreach (var prop in new[] { "url", "http_mp3_128_url", "hls_mp3_128_url", "preview_mp3_128_url" })
                                    {
                                        if (root.TryGetProperty(prop, out var urlEl))
                                        {
                                            mediaUrl = urlEl.GetString();
                                            break;
                                        }
                                    }
                            }
                            catch { /* ignore */ }
                        }
                    }
                }

                if (string.IsNullOrEmpty(mediaUrl))
                {
                    mediaUrl = await ResolveStreamUrlFromStreamsApiAsync(resolved.Id, token, log);
                    if (!string.IsNullOrEmpty(mediaUrl)) log?.Invoke("Resolved stream URL via /tracks/{id}/streams.");
                }

                if (string.IsNullOrEmpty(mediaUrl)) { log?.Invoke("Could not get stream URL."); return (null, null); }

                var httpReq = new HttpRequestMessage(HttpMethod.Get, mediaUrl);
                httpReq.Headers.Add("Accept", "*/*");
                httpReq.Headers.Add("Authorization", "OAuth " + token);
                httpReq.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                var httpResponse = await HttpClient.SendAsync(httpReq, HttpCompletionOption.ResponseHeadersRead);
                if (httpResponse.StatusCode == (System.Net.HttpStatusCode)429)
                {
                    log?.Invoke("Stream failed: Too many requests (429). Please wait a few minutes and try again.");
                    return (null, null);
                }
                if (httpResponse.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    log?.Invoke("Stream failed: 403 Forbidden. Try signing in with SoundCloud (Settings → Sign in with SoundCloud).");
                    return (null, null);
                }
                httpResponse.EnsureSuccessStatusCode();
                Stream mediaStream = await httpResponse.Content.ReadAsStreamAsync();
                Stream readFully = new ReadFullyStream(mediaStream);
                return (readFully, httpResponse);
            }
            catch (Exception ex) { log?.Invoke($"Stream open failed: {ex.Message}"); return (null, null); }
        }

        public async Task<SoundCloudTrack> GetTrackByIdAsync(long trackId, Action<string> log = null)
        {
            string token = null;
            lock (_tokenLock) { token = _userAccessToken; }
            if (string.IsNullOrEmpty(token)) await RefreshClientCredentialsTokenAsync(log);
            if (string.IsNullOrEmpty(token)) lock (_tokenLock) { token = _userAccessToken; }
            if (string.IsNullOrEmpty(token)) token = _clientAccessToken;
            if (string.IsNullOrEmpty(token)) { log?.Invoke("Not authorized."); return null; }
            var url = $"{ApiBase}/tracks/{trackId}";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("Authorization", "OAuth " + token);
            req.Headers.Add("Accept", "application/json; charset=utf-8");
            var response = await HttpClient.SendAsync(req);
            var json = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) { log?.Invoke($"Track error: {response.StatusCode}"); return null; }
            try
            {
                using var doc = JsonDocument.Parse(json);
                return ParseTrackFromJson(doc.RootElement);
            }
            catch { return null; }
        }

        private async Task RefreshClientCredentialsTokenAsync(Action<string> log = null)
        {
            var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{SoundCloudConfig.ClientId}:{SoundCloudConfig.ClientSecret}"));
            using var req = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint);
            req.Headers.Add("Authorization", "Basic " + auth);
            req.Content = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("grant_type", "client_credentials") });
            try
            {
                var response = await HttpClient.SendAsync(req);
                var json = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode) { log?.Invoke($"Client token error: {response.StatusCode}"); return; }
                var token = JsonSerializer.Deserialize<SoundCloudTokenResponse>(json);
                if (token != null)
                {
                    _clientAccessToken = token.AccessToken;
                    _clientTokenExpiry = DateTime.UtcNow.AddSeconds(token.ExpiresIn > 0 ? token.ExpiresIn : 3600);
                }
            }
            catch (Exception ex) { log?.Invoke($"Client token failed: {ex.Message}"); }
        }

        private static string GenerateCodeVerifier()
        {
            var bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            return Base64UrlEncode(bytes);
        }

        private static string ComputeCodeChallenge(string verifier)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(verifier));
            return Base64UrlEncode(hash);
        }

        private static string Base64UrlEncode(byte[] data)
        {
            return Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private void LoadStoredTokens()
        {
            try
            {
                if (!File.Exists(_tokenFilePath)) return;
                var json = File.ReadAllText(_tokenFilePath);
                var o = JsonSerializer.Deserialize<JsonElement>(json);
                lock (_tokenLock)
                {
                    _userAccessToken = o.TryGetProperty("access_token", out var t) ? t.GetString() : null;
                    _userRefreshToken = o.TryGetProperty("refresh_token", out var r) ? r.GetString() : null;
                    if (o.TryGetProperty("expires_at", out var e) && e.TryGetDateTime(out var dt))
                        _tokenExpiry = dt;
                }
            }
            catch { /* ignore */ }
        }

        private void SaveTokens()
        {
            try
            {
                var dir = Path.GetDirectoryName(_tokenFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var o = new Dictionary<string, object>
                {
                    ["access_token"] = _userAccessToken,
                    ["refresh_token"] = _userRefreshToken,
                    ["expires_at"] = _tokenExpiry
                };
                File.WriteAllText(_tokenFilePath, JsonSerializer.Serialize(o));
            }
            catch { /* ignore */ }
        }
    }
}
