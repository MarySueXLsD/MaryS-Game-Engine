using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MarySGameEngine.Modules.Music_essential.SoundCloud
{
    public class SoundCloudTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; }

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("scope")]
        public string Scope { get; set; }
    }

    public class SoundCloudTrack
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("duration")]
        public long DurationMs { get; set; }

        [JsonPropertyName("stream_url")]
        public string StreamUrl { get; set; }

        [JsonPropertyName("permalink_url")]
        public string PermalinkUrl { get; set; }

        [JsonPropertyName("user")]
        public SoundCloudUser User { get; set; }
    }

    public class SoundCloudUser
    {
        [JsonPropertyName("username")]
        public string Username { get; set; }
    }

    public class SoundCloudPlaylist
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("tracks")]
        public List<SoundCloudTrack> Tracks { get; set; }
    }

    /// <summary>Response from /tracks/:id/stream - contains transcodings with URLs.</summary>
    public class SoundCloudStreamResponse
    {
        [JsonPropertyName("url")]
        public string Url { get; set; }
    }
}
