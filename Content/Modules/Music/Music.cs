using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Media;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WinForms = System.Windows.Forms;
using MarySGameEngine.Modules.WindowManagement_essential;
using MarySGameEngine.Modules.TaskBar_essential;

namespace MarySGameEngine.Modules.Music
{
    public class Music : IModule
    {
        private WindowManagement _windowManagement;
        private GraphicsDevice _graphicsDevice;
        private SpriteFont _menuFont; // Used for window title bar only
        private SpriteFont _uiFont; // Used for all UI elements except title bar
        private int _windowWidth;
        private TaskBar _taskBar;
        private GameEngine _engine;
        private Texture2D _pixel;
        private MouseState _currentMouseState;
        private MouseState _previousMouseState;

        // Music properties
        private string _selectedFolderPath = "";
        private List<string> _musicFiles = new List<string>();
        private int _currentTrackIndex = -1;
        private Song _currentSong;
        private bool _isPlaying = false;
        private bool _isPaused = false;

        // Beat visualizer properties
        private const int VISUALIZER_BAR_COUNT = 32;
        private float[] _barHeights = new float[VISUALIZER_BAR_COUNT];
        private float[] _barTargetHeights = new float[VISUALIZER_BAR_COUNT];
        private float[] _barVelocities = new float[VISUALIZER_BAR_COUNT];
        private const float BAR_SMOOTHING = 0.15f;
        private const float BAR_MIN_HEIGHT = 5f;
        private const float BAR_MAX_HEIGHT = 150f;
        private const int BAR_WIDTH = 8;
        private const int BAR_SPACING = 2;
        private float _visualizerTimer = 0f;
        private const float BEAT_UPDATE_INTERVAL = 0.05f; // Update bars every 50ms

        // UI properties
        private Rectangle _selectFolderButtonBounds;
        private Rectangle _playPauseButtonBounds;
        private Rectangle _nextButtonBounds;
        private Rectangle _previousButtonBounds;
        private Rectangle _visualizerBounds;
        private bool _isSelectFolderHovered = false;
        private bool _isPlayPauseHovered = false;
        private bool _isNextHovered = false;
        private bool _isPreviousHovered = false;
        private const int BUTTON_HEIGHT = 30;
        private const int BUTTON_PADDING = 10;
        private const int VISUALIZER_PADDING = 20;

        // Colors (matching other modules)
        private readonly Color MIAMI_BACKGROUND = new Color(40, 40, 40);
        private readonly Color MIAMI_PURPLE = new Color(147, 112, 219);
        private readonly Color MIAMI_PURPLE_LIGHT = new Color(180, 145, 250);
        private readonly Color MIAMI_HOVER = new Color(147, 112, 219, 180);
        private readonly Color MIAMI_TEXT = new Color(220, 220, 220);
        private readonly Color BUTTON_COLOR = new Color(147, 112, 219);
        private readonly Color BUTTON_HOVER_COLOR = new Color(180, 145, 250);
        private readonly Color VISUALIZER_BAR_COLOR = new Color(147, 112, 219);
        private readonly Color VISUALIZER_BAR_PEAK_COLOR = new Color(255, 192, 203); // Pink for peaks

        public Music(GraphicsDevice graphicsDevice, SpriteFont menuFont, int windowWidth)
        {
            _graphicsDevice = graphicsDevice;
            _menuFont = menuFont;
            _windowWidth = windowWidth;
            _engine = (GameEngine)GameEngine.Instance;

            // Create pixel texture
            _pixel = new Texture2D(graphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });

            // Create window properties
            var properties = new WindowProperties
            {
                IsVisible = false,  // Start hidden to avoid initialization issues
                IsMovable = true,
                IsResizable = true
            };

            // Initialize window management
            _windowManagement = new WindowManagement(graphicsDevice, menuFont, windowWidth, properties);
            _windowManagement.SetWindowTitle("Music");
            _windowManagement.SetDefaultSize(600, 300);
            _windowManagement.SetCustomMinimumSize(400, 200);
            _windowManagement.SetPosition(new Vector2(200, 100));

            // Initialize bar heights
            for (int i = 0; i < VISUALIZER_BAR_COUNT; i++)
            {
                _barHeights[i] = BAR_MIN_HEIGHT;
                _barTargetHeights[i] = BAR_MIN_HEIGHT;
                _barVelocities[i] = 0f;
            }

            // Initialize bounds after window is set up
            try
            {
                UpdateBounds();
            }
            catch (Exception ex)
            {
                _engine.Log($"Music: Error initializing bounds: {ex.Message}");
            }
        }

        public void SetTaskBar(TaskBar taskBar)
        {
            _taskBar = taskBar;
            _windowManagement.SetTaskBar(taskBar);
        }

        public void Update()
        {
            try
            {
                _previousMouseState = _currentMouseState;
                _currentMouseState = Mouse.GetState();

                _windowManagement.Update();

                if (!_windowManagement.IsVisible())
                    return;

                // Update music state
                if (_isPlaying && !_isPaused && MediaPlayer.State == MediaState.Stopped && _currentTrackIndex >= 0)
                {
                    // Song ended, play next
                    PlayNextTrack();
                }

                // Update visualizer
                UpdateVisualizer();

                // Update UI bounds
                UpdateBounds();

                // Handle button clicks
                HandleInput();
            }
            catch (Exception ex)
            {
                _engine.Log($"Music: Error in Update: {ex.Message}");
                _engine.Log($"Music: Stack trace: {ex.StackTrace}");
            }
        }

        private void UpdateVisualizer()
        {
            _visualizerTimer += (float)GameEngine.Instance.TargetElapsedTime.TotalSeconds;

            if (_isPlaying && !_isPaused && MediaPlayer.State == MediaState.Playing)
            {
                // Update target heights based on music (simulated beat detection)
                if (_visualizerTimer >= BEAT_UPDATE_INTERVAL)
                {
                    _visualizerTimer = 0f;

                    // Generate random beat pattern (simulated)
                    Random random = new Random();
                    for (int i = 0; i < VISUALIZER_BAR_COUNT; i++)
                    {
                        // Create a more interesting pattern with some bars higher than others
                        float baseHeight = (float)(random.NextDouble() * 0.5 + 0.3); // 0.3 to 0.8
                        
                        // Add some frequency-like variation (lower bars on sides, higher in middle)
                        float frequencyFactor = 1f - Math.Abs((i - VISUALIZER_BAR_COUNT / 2f) / (VISUALIZER_BAR_COUNT / 2f)) * 0.5f;
                        
                        _barTargetHeights[i] = BAR_MIN_HEIGHT + (BAR_MAX_HEIGHT - BAR_MIN_HEIGHT) * baseHeight * frequencyFactor;
                    }
                }
            }
            else
            {
                // Fade out when not playing
                for (int i = 0; i < VISUALIZER_BAR_COUNT; i++)
                {
                    _barTargetHeights[i] = BAR_MIN_HEIGHT;
                }
            }

            // Smooth bar animations
            for (int i = 0; i < VISUALIZER_BAR_COUNT; i++)
            {
                float target = _barTargetHeights[i];
                float current = _barHeights[i];
                
                // Smooth interpolation
                _barHeights[i] = MathHelper.Lerp(current, target, BAR_SMOOTHING);
                
                // Ensure minimum height
                if (_barHeights[i] < BAR_MIN_HEIGHT)
                {
                    _barHeights[i] = BAR_MIN_HEIGHT;
                }
            }
        }

        private void HandleInput()
        {
            var mousePos = _currentMouseState.Position;
            bool leftJustPressed = _currentMouseState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed &&
                                   _previousMouseState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Released;

            // Update hover states
            _isSelectFolderHovered = _selectFolderButtonBounds.Contains(mousePos);
            _isPlayPauseHovered = _playPauseButtonBounds.Contains(mousePos);
            _isNextHovered = _nextButtonBounds.Contains(mousePos);
            _isPreviousHovered = _previousButtonBounds.Contains(mousePos);

            if (leftJustPressed)
            {
                if (_isSelectFolderHovered)
                {
                    SelectMusicFolder();
                }
                else if (_isPlayPauseHovered && _musicFiles.Count > 0)
                {
                    TogglePlayPause();
                }
                else if (_isNextHovered && _musicFiles.Count > 0)
                {
                    PlayNextTrack();
                }
                else if (_isPreviousHovered && _musicFiles.Count > 0)
                {
                    PlayPreviousTrack();
                }
            }
        }

        private void SelectMusicFolder()
        {
            try
            {
                // Run the dialog on a separate STA thread to avoid blocking the game thread
                System.Threading.Thread dialogThread = new System.Threading.Thread(() =>
                {
                    try
                    {
                        // Use Windows Forms FolderBrowserDialog for folder selection
                        using (WinForms.FolderBrowserDialog folderDialog = new WinForms.FolderBrowserDialog())
                        {
                            folderDialog.Description = "Select a folder containing music files";
                            folderDialog.ShowNewFolderButton = false;
                            
                            if (!string.IsNullOrEmpty(_selectedFolderPath) && Directory.Exists(_selectedFolderPath))
                            {
                                folderDialog.SelectedPath = _selectedFolderPath;
                            }

                            if (folderDialog.ShowDialog() == WinForms.DialogResult.OK)
                            {
                                _selectedFolderPath = folderDialog.SelectedPath;
                                LoadMusicFiles();
                                _engine.Log($"Music: Selected folder: {_selectedFolderPath}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _engine.Log($"Music: Error in folder dialog thread: {ex.Message}");
                        _engine.Log($"Music: Stack trace: {ex.StackTrace}");
                    }
                });
                
                dialogThread.SetApartmentState(System.Threading.ApartmentState.STA);
                dialogThread.IsBackground = true;
                dialogThread.Start();
            }
            catch (Exception ex)
            {
                _engine.Log($"Music: Error starting folder dialog thread: {ex.Message}");
                _engine.Log($"Music: Stack trace: {ex.StackTrace}");
            }
        }

        private void LoadMusicFiles()
        {
            _musicFiles.Clear();
            _currentTrackIndex = -1;
            StopMusic();

            if (string.IsNullOrEmpty(_selectedFolderPath) || !Directory.Exists(_selectedFolderPath))
                return;

            try
            {
                // Supported audio formats
                string[] extensions = { "*.mp3", "*.wav", "*.ogg", "*.wma", "*.m4a" };
                
                foreach (string extension in extensions)
                {
                    _musicFiles.AddRange(Directory.GetFiles(_selectedFolderPath, extension, SearchOption.TopDirectoryOnly));
                }

                // Sort files alphabetically
                _musicFiles = _musicFiles.OrderBy(f => Path.GetFileName(f)).ToList();

                _engine.Log($"Music: Loaded {_musicFiles.Count} music files from folder");
                
                if (_musicFiles.Count > 0)
                {
                    _currentTrackIndex = 0;
                }
            }
            catch (Exception ex)
            {
                _engine.Log($"Music: Error loading music files: {ex.Message}");
            }
        }

        private void PlayMusic(int trackIndex)
        {
            if (trackIndex < 0 || trackIndex >= _musicFiles.Count)
                return;

            try
            {
                StopMusic();
                _currentTrackIndex = trackIndex;

                string filePath = _musicFiles[trackIndex];
                
                // MonoGame's MediaPlayer requires songs to be loaded through Content pipeline
                // For runtime loading, we can use Song.FromUri, but it requires a valid URI
                // For local files, we need to convert the path to a file:// URI
                
                try
                {
                    // Convert file path to proper file:// URI format
                    // Ensure the path uses forward slashes and is properly formatted
                    string normalizedPath = Path.GetFullPath(filePath).Replace('\\', '/');
                    if (!normalizedPath.StartsWith("/"))
                    {
                        normalizedPath = "/" + normalizedPath;
                    }
                    Uri songUri = new Uri("file://" + normalizedPath);
                    
                    // Load song from URI (works for local files)
                    _currentSong = Song.FromUri(Path.GetFileName(filePath), songUri);
                    
                    // Play the song
                    MediaPlayer.Play(_currentSong);
                    MediaPlayer.IsRepeating = false; // Don't repeat, we'll handle next track manually
                    
                    _isPlaying = true;
                    _isPaused = false;
                    
                    _engine.Log($"Music: Playing track {trackIndex + 1}/{_musicFiles.Count}: {Path.GetFileName(filePath)}");
                }
                catch (Exception uriEx)
                {
                    // Fallback: If URI loading fails, just simulate playback for visualizer
                    // This allows the visualizer to work even if audio playback isn't available
                    _engine.Log($"Music: Could not load song via URI ({uriEx.Message}), using visualizer only mode");
                    _isPlaying = true;
                    _isPaused = false;
                }
            }
            catch (Exception ex)
            {
                _engine.Log($"Music: Error playing music: {ex.Message}");
                _isPlaying = false;
            }
        }

        private void StopMusic()
        {
            try
            {
                if (MediaPlayer.State != MediaState.Stopped)
                {
                    MediaPlayer.Stop();
                }
                if (_currentSong != null)
                {
                    _currentSong.Dispose();
                    _currentSong = null;
                }
                _isPlaying = false;
                _isPaused = false;
            }
            catch (Exception ex)
            {
                _engine.Log($"Music: Error stopping music: {ex.Message}");
            }
        }

        private void TogglePlayPause()
        {
            if (!_isPlaying)
            {
                if (_currentTrackIndex < 0 && _musicFiles.Count > 0)
                {
                    _currentTrackIndex = 0;
                }
                PlayMusic(_currentTrackIndex);
            }
            else
            {
                _isPaused = !_isPaused;
                if (_isPaused)
                {
                    MediaPlayer.Pause();
                }
                else
                {
                    MediaPlayer.Resume();
                }
            }
        }

        private void PlayNextTrack()
        {
            if (_musicFiles.Count == 0)
                return;

            _currentTrackIndex = (_currentTrackIndex + 1) % _musicFiles.Count;
            PlayMusic(_currentTrackIndex);
        }

        private void PlayPreviousTrack()
        {
            if (_musicFiles.Count == 0)
                return;

            _currentTrackIndex = (_currentTrackIndex - 1 + _musicFiles.Count) % _musicFiles.Count;
            PlayMusic(_currentTrackIndex);
        }

        private void UpdateBounds()
        {
            if (_windowManagement == null || !_windowManagement.IsVisible())
                return;

            Rectangle windowBounds = _windowManagement.GetWindowBounds();
            if (windowBounds.Width <= 0 || windowBounds.Height <= 0)
                return;
            int contentX = windowBounds.X;
            int contentY = windowBounds.Y + 40; // Title bar height
            int contentWidth = windowBounds.Width;
            int contentHeight = windowBounds.Height - 40;

            // Button layout
            int buttonY = contentY + BUTTON_PADDING;
            int buttonWidth = 120;

            _selectFolderButtonBounds = new Rectangle(
                contentX + BUTTON_PADDING,
                buttonY,
                buttonWidth,
                BUTTON_HEIGHT
            );

            _playPauseButtonBounds = new Rectangle(
                _selectFolderButtonBounds.Right + BUTTON_PADDING,
                buttonY,
                80,
                BUTTON_HEIGHT
            );

            _previousButtonBounds = new Rectangle(
                _playPauseButtonBounds.Right + BUTTON_PADDING,
                buttonY,
                60,
                BUTTON_HEIGHT
            );

            _nextButtonBounds = new Rectangle(
                _previousButtonBounds.Right + BUTTON_PADDING,
                buttonY,
                60,
                BUTTON_HEIGHT
            );

            // Visualizer bounds (below buttons)
            int visualizerY = buttonY + BUTTON_HEIGHT + VISUALIZER_PADDING;
            int visualizerHeight = contentHeight - (visualizerY - contentY) - VISUALIZER_PADDING;
            
            _visualizerBounds = new Rectangle(
                contentX + VISUALIZER_PADDING,
                visualizerY,
                contentWidth - (VISUALIZER_PADDING * 2),
                visualizerHeight
            );
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            try
            {
                _windowManagement.Draw(spriteBatch, "Music");

                if (!_windowManagement.IsVisible())
                    return;

            // Get UI font (fallback to menu font if not loaded)
            SpriteFont uiFont = _uiFont ?? _menuFont;

            Rectangle windowBounds = _windowManagement.GetWindowBounds();
            int contentX = windowBounds.X;
            int contentY = windowBounds.Y + 40; // Title bar height

            // Draw background
            Rectangle contentBounds = new Rectangle(
                contentX,
                contentY,
                windowBounds.Width,
                windowBounds.Height - 40
            );
            spriteBatch.Draw(_pixel, contentBounds, MIAMI_BACKGROUND);

            // Draw select folder button
            Color selectFolderColor = _isSelectFolderHovered ? BUTTON_HOVER_COLOR : BUTTON_COLOR;
            spriteBatch.Draw(_pixel, _selectFolderButtonBounds, selectFolderColor);
            string selectFolderText = string.IsNullOrEmpty(_selectedFolderPath) ? "Select Folder" : "Change Folder";
            Vector2 selectFolderTextSize = uiFont.MeasureString(selectFolderText);
            spriteBatch.DrawString(uiFont, selectFolderText,
                new Vector2(
                    _selectFolderButtonBounds.X + (_selectFolderButtonBounds.Width - selectFolderTextSize.X) / 2,
                    _selectFolderButtonBounds.Y + (_selectFolderButtonBounds.Height - selectFolderTextSize.Y) / 2
                ),
                Color.White);

            // Draw play/pause button
            Color playPauseColor = _isPlayPauseHovered ? BUTTON_HOVER_COLOR : BUTTON_COLOR;
            spriteBatch.Draw(_pixel, _playPauseButtonBounds, playPauseColor);
            string playPauseText = _isPlaying && !_isPaused ? "Pause" : "Play";
            Vector2 playPauseTextSize = uiFont.MeasureString(playPauseText);
            spriteBatch.DrawString(uiFont, playPauseText,
                new Vector2(
                    _playPauseButtonBounds.X + (_playPauseButtonBounds.Width - playPauseTextSize.X) / 2,
                    _playPauseButtonBounds.Y + (_playPauseButtonBounds.Height - playPauseTextSize.Y) / 2
                ),
                Color.White);

            // Draw previous button
            Color previousColor = _isPreviousHovered ? BUTTON_HOVER_COLOR : BUTTON_COLOR;
            spriteBatch.Draw(_pixel, _previousButtonBounds, previousColor);
            string previousText = "<<";
            Vector2 previousTextSize = uiFont.MeasureString(previousText);
            spriteBatch.DrawString(uiFont, previousText,
                new Vector2(
                    _previousButtonBounds.X + (_previousButtonBounds.Width - previousTextSize.X) / 2,
                    _previousButtonBounds.Y + (_previousButtonBounds.Height - previousTextSize.Y) / 2
                ),
                Color.White);

            // Draw next button
            Color nextColor = _isNextHovered ? BUTTON_HOVER_COLOR : BUTTON_COLOR;
            spriteBatch.Draw(_pixel, _nextButtonBounds, nextColor);
            string nextText = ">>";
            Vector2 nextTextSize = uiFont.MeasureString(nextText);
            spriteBatch.DrawString(uiFont, nextText,
                new Vector2(
                    _nextButtonBounds.X + (_nextButtonBounds.Width - nextTextSize.X) / 2,
                    _nextButtonBounds.Y + (_nextButtonBounds.Height - nextTextSize.Y) / 2
                ),
                Color.White);

            // Draw current track info
            if (_currentTrackIndex >= 0 && _currentTrackIndex < _musicFiles.Count)
            {
                string trackInfo = $"{_currentTrackIndex + 1}/{_musicFiles.Count}: {Path.GetFileName(_musicFiles[_currentTrackIndex])}";
                Vector2 trackInfoSize = uiFont.MeasureString(trackInfo);
                spriteBatch.DrawString(uiFont, trackInfo,
                    new Vector2(
                        contentX + BUTTON_PADDING,
                        _selectFolderButtonBounds.Bottom + 5
                    ),
                    MIAMI_TEXT);
            }

            // Draw visualizer
            DrawVisualizer(spriteBatch);
            }
            catch (Exception ex)
            {
                _engine.Log($"Music: Error in Draw: {ex.Message}");
                _engine.Log($"Music: Stack trace: {ex.StackTrace}");
            }
        }

        private void DrawVisualizer(SpriteBatch spriteBatch)
        {
            if (_visualizerBounds.Width <= 0 || _visualizerBounds.Height <= 0)
                return;

            // Calculate bar positions
            int totalBarWidth = (VISUALIZER_BAR_COUNT * BAR_WIDTH) + ((VISUALIZER_BAR_COUNT - 1) * BAR_SPACING);
            int startX = _visualizerBounds.X + (_visualizerBounds.Width - totalBarWidth) / 2;
            int baseY = _visualizerBounds.Bottom;

            for (int i = 0; i < VISUALIZER_BAR_COUNT; i++)
            {
                int barX = startX + (i * (BAR_WIDTH + BAR_SPACING));
                int barHeight = (int)_barHeights[i];
                int barY = baseY - barHeight;

                Rectangle barRect = new Rectangle(barX, barY, BAR_WIDTH, barHeight);

                // Use peak color for taller bars
                Color barColor = _barHeights[i] > BAR_MAX_HEIGHT * 0.7f ? VISUALIZER_BAR_PEAK_COLOR : VISUALIZER_BAR_COLOR;
                
                spriteBatch.Draw(_pixel, barRect, barColor);
            }
        }

        public void UpdateWindowWidth(int newWidth)
        {
            _windowWidth = newWidth;
            _windowManagement.UpdateWindowWidth(newWidth);
            UpdateBounds();
        }

        public void LoadContent(ContentManager content)
        {
            _windowManagement.LoadContent(content);
            
            // Load UI font (try Roboto, fallback to Open Sans, then menu font)
            try
            {
                _uiFont = content.Load<SpriteFont>("Fonts/SpriteFonts/roboto/regular");
                _engine.Log("Music: Successfully loaded Roboto font");
            }
            catch (Exception ex)
            {
                _engine.Log($"Music: Failed to load Roboto font: {ex.Message}");
                try
                {
                    _uiFont = content.Load<SpriteFont>("Fonts/SpriteFonts/open_sans/regular");
                    _engine.Log("Music: Using Open Sans font as fallback");
                }
                catch (Exception ex2)
                {
                    _engine.Log($"Music: Failed to load Open Sans font: {ex2.Message}");
                    _uiFont = _menuFont; // Fallback to menu font
                    _engine.Log("Music: Using menu font as last resort");
                }
            }
        }

        public void Dispose()
        {
            StopMusic();
            _pixel?.Dispose();
            _windowManagement?.Dispose();
        }
    }
}

