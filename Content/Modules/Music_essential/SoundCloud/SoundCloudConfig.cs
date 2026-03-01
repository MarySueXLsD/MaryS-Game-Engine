namespace MarySGameEngine.Modules.Music_essential.SoundCloud
{
    /// <summary>
    /// SoundCloud API credentials. For production, consider loading from environment variables or a secret config file.
    /// Add redirect URI in Your Apps: http://127.0.0.1:8765/callback
    /// </summary>
    public static class SoundCloudConfig
    {
        public const string ClientId = "fBGxEiSpcDwHvBRft7ql60GLezuzGQgs";
        public const string ClientSecret = "Nz9eSCG3w2ki1cWZfhX9sfIT049NaKNY";
        public const string RedirectUri = "http://127.0.0.1:8765/callback";
        public const int CallbackPort = 8765;
    }
}
