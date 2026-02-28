using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Content;
using System;
using System.Threading;
using MarySGameEngine.Modules.Music_essential.Audio;
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
                    _audioPlayback?.Load(pathToLoad);
                    _audioPlayback?.Play();
                }
                catch (Exception ex)
                {
                    _engine?.Log($"Music: Load error: {ex.Message}");
                }
            }

            if (_windowManagement?.IsVisible() != true) return;

            var pos = _currentMouseState.Position;
            bool leftClick = _currentMouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released;

            if (_currentMouseState.LeftButton == ButtonState.Released)
                _isDraggingVolume = false;
            if (leftClick && _contentBounds.Contains(pos))
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
                    OpenFile();
                else if (_playPauseButtonBounds.Contains(pos))
                {
                    if (_audioPlayback != null)
                    {
                        if (_audioPlayback.PlaybackState == NAudio.Wave.PlaybackState.Playing)
                            _audioPlayback.Pause();
                        else
                            _audioPlayback.Play();
                    }
                }
                else if (_openButtonBounds.Contains(pos))
                    OpenFile();
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
            // Show dialog on a separate STA thread so the game loop keeps running and doesn't freeze
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

        public void Draw(SpriteBatch spriteBatch)
        {
            if (_windowManagement?.IsVisible() != true) return;

            _windowManagement.Draw(spriteBatch, "Music");

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

            // Toolbar at bottom
            spriteBatch.Draw(_pixel, _toolbarBounds, new Color(50, 50, 55));
            SpriteFont font = _uiFont ?? _menuFont;
            const float titleScale = 1.05f;
            const float timeScale = 0.95f;

            // Row 1: Song name centered, time on right
            string label = string.IsNullOrEmpty(_currentFilePath)
                ? "No file loaded"
                : System.IO.Path.GetFileName(_currentFilePath);
            if (label.Length > 45) label = label.Substring(0, 42) + "...";
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

            DrawButton(spriteBatch, _openButtonBounds, "Open", _openButtonBounds.Contains(_currentMouseState.Position));
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

            _windowManagement.DrawOverlay(spriteBatch);
        }

        private static string FormatTime(TimeSpan t)
        {
            if (t.TotalHours >= 1)
                return $"{(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}";
            return $"{(int)t.TotalMinutes}:{t.Seconds:D2}";
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
