using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Content;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using MarySGameEngine;
using MarySGameEngine.Modules.WindowManagement_essential;
using MarySGameEngine.Modules.TaskBar_essential;
using System.Runtime.InteropServices;

namespace MarySGameEngine.Modules.Console_essential
{
    public class Console : IModule
    {
        // Windows API declarations for clipboard functionality
        [DllImport("user32.dll")]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll")]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll")]
        private static extern bool EmptyClipboard();

        [DllImport("user32.dll")]
        private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll")]
        private static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("kernel32.dll")]
        private static extern bool GlobalFree(IntPtr hMem);

        [DllImport("kernel32.dll")]
        private static extern UIntPtr lstrcpy(IntPtr lpString1, string lpString2);

        [DllImport("user32.dll")]
        private static extern IntPtr GetClipboardData(uint uFormat);

        private const uint CF_TEXT = 1;
        private const uint GMEM_MOVEABLE = 0x0002;
        private WindowManagement _windowManagement;
        private GraphicsDevice _graphicsDevice;
        private SpriteFont _consoleFont;
        private int _windowWidth;
        private TaskBar _taskBar;
        private GameEngine _engine;
        private Texture2D _pixel;
        private MouseState _currentMouseState;
        private MouseState _previousMouseState;
        private KeyboardState _currentKeyboardState;
        private KeyboardState _previousKeyboardState;

        // Console-specific properties
        private List<string> _consoleLines;
        private StringBuilder _currentInput;
        private int _cursorPosition;
        private int _scrollOffset;
        private int _maxVisibleLines;
        private int _lineHeight;
        private int _consolePadding = 10;
        private int _consoleMargin = 5;
        private Rectangle _consoleBounds;
        private Rectangle _inputBounds;
        private bool _autoScroll = true;
        private bool _isFocused = false;
        private float _cursorBlinkTimer;
        private bool _showCursor = true;
        private const float CURSOR_BLINK_INTERVAL = 0.5f;

        // Scrolling properties
        private int _scrollY = 0;
        private int _contentHeight = 0;
        private bool _needsScrollbar = false;
        private Rectangle _scrollbarBounds;
        private bool _isDraggingScrollbar = false;
        private Vector2 _scrollbarDragStart;
        private const int SCROLLBAR_WIDTH = 16;
        private const int SCROLLBAR_PADDING = 2;
        private bool _isHoveringScrollbar = false;
        private bool _scrollingEnabled = true;

        // Text selection properties
        private bool _isSelecting = false;
        private int _selectionStart = -1;
        private int _selectionEnd = -1;
        private string _selectedText = "";
        private bool _hasSelection = false;
        
        // Double-click detection
        private float _lastClickTime = 0f;
        private Point _lastClickPosition = Point.Zero;
        private const float DOUBLE_CLICK_TIME = 0.5f; // 500ms
        private const int DOUBLE_CLICK_DISTANCE = 5; // 5 pixels
        private float _currentTime = 0f;
        
        // Copy animation
        private bool _showCopyAnimation = false;
        private float _copyAnimationTimer = 0f;
        private Vector2 _copyAnimationPosition = Vector2.Zero;
        private const float COPY_ANIMATION_DURATION = 2.0f; // 2 seconds
        private const float COPY_ANIMATION_FADE_START = 1.5f; // Start fading after 1.5 seconds
        private string _lastCopiedText = "";
        
        // Console text selection properties
        private bool _isConsoleSelecting = false;
        private int _consoleSelectionStartLine = -1;
        private int _consoleSelectionStartChar = -1;
        private int _consoleSelectionEndLine = -1;
        private int _consoleSelectionEndChar = -1;
        private bool _hasConsoleSelection = false;

        // PowerShell process
        private Process _powershellProcess;
        private StreamWriter _powershellInput;
        private StreamReader _powershellOutput;
        private StreamReader _powershellError;
        private bool _isPowerShellRunning = false;
        private Task _outputReaderTask;
        private Task _errorReaderTask;
        private readonly object _consoleLock = new object();

        // Colors
        private readonly Color CONSOLE_BACKGROUND = new Color(12, 12, 12); // Dark console background
        private readonly Color CONSOLE_TEXT = new Color(200, 200, 200); // Light gray text
        private readonly Color CONSOLE_INPUT = new Color(255, 255, 255); // White input text
        private readonly Color CONSOLE_ERROR = new Color(255, 100, 100); // Red error text
        private readonly Color CONSOLE_WARNING = new Color(255, 255, 100); // Yellow warning text
        private readonly Color CONSOLE_SUCCESS = new Color(100, 255, 100); // Green success text
        private readonly Color CONSOLE_CURSOR = new Color(255, 255, 255); // White cursor
        private readonly Color CONSOLE_BORDER = new Color(60, 60, 60); // Dark border

        // History
        private List<string> _commandHistory;
        private int _historyIndex = -1;
        private string _currentHistoryEntry = "";

        public Console(GraphicsDevice graphicsDevice, SpriteFont consoleFont, int windowWidth)
        {
            _graphicsDevice = graphicsDevice;
            _consoleFont = consoleFont;
            _windowWidth = windowWidth;
            _engine = GameEngine.Instance;

            // Initialize console properties
            _consoleLines = new List<string>();
            _currentInput = new StringBuilder();
            _commandHistory = new List<string>();
            _cursorPosition = 0;
            _scrollOffset = 0;
            _lineHeight = 16; // Default height, will be updated in LoadContent
            _maxVisibleLines = 20; // Will be calculated based on window size

            // Create pixel texture
            _pixel = new Texture2D(_graphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });

            // Initialize window management
            var properties = new WindowProperties
            {
                IsVisible = false,
                IsMovable = true,
                IsResizable = true
            };

            _windowManagement = new WindowManagement(
                _graphicsDevice,
                _consoleFont,
                _windowWidth,
                properties
            );

            // Set custom default size for console (much wider to prevent wrapping issues)
            // Access the private fields using reflection
            var defaultWidthField = _windowManagement.GetType().GetField("_defaultWidth", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var defaultHeightField = _windowManagement.GetType().GetField("_defaultHeight", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (defaultWidthField != null)
                defaultWidthField.SetValue(_windowManagement, 1200); // Much wider to prevent wrapping
            if (defaultHeightField != null)
                defaultHeightField.SetValue(_windowManagement, 300); // Slightly taller

            _windowManagement.SetWindowTitle("Console");
            _windowManagement.SetTaskBar(_taskBar);
            
            // Set custom minimum window size for console to prevent it from being too small
            _windowManagement.SetCustomMinimumSize(1200, 300);

            // Initialize console bounds with default values
            _consoleBounds = new Rectangle(0, 0, 400, 300); // Default bounds
            _inputBounds = new Rectangle(0, 0, 400, 25);

            // Initialize PowerShell process
            InitializePowerShell();

            // Add welcome message
            AddConsoleLine("MaryS Game Engine Console v0.0.1.7", CONSOLE_SUCCESS);
            AddConsoleLine("Type 'help' for available commands or 'exit' to close console", CONSOLE_TEXT);
            AddConsoleLine("", CONSOLE_TEXT);
        }

        private void InitializePowerShell()
        {
            try
            {
                // Get the Content/Desktop directory as the working directory
                string desktopDirectory = GetDesktopDirectory();
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoExit -NoProfile -NoLogo",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = desktopDirectory, // Start in Content/Desktop directory
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8
                };

                _powershellProcess = new Process { StartInfo = startInfo };
                _powershellProcess.EnableRaisingEvents = true;
                _powershellProcess.Exited += OnPowerShellExited;
                
                _powershellProcess.Start();

                _powershellInput = _powershellProcess.StandardInput;
                _powershellOutput = _powershellProcess.StandardOutput;
                _powershellError = _powershellProcess.StandardError;

                _isPowerShellRunning = true;

                // Start reading output and error streams
                _outputReaderTask = Task.Run(ReadPowerShellOutput);
                _errorReaderTask = Task.Run(ReadPowerShellError);

                AddConsoleLine("PowerShell initialized successfully", CONSOLE_SUCCESS);
            }
            catch (Exception ex)
            {
                AddConsoleLine($"Failed to initialize PowerShell: {ex.Message}", CONSOLE_ERROR);
                _isPowerShellRunning = false;
            }
        }

        private void OnPowerShellExited(object sender, EventArgs e)
        {
            _isPowerShellRunning = false;
            AddConsoleLine("PowerShell process exited", CONSOLE_WARNING);
        }


        private async Task ReadPowerShellOutput()
        {
            try
            {
                while (_isPowerShellRunning && !_powershellProcess.HasExited)
                {
                    var line = await _powershellOutput.ReadLineAsync();
                    if (line != null)
                    {
                        lock (_consoleLock)
                        {
                            AddConsoleLine(line, new Color(147, 112, 219)); // Purple color for PowerShell output
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                lock (_consoleLock)
                {
                    AddConsoleLine($"Output reader error: {ex.Message}", CONSOLE_ERROR);
                }
            }
        }

        private async Task ReadPowerShellError()
        {
            try
            {
                while (_isPowerShellRunning && !_powershellProcess.HasExited)
                {
                    var line = await _powershellError.ReadLineAsync();
                    if (line != null)
                    {
                        lock (_consoleLock)
                        {
                            AddConsoleLine(line, new Color(147, 112, 219)); // Purple color for PowerShell errors
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                lock (_consoleLock)
                {
                    AddConsoleLine($"Error reader error: {ex.Message}", CONSOLE_ERROR);
                }
            }
        }

        private void AddConsoleLine(string text, Color color)
        {
            lock (_consoleLock)
            {
                // Filter out unsupported characters to prevent SpriteFont errors
                string filteredText = FilterUnsupportedCharacters(text);
                
                // Handle line wrapping - use the actual console bounds width
                int availableWidth = _consoleBounds.Width - _consolePadding * 2;
                
                // Ensure we have a much larger minimum width to prevent wrapping issues
                availableWidth = Math.Max(availableWidth, 800);
                
                // Debug output for new lines
                System.Diagnostics.Debug.WriteLine($"Console AddConsoleLine: ConsoleBounds={_consoleBounds}, AvailableWidth={availableWidth}, ConsolePadding={_consolePadding}, Text='{filteredText}'");
                
                // Check if this is a special line that should not be wrapped
                bool isSpecialLine = filteredText.StartsWith("> ") || // Command prompt
                                   filteredText.StartsWith("PS ") || // PowerShell prompt
                                   string.IsNullOrWhiteSpace(filteredText) || // Empty line
                                   filteredText.StartsWith("MaryS Game Engine") || // Welcome message
                                   filteredText.StartsWith("Type 'help'") || // Instructions
                                   filteredText.StartsWith("PowerShell initialized") || // Status message
                                   filteredText.StartsWith("Available commands:") || // Help header
                                   filteredText.StartsWith("  ") || // Help items (indented)
                                   filteredText.StartsWith("All other commands") || // Help footer
                                   filteredText.StartsWith("Selected text:") || // Copy feedback
                                   filteredText.StartsWith("Note:") || // Copy note
                                   filteredText.StartsWith("Failed to copy") || // Error messages
                                   filteredText.StartsWith("Error executing") || // Error messages
                                   filteredText.StartsWith("PowerShell is not running") || // Error messages
                                   filteredText.StartsWith("Output reader error:") || // Error messages
                                   filteredText.StartsWith("Error reader error:"); // Error messages
                
                if (isSpecialLine)
                {
                    // Special lines are added as-is without wrapping
                    _consoleLines.Add(filteredText);
                }
                else
                {
                    // Regular lines can be wrapped if they're too long
                var lines = WrapText(filteredText, availableWidth);
                foreach (var line in lines)
                {
                    _consoleLines.Add(line);
                    }
                }

                // Auto-scroll if at bottom
                if (_autoScroll)
                {
                    int contentHeight = _consoleLines.Count * _lineHeight;
                    _scrollY = Math.Max(0, contentHeight - _consoleBounds.Height);
                }

                // Limit console history to prevent memory issues
                if (_consoleLines.Count > 1000)
                {
                    _consoleLines.RemoveRange(0, 100);
                    _scrollOffset = Math.Max(0, _scrollOffset - 100);
                }
            }
        }

        private string FilterUnsupportedCharacters(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Replace common problematic characters with safe alternatives
            return text
                .Replace("│", "|")  // Vertical line
                .Replace("├", "+")  // Tree connector
                .Replace("└", "+")  // Tree end
                .Replace("─", "-")  // Horizontal line
                .Replace("┌", "+")  // Corner
                .Replace("┐", "+")  // Corner
                .Replace("┘", "+")  // Corner
                .Replace("┼", "+")  // Cross
                .Replace("┤", "+")  // Right connector
                .Replace("┬", "+")  // Top connector
                .Replace("┴", "+")  // Bottom connector
                .Replace("█", "#")  // Block
                .Replace("░", ".")  // Light shade
                .Replace("▒", "*")  // Medium shade
                .Replace("▓", "#")  // Dark shade
                .Replace("°", "o")  // Degree symbol
                .Replace("±", "+")  // Plus-minus
                .Replace("×", "x")  // Multiplication
                .Replace("÷", "/")  // Division
                .Replace("≤", "<=") // Less than or equal
                .Replace("≥", ">=") // Greater than or equal
                .Replace("≠", "!=") // Not equal
                .Replace("∞", "inf") // Infinity
                .Replace("√", "sqrt") // Square root
                .Replace("∑", "sum") // Summation
                .Replace("∏", "prod") // Product
                .Replace("∫", "int") // Integral
                .Replace("∆", "delta") // Delta
                .Replace("α", "alpha") // Alpha
                .Replace("β", "beta") // Beta
                .Replace("γ", "gamma") // Gamma
                .Replace("δ", "delta") // Delta
                .Replace("ε", "epsilon") // Epsilon
                .Replace("ζ", "zeta") // Zeta
                .Replace("η", "eta") // Eta
                .Replace("θ", "theta") // Theta
                .Replace("ι", "iota") // Iota
                .Replace("κ", "kappa") // Kappa
                .Replace("λ", "lambda") // Lambda
                .Replace("μ", "mu") // Mu
                .Replace("ν", "nu") // Nu
                .Replace("ξ", "xi") // Xi
                .Replace("ο", "omicron") // Omicron
                .Replace("π", "pi") // Pi
                .Replace("ρ", "rho") // Rho
                .Replace("σ", "sigma") // Sigma
                .Replace("τ", "tau") // Tau
                .Replace("υ", "upsilon") // Upsilon
                .Replace("φ", "phi") // Phi
                .Replace("χ", "chi") // Chi
                .Replace("ψ", "psi") // Psi
                .Replace("ω", "omega"); // Omega
        }

        private List<string> WrapText(string text, int maxWidth)
        {
            var lines = new List<string>();
            
            // If maxWidth is too small, just return the text as-is
            if (maxWidth < 50)
            {
                lines.Add(text);
                return lines;
            }
            
            // Debug output for wrapping
            System.Diagnostics.Debug.WriteLine($"Console WrapText: maxWidth={maxWidth}, text='{text}'");
            
            var words = text.Split(' ');
            var currentLine = new StringBuilder();

            foreach (var word in words)
            {
                var testLine = currentLine.Length > 0 ? currentLine + " " + word : word;
                
                // Safe measurement with error handling
                float testWidth;
                try
                {
                    testWidth = _consoleFont.MeasureString(testLine).X;
                }
                catch
                {
                    // If measurement fails, assume it's too wide and break the line
                    testWidth = maxWidth + 1;
                }

                // Debug output for each word (only for long lines to avoid spam)
                if (text.Length > 50)
                {
                    System.Diagnostics.Debug.WriteLine($"Console WrapText: word='{word}', testLine='{testLine}', testWidth={testWidth}, maxWidth={maxWidth}");
                }

                if (testWidth <= maxWidth)
                {
                    if (currentLine.Length > 0)
                        currentLine.Append(" ");
                    currentLine.Append(word);
                }
                else
                {
                    if (currentLine.Length > 0)
                    {
                        lines.Add(currentLine.ToString());
                        currentLine.Clear();
                    }

                    // If single word is too long, break it
                    float wordWidth;
                    try
                    {
                        wordWidth = _consoleFont.MeasureString(word).X;
                    }
                    catch
                    {
                        // If measurement fails, assume it's too wide
                        wordWidth = maxWidth + 1;
                    }
                    
                    if (wordWidth > maxWidth)
                    {
                        var remaining = word;
                        while (remaining.Length > 0)
                        {
                            var fitLength = 0;
                            for (int i = 1; i <= remaining.Length; i++)
                            {
                                var test = remaining.Substring(0, i);
                                try
                                {
                                if (_consoleFont.MeasureString(test).X <= maxWidth)
                                    fitLength = i;
                                else
                                    break;
                                }
                                catch
                                {
                                    // If measurement fails, break at this point
                                    break;
                                }
                            }

                            if (fitLength > 0)
                            {
                                lines.Add(remaining.Substring(0, fitLength));
                                remaining = remaining.Substring(fitLength);
                            }
                            else
                            {
                                // If even one character doesn't fit, just add it anyway
                                lines.Add(remaining.Substring(0, 1));
                                remaining = remaining.Substring(1);
                            }
                        }
                    }
                    else
                    {
                        currentLine.Append(word);
                    }
                }
            }

            if (currentLine.Length > 0)
                lines.Add(currentLine.ToString());

            return lines;
        }

        public void SetTaskBar(TaskBar taskBar)
        {
            _taskBar = taskBar;
            _windowManagement?.SetTaskBar(taskBar);
        }

        public void Update()
        {
            _previousMouseState = _currentMouseState;
            _currentMouseState = Mouse.GetState();
            _previousKeyboardState = _currentKeyboardState;
            _currentKeyboardState = Keyboard.GetState();

            // Update time for double-click detection
            _currentTime += (float)GameEngine.Instance.TargetElapsedTime.TotalSeconds;

            // Update copy animation
            if (_showCopyAnimation)
            {
                _copyAnimationTimer += (float)GameEngine.Instance.TargetElapsedTime.TotalSeconds;
                if (_copyAnimationTimer >= COPY_ANIMATION_DURATION)
                {
                    _showCopyAnimation = false;
                    _copyAnimationTimer = 0f;
                }
            }

            _windowManagement?.Update();

            // Always update bounds, even when not visible, to handle resizing
            UpdateBounds();

            // If window is being resized, update bounds more aggressively and force re-wrap
            if (_windowManagement != null && _windowManagement.IsResizing())
            {
                UpdateBounds();
                // Force re-wrap during resizing
                if (_consoleLines.Count > 0)
                {
                    ReWrapAllText();
                }
            }

            if (!_windowManagement?.IsVisible() == true)
                return;

            // Check if console window is focused for input
            if (_windowManagement != null && _windowManagement.IsVisible())
            {
                // Check if this window is the topmost window under the mouse
                var mousePos = _currentMouseState.Position;
                if (_windowManagement.GetWindowBounds().Contains(mousePos))
                {
                    _isFocused = true;
                }
            }

            // Handle mouse clicks directly in Update method
            HandleMouseClicks();

            // Update scrolling
            UpdateScrolling();

            UpdateConsoleInput();
            UpdateCursor();
            
            // Handle console text selection
            if (_isConsoleSelecting && _currentMouseState.LeftButton == ButtonState.Pressed)
            {
                UpdateConsoleSelection(_currentMouseState.Position);
            }
            else if (_isConsoleSelecting && _previousMouseState.LeftButton == ButtonState.Pressed && _currentMouseState.LeftButton == ButtonState.Released)
            {
                _isConsoleSelecting = false;
            }
        }

        private void HandleMouseClicks()
        {
            // Handle mouse clicks for focus and text selection
            if (_currentMouseState.LeftButton == ButtonState.Pressed && 
                _previousMouseState.LeftButton == ButtonState.Released)
            {
                OnMouseClick(_currentMouseState.Position);
            }
        }

        private void UpdateScrolling()
        {
            if (!_windowManagement?.IsVisible() == true) return;

            // Calculate content height
            _contentHeight = _consoleLines.Count * _lineHeight;
            
            // Check if scrollbar is needed
            _needsScrollbar = _contentHeight > _consoleBounds.Height;
            
            if (_needsScrollbar)
            {
                // Update scrollbar bounds
                _scrollbarBounds = new Rectangle(
                    _consoleBounds.Right - SCROLLBAR_WIDTH - 2,
                    _consoleBounds.Y,
                    SCROLLBAR_WIDTH,
                    _consoleBounds.Height
                );
                
                // Handle scrollbar interaction
                HandleScrollbarInteraction();
                
                // Handle mouse wheel scrolling
                HandleMouseWheelScrolling();
                
                // Clamp scroll position
                int maxScroll = Math.Max(0, _contentHeight - _consoleBounds.Height);
                _scrollY = MathHelper.Clamp(_scrollY, 0, maxScroll);
            }
            else
            {
                _scrollY = 0;
                _scrollbarBounds = Rectangle.Empty;
            }
        }

        private void HandleScrollbarInteraction()
        {
            if (_scrollbarBounds.IsEmpty || !_scrollingEnabled) return;
            
            var mousePos = _currentMouseState.Position;
            bool leftPressed = _currentMouseState.LeftButton == ButtonState.Pressed;
            bool leftJustPressed = leftPressed && _previousMouseState.LeftButton == ButtonState.Released;
            
            if (leftJustPressed && _scrollbarBounds.Contains(mousePos))
            {
                _isDraggingScrollbar = true;
                _scrollbarDragStart = new Vector2(mousePos.X, mousePos.Y);
            }
            else if (leftPressed && _isDraggingScrollbar)
            {
                // Handle scrollbar dragging
                float deltaY = mousePos.Y - _scrollbarDragStart.Y;
                float maxScroll = _contentHeight - _consoleBounds.Height;
                float scrollBarHeight = _scrollbarBounds.Height;
                
                // Calculate new scroll position based on mouse position relative to scrollbar
                float mouseRatio = (mousePos.Y - _scrollbarBounds.Y) / scrollBarHeight;
                mouseRatio = Math.Max(0, Math.Min(1, mouseRatio));
                
                _scrollY = (int)(mouseRatio * maxScroll);
                _scrollbarDragStart = new Vector2(mousePos.X, mousePos.Y);
            }
            else if (!leftPressed && _isDraggingScrollbar)
            {
                _isDraggingScrollbar = false;
            }
            
            // Check if hovering over scrollbar
            _isHoveringScrollbar = _scrollbarBounds.Contains(mousePos);
        }

        private void HandleMouseWheelScrolling()
        {
            if (!_needsScrollbar || !_scrollingEnabled) return;
            
            int scrollDelta = _currentMouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
            if (scrollDelta != 0)
            {
                int scrollStep = scrollDelta > 0 ? -20 : 20; // Scroll up/down
                _scrollY += scrollStep;
                
                // Clamp scroll position
                int maxScroll = Math.Max(0, _contentHeight - _consoleBounds.Height);
                _scrollY = MathHelper.Clamp(_scrollY, 0, maxScroll);
            }
        }

        // Method to force bounds update when window becomes visible
        public void OnWindowVisibilityChanged(bool isVisible)
        {
            if (isVisible)
            {
                // Force bounds update when window becomes visible
                UpdateBounds();
                // Re-wrap all text with the correct bounds
                if (_consoleLines.Count > 0)
                {
                    ReWrapAllText();
                }
            }
        }

        private void UpdateConsoleInput()
        {
            if (!_isFocused)
                return;

            // Handle keyboard input
            var pressedKeys = GetPressedKeys();
            foreach (var key in pressedKeys)
            {
                HandleKeyPress(key);
            }
        }

        private List<Keys> GetPressedKeys()
        {
            var pressedKeys = new List<Keys>();
            var currentKeys = _currentKeyboardState.GetPressedKeys();

            foreach (var key in currentKeys)
            {
                if (_previousKeyboardState.IsKeyUp(key))
                {
                    pressedKeys.Add(key);
                }
            }

            return pressedKeys;
        }

        private void HandleKeyPress(Keys key)
        {
            // Handle Ctrl+C for copying
            if (key == Keys.C && (_currentKeyboardState.IsKeyDown(Keys.LeftControl) || _currentKeyboardState.IsKeyDown(Keys.RightControl)))
            {
                try
                {
                    CopySelectedText();
                }
                catch (Exception ex)
                {
                    // If copy fails, just log the error and continue
                    AddConsoleLine($"Copy operation failed: {ex.Message}", CONSOLE_ERROR);
                }
                return;
            }

            // Handle Ctrl+V for pasting
            if (key == Keys.V && (_currentKeyboardState.IsKeyDown(Keys.LeftControl) || _currentKeyboardState.IsKeyDown(Keys.RightControl)))
            {
                try
                {
                    PasteFromClipboard();
                }
                catch (Exception ex)
                {
                    // If paste fails, just log the error and continue
                    AddConsoleLine($"Paste operation failed: {ex.Message}", CONSOLE_ERROR);
                }
                return;
            }

            // Handle Ctrl+A for selecting all
            if (key == Keys.A && (_currentKeyboardState.IsKeyDown(Keys.LeftControl) || _currentKeyboardState.IsKeyDown(Keys.RightControl)))
            {
                SelectAllText();
                return;
            }

            // Clear selection when typing
            if (_hasSelection && IsPrintableKey(key))
            {
                ClearSelection();
            }

            switch (key)
            {
                case Keys.Enter:
                    ExecuteCommand();
                    break;

                case Keys.Back:
                    if (_currentInput.Length > 0 && _cursorPosition > 0)
                    {
                        _currentInput.Remove(_cursorPosition - 1, 1);
                        _cursorPosition--;
                    }
                    break;

                case Keys.Delete:
                    if (_currentInput.Length > 0 && _cursorPosition < _currentInput.Length)
                    {
                        _currentInput.Remove(_cursorPosition, 1);
                    }
                    break;

                case Keys.Left:
                    if (_currentKeyboardState.IsKeyDown(Keys.LeftShift) || _currentKeyboardState.IsKeyDown(Keys.RightShift))
                    {
                        ExtendSelection(-1);
                    }
                    else
                    {
                        ClearSelection();
                    if (_cursorPosition > 0)
                        _cursorPosition--;
                    }
                    break;

                case Keys.Right:
                    if (_currentKeyboardState.IsKeyDown(Keys.LeftShift) || _currentKeyboardState.IsKeyDown(Keys.RightShift))
                    {
                        ExtendSelection(1);
                    }
                    else
                    {
                        ClearSelection();
                    if (_cursorPosition < _currentInput.Length)
                        _cursorPosition++;
                    }
                    break;

                case Keys.Home:
                    if (_currentKeyboardState.IsKeyDown(Keys.LeftShift) || _currentKeyboardState.IsKeyDown(Keys.RightShift))
                    {
                        ExtendSelectionToStart();
                    }
                    else
                    {
                        ClearSelection();
                    _cursorPosition = 0;
                    }
                    break;

                case Keys.End:
                    if (_currentKeyboardState.IsKeyDown(Keys.LeftShift) || _currentKeyboardState.IsKeyDown(Keys.RightShift))
                    {
                        ExtendSelectionToEnd();
                    }
                    else
                    {
                        ClearSelection();
                    _cursorPosition = _currentInput.Length;
                    }
                    break;

                case Keys.Up:
                    NavigateHistory(-1);
                    break;

                case Keys.Down:
                    NavigateHistory(1);
                    break;

                case Keys.PageUp:
                    ScrollConsole(-5);
                    break;

                case Keys.PageDown:
                    ScrollConsole(5);
                    break;

                default:
                    // Handle printable characters
                    if (IsPrintableKey(key))
                    {
                        var character = GetCharacterFromKey(key);
                        if (character != '\0')
                        {
                            _currentInput.Insert(_cursorPosition, character);
                            _cursorPosition++;
                        }
                    }
                    break;
            }
        }

        private bool IsPrintableKey(Keys key)
        {
            return (key >= Keys.A && key <= Keys.Z) ||
                   (key >= Keys.D0 && key <= Keys.D9) ||
                   key == Keys.Space ||
                   key == Keys.OemMinus ||
                   key == Keys.OemPlus ||
                   key == Keys.OemOpenBrackets ||
                   key == Keys.OemCloseBrackets ||
                   key == Keys.OemSemicolon ||
                   key == Keys.OemQuotes ||
                   key == Keys.OemComma ||
                   key == Keys.OemPeriod ||
                   key == Keys.OemQuestion ||
                   key == Keys.OemTilde ||
                   key == Keys.OemBackslash ||
                   key == Keys.OemPipe;
        }

        private char GetCharacterFromKey(Keys key)
        {
            var shiftPressed = _currentKeyboardState.IsKeyDown(Keys.LeftShift) || _currentKeyboardState.IsKeyDown(Keys.RightShift);

            switch (key)
            {
                case Keys.Space:
                    return ' ';

                case Keys.D0:
                    return shiftPressed ? ')' : '0';
                case Keys.D1:
                    return shiftPressed ? '!' : '1';
                case Keys.D2:
                    return shiftPressed ? '@' : '2';
                case Keys.D3:
                    return shiftPressed ? '#' : '3';
                case Keys.D4:
                    return shiftPressed ? '$' : '4';
                case Keys.D5:
                    return shiftPressed ? '%' : '5';
                case Keys.D6:
                    return shiftPressed ? '^' : '6';
                case Keys.D7:
                    return shiftPressed ? '&' : '7';
                case Keys.D8:
                    return shiftPressed ? '*' : '8';
                case Keys.D9:
                    return shiftPressed ? '(' : '9';

                case Keys.A:
                    return shiftPressed ? 'A' : 'a';
                case Keys.B:
                    return shiftPressed ? 'B' : 'b';
                case Keys.C:
                    return shiftPressed ? 'C' : 'c';
                case Keys.D:
                    return shiftPressed ? 'D' : 'd';
                case Keys.E:
                    return shiftPressed ? 'E' : 'e';
                case Keys.F:
                    return shiftPressed ? 'F' : 'f';
                case Keys.G:
                    return shiftPressed ? 'G' : 'g';
                case Keys.H:
                    return shiftPressed ? 'H' : 'h';
                case Keys.I:
                    return shiftPressed ? 'I' : 'i';
                case Keys.J:
                    return shiftPressed ? 'J' : 'j';
                case Keys.K:
                    return shiftPressed ? 'K' : 'k';
                case Keys.L:
                    return shiftPressed ? 'L' : 'l';
                case Keys.M:
                    return shiftPressed ? 'M' : 'm';
                case Keys.N:
                    return shiftPressed ? 'N' : 'n';
                case Keys.O:
                    return shiftPressed ? 'O' : 'o';
                case Keys.P:
                    return shiftPressed ? 'P' : 'p';
                case Keys.Q:
                    return shiftPressed ? 'Q' : 'q';
                case Keys.R:
                    return shiftPressed ? 'R' : 'r';
                case Keys.S:
                    return shiftPressed ? 'S' : 's';
                case Keys.T:
                    return shiftPressed ? 'T' : 't';
                case Keys.U:
                    return shiftPressed ? 'U' : 'u';
                case Keys.V:
                    return shiftPressed ? 'V' : 'v';
                case Keys.W:
                    return shiftPressed ? 'W' : 'w';
                case Keys.X:
                    return shiftPressed ? 'X' : 'x';
                case Keys.Y:
                    return shiftPressed ? 'Y' : 'y';
                case Keys.Z:
                    return shiftPressed ? 'Z' : 'z';

                case Keys.OemMinus:
                    return shiftPressed ? '_' : '-';
                case Keys.OemPlus:
                    return shiftPressed ? '+' : '=';
                case Keys.OemOpenBrackets:
                    return shiftPressed ? '{' : '[';
                case Keys.OemCloseBrackets:
                    return shiftPressed ? '}' : ']';
                case Keys.OemSemicolon:
                    return shiftPressed ? ':' : ';';
                case Keys.OemQuotes:
                    return shiftPressed ? '"' : '\'';
                case Keys.OemComma:
                    return shiftPressed ? '<' : ',';
                case Keys.OemPeriod:
                    return shiftPressed ? '>' : '.';
                case Keys.OemQuestion:
                    return shiftPressed ? '?' : '/';
                case Keys.OemTilde:
                    return shiftPressed ? '~' : '`';
                case Keys.OemBackslash:
                    return shiftPressed ? '|' : '\\';
                case Keys.OemPipe:
                    return shiftPressed ? '|' : '\\';

                default:
                    return '\0';
            }
        }

        private bool IsDirectoryChangeCommand(string command)
        {
            if (string.IsNullOrEmpty(command))
                return false;

            string lowerCommand = command.ToLower().Trim();
            
            // Block specific dangerous commands that would go outside Content/Desktop
            if (lowerCommand == "cd" || // cd without arguments goes to home
                lowerCommand.StartsWith("cd /") || // Go to root
                lowerCommand.StartsWith("cd \\") || // Go to root (Windows)
                lowerCommand.StartsWith("cd c:") || // Go to C drive
                lowerCommand.StartsWith("cd d:") || // Go to D drive
                lowerCommand.StartsWith("cd e:") || // Go to E drive
                lowerCommand.StartsWith("cd f:") || // Go to F drive
                lowerCommand.StartsWith("cd g:") || // Go to G drive
                lowerCommand.StartsWith("cd h:") || // Go to H drive
                lowerCommand.StartsWith("cd i:") || // Go to I drive
                lowerCommand.StartsWith("cd j:") || // Go to J drive
                lowerCommand.StartsWith("cd k:") || // Go to K drive
                lowerCommand.StartsWith("cd l:") || // Go to L drive
                lowerCommand.StartsWith("cd m:") || // Go to M drive
                lowerCommand.StartsWith("cd n:") || // Go to N drive
                lowerCommand.StartsWith("cd o:") || // Go to O drive
                lowerCommand.StartsWith("cd p:") || // Go to P drive
                lowerCommand.StartsWith("cd q:") || // Go to Q drive
                lowerCommand.StartsWith("cd r:") || // Go to R drive
                lowerCommand.StartsWith("cd s:") || // Go to S drive
                lowerCommand.StartsWith("cd t:") || // Go to T drive
                lowerCommand.StartsWith("cd u:") || // Go to U drive
                lowerCommand.StartsWith("cd v:") || // Go to V drive
                lowerCommand.StartsWith("cd w:") || // Go to W drive
                lowerCommand.StartsWith("cd x:") || // Go to X drive
                lowerCommand.StartsWith("cd y:") || // Go to Y drive
                lowerCommand.StartsWith("cd z:") || // Go to Z drive
                lowerCommand == "pushd" || // pushd without arguments
                lowerCommand.StartsWith("popd") || // popd commands
                lowerCommand == "set-location" || // set-location without arguments
                lowerCommand == "sl") // sl without arguments
            {
                return true;
            }
            
            // Check for commands that might go outside Content/Desktop
            if (lowerCommand.StartsWith("cd ") || lowerCommand.StartsWith("pushd ") || 
                lowerCommand.StartsWith("set-location ") || lowerCommand.StartsWith("sl "))
            {
                string targetPath = ExtractPathFromCommand(command);
                if (!string.IsNullOrEmpty(targetPath))
                {
                    return !IsPathWithinDesktopDirectory(targetPath);
                }
            }
            
            return false;
        }

        private string ExtractPathFromCommand(string command)
        {
            // Extract the path from commands like "cd path", "pushd path", etc.
            string[] parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1)
            {
                return parts[1];
            }
            return "";
        }

        private bool IsPathWithinDesktopDirectory(string targetPath)
        {
            try
            {
                // Get the Content/Desktop directory as the root boundary
                string desktopDirectory = GetDesktopDirectory();
                
                if (string.IsNullOrEmpty(desktopDirectory))
                {
                    return false; // Can't determine desktop directory
                }
                
                // For relative paths, we need to check if they would stay within desktop
                if (!Path.IsPathRooted(targetPath))
                {
                    // Count how many levels up the path goes
                    int upLevels = 0;
                    string remainingPath = targetPath;
                    while (remainingPath.StartsWith(".."))
                    {
                        upLevels++;
                        remainingPath = remainingPath.Substring(3).TrimStart('\\', '/');
                    }
                    
                    // If it goes up more than 2 levels from Content/Desktop, it's outside
                    // Content/Desktop -> Content -> MarySGameEngine (2 levels up)
                    if (upLevels > 2)
                    {
                        return false;
                    }
                    
                    // Allow relative navigation within the desktop structure
                    return true;
                }
                
                // For absolute paths, check if they're within the desktop directory
                string fullTargetPath = Path.GetFullPath(targetPath);
                return fullTargetPath.StartsWith(desktopDirectory, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                // If there's any error in path resolution, block the command for safety
                return false;
            }
        }


        private string GetDesktopDirectory()
        {
            try
            {
                // Find the Content/Desktop directory
                string currentDir = Directory.GetCurrentDirectory();
                
                // Look for Content/Desktop in the current path or parent directories
                string searchDir = currentDir;
                while (!string.IsNullOrEmpty(searchDir))
                {
                    string desktopPath = Path.Combine(searchDir, "Content", "Desktop");
                    if (Directory.Exists(desktopPath))
                    {
                        return desktopPath;
                    }
                    
                    string parentDir = Directory.GetParent(searchDir)?.FullName;
                    if (string.IsNullOrEmpty(parentDir) || parentDir == searchDir)
                    {
                        break;
                    }
                    searchDir = parentDir;
                }
                
                // Fallback: try to construct the path from current directory
                // Assuming we're in the engine directory structure
                string fallbackPath = Path.Combine(currentDir, "Content", "Desktop");
                if (Directory.Exists(fallbackPath))
                {
                    return fallbackPath;
                }
                
                // If we can't find Content/Desktop, use the current directory as fallback
                return currentDir;
            }
            catch
            {
                // If there's any error, use current directory as fallback
                return Directory.GetCurrentDirectory();
            }
        }

        private void ExecuteCommand()
        {
            var command = _currentInput.ToString().Trim();
            if (string.IsNullOrEmpty(command))
            {
                AddConsoleLine("", CONSOLE_TEXT);
                return;
            }

            // Add command to history
            if (_commandHistory.Count == 0 || _commandHistory[_commandHistory.Count - 1] != command)
            {
                _commandHistory.Add(command);
                if (_commandHistory.Count > 100)
                    _commandHistory.RemoveAt(0);
            }

            _historyIndex = _commandHistory.Count;

            // Display the command in purple
            AddConsoleLine($"> {command}", new Color(147, 112, 219)); // Purple color

            // Check for directory change commands and restrict them
            if (IsDirectoryChangeCommand(command))
            {
                AddConsoleLine("Directory navigation is restricted to the Content/Desktop directory.", CONSOLE_WARNING);
                _currentInput.Clear();
                _cursorPosition = 0;
                return;
            }

            // Handle special commands
            if (command.ToLower() == "exit" || command.ToLower() == "quit")
            {
                _windowManagement?.SetVisible(false);
                
                // Notify TaskBar to remove the console icon
                if (_taskBar != null)
                {
                    _taskBar.RemoveModuleIcon("Console");
                }
                
                _currentInput.Clear();
                _cursorPosition = 0;
                return;
            }

            if (command.ToLower() == "clear")
            {
                lock (_consoleLock)
                {
                    _consoleLines.Clear();
                    _scrollOffset = 0;
                }
                _currentInput.Clear();
                _cursorPosition = 0;
                return;
            }

            if (command.ToLower() == "help")
            {
                AddConsoleLine("Available commands:", CONSOLE_TEXT);
                AddConsoleLine("  help     - Show this help message", CONSOLE_TEXT);
                AddConsoleLine("  clear    - Clear the console", CONSOLE_TEXT);
                AddConsoleLine("  exit     - Close the console", CONSOLE_TEXT);
                AddConsoleLine("  quit     - Close the console", CONSOLE_TEXT);
                AddConsoleLine("", CONSOLE_TEXT);
                AddConsoleLine("All other commands are executed in PowerShell", CONSOLE_TEXT);
            }
            else if (_isPowerShellRunning)
            {
                // Send command to PowerShell
                try
                {
                    if (_powershellInput != null && !_powershellInput.BaseStream.CanWrite)
                    {
                        AddConsoleLine("PowerShell input stream is closed. Reinitializing...", CONSOLE_WARNING);
                        InitializePowerShell();
                        return;
                    }
                    
                    _powershellInput.WriteLine(command);
                    _powershellInput.Flush();
                }
                catch (Exception ex)
                {
                    AddConsoleLine($"Error executing command: {ex.Message}", CONSOLE_ERROR);
                    if (ex.Message.Contains("pipe") || ex.Message.Contains("closed"))
                    {
                        AddConsoleLine("PowerShell pipe closed. Attempting to reinitialize...", CONSOLE_WARNING);
                        _isPowerShellRunning = false;
                        InitializePowerShell();
                    }
                }
            }
            else
            {
                AddConsoleLine("PowerShell is not running. Attempting to reinitialize...", CONSOLE_WARNING);
                InitializePowerShell();
            }

            _currentInput.Clear();
            _cursorPosition = 0;
        }

        private void NavigateHistory(int direction)
        {
            if (_commandHistory.Count == 0)
                return;

            if (direction < 0) // Up
            {
                if (_historyIndex > 0)
                {
                    if (_historyIndex == _commandHistory.Count)
                        _currentHistoryEntry = _currentInput.ToString();
                    _historyIndex--;
                    _currentInput.Clear();
                    _currentInput.Append(_commandHistory[_historyIndex]);
                    _cursorPosition = _currentInput.Length;
                }
            }
            else // Down
            {
                if (_historyIndex < _commandHistory.Count - 1)
                {
                    _historyIndex++;
                    _currentInput.Clear();
                    _currentInput.Append(_commandHistory[_historyIndex]);
                    _cursorPosition = _currentInput.Length;
                }
                else if (_historyIndex == _commandHistory.Count - 1)
                {
                    _historyIndex = _commandHistory.Count;
                    _currentInput.Clear();
                    _currentInput.Append(_currentHistoryEntry);
                    _cursorPosition = _currentInput.Length;
                }
            }
        }

        private void ScrollConsole(int lines)
        {
            _autoScroll = false;
            int scrollStep = lines * _lineHeight;
            _scrollY = Math.Max(0, Math.Min(_scrollY + scrollStep, Math.Max(0, _contentHeight - _consoleBounds.Height)));
        }

        private void UpdateCursor()
        {
            _cursorBlinkTimer += (float)GameEngine.Instance.TargetElapsedTime.TotalSeconds;
            if (_cursorBlinkTimer >= CURSOR_BLINK_INTERVAL)
            {
                _showCursor = !_showCursor;
                _cursorBlinkTimer = 0f;
            }
        }

        private void UpdateBounds()
        {
            if (_windowManagement != null)
            {
                var windowBounds = _windowManagement.GetWindowBounds();
                
                // Enforce minimum console size - much larger minimums to prevent wrapping issues
                const int MIN_CONSOLE_WIDTH = 600;  // Much larger minimum width
                const int MIN_CONSOLE_HEIGHT = 200; // Reasonable minimum height
                
                // Calculate console bounds with proper margins and minimum size enforcement
                int consoleX = windowBounds.X + _consoleMargin;
                int consoleY = windowBounds.Y + _consoleMargin + 40; // Account for title bar
                int consoleWidth = Math.Max(MIN_CONSOLE_WIDTH, windowBounds.Width - _consoleMargin * 2);
                int consoleHeight = Math.Max(MIN_CONSOLE_HEIGHT, windowBounds.Height - _consoleMargin * 2 - 40 - 45); // Account for title bar, input area, and padding
                
                var newConsoleBounds = new Rectangle(consoleX, consoleY, consoleWidth, consoleHeight);

                // Check if the console width or height has changed significantly
                bool widthChanged = Math.Abs(_consoleBounds.Width - newConsoleBounds.Width) > 0;
                bool heightChanged = Math.Abs(_consoleBounds.Height - newConsoleBounds.Height) > 0;
                
                // Debug output for bounds changes
                if (widthChanged || heightChanged)
                {
                    System.Diagnostics.Debug.WriteLine($"Console UpdateBounds: Width changed from {_consoleBounds.Width} to {newConsoleBounds.Width}, Height changed from {_consoleBounds.Height} to {newConsoleBounds.Height}, WindowBounds={windowBounds}");
                }
                
                _consoleBounds = newConsoleBounds;

                _inputBounds = new Rectangle(
                    _consoleBounds.X,
                    _consoleBounds.Bottom + 15, // Increased padding from 5 to 15
                    _consoleBounds.Width,
                    25
                );

                _maxVisibleLines = _consoleBounds.Height / _lineHeight;

                // If width changed significantly and we have console lines, re-wrap them
                if (widthChanged && _consoleLines.Count > 0 && _consoleBounds.Width > 50)
                {
                    System.Diagnostics.Debug.WriteLine($"Console UpdateBounds: Triggering ReWrapAllText due to width change");
                    ReWrapAllText();
                }
            }
        }

        private void ReWrapAllText()
        {
            lock (_consoleLock)
            {
                // Store the original lines and clear the current ones
                var originalLines = new List<string>(_consoleLines);
                _consoleLines.Clear();

                // Re-wrap all text with the new width - use actual console bounds
                int availableWidth = _consoleBounds.Width - _consolePadding * 2;
                
                // Ensure we have a much larger minimum width to prevent wrapping issues
                availableWidth = Math.Max(availableWidth, 800);
                
                // Debug output
                System.Diagnostics.Debug.WriteLine($"Console ReWrapAllText: ConsoleBounds={_consoleBounds}, AvailableWidth={availableWidth}, ConsolePadding={_consolePadding}");
                
                // Process each line individually to preserve line breaks
                foreach (var line in originalLines)
                {
                    // Check if this is a special line that should not be wrapped
                    bool isSpecialLine = line.StartsWith("> ") || // Command prompt
                                       line.StartsWith("PS ") || // PowerShell prompt
                                       string.IsNullOrWhiteSpace(line) || // Empty line
                                       line.StartsWith("MaryS Game Engine") || // Welcome message
                                       line.StartsWith("Type 'help'") || // Instructions
                                       line.StartsWith("PowerShell initialized") || // Status message
                                       line.StartsWith("Available commands:") || // Help header
                                       line.StartsWith("  ") || // Help items (indented)
                                       line.StartsWith("All other commands") || // Help footer
                                       line.StartsWith("Selected text:") || // Copy feedback
                                       line.StartsWith("Note:") || // Copy note
                                       line.StartsWith("Failed to copy") || // Error messages
                                       line.StartsWith("Error executing") || // Error messages
                                       line.StartsWith("PowerShell is not running") || // Error messages
                                       line.StartsWith("Output reader error:") || // Error messages
                                       line.StartsWith("Error reader error:"); // Error messages
                    
                    if (isSpecialLine)
                    {
                        // Special lines are added as-is without wrapping
                        _consoleLines.Add(line);
                    }
                    else
                    {
                        // Regular lines can be wrapped if they're too long
                        var wrappedLines = WrapText(line, availableWidth);
                        foreach (var wrappedLine in wrappedLines)
                        {
                            _consoleLines.Add(wrappedLine);
                        }
                    }
                }

                // Adjust scroll position to maintain relative position
                if (_autoScroll)
                {
                    int contentHeight = _consoleLines.Count * _lineHeight;
                    _scrollY = Math.Max(0, contentHeight - _consoleBounds.Height);
                }
                else
                {
                    // Try to maintain the current scroll position proportionally
                    if (originalLines.Count > 0)
                    {
                        int originalContentHeight = originalLines.Count * _lineHeight;
                        float scrollRatio = (float)_scrollY / Math.Max(1, originalContentHeight - _consoleBounds.Height);
                        int newContentHeight = _consoleLines.Count * _lineHeight;
                        _scrollY = Math.Max(0, Math.Min((int)(scrollRatio * Math.Max(1, newContentHeight - _consoleBounds.Height)), Math.Max(0, newContentHeight - _consoleBounds.Height)));
                    }
                }
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            if (_windowManagement?.IsVisible() != true)
                return;

            _windowManagement.Draw(spriteBatch, "Console");

            // Draw console background
            spriteBatch.Draw(_pixel, _consoleBounds, CONSOLE_BACKGROUND);

            // Draw console border
            DrawBorder(spriteBatch, _consoleBounds, CONSOLE_BORDER);

            // Draw console text
            DrawConsoleText(spriteBatch);

            // Draw scrollbar if needed
            if (_needsScrollbar)
            {
                DrawScrollbar(spriteBatch);
            }

            // Draw input area
            DrawInputArea(spriteBatch);

            // Draw copy animation
            if (_showCopyAnimation)
            {
                DrawCopyAnimation(spriteBatch);
            }
        }

        private void DrawBorder(SpriteBatch spriteBatch, Rectangle bounds, Color color)
        {
            const int borderThickness = 1;
            
            // Top
            spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, borderThickness), color);
            // Bottom
            spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Bottom - borderThickness, bounds.Width, borderThickness), color);
            // Left
            spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, borderThickness, bounds.Height), color);
            // Right
            spriteBatch.Draw(_pixel, new Rectangle(bounds.Right - borderThickness, bounds.Y, borderThickness, bounds.Height), color);
        }

        private void DrawConsoleText(SpriteBatch spriteBatch)
        {
            lock (_consoleLock)
            {
                // Calculate which lines to draw based on scroll position
                int startLine = _scrollY / _lineHeight;
                int endLine = Math.Min(startLine + _maxVisibleLines + 1, _consoleLines.Count); // +1 to ensure we don't miss lines

                for (int i = startLine; i < endLine; i++)
                {
                    var line = _consoleLines[i];
                    var y = _consoleBounds.Y + _consolePadding + (i - startLine) * _lineHeight - (_scrollY % _lineHeight);
                    var position = new Vector2(_consoleBounds.X + _consolePadding, y);

                    // Skip drawing if the line would be too close to the input area
                    if (y + _lineHeight > _inputBounds.Y - 10) // 10px buffer before input area
                    {
                        break;
                    }

                    // Draw selection background if this line is selected
                    if (_hasConsoleSelection && i >= Math.Min(_consoleSelectionStartLine, _consoleSelectionEndLine) && 
                        i <= Math.Max(_consoleSelectionStartLine, _consoleSelectionEndLine))
                    {
                        DrawConsoleLineSelection(spriteBatch, line, position, i);
                    }

                    // Determine color based on line content
                    Color lineColor = CONSOLE_TEXT;
                    if (line.StartsWith("> "))
                        lineColor = CONSOLE_INPUT;
                    else if (line.ToLower().Contains("error") || line.ToLower().Contains("exception"))
                        lineColor = CONSOLE_ERROR;
                    else if (line.ToLower().Contains("warning"))
                        lineColor = CONSOLE_WARNING;
                    else if (line.ToLower().Contains("success") || line.ToLower().Contains("completed"))
                        lineColor = CONSOLE_SUCCESS;

                    // Safe text rendering with error handling
                    try
                    {
                    spriteBatch.DrawString(_consoleFont, line, position, lineColor);
                }
                    catch
                    {
                        // If rendering fails, try to render a safe version
                        try
                        {
                            string safeLine = FilterUnsupportedCharacters(line);
                            spriteBatch.DrawString(_consoleFont, safeLine, position, lineColor);
                        }
                        catch
                        {
                            // If even the safe version fails, render a placeholder
                            spriteBatch.DrawString(_consoleFont, "[Unsupported characters]", position, lineColor);
                        }
                    }
                }
            }
        }

        private void DrawConsoleLineSelection(SpriteBatch spriteBatch, string line, Vector2 position, int lineIndex)
        {
            // Determine the actual start and end based on selection direction
            bool isReversed = (_consoleSelectionStartLine > _consoleSelectionEndLine) || 
                             (_consoleSelectionStartLine == _consoleSelectionEndLine && _consoleSelectionStartChar > _consoleSelectionEndChar);
            
            int startChar, endChar;
            
            if (lineIndex == _consoleSelectionStartLine && lineIndex == _consoleSelectionEndLine)
            {
                // Single line selection
                if (isReversed)
                {
                    startChar = _consoleSelectionEndChar;
                    endChar = _consoleSelectionStartChar;
                }
                else
                {
                    startChar = _consoleSelectionStartChar;
                    endChar = _consoleSelectionEndChar;
                }
            }
            else if (lineIndex == _consoleSelectionStartLine)
            {
                // First line of multi-line selection
                startChar = isReversed ? 0 : _consoleSelectionStartChar;
                endChar = isReversed ? _consoleSelectionStartChar : line.Length;
            }
            else if (lineIndex == _consoleSelectionEndLine)
            {
                // Last line of multi-line selection
                startChar = isReversed ? _consoleSelectionEndChar : 0;
                endChar = isReversed ? line.Length : _consoleSelectionEndChar;
            }
            else
            {
                // Middle line of multi-line selection - select entire line
                startChar = 0;
                endChar = line.Length;
            }

            if (startChar < endChar && startChar < line.Length)
            {
                var beforeSelection = line.Substring(0, startChar);
                var selectionText = line.Substring(startChar, Math.Min(endChar - startChar, line.Length - startChar));

                var selectionX = position.X + _consoleFont.MeasureString(beforeSelection).X;
                var selectionWidth = _consoleFont.MeasureString(selectionText).X;
                var selectionHeight = _consoleFont.MeasureString("A").Y;

                // Draw selection background
                spriteBatch.Draw(_pixel, new Rectangle((int)selectionX, (int)position.Y, (int)selectionWidth, (int)selectionHeight), Color.Blue);
            }
        }

        private void DrawScrollbar(SpriteBatch spriteBatch)
        {
            // Draw scrollbar background
            spriteBatch.Draw(_pixel, _scrollbarBounds, new Color(60, 60, 60, 200));
            
            // Calculate scrollbar thumb size and position
            float scrollRatio = (float)_scrollY / Math.Max(1, _contentHeight - _consoleBounds.Height);
            int thumbHeight = Math.Max(20, (int)(_scrollbarBounds.Height * (_consoleBounds.Height / (float)_contentHeight)));
            int thumbY = _scrollbarBounds.Y + (int)((_scrollbarBounds.Height - thumbHeight) * scrollRatio);
            
            Rectangle thumbBounds = new Rectangle(
                _scrollbarBounds.X + 2,
                thumbY,
                _scrollbarBounds.Width - 4,
                thumbHeight
            );
            
            // Draw scrollbar thumb
            Color thumbColor = _isDraggingScrollbar ? new Color(180, 145, 250) : new Color(147, 112, 219);
            spriteBatch.Draw(_pixel, thumbBounds, thumbColor);
            
            // Draw scrollbar border
            Color scrollBorderColor = new Color(100, 100, 100);
            spriteBatch.Draw(_pixel, new Rectangle(_scrollbarBounds.X, _scrollbarBounds.Y, 1, _scrollbarBounds.Height), scrollBorderColor);
        }

        private void DrawInputArea(SpriteBatch spriteBatch)
        {
            // Draw input background
            spriteBatch.Draw(_pixel, _inputBounds, CONSOLE_BACKGROUND);
            DrawBorder(spriteBatch, _inputBounds, CONSOLE_BORDER);

            // Draw prompt - show only Desktop/ instead of full path
            var promptText = "PS Desktop> ";
            var promptPosition = new Vector2(_inputBounds.X + 5, _inputBounds.Y + 5);
            spriteBatch.DrawString(_consoleFont, promptText, promptPosition, CONSOLE_SUCCESS);

            // Draw input text
            var inputText = _currentInput.ToString();
            var inputPosition = new Vector2(promptPosition.X + _consoleFont.MeasureString(promptText).X, promptPosition.Y);
            
            // Draw text selection background if there's a selection
            if (_hasSelection && _selectionStart >= 0 && _selectionEnd >= 0)
            {
                var selectionStart = Math.Min(_selectionStart, _selectionEnd);
                var selectionLength = Math.Abs(_selectionEnd - _selectionStart);
                
                if (selectionLength > 0)
                {
                    var beforeSelection = inputText.Substring(0, selectionStart);
                    var selectionText = inputText.Substring(selectionStart, selectionLength);
                    
                    var selectionX = inputPosition.X + _consoleFont.MeasureString(beforeSelection).X;
                    var selectionWidth = _consoleFont.MeasureString(selectionText).X;
                    var selectionHeight = _consoleFont.MeasureString("A").Y;
                    
                    // Draw selection background
                    spriteBatch.Draw(_pixel, new Rectangle((int)selectionX, (int)inputPosition.Y, (int)selectionWidth, (int)selectionHeight), Color.Blue);
                }
            }
            
            spriteBatch.DrawString(_consoleFont, inputText, inputPosition, CONSOLE_INPUT);

            // Draw cursor
            if (_showCursor && _isFocused)
            {
                var cursorX = inputPosition.X + _consoleFont.MeasureString(inputText.Substring(0, _cursorPosition)).X;
                var cursorY = inputPosition.Y;
                var cursorHeight = _consoleFont.MeasureString("A").Y;
                spriteBatch.Draw(_pixel, new Rectangle((int)cursorX, (int)cursorY, 1, (int)cursorHeight), CONSOLE_CURSOR);
            }
        }

        public void UpdateWindowWidth(int newWidth)
        {
            _windowWidth = newWidth;
            _windowManagement?.UpdateWindowWidth(newWidth);
        }

        public void LoadContent(ContentManager content)
        {
            // Use a monospace font for console (better for terminal display)
            _consoleFont = content.Load<SpriteFont>("Fonts/SpriteFonts/inconsolata/regular");
            
            // Update line height now that font is loaded
            _lineHeight = (int)(_consoleFont.MeasureString("A").Y);
            
            _windowManagement?.LoadContent(content);
            
            // Don't add TaskBar icon by default - it will be added when the window is opened
        }

        public void Dispose()
        {
            _isPowerShellRunning = false;
            
            try
            {
                _powershellProcess?.Kill();
                _powershellProcess?.Dispose();
            }
            catch { }

            _powershellInput?.Dispose();
            _powershellOutput?.Dispose();
            _powershellError?.Dispose();

            _pixel?.Dispose();
            _windowManagement?.Dispose();
        }

        public void ResetCursor()
        {
            _windowManagement?.ResetCursor();
        }

        // Handle mouse clicks for focus
        public void OnMouseClick(Point mousePosition)
        {
            bool isDoubleClick = false;
            
            // Check for double-click
            if (_currentTime - _lastClickTime < DOUBLE_CLICK_TIME && 
                Vector2.Distance(new Vector2(mousePosition.X, mousePosition.Y), new Vector2(_lastClickPosition.X, _lastClickPosition.Y)) < DOUBLE_CLICK_DISTANCE)
            {
                isDoubleClick = true;
            }
            
            _lastClickTime = _currentTime;
            _lastClickPosition = mousePosition;
            
            if (_inputBounds.Contains(mousePosition))
            {
                _isFocused = true;
                _showCursor = true;
                _cursorBlinkTimer = 0f;
                
                // Calculate cursor position based on mouse click
                var promptText = "PS Desktop> ";
                var promptWidth = _consoleFont.MeasureString(promptText).X;
                var clickX = mousePosition.X - _inputBounds.X - 5 - promptWidth;
                
                // Find the closest character position
                var inputText = _currentInput.ToString();
                var bestPosition = 0;
                var bestDistance = float.MaxValue;
                
                for (int i = 0; i <= inputText.Length; i++)
                {
                    var testText = inputText.Substring(0, i);
                    var textWidth = _consoleFont.MeasureString(testText).X;
                    var distance = Math.Abs(clickX - textWidth);
                    
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestPosition = i;
                    }
                }
                
                _cursorPosition = bestPosition;
                
                if (isDoubleClick)
                {
                    // Double-click: select word at cursor position
                    SelectWordAtPosition(bestPosition);
                }
                else
                {
                    // Single click: clear selection and set cursor
                    ClearSelection();
                }
                ClearConsoleSelection();
            }
            else if (_consoleBounds.Contains(mousePosition))
            {
                _isFocused = false;
                ClearSelection();
                
                if (isDoubleClick)
                {
                    // Double-click in console: select word at position
                    SelectConsoleWordAtPosition(mousePosition);
                }
                else
                {
                    // Single click: start console text selection
                    StartConsoleSelection(mousePosition);
                }
            }
        }

        // Handle mouse wheel for scrolling
        public void OnMouseWheel(int delta)
        {
            if (_consoleBounds.Contains(_currentMouseState.Position))
            {
                _autoScroll = false;
                int scrollStep = -delta / 120 * 20; // Standard wheel delta is 120, scroll by 20 pixels
                _scrollY = Math.Max(0, Math.Min(_scrollY + scrollStep, Math.Max(0, _contentHeight - _consoleBounds.Height)));
            }
        }

        // Text selection methods
        private void ClearSelection()
        {
            _hasSelection = false;
            _selectionStart = -1;
            _selectionEnd = -1;
            _selectedText = "";
        }

        private void SelectAllText()
        {
            _selectionStart = 0;
            _selectionEnd = _currentInput.Length;
            _hasSelection = true;
            _selectedText = _currentInput.ToString();
        }

        private void ExtendSelection(int direction)
        {
            if (!_hasSelection)
            {
                _selectionStart = _cursorPosition;
                _hasSelection = true;
            }

            _selectionEnd = Math.Max(0, Math.Min(_currentInput.Length, _selectionEnd + direction));
            _selectedText = _currentInput.ToString().Substring(
                Math.Min(_selectionStart, _selectionEnd),
                Math.Abs(_selectionEnd - _selectionStart)
            );
        }

        private void ExtendSelectionToStart()
        {
            if (!_hasSelection)
            {
                _selectionStart = _cursorPosition;
                _hasSelection = true;
            }
            _selectionEnd = 0;
            _selectedText = _currentInput.ToString().Substring(0, _selectionStart);
        }

        private void ExtendSelectionToEnd()
        {
            if (!_hasSelection)
            {
                _selectionStart = _cursorPosition;
                _hasSelection = true;
            }
            _selectionEnd = _currentInput.Length;
            _selectedText = _currentInput.ToString().Substring(_selectionStart);
        }

        private void SelectWordAtPosition(int position)
        {
            var inputText = _currentInput.ToString();
            if (string.IsNullOrEmpty(inputText) || position < 0 || position > inputText.Length)
            {
                ClearSelection();
                return;
            }

            // Find word boundaries
            int start = position;
            int end = position;

            // Move start to beginning of word
            while (start > 0 && IsWordCharacter(inputText[start - 1]))
            {
                start--;
            }

            // Move end to end of word
            while (end < inputText.Length && IsWordCharacter(inputText[end]))
            {
                end++;
            }

            // If no word found at position, select the character
            if (start == end)
            {
                if (position < inputText.Length)
                {
                    start = position;
                    end = position + 1;
                }
                else
                {
                    start = position - 1;
                    end = position;
                }
            }

            _selectionStart = start;
            _selectionEnd = end;
            _hasSelection = true;
            _selectedText = inputText.Substring(start, end - start);
        }

        private bool IsWordCharacter(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.';
        }

        private void CopySelectedText()
        {
            try
            {
                string textToCopy = "";
                Vector2 copyPosition = Vector2.Zero;
                
                if (_hasSelection && !string.IsNullOrEmpty(_selectedText))
                {
                    textToCopy = _selectedText;
                    // Calculate position near the input area for animation
                    copyPosition = new Vector2(_inputBounds.X + _inputBounds.Width / 2, _inputBounds.Y - 30);
                }
                else if (_hasConsoleSelection)
                {
                    textToCopy = GetSelectedConsoleText();
                    // Calculate position near the selected text for animation
                    var startLine = Math.Min(_consoleSelectionStartLine, _consoleSelectionEndLine);
                    var y = _consoleBounds.Y + _consolePadding + (startLine - (_scrollY / _lineHeight)) * _lineHeight - (_scrollY % _lineHeight);
                    copyPosition = new Vector2(_consoleBounds.X + _consoleBounds.Width / 2, y);
                }
                
                if (!string.IsNullOrEmpty(textToCopy))
                {
                    // Copy to clipboard using safe method
                    CopyToClipboard(textToCopy);
                    
                    // Show copy animation safely
                    try
                    {
                        _copyAnimationPosition = copyPosition;
                        _showCopyAnimation = true;
                        _copyAnimationTimer = 0f;
                    }
                    catch (Exception animEx)
                    {
                        // If animation fails, just log and continue
                        AddConsoleLine($"Animation error: {animEx.Message}", CONSOLE_WARNING);
                    }
                    
                    // Success - only show animation, no console message
                }
                else
                {
                    // No text selected - do nothing
                }
            }
            catch (Exception ex)
            {
                AddConsoleLine($"Copy operation failed: {ex.Message}", CONSOLE_ERROR);
            }
        }

        private void CopyToClipboard(string text)
        {
            try
            {
                if (string.IsNullOrEmpty(text))
                {
                    return;
                }

                // Convert string to bytes for clipboard
                byte[] textBytes = Encoding.UTF8.GetBytes(text);
                IntPtr hGlobal = IntPtr.Zero;
                IntPtr hLock = IntPtr.Zero;

                try
                {
                    // Open clipboard
                    if (!OpenClipboard(IntPtr.Zero))
                    {
                        return;
                    }

                    // Empty clipboard
                    if (!EmptyClipboard())
                    {
                        return;
                    }

                    // Allocate global memory
                    hGlobal = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)(textBytes.Length + 1));
                    if (hGlobal == IntPtr.Zero)
                    {
                        return;
                    }

                    // Lock memory and copy text
                    hLock = GlobalLock(hGlobal);
                    if (hLock == IntPtr.Zero)
                    {
                        return;
                    }

                    // Copy text to clipboard memory
                    Marshal.Copy(textBytes, 0, hLock, textBytes.Length);
                    Marshal.WriteByte(hLock, textBytes.Length, 0); // Null terminator

                    // Set clipboard data
                    if (SetClipboardData(CF_TEXT, hGlobal) == IntPtr.Zero)
                    {
                        return;
                    }

                    // Success - store text (message will be shown by caller)
                    _lastCopiedText = text;
                }
                finally
                {
                    // Clean up
                    if (hLock != IntPtr.Zero)
                        GlobalUnlock(hGlobal);
                    
                    CloseClipboard();
                    
                    // Note: Don't free hGlobal here - Windows owns it after SetClipboardData
                }
            }
            catch (Exception)
            {
                // If anything fails, silently fail - no console messages
            }
        }

        private void PasteFromClipboard()
        {
            try
            {
                // Clear any existing selection
                ClearSelection();

                // Get clipboard text using Windows API
                string clipboardText = GetClipboardText();
                
                if (!string.IsNullOrEmpty(clipboardText))
                {
                    // Insert text at cursor position
                    _currentInput.Insert(_cursorPosition, clipboardText);
                    _cursorPosition += clipboardText.Length;
                }
            }
            catch (Exception)
            {
                // If paste fails, silently fail - no console messages
            }
        }

        private string GetClipboardText()
        {
            try
            {
                if (!OpenClipboard(IntPtr.Zero))
                {
                    return "";
                }

                IntPtr hClipboardData = GetClipboardData(CF_TEXT);
                if (hClipboardData == IntPtr.Zero)
                {
                    CloseClipboard();
                    return "";
                }

                IntPtr hLock = GlobalLock(hClipboardData);
                if (hLock == IntPtr.Zero)
                {
                    CloseClipboard();
                    return "";
                }

                try
                {
                    // Read the text from clipboard memory
                    string text = Marshal.PtrToStringAnsi(hLock);
                    return text ?? "";
                }
                finally
                {
                    GlobalUnlock(hClipboardData);
                    CloseClipboard();
                }
            }
            catch (Exception)
            {
                return "";
            }
        }

        // Console text selection methods
        private void ClearConsoleSelection()
        {
            _hasConsoleSelection = false;
            _consoleSelectionStartLine = -1;
            _consoleSelectionStartChar = -1;
            _consoleSelectionEndLine = -1;
            _consoleSelectionEndChar = -1;
        }

        private void StartConsoleSelection(Point mousePosition)
        {
            var relativePos = new Point(
                mousePosition.X - _consoleBounds.X - _consolePadding,
                mousePosition.Y - _consoleBounds.Y - _consolePadding
            );

            // Calculate line index based on scroll position
            var lineIndex = (_scrollY + relativePos.Y) / _lineHeight;
            
            // If clicking beyond the last line, snap to the last line
            if (lineIndex >= _consoleLines.Count)
            {
                lineIndex = _consoleLines.Count - 1;
            }
            
            // If clicking before the first line, snap to the first line
            if (lineIndex < 0)
            {
                lineIndex = 0;
            }

            var charIndex = GetCharacterIndexAtPosition(relativePos.X, lineIndex);

            if (lineIndex >= 0 && lineIndex < _consoleLines.Count)
            {
                _consoleSelectionStartLine = lineIndex;
                _consoleSelectionStartChar = charIndex;
                _consoleSelectionEndLine = lineIndex;
                _consoleSelectionEndChar = charIndex;
                _hasConsoleSelection = true;
                _isConsoleSelecting = true;
            }
        }

        private void SelectConsoleWordAtPosition(Point mousePosition)
        {
            var relativePos = new Point(
                mousePosition.X - _consoleBounds.X - _consolePadding,
                mousePosition.Y - _consoleBounds.Y - _consolePadding
            );

            // Calculate line index based on scroll position
            var lineIndex = (_scrollY + relativePos.Y) / _lineHeight;
            
            // If clicking beyond the last line, snap to the last line
            if (lineIndex >= _consoleLines.Count)
            {
                lineIndex = _consoleLines.Count - 1;
            }
            
            // If clicking before the first line, snap to the first line
            if (lineIndex < 0)
            {
                lineIndex = 0;
            }

            var charIndex = GetCharacterIndexAtPosition(relativePos.X, lineIndex);

            if (lineIndex >= 0 && lineIndex < _consoleLines.Count)
            {
                var line = _consoleLines[lineIndex];
                if (string.IsNullOrEmpty(line))
                {
                    ClearConsoleSelection();
                    return;
                }

                // Find word boundaries
                int start = charIndex;
                int end = charIndex;

                // Move start to beginning of word
                while (start > 0 && IsWordCharacter(line[start - 1]))
                {
                    start--;
                }

                // Move end to end of word
                while (end < line.Length && IsWordCharacter(line[end]))
                {
                    end++;
                }

                // If no word found at position, select the character
                if (start == end)
                {
                    if (charIndex < line.Length)
                    {
                        start = charIndex;
                        end = charIndex + 1;
                    }
                    else
                    {
                        start = charIndex - 1;
                        end = charIndex;
                    }
                }

                _consoleSelectionStartLine = lineIndex;
                _consoleSelectionStartChar = start;
                _consoleSelectionEndLine = lineIndex;
                _consoleSelectionEndChar = end;
                _hasConsoleSelection = true;
                _isConsoleSelecting = false; // Word selection is immediate, not dragging
            }
        }

        private int GetCharacterIndexAtPosition(int x, int lineIndex)
        {
            if (lineIndex < 0 || lineIndex >= _consoleLines.Count)
                return 0;

            var line = _consoleLines[lineIndex];
            var bestIndex = 0;
            var bestDistance = float.MaxValue;

            for (int i = 0; i <= line.Length; i++)
            {
                var testText = line.Substring(0, i);
                var textWidth = _consoleFont.MeasureString(testText).X;
                var distance = Math.Abs(x - textWidth);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private void UpdateConsoleSelection(Point mousePosition)
        {
            if (!_isConsoleSelecting) return;

            var relativePos = new Point(
                mousePosition.X - _consoleBounds.X - _consolePadding,
                mousePosition.Y - _consoleBounds.Y - _consolePadding
            );

            // Calculate line index based on scroll position
            var lineIndex = (_scrollY + relativePos.Y) / _lineHeight;
            
            // If dragging beyond the last line, snap to the last line
            if (lineIndex >= _consoleLines.Count)
            {
                lineIndex = _consoleLines.Count - 1;
            }
            
            // If dragging before the first line, snap to the first line
            if (lineIndex < 0)
            {
                lineIndex = 0;
            }

            var charIndex = GetCharacterIndexAtPosition(relativePos.X, lineIndex);

            if (lineIndex >= 0 && lineIndex < _consoleLines.Count)
            {
                _consoleSelectionEndLine = lineIndex;
                _consoleSelectionEndChar = charIndex;
            }
        }

        private string GetSelectedConsoleText()
        {
            try
            {
                if (!_hasConsoleSelection) return "";

                // Validate selection bounds
                if (_consoleSelectionStartLine < 0 || _consoleSelectionEndLine < 0 || 
                    _consoleSelectionStartLine >= _consoleLines.Count || _consoleSelectionEndLine >= _consoleLines.Count)
                {
                    return "";
                }

                // Determine the actual start and end based on selection direction
                bool isReversed = (_consoleSelectionStartLine > _consoleSelectionEndLine) || 
                                 (_consoleSelectionStartLine == _consoleSelectionEndLine && _consoleSelectionStartChar > _consoleSelectionEndChar);
                
                var startLine = isReversed ? _consoleSelectionEndLine : _consoleSelectionStartLine;
                var endLine = isReversed ? _consoleSelectionStartLine : _consoleSelectionEndLine;
                var startChar = isReversed ? _consoleSelectionEndChar : _consoleSelectionStartChar;
                var endChar = isReversed ? _consoleSelectionStartChar : _consoleSelectionEndChar;

                var result = new StringBuilder();

                for (int i = startLine; i <= endLine; i++)
                {
                    if (i >= 0 && i < _consoleLines.Count)
                    {
                        var line = _consoleLines[i];
                        if (line == null) continue; // Skip null lines
                        
                        var lineStart = (i == startLine) ? startChar : 0;
                        var lineEnd = (i == endLine) ? endChar : line.Length;

                        // Ensure we have valid bounds
                        lineStart = Math.Max(0, Math.Min(lineStart, line.Length));
                        lineEnd = Math.Max(lineStart, Math.Min(lineEnd, line.Length));

                        if (lineStart < lineEnd)
                        {
                            var selectedText = line.Substring(lineStart, lineEnd - lineStart);
                            result.Append(selectedText);
                            if (i < endLine) result.AppendLine();
                        }
                    }
                }

                return result.ToString();
            }
            catch (Exception ex)
            {
                // If anything fails, return empty string and log error
                AddConsoleLine($"Text extraction failed: {ex.Message}", CONSOLE_ERROR);
                return "";
            }
        }

        private void DrawCopyAnimation(SpriteBatch spriteBatch)
        {
            try
            {
                if (!_showCopyAnimation || _consoleFont == null) return;

                // Calculate animation progress (0 to 1)
                float progress = _copyAnimationTimer / COPY_ANIMATION_DURATION;
                
                // Calculate alpha (fade out after COPY_ANIMATION_FADE_START)
                float alpha = 1.0f;
                if (_copyAnimationTimer > COPY_ANIMATION_FADE_START)
                {
                    float fadeProgress = (_copyAnimationTimer - COPY_ANIMATION_FADE_START) / (COPY_ANIMATION_DURATION - COPY_ANIMATION_FADE_START);
                    alpha = Math.Max(0f, 1.0f - fadeProgress);
                }

                // Calculate position with slight upward movement
                float yOffset = -20 * progress; // Move up 20 pixels over time
                Vector2 position = new Vector2(_copyAnimationPosition.X, _copyAnimationPosition.Y + yOffset);

                // Draw background rectangle
                string text = "Text copied [OK]";
                Vector2 textSize;
                
                try
                {
                    textSize = _consoleFont.MeasureString(text);
                }
                catch
                {
                    // Fallback to simple text if font has issues
                    text = "Text copied";
                    textSize = _consoleFont.MeasureString(text);
                }
                Rectangle backgroundRect = new Rectangle(
                    (int)(position.X - textSize.X / 2 - 10),
                    (int)(position.Y - textSize.Y / 2 - 5),
                    (int)textSize.X + 20,
                    (int)textSize.Y + 10
                );

                // Ensure alpha is within valid range
                alpha = Math.Max(0f, Math.Min(1f, alpha));

                // Draw background with alpha
                Color backgroundColor = new Color((byte)0, (byte)100, (byte)0, (byte)(128 * alpha)); // Dark green with alpha
                spriteBatch.Draw(_pixel, backgroundRect, backgroundColor);

                // Draw border
                Color borderColor = new Color((byte)0, (byte)255, (byte)0, (byte)(255 * alpha)); // Bright green with alpha
                DrawBorder(spriteBatch, backgroundRect, borderColor);

                // Draw text
                try
                {
                    Color textColor = new Color((byte)255, (byte)255, (byte)255, (byte)(255 * alpha)); // White with alpha
                    Vector2 textPosition = new Vector2(
                        position.X - textSize.X / 2,
                        position.Y - textSize.Y / 2
                    );
                    spriteBatch.DrawString(_consoleFont, text, textPosition, textColor);
                }
                catch
                {
                    // If text drawing fails, just skip it - the background will still show
                    // This prevents crashes from unsupported characters
                }
            }
            catch (Exception ex)
            {
                // If animation fails, just disable it and log error
                _showCopyAnimation = false;
                AddConsoleLine($"Animation error: {ex.Message}", CONSOLE_WARNING);
            }
        }
    }
}


