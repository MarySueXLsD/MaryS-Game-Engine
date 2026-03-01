using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Content;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MarySGameEngine.Modules.Music_essential.Audio;
using MarySGameEngine.Modules.Music_essential.SoundCloud;
using MarySGameEngine.Modules.WindowManagement_essential;
using MarySGameEngine.Modules.TaskBar_essential;
using MarySGameEngine;

namespace MarySGameEngine.Modules.Music_essential
{
    public class Music : IModule
    {
        private const int TitleBarHeight = 40;
        private const int ToolbarHeight = 72; // two rows: title + time, then centered buttons
        private const float BarSmoothFactor = 0.35f; // lerp toward target height (0=no move, 1=instant)
        private const int FftLength = 8192;
        private const float WaveformScale = 120f;
        private const float WaveformMin = -1f;
        private const float WaveformMax = 1f;
        private const int SpectrumBarCount = 128; // many bars across full width
        private const float BarMagnitudeScale = 2500f; // scale FFT magnitude so bars fill viz height

        private WindowManagement _windowManagement;
        private GraphicsDevice _graphicsDevice;
        private SpriteFont _menuFont;
        private SpriteFont _uiFont; // larger font for title, time, buttons
        private int _windowWidth;
        private TaskBar _taskBar;
        private GameEngine _engine;
        private Texture2D _pixel;
        private Texture2D _circleTexture;
        private Texture2D _playIcon;
        private Texture2D _pauseIcon;
        private Texture2D _nextIcon;
        private ContentManager _content;
        private MouseState _currentMouseState;
        private MouseState _previousMouseState;

        private AudioPlayback _audioPlayback;
        private AudioAnalyzer _audioAnalyzer;
        private BasicEffect _basicEffect;
        private VertexPositionColor[] _waveformVertices1;
        private VertexPositionColor[] _waveformVertices2;

        private Rectangle _contentBounds;
        private Rectangle _toolbarBounds;
        private Rectangle _vizBounds;
        private Rectangle _openButtonBounds;
        private Rectangle _playPauseButtonBounds;
        private Rectangle _stopButtonBounds;
        private Rectangle _nextButtonLeftBounds;
        private Rectangle _nextButtonRightBounds;
        private Rectangle _progressBarBounds;
        private Rectangle _progressFillBounds;
        private Rectangle _volumeTrackBounds;

        private bool _isDraggingVolume;

        private float[] _smoothedSpectrumHeights; // length SpectrumBarCount
        private float[] _smoothedTimeDomainHeights; // length 64

        private string _currentFilePath;
        private bool _hasAudioData;
        private readonly object _pendingPathLock = new object();
        private string _pendingFilePath; // Set by STA thread after dialog; consumed in Update
        private string _currentTrackTitle;
        private SoundCloudApiClient _soundCloudClient;
        private bool _soundCloudPickerVisible;
        private List<SoundCloudTrack> _soundCloudTracks;
        private string _soundCloudMessage;
        private bool _soundCloudLoading;
        private long? _soundCloudDownloadingTrackId;
        private int _soundCloudScrollOffset;
        private const int TrackRowHeight = 32;
        private const int PickerPadding = 12;
        private const int SCROLLBAR_WIDTH = 16;
        private const int SCROLLBAR_PADDING = 2;
        private int _soundCloudListContentHeight;
        private bool _soundCloudNeedsScrollbar;
        private Rectangle _soundCloudScrollbarBounds;
        private Rectangle _soundCloudListBounds;
        private bool _soundCloudIsDraggingScrollbar;
        private Vector2 _soundCloudScrollbarDragStart;
        private int _soundCloudScrollOffsetAtDragStart;
        private Task _soundCloudTask;
        private string _pendingSoundCloudPath;
        private Task<(Stream stream, IDisposable owner)> _soundCloudStreamTask;
        private Task<string> _soundCloudDownloadTask;
        private SoundCloudTrack _soundCloudPendingTrack;
        private int _soundCloudSection;
        private List<SoundCloudPlaylist> _soundCloudPlaylists;
        private SoundCloudPlaylist _soundCloudSelectedPlaylist;
        private string _soundCloudSearchQuery;
        private Task<List<SoundCloudPlaylist>> _soundCloudPlaylistsTask;
        private string _soundCloudLastDownloadError; // set by download log callback for user-facing message

        private bool _wasWindowVisible; // used to detect close so we can stop playback

        private void SoundCloudDownloadLog(string msg)
        {
            _engine?.Log(msg);
            if (msg == null) return;
            if (msg.Contains("429") || msg.Contains("Too many requests"))
                _soundCloudLastDownloadError = "Too many requests. Wait a few minutes and try again.";
            else if (msg.Contains("403") || msg.Contains("Forbidden"))
                _soundCloudLastDownloadError = "Could not load track. Sign in with SoundCloud and try again.";
        }

        public Music(GraphicsDevice graphicsDevice, SpriteFont menuFont, int windowWidth)
        {
            _graphicsDevice = graphicsDevice;
            _menuFont = menuFont;
            _windowWidth = windowWidth;
            _engine = (GameEngine)GameEngine.Instance;
            _pixel = new Texture2D(graphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
            _circleTexture = CreateCircleTexture(graphicsDevice, 64);

            var windowProperties = new WindowProperties
            {
                IsVisible = false,
                IsMovable = true,
                IsResizable = true
            };
            _windowManagement = new WindowManagement(graphicsDevice, menuFont, windowWidth, windowProperties);
            _windowManagement.SetWindowTitle("Music");
            _windowManagement.SetDefaultSize(700, 400);
            _windowManagement.SetCustomMinimumSize(400, 250);
            _windowManagement.SetPosition(new Vector2(120, 80));

            _audioPlayback = new AudioPlayback(FftLength);
            _audioAnalyzer = new AudioAnalyzer(_audioPlayback, 64, 0.25f); // 64 level samples for many time-domain bars
            _basicEffect = new BasicEffect(graphicsDevice) { VertexColorEnabled = true, World = Matrix.Identity };
            _soundCloudClient = new SoundCloudApiClient();
            _soundCloudTracks = new List<SoundCloudTrack>();
            _soundCloudPlaylists = new List<SoundCloudPlaylist>();
        }

        public void SetTaskBar(TaskBar taskBar)
        {
            _taskBar = taskBar;
            _windowManagement?.SetTaskBar(taskBar);
        }

        private void UpdateBounds()
        {
            if (_windowManagement == null || !_windowManagement.IsVisible()) return;
            var windowBounds = _windowManagement.GetWindowBounds();
            _contentBounds = new Rectangle(windowBounds.X, windowBounds.Y + TitleBarHeight, windowBounds.Width, windowBounds.Height - TitleBarHeight);
            _toolbarBounds = new Rectangle(_contentBounds.X, _contentBounds.Y + _contentBounds.Height - ToolbarHeight, _contentBounds.Width, ToolbarHeight);
            const int progressBarHeight = 10;
            int progressBarY = _toolbarBounds.Y - progressBarHeight - 4;
            _progressBarBounds = new Rectangle(_contentBounds.X + 8, progressBarY, _contentBounds.Width - 16, progressBarHeight);
            _vizBounds = new Rectangle(_contentBounds.X, _contentBounds.Y + 4, _contentBounds.Width, progressBarY - _contentBounds.Y - 4);

            // Buttons centered on second row of toolbar (at bottom), moved up a bit
            const int btnSize = 56;
            const int pad = 10;
            int buttonRowY = _toolbarBounds.Y + 20;
            int totalButtonsWidth = btnSize * 5 + pad * 4;
            int startX = _toolbarBounds.X + Math.Max(0, (_toolbarBounds.Width - totalButtonsWidth) / 2);

            _nextButtonLeftBounds = new Rectangle(startX, buttonRowY, btnSize, btnSize);
            _playPauseButtonBounds = new Rectangle(_nextButtonLeftBounds.Right + pad, buttonRowY, btnSize, btnSize);
            _nextButtonRightBounds = new Rectangle(_playPauseButtonBounds.Right + pad, buttonRowY, btnSize, btnSize);
            _openButtonBounds = new Rectangle(_nextButtonRightBounds.Right + pad, buttonRowY, btnSize, btnSize);
            _stopButtonBounds = new Rectangle(_openButtonBounds.Right + pad, buttonRowY, btnSize, btnSize);

            const int volumeTrackWidth = 80;
            const int volumeTrackHeight = 8;
            int volumeTrackX = _toolbarBounds.Right - 12 - volumeTrackWidth - 42; // leave ~42px for "100%" text
            _volumeTrackBounds = new Rectangle(volumeTrackX, buttonRowY + (btnSize - volumeTrackHeight) / 2, volumeTrackWidth, volumeTrackHeight);
        }

        public void Update()
        {
            _currentMouseState = Mouse.GetState();
            if (_windowManagement != null)
                _windowManagement.Update();

            // When the module window is closed, stop playback
            if (_wasWindowVisible && _windowManagement != null && !_windowManagement.IsVisible())
                _audioPlayback?.Stop();
            _wasWindowVisible = _windowManagement?.IsVisible() ?? false;

            UpdateBounds();

            // Apply any file selected by the Open dialog (run on STA thread)
            string pathToLoad = null;
            lock (_pendingPathLock)
            {
                if (_pendingFilePath != null)
                {
                    pathToLoad = _pendingFilePath;
                    _pendingFilePath = null;
                }
            }
            if (pathToLoad != null)
            {
                try
                {
                    _currentFilePath = pathToLoad;
                    _currentTrackTitle = null;
                    _audioPlayback?.Load(pathToLoad);
                    _audioPlayback?.Play();
                }
                catch (Exception ex)
                {
                    _engine?.Log($"Music: Load error: {ex.Message}");
                }
            }

            if (_pendingSoundCloudPath != null)
            {
                pathToLoad = _pendingSoundCloudPath;
                _pendingSoundCloudPath = null;
                _soundCloudPickerVisible = false;
                _soundCloudDownloadingTrackId = null;
                try
                {
                    _currentFilePath = pathToLoad;
                    _audioPlayback?.Load(pathToLoad);
                    _audioPlayback?.Play();
                }
                catch (Exception ex) { _engine?.Log($"Music: Load error: {ex.Message}"); }
            }

            if (_soundCloudTask != null && _soundCloudTask.IsCompleted)
            {
                try
                {
                    _soundCloudLoading = false;
                    if (_soundCloudTask.IsFaulted)
                        _soundCloudMessage = _soundCloudTask.Exception?.GetBaseException()?.Message ?? "Error";
                    else if (_soundCloudTask is Task<List<SoundCloudTrack>> listTask && listTask.IsCompletedSuccessfully)
                    {
                        _soundCloudTracks = listTask.Result ?? new List<SoundCloudTrack>();
                        _soundCloudMessage = null;
                    }
                }
                catch (Exception ex) { _soundCloudMessage = ex.Message; _engine?.Log($"Music: task error: {ex.Message}"); }
                _soundCloudTask = null;
            }
            if (_soundCloudStreamTask != null && _soundCloudStreamTask.IsCompleted)
            {
                try
                {
                    if (_soundCloudStreamTask.IsCompletedSuccessfully)
                    {
                        var (stream, owner) = _soundCloudStreamTask.Result;
                        if (stream != null && owner != null)
                        {
                            bool played = false;
                            try
                            {
                                _audioPlayback.Stop();
                                _audioPlayback.Load(stream, owner);
                                _audioPlayback.Play();
                                played = true;
                            }
                            catch (Exception loadEx)
                            {
                                _engine?.Log($"Music: stream play failed: {loadEx.Message}, falling back to download.");
                                owner?.Dispose();
                                if (_soundCloudPendingTrack != null && _soundCloudDownloadTask == null)
                                    _soundCloudDownloadTask = _soundCloudClient.DownloadTrackToTempFileAsync(_soundCloudPendingTrack, SoundCloudDownloadLog);
                            }
                            if (played)
                            {
                                _currentTrackTitle = SanitizeForFont(_soundCloudPendingTrack?.Title ?? "Untitled");
                                _soundCloudDownloadingTrackId = null;
                                _soundCloudPendingTrack = null;
                                _currentFilePath = null;
                            }
                        }
                        else
                        {
                            _engine?.Log("Music: SoundCloud stream returned no stream, falling back to download.");
                            if (_soundCloudPendingTrack != null && _soundCloudDownloadTask == null)
                                _soundCloudDownloadTask = _soundCloudClient.DownloadTrackToTempFileAsync(_soundCloudPendingTrack, SoundCloudDownloadLog);
                        }
                    }
                    else if (_soundCloudStreamTask.IsFaulted)
                    {
                        _engine?.Log($"Music: SoundCloud stream failed: {_soundCloudStreamTask.Exception?.GetBaseException()?.Message ?? "Unknown"}, falling back to download.");
                        if (_soundCloudPendingTrack != null && _soundCloudDownloadTask == null)
                            _soundCloudDownloadTask = _soundCloudClient.DownloadTrackToTempFileAsync(_soundCloudPendingTrack, SoundCloudDownloadLog);
                    }
                }
                catch (Exception ex) { _engine?.Log($"Music: stream task error: {ex.Message}"); }
                _soundCloudStreamTask = null;
            }
            if (_soundCloudDownloadTask != null && _soundCloudDownloadTask.IsCompleted)
            {
                try
                {
                    if (_soundCloudDownloadTask.IsCompletedSuccessfully && _soundCloudDownloadTask.Result != null)
                    {
                        string path = _soundCloudDownloadTask.Result;
                        _engine?.Log($"Music: Download completed, path={path}, FileExists={System.IO.File.Exists(path)}");
                        _currentFilePath = path;
                        bool played = false;
                        try
                        {
                            _audioPlayback.Stop();
                            _audioPlayback.Load(path);
                            _engine?.Log("Music: Load(path) succeeded.");
                            _audioPlayback.Play();
                            played = true;
                            _engine?.Log($"Music: Play() after download, state={_audioPlayback.PlaybackState}");
                        }
                        catch (Exception loadEx) { _engine?.Log($"Music: play failed after download: {loadEx.Message}"); }
                        if (played)
                        {
                            _currentTrackTitle = SanitizeForFont(_soundCloudPendingTrack?.Title ?? "Untitled");
                            _soundCloudDownloadingTrackId = null;
                            _soundCloudPendingTrack = null;
                        }
                        else
                            _soundCloudDownloadingTrackId = null;
                    }
                    else
                    {
                        _engine?.Log($"Music: Download task failed or null result. Success={_soundCloudDownloadTask.IsCompletedSuccessfully}, Result={(_soundCloudDownloadTask.IsCompletedSuccessfully ? _soundCloudDownloadTask.Result ?? "(null)" : "n/a")}");
                        _soundCloudMessage = !string.IsNullOrEmpty(_soundCloudLastDownloadError)
                            ? _soundCloudLastDownloadError
                            : (_soundCloudClient.HasUserToken
                                ? "Could not load track. Try again."
                                : "Could not load track. Sign in with SoundCloud and try again.");
                        _soundCloudDownloadingTrackId = null;
                    }
                }
                catch (Exception ex) { _engine?.Log($"Music: download task error: {ex.Message}"); _soundCloudDownloadingTrackId = null; }
                _soundCloudDownloadTask = null;
            }
            if (_soundCloudPlaylistsTask != null && _soundCloudPlaylistsTask.IsCompleted)
            {
                try
                {
                    _soundCloudLoading = false;
                    if (_soundCloudPlaylistsTask.IsFaulted)
                        _soundCloudMessage = _soundCloudPlaylistsTask.Exception?.GetBaseException()?.Message ?? "Failed to load playlists";
                    else if (_soundCloudPlaylistsTask.IsCompletedSuccessfully)
                        _soundCloudPlaylists = _soundCloudPlaylistsTask.Result ?? new List<SoundCloudPlaylist>();
                }
                catch (Exception ex) { _soundCloudMessage = ex.Message; _engine?.Log($"Music: playlists task error: {ex.Message}"); }
                _soundCloudPlaylistsTask = null;
            }

            if (_windowManagement?.IsVisible() != true) return;

            var pos = _currentMouseState.Position;
            bool leftClick = _currentMouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released;

            if (_soundCloudPickerVisible && leftClick)
            {
                try
                {
                    if (HandleSoundCloudPickerClick(pos)) { _previousMouseState = _currentMouseState; return; }
                }
                catch (Exception ex) { _engine?.Log($"Music: SoundCloud picker error: {ex.Message}"); _soundCloudPickerVisible = false; }
            }
            if (_soundCloudPickerVisible)
            {
                int scroll = _previousMouseState.ScrollWheelValue - _currentMouseState.ScrollWheelValue;
                GetSoundCloudPickerLayout(out _, out _, out _, out _, out int listH, out _, out _, out int contentHeight, out _, out _);
                int maxScroll = Math.Max(0, contentHeight - listH);

                if (_soundCloudIsDraggingScrollbar)
                {
                    if (_currentMouseState.LeftButton == ButtonState.Pressed)
                    {
                        float deltaY = pos.Y - _soundCloudScrollbarDragStart.Y;
                        float scrollbarHeight = Math.Max(1, _soundCloudScrollbarBounds.Height);
                        float scrollRatio = deltaY / scrollbarHeight;
                        _soundCloudScrollOffset = _soundCloudScrollOffsetAtDragStart + (int)(scrollRatio * maxScroll);
                        _soundCloudScrollOffset = MathHelper.Clamp(_soundCloudScrollOffset, 0, maxScroll);
                    }
                    else
                    {
                        _soundCloudIsDraggingScrollbar = false;
                    }
                }
                else
                {
                    if (_currentMouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released
                        && _soundCloudNeedsScrollbar && _soundCloudScrollbarBounds.Contains(pos))
                    {
                        _soundCloudIsDraggingScrollbar = true;
                        _soundCloudScrollbarDragStart = new Vector2(pos.X, pos.Y);
                        _soundCloudScrollOffsetAtDragStart = _soundCloudScrollOffset;
                    }
                    if (scroll != 0 && _soundCloudListBounds.Contains(pos))
                    {
                        _soundCloudScrollOffset = MathHelper.Clamp(_soundCloudScrollOffset + scroll / 120 * TrackRowHeight, 0, maxScroll);
                    }
                }
            }

            if (_currentMouseState.LeftButton == ButtonState.Released)
                _isDraggingVolume = false;
            if (leftClick && _contentBounds.Contains(pos) && !_soundCloudPickerVisible)
            {
                if (_progressBarBounds.Contains(pos))
                {
                    float progress = (pos.X - _progressBarBounds.X) / (float)Math.Max(1, _progressBarBounds.Width);
                    progress = MathHelper.Clamp(progress, 0f, 1f);
                    _audioPlayback?.SeekTo(progress);
                }
                else if (_volumeTrackBounds.Contains(pos))
                    _isDraggingVolume = true;
                else if (_nextButtonLeftBounds.Contains(pos) || _nextButtonRightBounds.Contains(pos))
                    ShowSoundCloudPicker();
                else if (_playPauseButtonBounds.Contains(pos))
                {
                    _engine?.Log($"Music: Play button clicked. PlaybackState={_audioPlayback?.PlaybackState}, _currentFilePath={_currentFilePath ?? "(null)"}, FileExists={(!string.IsNullOrEmpty(_currentFilePath) && System.IO.File.Exists(_currentFilePath))}");
                    if (_audioPlayback != null)
                    {
                        if (_audioPlayback.PlaybackState == NAudio.Wave.PlaybackState.Playing)
                        {
                            _audioPlayback.Pause();
                            _engine?.Log("Music: Paused.");
                        }
                        else
                        {
                            _audioPlayback.Play();
                            var stateAfter = _audioPlayback.PlaybackState;
                            _engine?.Log($"Music: Play() called, state after={stateAfter}");
                            if (stateAfter != NAudio.Wave.PlaybackState.Playing && !string.IsNullOrEmpty(_currentFilePath) && System.IO.File.Exists(_currentFilePath))
                            {
                                _engine?.Log($"Music: Retrying Load+Play from {_currentFilePath}");
                                try
                                {
                                    _audioPlayback.Load(_currentFilePath);
                                    _audioPlayback.Play();
                                    _engine?.Log($"Music: Retry Play state={_audioPlayback.PlaybackState}");
                                }
                                catch (Exception ex) { _engine?.Log($"Music: retry play failed: {ex.Message}"); }
                            }
                            else if (stateAfter != NAudio.Wave.PlaybackState.Playing)
                                _engine?.Log("Music: Not playing (no file loaded or path missing).");
                        }
                    }
                    else
                        _engine?.Log("Music: Play button clicked but _audioPlayback is null.");
                }
                else if (_openButtonBounds.Contains(pos))
                    ShowSoundCloudPicker();
                else if (_stopButtonBounds.Contains(pos))
                    _audioPlayback?.Stop();
            }

            if ((_currentMouseState.LeftButton == ButtonState.Pressed && _volumeTrackBounds.Contains(pos)) || _isDraggingVolume)
            {
                float vol = (pos.X - _volumeTrackBounds.X) / (float)Math.Max(1, _volumeTrackBounds.Width);
                vol = MathHelper.Clamp(vol, 0f, 1f);
                if (_audioPlayback != null)
                    _audioPlayback.Volume = vol;
            }

            _previousMouseState = _currentMouseState;
        }

        private void OpenFile()
        {
            // Legacy: local file open (kept for compatibility). Prefer SoundCloud via Open button.
            var thread = new Thread(() =>
            {
                try
                {
                    using (var dlg = new System.Windows.Forms.OpenFileDialog())
                    {
                        dlg.Filter = "Audio (*.wav;*.mp3)|*.wav;*.mp3|All files (*.*)|*.*";
                        if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                        string chosen = dlg.FileName;
                        if (string.IsNullOrEmpty(chosen)) return;
                        lock (_pendingPathLock)
                        {
                            _pendingFilePath = chosen;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _engine?.Log($"Music: OpenFile error: {ex.Message}");
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        private void ShowSoundCloudPicker()
        {
            _soundCloudPickerVisible = true;
            _soundCloudMessage = null;
            _soundCloudScrollOffset = 0;
            _soundCloudSection = 0;
            _soundCloudSelectedPlaylist = null;
            _soundCloudSearchQuery = null;
            if (_soundCloudTracks == null) _soundCloudTracks = new List<SoundCloudTrack>();
            else _soundCloudTracks.Clear();
            _soundCloudLoading = true;
            _soundCloudTask = _soundCloudClient.GetChartsAsync(30, msg => _engine?.Log(msg));
        }

        /// <summary>
        /// Computes SoundCloud picker layout: panel bounds, list area (listTop, listH, listLeft, listW),
        /// content height, whether scrollbar is needed, and scrollbar bounds.
        /// </summary>
        private void GetSoundCloudPickerLayout(
            out int panelW, out int panelH, out Rectangle panelBounds,
            out int listTop, out int listH, out int listLeft, out int listW,
            out int contentHeight, out bool needsScrollbar, out Rectangle scrollbarBounds)
        {
            panelW = Math.Min(500, _contentBounds.Width - 40);
            panelH = Math.Min(420, _contentBounds.Height - 50);
            panelBounds = new Rectangle(_contentBounds.X + (_contentBounds.Width - panelW) / 2, _contentBounds.Y + (_contentBounds.Height - panelH) / 2, panelW, panelH);
            int headerH = 44;
            int tabH = 28;
            int searchRowH = 36;
            int backButtonH = 28;

            listTop = panelBounds.Y + headerH + tabH + PickerPadding;
            listH = panelBounds.Height - headerH - tabH - PickerPadding * 2;
            listLeft = panelBounds.X + PickerPadding;
            listW = panelBounds.Width - PickerPadding * 2;

            int rowCount = 0;
            if (_soundCloudSection == 3 && _soundCloudSelectedPlaylist != null)
            {
                listTop += backButtonH + 4;
                listH -= backButtonH + 4;
                var trackList = _soundCloudSelectedPlaylist?.Tracks;
                rowCount = trackList?.Count ?? 0;
            }
            else if (_soundCloudSection == 1)
            {
                listTop += searchRowH + 4 + (6 / 3 + 1) * 26 + 4; // presets
                listH -= searchRowH + 4 + (6 / 3 + 1) * 26 + 4;
                rowCount = _soundCloudTracks?.Count ?? 0;
            }
            else if (_soundCloudSection == 3 && _soundCloudSelectedPlaylist == null)
            {
                rowCount = _soundCloudPlaylists?.Count ?? 0;
            }
            else
            {
                rowCount = _soundCloudTracks?.Count ?? 0;
            }

            contentHeight = rowCount * TrackRowHeight;
            needsScrollbar = contentHeight > listH;
            if (needsScrollbar)
            {
                listW -= SCROLLBAR_WIDTH + SCROLLBAR_PADDING;
                scrollbarBounds = new Rectangle(panelBounds.Right - SCROLLBAR_WIDTH - 2, listTop, SCROLLBAR_WIDTH, listH);
            }
            else
            {
                scrollbarBounds = Rectangle.Empty;
            }

            _soundCloudListContentHeight = contentHeight;
            _soundCloudNeedsScrollbar = needsScrollbar;
            _soundCloudScrollbarBounds = scrollbarBounds;
            _soundCloudListBounds = new Rectangle(listLeft, listTop, listW, listH);

            int maxScroll = Math.Max(0, contentHeight - listH);
            _soundCloudScrollOffset = MathHelper.Clamp(_soundCloudScrollOffset, 0, maxScroll);
        }

        private bool HandleSoundCloudPickerClick(Point pos)
        {
            int panelW = Math.Min(500, _contentBounds.Width - 40);
            int panelH = Math.Min(420, _contentBounds.Height - 50);
            var panelBounds = new Rectangle(_contentBounds.X + (_contentBounds.Width - panelW) / 2, _contentBounds.Y + (_contentBounds.Height - panelH) / 2, panelW, panelH);
            if (!panelBounds.Contains(pos)) { _soundCloudPickerVisible = false; _windowManagement?.BringToFront(); return true; }

            int headerH = 44;
            int tabH = 28;
            var closeBounds = new Rectangle(panelBounds.Right - 36, panelBounds.Y + 8, 28, 28);
            if (closeBounds.Contains(pos)) { _soundCloudPickerVisible = false; _windowManagement?.BringToFront(); return true; }

            int tabY = panelBounds.Y + headerH;
            int tabW = panelBounds.Width / 4;
            var tabPopular = new Rectangle(panelBounds.X, tabY, tabW, tabH);
            var tabSearch = new Rectangle(panelBounds.X + tabW, tabY, tabW, tabH);
            var tabMyTracks = new Rectangle(panelBounds.X + tabW * 2, tabY, tabW, tabH);
            var tabPlaylists = new Rectangle(panelBounds.X + tabW * 3, tabY, tabW, tabH);
            if (tabPopular.Contains(pos)) { _soundCloudSection = 0; _soundCloudSelectedPlaylist = null; if (_soundCloudTracks == null) _soundCloudTracks = new List<SoundCloudTrack>(); else _soundCloudTracks.Clear(); _soundCloudMessage = null; _soundCloudLoading = true; _soundCloudTask = _soundCloudClient.GetChartsAsync(30, msg => _engine?.Log(msg)); return true; }
            if (tabSearch.Contains(pos)) { _soundCloudSection = 1; _soundCloudSelectedPlaylist = null; _soundCloudMessage = null; if (_soundCloudTracks == null) _soundCloudTracks = new List<SoundCloudTrack>(); return true; }
            if (tabMyTracks.Contains(pos)) { _soundCloudSection = 2; _soundCloudSelectedPlaylist = null; if (!_soundCloudClient.HasUserToken) { _soundCloudMessage = "Sign in to see your likes"; return true; } if (_soundCloudTracks == null) _soundCloudTracks = new List<SoundCloudTrack>(); else _soundCloudTracks.Clear(); _soundCloudLoading = true; _soundCloudTask = _soundCloudClient.GetMyLikesAsync(msg => _engine?.Log(msg)); return true; }
            if (tabPlaylists.Contains(pos)) { _soundCloudSection = 3; _soundCloudSelectedPlaylist = null; if (!_soundCloudClient.HasUserToken) { _soundCloudMessage = "Sign in to see playlists"; _soundCloudPlaylists = _soundCloudPlaylists ?? new List<SoundCloudPlaylist>(); _soundCloudPlaylists.Clear(); return true; } _soundCloudPlaylists = _soundCloudPlaylists ?? new List<SoundCloudPlaylist>(); _soundCloudPlaylists.Clear(); _soundCloudLoading = true; _soundCloudPlaylistsTask = _soundCloudClient.GetMyPlaylistsAsync(msg => _engine?.Log(msg)); return true; }

            GetSoundCloudPickerLayout(out _, out _, out _, out int listTop, out int listH, out int listLeft, out int listW, out _, out _, out _);
            if (_soundCloudNeedsScrollbar && _soundCloudScrollbarBounds.Contains(pos))
                return true;

            int searchRowH = 36;
            int backButtonH = 28;
            int listAreaTop = panelBounds.Y + headerH + tabH + PickerPadding;

            if (_soundCloudSection == 2 && !_soundCloudClient.HasUserToken && !_soundCloudLoading)
            {
                var signInBounds = new Rectangle(panelBounds.X + PickerPadding, listAreaTop, panelBounds.Width - PickerPadding * 2, 40);
                if (signInBounds.Contains(pos)) { _soundCloudLoading = true; _soundCloudMessage = null; _soundCloudTask = SignInThenLoadLikesAsync(); return true; }
            }
            if (_soundCloudSection == 3 && !_soundCloudClient.HasUserToken && !_soundCloudLoading)
            {
                var signInBounds = new Rectangle(panelBounds.X + PickerPadding, listAreaTop, panelBounds.Width - PickerPadding * 2, 40);
                if (signInBounds.Contains(pos)) { _soundCloudLoading = true; _soundCloudMessage = null; _soundCloudPlaylistsTask = SignInThenLoadPlaylistsAsync(); return true; }
            }

            if (_soundCloudSection == 3 && _soundCloudSelectedPlaylist != null)
            {
                var backBounds = new Rectangle(panelBounds.X + PickerPadding, listAreaTop, 80, backButtonH);
                if (backBounds.Contains(pos)) { _soundCloudSelectedPlaylist = null; _soundCloudTracks = new List<SoundCloudTrack>(); return true; }
            }

            if (_soundCloudSection == 1)
            {
                var searchBoxBounds = new Rectangle(panelBounds.X + PickerPadding, listAreaTop, panelBounds.Width - PickerPadding * 2 - 50, searchRowH - 4);
                var goBounds = new Rectangle(searchBoxBounds.Right + 4, listAreaTop, 46, searchRowH - 4);
                if (goBounds.Contains(pos))
                {
                    _soundCloudSearchQuery = _soundCloudSearchQuery ?? "";
                    _soundCloudTracks.Clear();
                    _soundCloudLoading = true;
                    _soundCloudTask = _soundCloudClient.SearchTracksAsync(_soundCloudSearchQuery.Trim().Length > 0 ? _soundCloudSearchQuery.Trim() : "music", 40, msg => _engine?.Log(msg));
                    _soundCloudMessage = null;
                    return true;
                }
                int presetY = listAreaTop + searchRowH + 4;
                string[] presets = { "electronic", "chill", "rock", "pop", "jazz", "hip hop" };
                int pw = (panelBounds.Width - PickerPadding * 2 - 20) / 3;
                for (int i = 0; i < presets.Length; i++)
                {
                    int col = i % 3, rw = i / 3;
                    var preBounds = new Rectangle(panelBounds.X + PickerPadding + col * (pw + 6), presetY + rw * 26, pw, 22);
                    if (preBounds.Contains(pos))
                    {
                        _soundCloudSearchQuery = presets[i];
                        _soundCloudTracks.Clear();
                        _soundCloudLoading = true;
                        _soundCloudTask = _soundCloudClient.SearchTracksAsync(presets[i], 40, msg => _engine?.Log(msg));
                        _soundCloudMessage = null;
                        return true;
                    }
                }
            }

            var trackList = _soundCloudSection == 3 && _soundCloudSelectedPlaylist != null ? _soundCloudSelectedPlaylist?.Tracks : _soundCloudTracks;
            if (trackList == null) trackList = _soundCloudTracks;
            for (int i = 0; i < (trackList?.Count ?? 0); i++)
            {
                int rowY = _soundCloudListBounds.Y + i * TrackRowHeight - _soundCloudScrollOffset;
                if (rowY + TrackRowHeight < _soundCloudListBounds.Y || rowY > _soundCloudListBounds.Bottom) continue;
                var rowBounds = new Rectangle(_soundCloudListBounds.X, rowY, _soundCloudListBounds.Width, TrackRowHeight);
                if (rowBounds.Contains(pos) && _soundCloudStreamTask == null && _soundCloudDownloadTask == null && !_soundCloudDownloadingTrackId.HasValue)
                {
                    var track = trackList[i];
                    _soundCloudDownloadingTrackId = track.Id;
                    _soundCloudPendingTrack = track;
                    _soundCloudLastDownloadError = null;
                    // Don't set _currentTrackTitle here — only set it when we actually start playing (so if load fails, title and playback stay correct)
                    _soundCloudPickerVisible = false;
                    // Bring Music window to front so Play button receives clicks (otherwise Desktop can be on top)
                    _windowManagement?.BringToFront();
                    // Use download-to-file only so Load(path)+Play() always works and Play button works
                    _soundCloudDownloadTask = _soundCloudClient.DownloadTrackToTempFileAsync(track, SoundCloudDownloadLog);
                    return true;
                }
            }

            if (_soundCloudSection == 3 && _soundCloudSelectedPlaylist == null && (_soundCloudPlaylists?.Count ?? 0) > 0)
            {
                var list = _soundCloudPlaylists ?? new List<SoundCloudPlaylist>();
                for (int i = 0; i < list.Count; i++)
                {
                    int rowY = _soundCloudListBounds.Y + i * TrackRowHeight - _soundCloudScrollOffset;
                    if (rowY + TrackRowHeight < _soundCloudListBounds.Y || rowY > _soundCloudListBounds.Bottom) continue;
                    var rowBounds = new Rectangle(_soundCloudListBounds.X, rowY, _soundCloudListBounds.Width, TrackRowHeight);
                    if (rowBounds.Contains(pos))
                    {
                        var pl = list[i];
                        if (pl == null) return true;
                        _soundCloudSelectedPlaylist = pl;
                        _soundCloudTracks = pl.Tracks != null ? new List<SoundCloudTrack>(pl.Tracks) : new List<SoundCloudTrack>();
                        _soundCloudScrollOffset = 0;
                        return true;
                    }
                }
            }
            return true;
        }

        private async Task<List<SoundCloudTrack>> SignInThenLoadLikesAsync()
        {
            var token = await _soundCloudClient.SignInWithSoundCloudAsync(msg => _engine?.Log(msg));
            if (token == null) return new List<SoundCloudTrack>();
            return await _soundCloudClient.GetMyLikesAsync(msg => _engine?.Log(msg));
        }
        private async Task<List<SoundCloudTrack>> SignInThenLoadTracksAsync()
        {
            var token = await _soundCloudClient.SignInWithSoundCloudAsync(msg => _engine?.Log(msg));
            if (token == null) return new List<SoundCloudTrack>();
            return await _soundCloudClient.GetMyTracksAsync(msg => _engine?.Log(msg));
        }

        private async Task<List<SoundCloudPlaylist>> SignInThenLoadPlaylistsAsync()
        {
            var token = await _soundCloudClient.SignInWithSoundCloudAsync(msg => _engine?.Log(msg));
            if (token == null) return new List<SoundCloudPlaylist>();
            return await _soundCloudClient.GetMyPlaylistsAsync(msg => _engine?.Log(msg));
        }

        private void DrawSoundCloudPicker(SpriteBatch spriteBatch)
        {
            SpriteFont font = _uiFont ?? _menuFont;
            if (font == null) return;
            int panelW = Math.Min(500, _contentBounds.Width - 40);
            int panelH = Math.Min(420, _contentBounds.Height - 50);
            var panelBounds = new Rectangle(_contentBounds.X + (_contentBounds.Width - panelW) / 2, _contentBounds.Y + (_contentBounds.Height - panelH) / 2, panelW, panelH);
            GetSoundCloudPickerLayout(out _, out _, out _, out _, out _, out _, out _, out _, out _, out _);
            int listAreaTop = panelBounds.Y + 44 + 28 + PickerPadding; // headerH + tabH + PickerPadding

            var overlay = new Rectangle(_contentBounds.X, _contentBounds.Y, _contentBounds.Width, _contentBounds.Height);
            spriteBatch.Draw(_pixel, overlay, new Color(0, 0, 0, 160));
            spriteBatch.Draw(_pixel, panelBounds, new Color(35, 35, 42));
            var borderColor = new Color(147, 112, 219);
            int b = 2;
            spriteBatch.Draw(_pixel, new Rectangle(panelBounds.X, panelBounds.Y, panelBounds.Width, b), borderColor);
            spriteBatch.Draw(_pixel, new Rectangle(panelBounds.X, panelBounds.Bottom - b, panelBounds.Width, b), borderColor);
            spriteBatch.Draw(_pixel, new Rectangle(panelBounds.X, panelBounds.Y, b, panelBounds.Height), borderColor);
            spriteBatch.Draw(_pixel, new Rectangle(panelBounds.Right - b, panelBounds.Y, b, panelBounds.Height), borderColor);
            var titlePos = new Vector2(panelBounds.X + 12, panelBounds.Y + 12);
            spriteBatch.DrawString(font, "SoundCloud", titlePos, Color.White, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
            var closeBounds = new Rectangle(panelBounds.Right - 36, panelBounds.Y + 8, 28, 28);
            spriteBatch.DrawString(font, "X", new Vector2(closeBounds.X + 8, closeBounds.Y + 4), Color.White, 0f, Vector2.Zero, 0.9f, SpriteEffects.None, 0f);

            int headerH = 44;
            int tabH = 28;
            int tabY = panelBounds.Y + headerH;
            int tabW = panelBounds.Width / 4;
            var tabPopular = new Rectangle(panelBounds.X, tabY, tabW, tabH);
            var tabSearch = new Rectangle(panelBounds.X + tabW, tabY, tabW, tabH);
            var tabMyTracks = new Rectangle(panelBounds.X + tabW * 2, tabY, tabW, tabH);
            var tabPlaylists = new Rectangle(panelBounds.X + tabW * 3, tabY, tabW, tabH);
            var tabBg = new Color(45, 45, 52);
            spriteBatch.Draw(_pixel, tabPopular, _soundCloudSection == 0 ? new Color(99, 102, 241) : tabBg);
            spriteBatch.Draw(_pixel, tabSearch, _soundCloudSection == 1 ? new Color(99, 102, 241) : tabBg);
            spriteBatch.Draw(_pixel, tabMyTracks, _soundCloudSection == 2 ? new Color(99, 102, 241) : tabBg);
            spriteBatch.Draw(_pixel, tabPlaylists, _soundCloudSection == 3 ? new Color(99, 102, 241) : tabBg);
            spriteBatch.DrawString(font, "Popular", new Vector2(tabPopular.X + 8, tabPopular.Y + 6), Color.White, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);
            spriteBatch.DrawString(font, "Search", new Vector2(tabSearch.X + 8, tabSearch.Y + 6), Color.White, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);
            spriteBatch.DrawString(font, "Likes", new Vector2(tabMyTracks.X + 8, tabMyTracks.Y + 6), Color.White, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);
            spriteBatch.DrawString(font, "Playlists", new Vector2(tabPlaylists.X + 8, tabPlaylists.Y + 6), Color.White, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);

            int searchRowH = 36;
            int backButtonH = 28;

            if (_soundCloudSection == 3 && _soundCloudSelectedPlaylist != null)
            {
                var backBounds = new Rectangle(panelBounds.X + PickerPadding, listAreaTop, 80, backButtonH);
                spriteBatch.Draw(_pixel, backBounds, backBounds.Contains(_currentMouseState.Position) ? new Color(99, 102, 241) : new Color(60, 60, 70));
                spriteBatch.DrawString(font, "< Back", new Vector2(backBounds.X + 4, backBounds.Y + 4), Color.White, 0f, Vector2.Zero, 0.85f, SpriteEffects.None, 0f);
            }

            if (_soundCloudSection == 1)
            {
                var searchBoxBounds = new Rectangle(panelBounds.X + PickerPadding, listAreaTop, panelBounds.Width - PickerPadding * 2 - 50, searchRowH - 4);
                var goBounds = new Rectangle(searchBoxBounds.Right + 4, listAreaTop, 46, searchRowH - 4);
                spriteBatch.Draw(_pixel, searchBoxBounds, new Color(50, 50, 58));
                spriteBatch.Draw(_pixel, goBounds, goBounds.Contains(_currentMouseState.Position) ? new Color(99, 102, 241) : new Color(60, 60, 70));
                string searchLabel = string.IsNullOrEmpty(_soundCloudSearchQuery) ? "Click a genre below or Go" : _soundCloudSearchQuery;
                if (searchLabel.Length > 35) searchLabel = searchLabel.Substring(0, 32) + "...";
                searchLabel = SanitizeForFont(searchLabel);
                spriteBatch.DrawString(font, searchLabel, new Vector2(searchBoxBounds.X + 6, searchBoxBounds.Y + 6), new Color(180, 180, 180), 0f, Vector2.Zero, 0.85f, SpriteEffects.None, 0f);
                spriteBatch.DrawString(font, "Go", new Vector2(goBounds.X + 14, goBounds.Y + 8), Color.White, 0f, Vector2.Zero, 0.85f, SpriteEffects.None, 0f);
                int presetY = listAreaTop + searchRowH + 4;
                string[] presets = { "electronic", "chill", "rock", "pop", "jazz", "hip hop" };
                int pw = (panelBounds.Width - PickerPadding * 2 - 20) / 3;
                for (int i = 0; i < presets.Length; i++)
                {
                    int col = i % 3, rw = i / 3;
                    var preBounds = new Rectangle(panelBounds.X + PickerPadding + col * (pw + 6), presetY + rw * 26, pw, 22);
                    spriteBatch.Draw(_pixel, preBounds, preBounds.Contains(_currentMouseState.Position) ? new Color(99, 102, 241) : new Color(55, 55, 65));
                    spriteBatch.DrawString(font, presets[i], new Vector2(preBounds.X + 6, preBounds.Y + 3), Color.White, 0f, Vector2.Zero, 0.75f, SpriteEffects.None, 0f);
                }
            }

            // Scissor the list area and draw scrollable content
            Rectangle originalScissor = spriteBatch.GraphicsDevice.ScissorRectangle;
            spriteBatch.End();
            if (_soundCloudListBounds.Height > 0)
            {
                spriteBatch.GraphicsDevice.ScissorRectangle = _soundCloudListBounds;
                spriteBatch.GraphicsDevice.RasterizerState = new RasterizerState { ScissorTestEnable = true, CullMode = CullMode.None };
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, spriteBatch.GraphicsDevice.RasterizerState);

            if (_soundCloudLoading || _soundCloudDownloadingTrackId.HasValue)
            {
                string msg = _soundCloudDownloadingTrackId.HasValue ? "Loading track..." : "Loading...";
                spriteBatch.DrawString(font, msg, new Vector2(_soundCloudListBounds.X, _soundCloudListBounds.Y), new Color(200, 200, 200), 0f, Vector2.Zero, 0.9f, SpriteEffects.None, 0f);
            }
            else if ((_soundCloudSection == 2 || _soundCloudSection == 3) && !_soundCloudClient.HasUserToken)
            {
                var signInBounds = new Rectangle(_soundCloudListBounds.X, _soundCloudListBounds.Y, _soundCloudListBounds.Width, 40);
                spriteBatch.Draw(_pixel, signInBounds, new Color(60, 60, 70));
                if (signInBounds.Contains(_currentMouseState.Position)) spriteBatch.Draw(_pixel, signInBounds, new Color(99, 102, 241, 80));
                spriteBatch.DrawString(font, "Sign in with SoundCloud", new Vector2(signInBounds.X + (signInBounds.Width - font.MeasureString("Sign in with SoundCloud").X * 0.95f) / 2f, signInBounds.Y + 10), Color.White, 0f, Vector2.Zero, 0.95f, SpriteEffects.None, 0f);
            }
            else if (!string.IsNullOrEmpty(_soundCloudMessage))
            {
                string msg = SanitizeForFont(_soundCloudMessage);
                spriteBatch.DrawString(font, msg, new Vector2(_soundCloudListBounds.X, _soundCloudListBounds.Y), new Color(220, 120, 120), 0f, Vector2.Zero, 0.85f, SpriteEffects.None, 0f);
            }
            else if (_soundCloudSection == 3 && _soundCloudSelectedPlaylist == null && (_soundCloudPlaylists?.Count ?? 0) > 0)
            {
                var list = _soundCloudPlaylists ?? new List<SoundCloudPlaylist>();
                for (int i = 0; i < list.Count; i++)
                {
                    int rowY = _soundCloudListBounds.Y + i * TrackRowHeight - _soundCloudScrollOffset;
                    if (rowY + TrackRowHeight < _soundCloudListBounds.Y || rowY > _soundCloudListBounds.Bottom) continue;
                    var rowBounds = new Rectangle(_soundCloudListBounds.X, rowY, _soundCloudListBounds.Width, TrackRowHeight);
                    bool hover = rowBounds.Contains(_currentMouseState.Position);
                    if (hover) spriteBatch.Draw(_pixel, rowBounds, new Color(60, 60, 75));
                    var pl = list[i];
                    if (pl == null) continue;
                    string title = string.IsNullOrEmpty(pl.Title) ? "Untitled" : pl.Title;
                    if (title.Length > 50) title = title.Substring(0, 47) + "...";
                    title = SanitizeForFont(title);
                    try { spriteBatch.DrawString(font, title, new Vector2(rowBounds.X + 4, rowBounds.Y + (TrackRowHeight - font.LineSpacing * 0.85f) / 2), Color.White, 0f, Vector2.Zero, 0.85f, SpriteEffects.None, 0f); }
                    catch { spriteBatch.DrawString(font, "?", new Vector2(rowBounds.X + 4, rowBounds.Y + (TrackRowHeight - font.LineSpacing * 0.85f) / 2), Color.White, 0f, Vector2.Zero, 0.85f, SpriteEffects.None, 0f); }
                }
            }
            else
            {
                var trackList = _soundCloudSection == 3 && _soundCloudSelectedPlaylist != null ? _soundCloudSelectedPlaylist?.Tracks : _soundCloudTracks;
                if (trackList != null && trackList.Count > 0)
                {
                    for (int i = 0; i < trackList.Count; i++)
                    {
                        int rowY = _soundCloudListBounds.Y + i * TrackRowHeight - _soundCloudScrollOffset;
                        if (rowY + TrackRowHeight < _soundCloudListBounds.Y || rowY > _soundCloudListBounds.Bottom) continue;
                        var rowBounds = new Rectangle(_soundCloudListBounds.X, rowY, _soundCloudListBounds.Width, TrackRowHeight);
                        bool hover = rowBounds.Contains(_currentMouseState.Position) && !_soundCloudDownloadingTrackId.HasValue;
                        if (hover) spriteBatch.Draw(_pixel, rowBounds, new Color(60, 60, 75));
                        var track = trackList[i];
                        string title = string.IsNullOrEmpty(track.Title) ? "Untitled" : track.Title;
                        if (title.Length > 50) title = title.Substring(0, 47) + "...";
                        title = SanitizeForFont(title);
                        try { spriteBatch.DrawString(font, title, new Vector2(rowBounds.X + 4, rowBounds.Y + (TrackRowHeight - font.LineSpacing * 0.85f) / 2), Color.White, 0f, Vector2.Zero, 0.85f, SpriteEffects.None, 0f); }
                        catch { spriteBatch.DrawString(font, "?", new Vector2(rowBounds.X + 4, rowBounds.Y + (TrackRowHeight - font.LineSpacing * 0.85f) / 2), Color.White, 0f, Vector2.Zero, 0.85f, SpriteEffects.None, 0f); }
                    }
                }
                else if (_soundCloudSection == 1 && (_soundCloudTracks?.Count ?? 0) == 0 && !_soundCloudLoading)
                {
                    spriteBatch.DrawString(font, "Enter a search term above and click Go.", new Vector2(_soundCloudListBounds.X, _soundCloudListBounds.Y), new Color(180, 180, 180), 0f, Vector2.Zero, 0.85f, SpriteEffects.None, 0f);
                }
                else if (_soundCloudSection == 2 && (_soundCloudTracks?.Count ?? 0) == 0 && !_soundCloudLoading)
                {
                    spriteBatch.DrawString(font, "No likes.", new Vector2(_soundCloudListBounds.X, _soundCloudListBounds.Y), new Color(180, 180, 180), 0f, Vector2.Zero, 0.9f, SpriteEffects.None, 0f);
                }
                else if (_soundCloudSection == 3 && (_soundCloudPlaylists?.Count ?? 0) == 0 && _soundCloudSelectedPlaylist == null && !_soundCloudLoading)
                {
                    spriteBatch.DrawString(font, "No playlists.", new Vector2(_soundCloudListBounds.X, _soundCloudListBounds.Y), new Color(180, 180, 180), 0f, Vector2.Zero, 0.9f, SpriteEffects.None, 0f);
                }
                else if ((_soundCloudTracks?.Count ?? 0) == 0 && !_soundCloudLoading)
                {
                    spriteBatch.DrawString(font, "No tracks found.", new Vector2(_soundCloudListBounds.X, _soundCloudListBounds.Y), new Color(180, 180, 180), 0f, Vector2.Zero, 0.9f, SpriteEffects.None, 0f);
                }
            }

                spriteBatch.End();
            }
            spriteBatch.GraphicsDevice.ScissorRectangle = originalScissor;
            spriteBatch.GraphicsDevice.RasterizerState = new RasterizerState { ScissorTestEnable = false };
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, spriteBatch.GraphicsDevice.RasterizerState);

            if (_soundCloudNeedsScrollbar && !_soundCloudScrollbarBounds.IsEmpty)
            {
                spriteBatch.Draw(_pixel, _soundCloudScrollbarBounds, new Color(55, 65, 81));
                int thumbHeight = Math.Max(20, (int)(_soundCloudScrollbarBounds.Height * (float)_soundCloudListBounds.Height / Math.Max(1, _soundCloudListContentHeight)));
                int maxScroll = Math.Max(1, _soundCloudListContentHeight - _soundCloudListBounds.Height);
                int thumbY = _soundCloudScrollbarBounds.Y + (int)((_soundCloudScrollbarBounds.Height - thumbHeight) * (_soundCloudScrollOffset / (float)maxScroll));
                var thumbBounds = new Rectangle(_soundCloudScrollbarBounds.X + 2, thumbY + 2, _soundCloudScrollbarBounds.Width - 4, thumbHeight - 4);
                bool thumbHover = thumbBounds.Contains(_currentMouseState.Position) || _soundCloudIsDraggingScrollbar;
                spriteBatch.Draw(_pixel, thumbBounds, thumbHover ? new Color(99, 102, 241) : new Color(75, 85, 99));
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            if (_windowManagement?.IsVisible() != true) return;

            _windowManagement.Draw(spriteBatch, "Music");

            try
            {
            // Viz area (top)
            if (_vizBounds.Width > 0 && _vizBounds.Height > 0)
                spriteBatch.Draw(_pixel, _vizBounds, new Color(25, 25, 30));

            AnalyzedAudio data = _audioAnalyzer != null ? _audioAnalyzer.CurrentAnalyzedAudio : default;
            _hasAudioData = data.FFT != null && data.FFT.Length > 0;
            bool hasSamples = data.Samples != null && data.Samples.Length > 0;
            if (_vizBounds.Width > 0 && _vizBounds.Height > 0)
            {
                if (_hasAudioData)
                    DrawBarSpectrum(spriteBatch, data);
                if (hasSamples)
                    DrawTimeDomainWave(spriteBatch, data.Samples);
            }

            // Progress bar below viz, above toolbar (clickable for seeking)
            if (_progressBarBounds.Width > 0 && _progressBarBounds.Height > 0)
            {
                spriteBatch.Draw(_pixel, _progressBarBounds, new Color(40, 40, 45));
                float progress = MathHelper.Clamp(_audioPlayback?.Progress ?? 0f, 0f, 1f);
                int fillW = (int)(_progressBarBounds.Width * progress);
                if (fillW > 0)
                {
                    _progressFillBounds = new Rectangle(_progressBarBounds.X, _progressBarBounds.Y, fillW, _progressBarBounds.Height);
                    spriteBatch.Draw(_pixel, _progressFillBounds, new Color(147, 112, 219));
                }
            }

            // Show SoundCloud error in main view when picker is closed (e.g. "Could not load track. Sign in...")
            if (!_soundCloudPickerVisible && !string.IsNullOrEmpty(_soundCloudMessage) && _contentBounds.Width > 0)
            {
                SpriteFont msgFont = _uiFont ?? _menuFont;
                if (msgFont != null)
                {
                    string msg = SanitizeForFont(_soundCloudMessage);
                    if (msg.Length > 60) msg = msg.Substring(0, 57) + "...";
                    var msgSize = msgFont.MeasureString(msg) * 0.9f;
                    float msgY = _toolbarBounds.Y > 0 ? _toolbarBounds.Y - 22 : _contentBounds.Y + 8;
                    var msgPos = new Vector2(_contentBounds.X + (_contentBounds.Width - msgSize.X) / 2f, msgY);
                    spriteBatch.DrawString(msgFont, msg, msgPos, new Color(220, 120, 120), 0f, Vector2.Zero, 0.9f, SpriteEffects.None, 0f);
                }
            }

            // Toolbar at bottom
            spriteBatch.Draw(_pixel, _toolbarBounds, new Color(50, 50, 55));
            SpriteFont font = _uiFont ?? _menuFont;
            const float titleScale = 1.05f;
            const float timeScale = 0.95f;

            // Row 1: Song name centered, time on right
            string label = _soundCloudDownloadingTrackId.HasValue
                ? "Loading track..."
                : (string.IsNullOrEmpty(_currentTrackTitle)
                    ? (string.IsNullOrEmpty(_currentFilePath) ? "No file loaded" : System.IO.Path.GetFileName(_currentFilePath))
                    : _currentTrackTitle);
            if (label.Length > 45) label = label.Substring(0, 42) + "...";
            label = SanitizeForFont(label);
            float labelW = font.MeasureString(label).X * titleScale;
            var labelPos = new Vector2(_toolbarBounds.X + (_toolbarBounds.Width - labelW) / 2f, _toolbarBounds.Y + (34 - font.LineSpacing * titleScale) / 2);
            spriteBatch.DrawString(font, label, labelPos, new Color(220, 220, 220), 0f, Vector2.Zero, titleScale, SpriteEffects.None, 0f);

            string timeText = FormatTime(_audioPlayback?.CurrentTime ?? TimeSpan.Zero) + " / " + FormatTime(_audioPlayback?.TotalTime ?? TimeSpan.Zero);
            float timeW = font.MeasureString(timeText).X * timeScale;
            var timePos = new Vector2(_toolbarBounds.Right - timeW - 12, _toolbarBounds.Y + (34 - font.LineSpacing * timeScale) / 2);
            spriteBatch.DrawString(font, timeText, timePos, new Color(190, 190, 190), 0f, Vector2.Zero, timeScale, SpriteEffects.None, 0f);

            // Row 2: Buttons centered (icon buttons + text buttons)
            bool hoverPlayPause = _playPauseButtonBounds.Contains(_currentMouseState.Position);
            bool hoverNextRight = _nextButtonRightBounds.Contains(_currentMouseState.Position);
            bool hoverNextLeft = _nextButtonLeftBounds.Contains(_currentMouseState.Position);

            var playbackState = _audioPlayback?.PlaybackState ?? NAudio.Wave.PlaybackState.Stopped;
            Texture2D playPauseTexture = playbackState == NAudio.Wave.PlaybackState.Playing
                ? _pauseIcon
                : _playIcon;

            if (_nextIcon != null)
            {
                DrawIconButton(spriteBatch, _nextButtonLeftBounds, _nextIcon, hoverNextLeft, SpriteEffects.FlipHorizontally);
                DrawIconButton(spriteBatch, _nextButtonRightBounds, _nextIcon, hoverNextRight, SpriteEffects.None);
            }

            if (playPauseTexture != null)
            {
                DrawIconButton(spriteBatch, _playPauseButtonBounds, playPauseTexture, hoverPlayPause);
            }

            DrawButton(spriteBatch, _openButtonBounds, "SC", _openButtonBounds.Contains(_currentMouseState.Position));
            DrawButton(spriteBatch, _stopButtonBounds, "Stop", _stopButtonBounds.Contains(_currentMouseState.Position));

            // Volume slider (right side of row 2) + percent text
            if (_volumeTrackBounds.Width > 0 && _volumeTrackBounds.Height > 0)
            {
                float vol = _audioPlayback?.Volume ?? 1f;
                vol = MathHelper.Clamp(vol, 0f, 1f);

                // Track
                spriteBatch.Draw(_pixel, _volumeTrackBounds, new Color(35, 35, 40));

                // Fill
                int fillW = (int)(_volumeTrackBounds.Width * vol);
                if (fillW > 0)
                {
                    var fill = new Rectangle(_volumeTrackBounds.X, _volumeTrackBounds.Y, fillW, _volumeTrackBounds.Height);
                    spriteBatch.Draw(_pixel, fill, new Color(147, 112, 219));
                }

                // Knob
                int knobX = _volumeTrackBounds.X + (int)(_volumeTrackBounds.Width * vol) - 2;
                var knob = new Rectangle(knobX, _volumeTrackBounds.Y - 4, 4, _volumeTrackBounds.Height + 8);
                spriteBatch.Draw(_pixel, knob, new Color(230, 230, 230));

                // Percent text
                int percent = (int)Math.Round(vol * 100f);
                string volText = $"{percent}%";
                const float volScale = 0.8f;
                var volPos = new Vector2(_volumeTrackBounds.Right + 8, _volumeTrackBounds.Y + (_volumeTrackBounds.Height - font.LineSpacing * volScale) / 2);
                spriteBatch.DrawString(font, volText, volPos, new Color(220, 220, 220), 0f, Vector2.Zero, volScale, SpriteEffects.None, 0f);
            }

            // "Loading track..." overlay on top of everything when connecting (so user always sees it)
            if (_soundCloudDownloadingTrackId.HasValue && _contentBounds.Width > 0 && _contentBounds.Height > 0)
            {
                SpriteFont overlayFont = _uiFont ?? _menuFont;
                if (overlayFont != null)
                {
                    spriteBatch.Draw(_pixel, _contentBounds, new Color(0, 0, 0, 200));
                    const float overlayScale = 1.35f;
                    string loadingText = "Loading track...";
                    var size = overlayFont.MeasureString(loadingText) * overlayScale;
                    var pos = new Vector2(
                        _contentBounds.X + (_contentBounds.Width - size.X) / 2f,
                        _contentBounds.Y + (_contentBounds.Height - size.Y) / 2f);
                    spriteBatch.DrawString(overlayFont, loadingText, pos, new Color(250, 250, 255), 0f, Vector2.Zero, overlayScale, SpriteEffects.None, 0f);
                }
            }

            if (_soundCloudPickerVisible)
            {
                try { DrawSoundCloudPicker(spriteBatch); }
                catch (Exception ex) { _engine?.Log($"Music: Draw SoundCloud error: {ex.Message}"); }
            }

            _windowManagement.DrawOverlay(spriteBatch);
            }
            catch (Exception ex) { _engine?.Log($"Music: Draw error: {ex.Message}"); }
        }

        private static string FormatTime(TimeSpan t)
        {
            if (t.TotalHours >= 1)
                return $"{(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}";
            return $"{(int)t.TotalMinutes}:{t.Seconds:D2}";
        }

        /// <summary>Keeps only characters safe for SpriteFont (printable ASCII) to avoid crash when track titles contain Unicode.</summary>
        private static string SanitizeForFont(string text)
        {
            if (string.IsNullOrEmpty(text)) return text ?? "";
            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (c >= 32 && c <= 126) sb.Append(c);
                else if (c == '\n' || c == '\r' || c == '\t') sb.Append(' ');
                else sb.Append('?');
            }
            return sb.ToString();
        }

        private static Texture2D CreateCircleTexture(GraphicsDevice graphicsDevice, int size)
        {
            var tex = new Texture2D(graphicsDevice, size, size);
            Color[] data = new Color[size * size];
            float center = (size - 1) / 2f;
            float radiusSq = center * center;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    data[y * size + x] = (dx * dx + dy * dy <= radiusSq) ? Color.White : Color.Transparent;
                }
            tex.SetData(data);
            return tex;
        }

        private static readonly Color PurpleBorder = new Color(147, 112, 219);

        private void DrawCircleButton(SpriteBatch spriteBatch, Rectangle bounds, Color fillColor)
        {
            if (_circleTexture == null) return;
            spriteBatch.Draw(_circleTexture, bounds, PurpleBorder);
            int inset = 3;
            var inner = new Rectangle(bounds.X + inset, bounds.Y + inset, bounds.Width - inset * 2, bounds.Height - inset * 2);
            spriteBatch.Draw(_circleTexture, inner, fillColor);
        }

        private void DrawButton(SpriteBatch spriteBatch, Rectangle bounds, string text, bool hover)
        {
            SpriteFont font = _uiFont ?? _menuFont;
            var bg = hover ? new Color(99, 102, 241) : new Color(60, 60, 65);
            DrawCircleButton(spriteBatch, bounds, bg);
            const float btnScale = 0.7f;
            var size = font.MeasureString(text) * btnScale;
            var pos = new Vector2(bounds.X + (bounds.Width - size.X) / 2, bounds.Y + (bounds.Height - size.Y) / 2);
            spriteBatch.DrawString(font, text, pos, Color.White, 0f, Vector2.Zero, btnScale, SpriteEffects.None, 0f);
        }

        private void DrawIconButton(SpriteBatch spriteBatch, Rectangle bounds, Texture2D icon, bool hover, SpriteEffects effects = SpriteEffects.None)
        {
            if (icon == null) return;

            var bg = hover ? new Color(99, 102, 241) : new Color(60, 60, 65);
            DrawCircleButton(spriteBatch, bounds, bg);

            float availableWidth = bounds.Width * 0.92f;
            float availableHeight = bounds.Height * 0.92f;
            float scale = Math.Min(availableWidth / icon.Width, availableHeight / icon.Height);

            var size = new Vector2(icon.Width, icon.Height) * scale;
            var pos = new Vector2(
                bounds.X + (bounds.Width - size.X) / 2f,
                bounds.Y + (bounds.Height - size.Y) / 2f
            );

            spriteBatch.Draw(icon, pos, null, Color.White, 0f, Vector2.Zero, scale, effects, 0f);
        }

        /// <summary>Draw time-domain level bars with smoothed height.</summary>
        private void DrawTimeDomainWave(SpriteBatch spriteBatch, AudioSample[] samples)
        {
            if (samples == null || samples.Length == 0) return;
            int n = samples.Length;
            if (_smoothedTimeDomainHeights == null || _smoothedTimeDomainHeights.Length != n)
                _smoothedTimeDomainHeights = new float[n];

            float barW = (float)_vizBounds.Width / n;
            float maxBarH = _vizBounds.Height * 0.45f;
            float centerY = _vizBounds.Y + _vizBounds.Height * 0.75f;

            for (int i = 0; i < n; i++)
            {
                float level = Math.Max(Math.Abs(samples[i].MaxSample), Math.Abs(samples[i].MinSample));
                level = MathHelper.Clamp(level, 0f, 1f);
                float targetH = Math.Max(4f, level * maxBarH);
                _smoothedTimeDomainHeights[i] = MathHelper.Lerp(_smoothedTimeDomainHeights[i], targetH, BarSmoothFactor);
                float barH = _smoothedTimeDomainHeights[i];
                int x = _vizBounds.X + (int)(i * barW);
                int y = (int)(centerY - barH);
                int w = Math.Max(1, (int)barW - 1);
                var rect = new Rectangle(x, y, w, (int)Math.Max(2, barH));
                spriteBatch.Draw(_pixel, rect, new Color(180, 120, 255, 230));
            }
        }

        /// <summary>Draw spectrum as vertical bars with smoothed height.</summary>
        private void DrawBarSpectrum(SpriteBatch spriteBatch, AnalyzedAudio data)
        {
            if (data.FFT == null || data.FFT.Length < SpectrumBarCount) return;

            if (_smoothedSpectrumHeights == null || _smoothedSpectrumHeights.Length != SpectrumBarCount)
                _smoothedSpectrumHeights = new float[SpectrumBarCount];

            int n = data.FFT.Length;
            int binsPerBar = Math.Max(1, n / SpectrumBarCount);
            float barW = (float)_vizBounds.Width / SpectrumBarCount;
            float maxBarH = _vizBounds.Height;
            var colorBar = new Color(147, 112, 219);
            var colorBarAlt = new Color(99, 102, 241);

            for (int b = 0; b < SpectrumBarCount; b++)
            {
                float sumMag = 0f;
                int start = b * binsPerBar;
                int end = Math.Min(start + binsPerBar, n);
                for (int i = start; i < end; i++)
                {
                    float x = data.SmoothFFT[i].X;
                    float y = data.SmoothFFT[i].Y;
                    sumMag += (float)Math.Sqrt(x * x + y * y);
                }
                float avgMag = (end > start) ? sumMag / (end - start) : 0f;
                float targetH = MathHelper.Clamp(avgMag * BarMagnitudeScale, 4f, maxBarH);
                _smoothedSpectrumHeights[b] = MathHelper.Lerp(_smoothedSpectrumHeights[b], targetH, BarSmoothFactor);
                float barH = _smoothedSpectrumHeights[b];
                int x1 = _vizBounds.X + (int)(b * barW);
                int y1 = _vizBounds.Bottom - (int)barH;
                int w = Math.Max(1, (int)barW - 1);
                var barRect = new Rectangle(x1, y1, w, (int)Math.Max(2, barH));
                var col = (b % 2 == 0) ? colorBar : colorBarAlt;
                spriteBatch.Draw(_pixel, barRect, col);
            }
        }

        public void UpdateWindowWidth(int width)
        {
            _windowWidth = width;
            _windowManagement?.UpdateWindowWidth(width);
            UpdateBounds();
        }

        public void LoadContent(ContentManager content)
        {
            _content = content;
            _windowManagement?.LoadContent(content);
            try
            {
                _uiFont = content.Load<SpriteFont>("Fonts/SpriteFonts/roboto/regular");
            }
            catch
            {
                try { _uiFont = content.Load<SpriteFont>("Fonts/SpriteFonts/open_sans/regular"); }
                catch { _uiFont = null; }
            }
            try
            {
                var logo = content.Load<Texture2D>("Modules/Music_essential/logo");
                _windowManagement?.SetWindowLogo(logo);
            }
            catch { /* optional */ }
            try
            {
                _playIcon = content.Load<Texture2D>("Modules/Music_essential/Images/Play");
                _pauseIcon = content.Load<Texture2D>("Modules/Music_essential/Images/Pause");
                _nextIcon = content.Load<Texture2D>("Modules/Music_essential/Images/NextSong");
            }
            catch
            {
                _playIcon = null;
                _pauseIcon = null;
                _nextIcon = null;
            }
            if (_taskBar != null)
                _taskBar.EnsureModuleIconExists("Music", _content);
        }

        public void Dispose()
        {
            _audioAnalyzer?.Dispose();
            _audioPlayback?.Dispose();
            _basicEffect?.Dispose();
            _circleTexture?.Dispose();
            _pixel?.Dispose();
            _windowManagement?.Dispose();
        }
    }
}
