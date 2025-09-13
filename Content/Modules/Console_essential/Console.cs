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

namespace MarySGameEngine.Modules.Console_essential
{
    public class Console : IModule
    {
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

            // Set custom default size for console (wider, less tall)
            // Access the private fields using reflection
            var defaultWidthField = _windowManagement.GetType().GetField("_defaultWidth", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var defaultHeightField = _windowManagement.GetType().GetField("_defaultHeight", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (defaultWidthField != null)
                defaultWidthField.SetValue(_windowManagement, 800);
            if (defaultHeightField != null)
                defaultHeightField.SetValue(_windowManagement, 300);

            _windowManagement.SetWindowTitle("Console");
            _windowManagement.SetTaskBar(_taskBar);

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
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoExit -Command",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Directory.GetCurrentDirectory()
                };

                _powershellProcess = new Process { StartInfo = startInfo };
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
                            AddConsoleLine(line, CONSOLE_TEXT);
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
                            AddConsoleLine(line, CONSOLE_ERROR);
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
                // Handle line wrapping - use the actual console bounds width
                int availableWidth = _consoleBounds.Width - _consolePadding * 2;
                
                // Ensure we have a reasonable minimum width
                availableWidth = Math.Max(availableWidth, 200);
                
                // Debug output for new lines
                System.Diagnostics.Debug.WriteLine($"Console AddConsoleLine: ConsoleBounds={_consoleBounds}, AvailableWidth={availableWidth}, ConsolePadding={_consolePadding}, Text='{text}'");
                
                var lines = WrapText(text, availableWidth);
                foreach (var line in lines)
                {
                    _consoleLines.Add(line);
                }

                // Auto-scroll if at bottom
                if (_autoScroll)
                {
                    _scrollOffset = Math.Max(0, _consoleLines.Count - _maxVisibleLines);
                }

                // Limit console history to prevent memory issues
                if (_consoleLines.Count > 1000)
                {
                    _consoleLines.RemoveRange(0, 100);
                    _scrollOffset = Math.Max(0, _scrollOffset - 100);
                }
            }
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
                var testWidth = _consoleFont.MeasureString(testLine).X;

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
                    if (_consoleFont.MeasureString(word).X > maxWidth)
                    {
                        var remaining = word;
                        while (remaining.Length > 0)
                        {
                            var fitLength = 0;
                            for (int i = 1; i <= remaining.Length; i++)
                            {
                                var test = remaining.Substring(0, i);
                                if (_consoleFont.MeasureString(test).X <= maxWidth)
                                    fitLength = i;
                                else
                                    break;
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

            UpdateConsoleInput();
            UpdateCursor();
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
                    if (_cursorPosition > 0)
                        _cursorPosition--;
                    break;

                case Keys.Right:
                    if (_cursorPosition < _currentInput.Length)
                        _cursorPosition++;
                    break;

                case Keys.Home:
                    _cursorPosition = 0;
                    break;

                case Keys.End:
                    _cursorPosition = _currentInput.Length;
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

            // Display the command
            AddConsoleLine($"> {command}", CONSOLE_INPUT);

            // Handle special commands
            if (command.ToLower() == "exit" || command.ToLower() == "quit")
            {
                _windowManagement?.SetVisible(false);
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
                    _powershellInput.WriteLine(command);
                    _powershellInput.Flush();
                }
                catch (Exception ex)
                {
                    AddConsoleLine($"Error executing command: {ex.Message}", CONSOLE_ERROR);
                }
            }
            else
            {
                AddConsoleLine("PowerShell is not running. Cannot execute command.", CONSOLE_ERROR);
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
            _scrollOffset = Math.Max(0, Math.Min(_scrollOffset + lines, Math.Max(0, _consoleLines.Count - _maxVisibleLines)));
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
                
                // Calculate console bounds with proper margins
                int consoleX = windowBounds.X + _consoleMargin;
                int consoleY = windowBounds.Y + _consoleMargin + 40; // Account for title bar
                int consoleWidth = Math.Max(100, windowBounds.Width - _consoleMargin * 2); // Ensure minimum width
                int consoleHeight = Math.Max(100, windowBounds.Height - _consoleMargin * 2 - 40 - 30); // Account for title bar and input area
                
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
                    _consoleBounds.Bottom + 5,
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
                
                // Ensure we have a reasonable minimum width
                availableWidth = Math.Max(availableWidth, 200);
                
                // Debug output
                System.Diagnostics.Debug.WriteLine($"Console ReWrapAllText: ConsoleBounds={_consoleBounds}, AvailableWidth={availableWidth}, ConsolePadding={_consolePadding}");
                
                // Group consecutive lines that are not command prompts or special lines
                // This allows proper re-wrapping while preserving structure
                var groupedLines = new List<List<string>>();
                var currentGroup = new List<string>();
                
                foreach (var line in originalLines)
                {
                    // Check if this is a special line that should not be grouped
                    bool isSpecialLine = line.StartsWith("> ") || // Command prompt
                                       line.StartsWith("PS ") || // PowerShell prompt
                                       string.IsNullOrWhiteSpace(line) || // Empty line
                                       line.StartsWith("MaryS Game Engine") || // Welcome message
                                       line.StartsWith("Type 'help'") || // Instructions
                                       line.StartsWith("PowerShell initialized") || // Status message
                                       line.StartsWith("Available commands:") || // Help header
                                       line.StartsWith("  ") || // Help items (indented)
                                       line.StartsWith("All other commands"); // Help footer
                    
                    if (isSpecialLine)
                    {
                        // If we have a current group, add it and start a new one
                        if (currentGroup.Count > 0)
                        {
                            groupedLines.Add(new List<string>(currentGroup));
                            currentGroup.Clear();
                        }
                        // Add the special line as its own group
                        groupedLines.Add(new List<string> { line });
                    }
                    else
                    {
                        // Add to current group for potential re-wrapping
                        currentGroup.Add(line);
                    }
                }
                
                // Add any remaining group
                if (currentGroup.Count > 0)
                {
                    groupedLines.Add(currentGroup);
                }
                
                // Process each group
                foreach (var group in groupedLines)
                {
                    if (group.Count == 1)
                    {
                        // Single line - just add it
                        _consoleLines.Add(group[0]);
                    }
                    else
                    {
                        // Multiple lines - re-wrap them as a single text
                        var fullText = string.Join(" ", group);
                        var wrappedLines = WrapText(fullText, availableWidth);
                        
                        foreach (var wrappedLine in wrappedLines)
                        {
                            _consoleLines.Add(wrappedLine);
                        }
                    }
                }

                // Adjust scroll offset to maintain relative position
                if (_autoScroll)
                {
                    _scrollOffset = Math.Max(0, _consoleLines.Count - _maxVisibleLines);
                }
                else
                {
                    // Try to maintain the current scroll position proportionally
                    if (originalLines.Count > 0)
                    {
                        float scrollRatio = (float)_scrollOffset / originalLines.Count;
                        _scrollOffset = Math.Max(0, Math.Min((int)(scrollRatio * _consoleLines.Count), _consoleLines.Count - _maxVisibleLines));
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

            // Draw input area
            DrawInputArea(spriteBatch);
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
                var startLine = _scrollOffset;
                var endLine = Math.Min(startLine + _maxVisibleLines, _consoleLines.Count);

                for (int i = startLine; i < endLine; i++)
                {
                    var line = _consoleLines[i];
                    var y = _consoleBounds.Y + _consolePadding + (i - startLine) * _lineHeight;
                    var position = new Vector2(_consoleBounds.X + _consolePadding, y);

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

                    spriteBatch.DrawString(_consoleFont, line, position, lineColor);
                }
            }
        }

        private void DrawInputArea(SpriteBatch spriteBatch)
        {
            // Draw input background
            spriteBatch.Draw(_pixel, _inputBounds, CONSOLE_BACKGROUND);
            DrawBorder(spriteBatch, _inputBounds, CONSOLE_BORDER);

            // Draw prompt
            var promptText = "PS " + Directory.GetCurrentDirectory() + "> ";
            var promptPosition = new Vector2(_inputBounds.X + 5, _inputBounds.Y + 5);
            spriteBatch.DrawString(_consoleFont, promptText, promptPosition, CONSOLE_SUCCESS);

            // Draw input text
            var inputText = _currentInput.ToString();
            var inputPosition = new Vector2(promptPosition.X + _consoleFont.MeasureString(promptText).X, promptPosition.Y);
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
            if (_inputBounds.Contains(mousePosition))
            {
                _isFocused = true;
                _showCursor = true;
                _cursorBlinkTimer = 0f;
            }
            else if (_consoleBounds.Contains(mousePosition))
            {
                _isFocused = false;
            }
        }

        // Handle mouse wheel for scrolling
        public void OnMouseWheel(int delta)
        {
            if (_consoleBounds.Contains(_currentMouseState.Position))
            {
                _autoScroll = false;
                ScrollConsole(-delta / 120); // Standard wheel delta is 120
            }
        }
    }
}


