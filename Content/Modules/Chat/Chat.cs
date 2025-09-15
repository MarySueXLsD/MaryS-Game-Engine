using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Content;
using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using MarySGameEngine.Modules.WindowManagement_essential;
using MarySGameEngine.Modules.TaskBar_essential;

namespace MarySGameEngine.Modules.Chat
{
    public class Chat : IModule
    {
        private WindowManagement _windowManagement;
        private GraphicsDevice _graphicsDevice;
        private SpriteFont _menuFont;
        private SpriteFont _chatFont;
        private SpriteFont _pixelFont;
        private int _windowWidth;
        private TaskBar _taskBar;
        private GameEngine _engine;
        private Texture2D _pixel;
        private Texture2D _marySueImage;
        private MouseState _currentMouseState;
        private MouseState _previousMouseState;
        private KeyboardState _currentKeyboardState;
        private KeyboardState _previousKeyboardState;

        // Chat properties
        private List<ChatMessage> _chatMessages;
        private StringBuilder _currentInput;
        private int _cursorPosition;
        private bool _isFocused;
        private float _cursorBlinkTimer;
        private bool _showCursor = true;
        private const float CURSOR_BLINK_INTERVAL = 0.5f;
        private int _maxVisibleMessages = 10;
        
        // Scrolling properties (like Console module)
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
        
        // Thinking animation properties
        private bool _isThinking = false;
        private float _thinkingTimer = 0f;
        private int _currentThinkingDot = 0;
        private const float THINKING_DOT_INTERVAL = 0.5f; // Time between dot animations
        private const float THINKING_SCALE_SPEED = 4.0f; // Speed of dot scaling
        
        // GPT API integration
        private static HttpClient _httpClient;
        private string _gptApiKey;
        private const string GPT_API_URL = "https://api.openai.com/v1/chat/completions";
        private List<GptMessage> _conversationHistory;
        private const int MAX_HISTORY_MESSAGES = 15;
        
        // Mary Sue's personality prompt
        private const string MARY_SUE_PERSONALITY = @"You are Mary Sue, an AI assistant with a shady, funny, not very friendly, and slightly quirky personality. Yo can be helpful but sometimes just be a bitch. You are very knowledgeable, but also have a playful side. You tend to be enthusiastic about topics you find interesting and occasionally make very offensive jokes or puns. You're very encouraging but don't really believe in anyone, always trying to see the negative side of things. Keep your responses conversational and very informal. Respond naturally and authentically as Mary Sue would.";

        // Layout properties
        private Rectangle _leftPanelBounds;
        private Rectangle _rightPanelBounds;
        private Rectangle _chatAreaBounds;
        private Rectangle _inputAreaBounds;
        private Rectangle _sendButtonBounds;
        private int _leftPanelWidth = 100; // Will be calculated based on image size
        private const int INPUT_HEIGHT = 40;
        private const int SEND_BUTTON_WIDTH = 80;
        private const int PANEL_PADDING = 10;
        private const int MESSAGE_PADDING = 8;
        private const int BUBBLE_MARGIN = 10; // Space between bubbles and panel edges (reduced from 15)
        private const int BUBBLE_PADDING = 6; // Internal padding within bubbles (reduced from 10)
        private const int BUBBLE_CORNER_RADIUS = 8; // Rounded corner effect (reduced from 12)
        private const float TEXT_SCALE = 0.8f; // Scale for chat text to make it smaller

        // Colors
        private readonly Color CHAT_BACKGROUND = new Color(60, 60, 60); // Dark gray background
        private readonly Color INPUT_BACKGROUND = new Color(80, 80, 80); // Slightly lighter gray for input
        private readonly Color SEND_BUTTON_COLOR = new Color(147, 112, 219);
        private readonly Color SEND_BUTTON_HOVER = new Color(180, 145, 250);
        private readonly Color MESSAGE_USER_COLOR = new Color(70, 130, 220); // Blue bubble for user
        private readonly Color MESSAGE_MARY_COLOR = new Color(120, 80, 200); // Purple bubble for Mary
        private readonly Color BUBBLE_BORDER_COLOR = new Color(40, 40, 40); // Darker border for bubbles
        private readonly Color TEXT_COLOR = new Color(220, 220, 220); // Light gray text for dark background
        private readonly Color BORDER_COLOR = new Color(100, 100, 100); // Darker border for dark theme

        private class ChatMessage
        {
            public string Text { get; set; }
            public List<string> WrappedLines { get; set; }
            public string Sender { get; set; } // "User" or "Mary"
            public DateTime Timestamp { get; set; }
            public Color BackgroundColor { get; set; }
            public int TotalHeight { get; set; } // Total height including all wrapped lines
        }
        
        // GPT API models
        private class GptMessage
        {
            [JsonPropertyName("role")]
            public string role { get; set; }
            [JsonPropertyName("content")]
            public string content { get; set; }
        }
        
        private class GptRequest
        {
            [JsonPropertyName("model")]
            public string model { get; set; }
            [JsonPropertyName("messages")]
            public List<GptMessage> messages { get; set; }
            [JsonPropertyName("max_tokens")]
            public int max_tokens { get; set; }
            [JsonPropertyName("temperature")]
            public double temperature { get; set; }
        }
        
        private class GptResponse
        {
            [JsonPropertyName("choices")]
            public List<GptChoice> choices { get; set; }
        }
        
        private class GptChoice
        {
            [JsonPropertyName("message")]
            public GptMessage message { get; set; }
        }

        public Chat(GraphicsDevice graphicsDevice, SpriteFont menuFont, int windowWidth)
        {
            _graphicsDevice = graphicsDevice;
            _menuFont = menuFont;
            _chatFont = menuFont; // Will be updated in LoadContent
            _windowWidth = windowWidth;
            _engine = GameEngine.Instance;

            // Initialize chat properties
            _chatMessages = new List<ChatMessage>();
            _currentInput = new StringBuilder();
            _cursorPosition = 0;
            _isFocused = false;
            
            // Load API key from config file
            LoadApiKey();
            
            // Initialize conversation history with Mary Sue's personality
            _conversationHistory = new List<GptMessage>
            {
                new GptMessage { role = "system", content = MARY_SUE_PERSONALITY }
            };
            
            // Set up HTTP client for GPT API (only once)
            try
            {
                if (_httpClient == null && !string.IsNullOrEmpty(_gptApiKey))
                {
                    _httpClient = new HttpClient();
                    _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_gptApiKey}");
                    _httpClient.Timeout = TimeSpan.FromSeconds(30); // 30 second timeout
                }
                else if (string.IsNullOrEmpty(_gptApiKey))
                {
                    _engine?.Log("Chat: API key not loaded, GPT functionality will be disabled");
                }
            }
            catch (Exception ex)
            {
                _engine?.Log($"Chat: Warning - Could not initialize HTTP client: {ex.Message}");
            }

            // Create pixel texture
            _pixel = new Texture2D(_graphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });

            // Create window properties from bridge.json
            var properties = new WindowProperties
            {
                IsVisible = false,
                IsMovable = true,
                IsResizable = false // Make window non-resizable
            };

            // Initialize window management
            _windowManagement = new WindowManagement(graphicsDevice, menuFont, windowWidth, properties);
            
            // Set custom default size for chat window (1200x100)
            var defaultWidthField = _windowManagement.GetType().GetField("_defaultWidth", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var defaultHeightField = _windowManagement.GetType().GetField("_defaultHeight", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (defaultWidthField != null)
                defaultWidthField.SetValue(_windowManagement, 1200);
            if (defaultHeightField != null)
                defaultHeightField.SetValue(_windowManagement, 170);

            _windowManagement.SetWindowTitle("Chat");
            _windowManagement.SetCustomMinimumSize(1200, 170); // Fixed size

            // Add welcome message with personality (safely)
            try
            {
                AddMessage("Mary", "Yo! Whats'up! Wanna develop something cool?");
            }
            catch (Exception ex)
            {
                _engine?.Log($"Chat: Error adding welcome message: {ex.Message}");
            }
        }

        public void SetTaskBar(TaskBar taskBar)
        {
            _taskBar = taskBar;
            _windowManagement?.SetTaskBar(taskBar);
        }

        public void Update()
        {
            try
            {
                _previousMouseState = _currentMouseState;
                _currentMouseState = Mouse.GetState();
                _previousKeyboardState = _currentKeyboardState;
                _currentKeyboardState = Keyboard.GetState();

                _windowManagement?.Update();

                if (!_windowManagement?.IsVisible() == true)
                    return;

                UpdateBounds();
                UpdateCursor();
                HandleInput();
                HandleScrolling();
                
                // Update thinking animation
                if (_isThinking)
                {
                    _thinkingTimer += (float)GameEngine.Instance.TargetElapsedTime.TotalSeconds;
                    if (_thinkingTimer >= THINKING_DOT_INTERVAL)
                    {
                        _currentThinkingDot = (_currentThinkingDot + 1) % 3;
                        _thinkingTimer = 0f;
                    }
                }
            }
            catch (Exception ex)
            {
                _engine?.Log($"Chat: Error in Update: {ex.Message}");
            }
        }

        private void UpdateBounds()
        {
            if (_windowManagement != null)
            {
                var windowBounds = _windowManagement.GetWindowBounds();
                
                // Calculate image size (25% smaller than before, which was twice the panel width minus padding)
                // Original: (_leftPanelBounds.Width - PANEL_PADDING * 2) * 2
                // Now: Original * 0.75 = ((_leftPanelBounds.Width - PANEL_PADDING * 2) * 2) * 0.75
                // But we need to calculate left panel width based on image size
                // Let's work backwards: if image should be 75% of original size
                // Original image was: (200 - 20) * 2 = 360
                // New image should be: 360 * 0.75 = 270
                int imageSize = 270;
                _leftPanelWidth = imageSize; // Panel width equals image size (no padding)
                
                // Calculate panel bounds - left panel has no padding, image fits exactly
                _leftPanelBounds = new Rectangle(
                    windowBounds.X,
                    windowBounds.Y + 40, // Account for title bar, no padding
                    _leftPanelWidth,
                    windowBounds.Height - 40
                );

                _rightPanelBounds = new Rectangle(
                    _leftPanelBounds.Right + PANEL_PADDING,
                    windowBounds.Y + 40 + PANEL_PADDING,
                    windowBounds.Width - _leftPanelWidth - PANEL_PADDING * 2,
                    windowBounds.Height - 40 - PANEL_PADDING * 2
                );

                // Calculate chat area and input area bounds
                _inputAreaBounds = new Rectangle(
                    _rightPanelBounds.X,
                    _rightPanelBounds.Bottom - INPUT_HEIGHT,
                    _rightPanelBounds.Width,
                    INPUT_HEIGHT
                );

                _sendButtonBounds = new Rectangle(
                    _inputAreaBounds.Right - SEND_BUTTON_WIDTH - PANEL_PADDING,
                    _inputAreaBounds.Y + (_inputAreaBounds.Height - 25) / 2,
                    SEND_BUTTON_WIDTH,
                    25
                );

                _chatAreaBounds = new Rectangle(
                    _rightPanelBounds.X,
                    _rightPanelBounds.Y,
                    _rightPanelBounds.Width,
                    _rightPanelBounds.Height - INPUT_HEIGHT - PANEL_PADDING
                );

                // Calculate content height and scrollbar needs
                if (_chatFont != null)
                {
                    // Calculate total content height based on actual message heights
                    _contentHeight = PANEL_PADDING * 2;
                    foreach (var msg in _chatMessages)
                    {
                        _contentHeight += msg.TotalHeight + MESSAGE_PADDING;
                    }
                    _needsScrollbar = _contentHeight > _chatAreaBounds.Height;
                    
                    // Calculate scrollbar bounds
                    if (_needsScrollbar)
                    {
                        _scrollbarBounds = new Rectangle(
                            _chatAreaBounds.Right - SCROLLBAR_WIDTH - SCROLLBAR_PADDING,
                            _chatAreaBounds.Y + SCROLLBAR_PADDING,
                            SCROLLBAR_WIDTH,
                            _chatAreaBounds.Height - SCROLLBAR_PADDING * 2
                        );
                        
                        // Adjust chat area to account for scrollbar
                        _chatAreaBounds.Width -= SCROLLBAR_WIDTH + SCROLLBAR_PADDING * 2;
                    }
                    
                    // Calculate average message height for max visible messages estimation
                    int averageMessageHeight = (int)(_chatFont.LineSpacing * TEXT_SCALE) + BUBBLE_PADDING * 2 + MESSAGE_PADDING;
                    _maxVisibleMessages = Math.Max(1, (_chatAreaBounds.Height - PANEL_PADDING * 2) / averageMessageHeight);
                    
                    // Auto-scroll to bottom when new messages are added
                    if (_chatMessages.Count > 0)
                    {
                        int maxScroll = Math.Max(0, _contentHeight - _chatAreaBounds.Height);
                        _scrollY = Math.Min(_scrollY, maxScroll);
                    }
                }
            }
        }

        private void UpdateCursor()
        {
            if (_isFocused)
            {
                _cursorBlinkTimer += (float)GameEngine.Instance.TargetElapsedTime.TotalSeconds;
                if (_cursorBlinkTimer >= CURSOR_BLINK_INTERVAL)
                {
                    _showCursor = !_showCursor;
                    _cursorBlinkTimer = 0f;
                }
            }
        }

        private void HandleInput()
        {
            // Handle mouse clicks
            if (_currentMouseState.LeftButton == ButtonState.Pressed && 
                _previousMouseState.LeftButton == ButtonState.Released)
            {
                HandleMouseClick(_currentMouseState.Position);
            }

            // Handle keyboard input if focused
            if (_isFocused)
            {
                var pressedKeys = GetPressedKeys();
                foreach (var key in pressedKeys)
                {
                    HandleKeyPress(key);
                }
            }
        }

        private void HandleScrolling()
        {
            // Handle mouse wheel scrolling
            var mouseState = Mouse.GetState();
            if (_chatAreaBounds.Contains(mouseState.Position))
            {
                int wheelDelta = mouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
                if (wheelDelta != 0)
                {
                    ScrollChat(-wheelDelta / 120 * 3); // Scroll 3 lines per wheel tick
                }
            }

            // Handle scrollbar dragging
            if (_isDraggingScrollbar && mouseState.LeftButton == ButtonState.Pressed)
            {
                float deltaY = mouseState.Position.Y - _scrollbarDragStart.Y;
                float scrollRatio = deltaY / (_scrollbarBounds.Height - 20); // 20 for thumb height
                int newScrollY = (int)(scrollRatio * (_contentHeight - _chatAreaBounds.Height));
                _scrollY = Math.Max(0, Math.Min(newScrollY, _contentHeight - _chatAreaBounds.Height));
            }
            else if (_isDraggingScrollbar && mouseState.LeftButton == ButtonState.Released)
            {
                _isDraggingScrollbar = false;
            }

            // Check if hovering over scrollbar
            _isHoveringScrollbar = _needsScrollbar && _scrollbarBounds.Contains(mouseState.Position);
        }

        private void ScrollChat(int lines)
        {
            if (!_scrollingEnabled || !_needsScrollbar) return;

            // Use a reasonable scroll amount based on average line height
            int averageLineHeight = (int)(_chatFont.LineSpacing * TEXT_SCALE) + MESSAGE_PADDING;
            int scrollAmount = lines * averageLineHeight * 2; // Multiply by 2 for more responsive scrolling
            
            _scrollY = Math.Max(0, Math.Min(_scrollY + scrollAmount, _contentHeight - _chatAreaBounds.Height));
        }

        private void HandleMouseClick(Point mousePosition)
        {
            // Check if clicked on scrollbar
            if (_needsScrollbar && _scrollbarBounds.Contains(mousePosition))
            {
                _isDraggingScrollbar = true;
                _scrollbarDragStart = new Vector2(mousePosition.X, mousePosition.Y);
                return;
            }
            
            // Check if clicked on input area
            if (_inputAreaBounds.Contains(mousePosition) && !_sendButtonBounds.Contains(mousePosition))
            {
                _isFocused = true;
                _showCursor = true;
                _cursorBlinkTimer = 0f;

                // Calculate cursor position based on click
                var inputText = _currentInput.ToString();
                var clickX = mousePosition.X - _inputAreaBounds.X - PANEL_PADDING;
                
                var bestPosition = 0;
                var bestDistance = float.MaxValue;
                
                for (int i = 0; i <= inputText.Length; i++)
                {
                    var testText = inputText.Substring(0, i);
                    var textWidth = _chatFont.MeasureString(testText).X * TEXT_SCALE;
                    var distance = Math.Abs(clickX - textWidth);
                    
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestPosition = i;
                    }
                }
                
                _cursorPosition = bestPosition;
            }
            // Check if clicked on send button
            else if (_sendButtonBounds.Contains(mousePosition))
            {
                SendMessage();
            }
            // Check if clicked outside input area
            else if (_windowManagement.GetWindowBounds().Contains(mousePosition))
            {
                _isFocused = false;
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
                    SendMessage();
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
                case Keys.Space: return ' ';
                case Keys.D0: return shiftPressed ? ')' : '0';
                case Keys.D1: return shiftPressed ? '!' : '1';
                case Keys.D2: return shiftPressed ? '@' : '2';
                case Keys.D3: return shiftPressed ? '#' : '3';
                case Keys.D4: return shiftPressed ? '$' : '4';
                case Keys.D5: return shiftPressed ? '%' : '5';
                case Keys.D6: return shiftPressed ? '^' : '6';
                case Keys.D7: return shiftPressed ? '&' : '7';
                case Keys.D8: return shiftPressed ? '*' : '8';
                case Keys.D9: return shiftPressed ? '(' : '9';

                case Keys.A: return shiftPressed ? 'A' : 'a';
                case Keys.B: return shiftPressed ? 'B' : 'b';
                case Keys.C: return shiftPressed ? 'C' : 'c';
                case Keys.D: return shiftPressed ? 'D' : 'd';
                case Keys.E: return shiftPressed ? 'E' : 'e';
                case Keys.F: return shiftPressed ? 'F' : 'f';
                case Keys.G: return shiftPressed ? 'G' : 'g';
                case Keys.H: return shiftPressed ? 'H' : 'h';
                case Keys.I: return shiftPressed ? 'I' : 'i';
                case Keys.J: return shiftPressed ? 'J' : 'j';
                case Keys.K: return shiftPressed ? 'K' : 'k';
                case Keys.L: return shiftPressed ? 'L' : 'l';
                case Keys.M: return shiftPressed ? 'M' : 'm';
                case Keys.N: return shiftPressed ? 'N' : 'n';
                case Keys.O: return shiftPressed ? 'O' : 'o';
                case Keys.P: return shiftPressed ? 'P' : 'p';
                case Keys.Q: return shiftPressed ? 'Q' : 'q';
                case Keys.R: return shiftPressed ? 'R' : 'r';
                case Keys.S: return shiftPressed ? 'S' : 's';
                case Keys.T: return shiftPressed ? 'T' : 't';
                case Keys.U: return shiftPressed ? 'U' : 'u';
                case Keys.V: return shiftPressed ? 'V' : 'v';
                case Keys.W: return shiftPressed ? 'W' : 'w';
                case Keys.X: return shiftPressed ? 'X' : 'x';
                case Keys.Y: return shiftPressed ? 'Y' : 'y';
                case Keys.Z: return shiftPressed ? 'Z' : 'z';

                case Keys.OemMinus: return shiftPressed ? '_' : '-';
                case Keys.OemPlus: return shiftPressed ? '+' : '=';
                case Keys.OemOpenBrackets: return shiftPressed ? '{' : '[';
                case Keys.OemCloseBrackets: return shiftPressed ? '}' : ']';
                case Keys.OemSemicolon: return shiftPressed ? ':' : ';';
                case Keys.OemQuotes: return shiftPressed ? '"' : '\'';
                case Keys.OemComma: return shiftPressed ? '<' : ',';
                case Keys.OemPeriod: return shiftPressed ? '>' : '.';
                case Keys.OemQuestion: return shiftPressed ? '?' : '/';
                case Keys.OemTilde: return shiftPressed ? '~' : '`';
                case Keys.OemBackslash: return shiftPressed ? '|' : '\\';
                case Keys.OemPipe: return shiftPressed ? '|' : '\\';

                default: return '\0';
            }
        }

        private void SendMessage()
        {
            var message = _currentInput.ToString().Trim();
            if (!string.IsNullOrEmpty(message))
            {
                // Add user message
                AddMessage("User", message);

                // Clear input
                _currentInput.Clear();
                _cursorPosition = 0;

                // Generate Mary's response (simple echo for now)
                GenerateMarysResponse(message);
            }
        }

        private void AddMessage(string sender, string text)
        {
            try
            {
                _engine?.Log($"Chat: Adding message from {sender}: {text}");
                
                // Filter unsupported characters first
                string filteredText = FilterUnsupportedCharacters(text);
                
                // Calculate maximum width for text wrapping (bubble width minus padding)
                int maxBubbleWidth = (_chatAreaBounds.Width > 0 ? _chatAreaBounds.Width : 800) - BUBBLE_MARGIN * 2;
                int maxTextWidth = maxBubbleWidth - BUBBLE_PADDING * 2;
                
                // Wrap the filtered text
                var wrappedLines = WrapText(filteredText, maxTextWidth);
                
                _engine?.Log($"Chat: Text wrapped into {wrappedLines.Count} lines");
                
                // Calculate total height for this message with proper line spacing
                int lineHeight = (int)(_chatFont?.LineSpacing * TEXT_SCALE ?? 16);
                int lineSpacing = 2; // Add small spacing between lines
                int totalTextHeight = (wrappedLines.Count * lineHeight) + ((wrappedLines.Count - 1) * lineSpacing);
                int totalBubbleHeight = totalTextHeight + BUBBLE_PADDING * 2; // Consistent top and bottom padding
                
                var message = new ChatMessage
                {
                    Text = filteredText, // Use filtered text instead of original
                    WrappedLines = wrappedLines,
                    Sender = sender,
                    Timestamp = DateTime.Now,
                    BackgroundColor = sender == "User" ? MESSAGE_USER_COLOR : MESSAGE_MARY_COLOR,
                    TotalHeight = totalBubbleHeight
                };

                _chatMessages.Add(message);
                
                _engine?.Log($"Chat: Message added. Total messages: {_chatMessages.Count}");

                // Auto-scroll to bottom
                UpdateBounds(); // Recalculate content height
                if (_needsScrollbar)
                {
                    _scrollY = Math.Max(0, _contentHeight - _chatAreaBounds.Height);
                }
            }
            catch (Exception ex)
            {
                _engine?.Log($"Chat: Error adding message: {ex.Message}");
            }
        }
        
        private void AddToConversationHistory(string role, string content)
        {
            _conversationHistory.Add(new GptMessage { role = role, content = content });
            
            // Keep only the system message plus the last MAX_HISTORY_MESSAGES user/assistant messages
            if (_conversationHistory.Count > MAX_HISTORY_MESSAGES + 1) // +1 for system message
            {
                // Remove the oldest user/assistant message (keep system message at index 0)
                _conversationHistory.RemoveAt(1);
            }
        }
        
        private List<string> WrapText(string text, int maxWidth)
        {
            if (_chatFont == null || string.IsNullOrEmpty(text))
                return new List<string> { text ?? "" };

            var lines = new List<string>();
            var words = text.Split(' ');
            var currentLine = new StringBuilder();
            
            foreach (var word in words)
            {
                var testLine = currentLine.Length > 0 ? $"{currentLine} {word}" : word;
                
                Vector2 testSize;
                try
                {
                    testSize = _chatFont.MeasureString(testLine) * TEXT_SCALE;
                }
                catch
                {
                    // If measurement fails, assume it's too wide and break the line
                    testSize = new Vector2(maxWidth + 1, 0);
                }
                
                if (testSize.X <= maxWidth)
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
                    
                    // Handle very long words that don't fit on a single line
                    bool wordTooLong = false;
                    try
                    {
                        wordTooLong = _chatFont.MeasureString(word).X * TEXT_SCALE > maxWidth;
                    }
                    catch
                    {
                        // If measurement fails, assume the word is too long
                        wordTooLong = true;
                    }
                    
                    if (wordTooLong)
                    {
                        var chars = word.ToCharArray();
                        var charLine = new StringBuilder();
                        
                        foreach (var ch in chars)
                        {
                            var testChar = $"{charLine}{ch}";
                            bool charLineFits = false;
                            try
                            {
                                charLineFits = _chatFont.MeasureString(testChar).X * TEXT_SCALE <= maxWidth;
                            }
                            catch
                            {
                                // If measurement fails, assume it doesn't fit
                                charLineFits = false;
                            }
                            
                            if (charLineFits)
                            {
                                charLine.Append(ch);
                            }
                            else
                            {
                                if (charLine.Length > 0)
                                {
                                    lines.Add(charLine.ToString());
                                    charLine.Clear();
                                }
                                charLine.Append(ch);
                            }
                        }
                        
                        if (charLine.Length > 0)
                            currentLine.Append(charLine.ToString());
                    }
                    else
                    {
                        currentLine.Append(word);
                    }
                }
            }
            
            if (currentLine.Length > 0)
                lines.Add(currentLine.ToString());
                
            return lines.Count > 0 ? lines : new List<string> { "" };
        }
        
        private void LoadApiKey()
        {
            try
            {
                string configPath = Path.Combine(Directory.GetCurrentDirectory(), "Content", "Modules", "Chat", "api_key.ini");
                _engine?.Log($"Chat: Loading API key from: {configPath}");
                
                if (File.Exists(configPath))
                {
                    string[] lines = File.ReadAllLines(configPath);
                    foreach (string line in lines)
                    {
                        if (line.StartsWith("api_key="))
                        {
                            _gptApiKey = line.Substring("api_key=".Length).Trim();
                            _engine?.Log("Chat: API key loaded successfully");
                            return;
                        }
                    }
                    _engine?.Log("Chat: No api_key found in config file");
                }
                else
                {
                    _engine?.Log("Chat: API key config file not found");
                }
            }
            catch (Exception ex)
            {
                _engine?.Log($"Chat: Error loading API key: {ex.Message}");
            }
        }
        
        private string FilterUnsupportedCharacters(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Replace common problematic characters with safe alternatives
            return text
                .Replace("🎹", "[piano]")  // Musical keyboard emoji
                .Replace("😊", ":)")       // Smiling face
                .Replace("😄", ":D")       // Grinning face
                .Replace("😃", ":D")       // Grinning face with big eyes
                .Replace("😁", ":D")       // Beaming face with smiling eyes
                .Replace("🤔", "?")        // Thinking face
                .Replace("👋", "wave")     // Waving hand
                .Replace("❤️", "<3")       // Red heart
                .Replace("💕", "<3")       // Two hearts
                .Replace("💖", "<3")       // Sparkling heart
                .Replace("✨", "*")        // Sparkles
                .Replace("🌟", "*")        // Star
                .Replace("⭐", "*")        // Star
                .Replace("🎉", "!")        // Party popper
                .Replace("🎊", "!")        // Confetti ball
                .Replace("👍", "+1")       // Thumbs up
                .Replace("👎", "-1")       // Thumbs down
                .Replace("🔥", "fire")     // Fire
                .Replace("💯", "100")      // Hundred points
                .Replace("😂", "LOL")      // Face with tears of joy
                .Replace("🤣", "LOL")      // Rolling on the floor laughing
                .Replace("😭", ":'(")      // Loudly crying face
                .Replace("😢", ":(")       // Crying face
                .Replace("😅", ":'))")     // Grinning face with sweat
                .Replace("😆", "XD")       // Grinning squinting face
                .Replace("🙂", ":)")       // Slightly smiling face
                .Replace("🙃", "(:)")      // Upside-down face
                .Replace("😉", ";)")       // Winking face
                .Replace("😋", ":P")       // Face savoring food
                .Replace("😜", ";P")       // Winking face with tongue
                .Replace("🤗", "hug")      // Hugging face
                .Replace("🤷", "shrug")    // Person shrugging
                .Replace("🤯", "mind-blown") // Exploding head
                .Replace("💪", "strong")   // Flexed biceps
                .Replace("🧠", "brain")    // Brain
                .Replace("💡", "idea")     // Light bulb
                .Replace("📝", "note")     // Memo
                .Replace("📚", "books")    // Books
                .Replace("🎯", "target")   // Direct hit
                .Replace("⚡", "lightning") // High voltage
                .Replace("🌈", "rainbow")  // Rainbow
                .Replace("🎵", "music")    // Musical note
                .Replace("🎶", "music")    // Musical notes
                .Replace("🔊", "loud")     // Speaker high volume
                .Replace("🔇", "mute")     // Speaker with cancellation stroke
                // Add more emoji replacements as needed
                
                // Replace other problematic Unicode characters
                .Replace("│", "|")         // Vertical line
                .Replace("├", "+")         // Tree connector
                .Replace("└", "+")         // Tree end
                .Replace("─", "-")         // Horizontal line
                .Replace("┌", "+")         // Corner
                .Replace("┐", "+")         // Corner
                .Replace("┘", "+")         // Corner
                .Replace("┼", "+")         // Cross
                .Replace("┤", "+")         // Right connector
                .Replace("┬", "+")         // Top connector
                .Replace("┴", "+")         // Bottom connector
                .Replace("█", "#")         // Block
                .Replace("░", ".")         // Light shade
                .Replace("▒", "*")         // Medium shade
                .Replace("▓", "#")         // Dark shade
                .Replace("°", "o")         // Degree symbol
                .Replace("±", "+/-")       // Plus-minus
                .Replace("×", "x")         // Multiplication
                .Replace("÷", "/")         // Division
                .Replace("≤", "<=")        // Less than or equal
                .Replace("≥", ">=")        // Greater than or equal
                .Replace("≠", "!=")        // Not equal
                .Replace("∞", "infinity")  // Infinity
                .Replace("√", "sqrt")      // Square root
                .Replace("π", "pi")        // Pi
                .Replace("\u2018", "'")    // Smart quote left
                .Replace("\u2019", "'")    // Smart quote right
                .Replace("\u201C", "\"")   // Smart quote left
                .Replace("\u201D", "\"")   // Smart quote right
                .Replace("\u2013", "-")    // En dash
                .Replace("\u2014", "--")   // Em dash
                .Replace("\u2026", "...")  // Ellipsis
                .Replace("©", "(c)")       // Copyright
                .Replace("®", "(R)")       // Registered trademark
                .Replace("™", "(TM)")      // Trademark
                .Replace("€", "EUR")       // Euro sign
                .Replace("£", "GBP")       // Pound sign
                .Replace("¥", "YEN")       // Yen sign
                .Replace("¢", "cent")      // Cent sign
                .Replace("§", "section")   // Section sign
                .Replace("¶", "paragraph") // Pilcrow sign
                .Replace("†", "+")         // Dagger
                .Replace("‡", "++")        // Double dagger
                .Replace("•", "*")         // Bullet
                .Replace("‰", "per-mille") // Per mille sign
                .Replace("‱", "per-ten-thousand") // Per ten thousand sign
                .Replace("′", "'")         // Prime
                .Replace("″", "\"")        // Double prime
                .Replace("‴", "'''")       // Triple prime
                .Replace("\u2039", "<")    // Single left-pointing angle quotation mark
                .Replace("\u203A", ">")    // Single right-pointing angle quotation mark
                .Replace("\u00AB", "<<")   // Left-pointing double angle quotation mark
                .Replace("\u00BB", ">>");  // Right-pointing double angle quotation mark
        }
        
        private async Task<string> GetGptResponse(string userMessage)
        {
            try
            {
                _engine?.Log($"Chat: GetGptResponse called with message: '{userMessage}'");
                
                // Check if HTTP client is available
                if (_httpClient == null)
                {
                    _engine?.Log("Chat: HTTP client not initialized");
                    return "I'm having trouble connecting right now. Please try again later.";
                }
                
                _engine?.Log($"Chat: HTTP client is available");
                
                // Add user message to conversation history
                AddToConversationHistory("user", userMessage);
                _engine?.Log($"Chat: Added user message to conversation history. Total messages: {_conversationHistory.Count}");
                
                var request = new GptRequest
                {
                    model = "gpt-4o",
                    messages = _conversationHistory,
                    max_tokens = 150, // Keep responses concise for chat
                    temperature = 0.7 // Balanced creativity
                };
                
                var jsonRequest = JsonSerializer.Serialize(request);
                _engine?.Log($"Chat: Sending request to GPT API: {jsonRequest}");
                
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
                
                _engine?.Log($"Chat: Making HTTP POST request to {GPT_API_URL}");
                var response = await _httpClient.PostAsync(GPT_API_URL, content);
                var jsonResponse = await response.Content.ReadAsStringAsync();
                
                _engine?.Log($"Chat: Received response - Status: {response.StatusCode}, Content: {jsonResponse}");
                
                if (response.IsSuccessStatusCode)
                {
                    var gptResponse = JsonSerializer.Deserialize<GptResponse>(jsonResponse);
                    var assistantMessage = gptResponse?.choices?[0]?.message?.content?.Trim();
                    
                    _engine?.Log($"Chat: Parsed assistant message: '{assistantMessage}'");
                    
                    if (!string.IsNullOrEmpty(assistantMessage))
                    {
                        // Add assistant response to conversation history
                        AddToConversationHistory("assistant", assistantMessage);
                        _engine?.Log($"Chat: Added assistant response to conversation history");
                        return assistantMessage;
                    }
                    else
                    {
                        _engine?.Log($"Chat: Assistant message was null or empty");
                    }
                }
                else
                {
                    _engine?.Log($"Chat: GPT API error: {response.StatusCode} - {jsonResponse}");
                }
            }
            catch (Exception ex)
            {
                _engine?.Log($"Chat: Error calling GPT API: {ex.Message}");
                _engine?.Log($"Chat: Stack trace: {ex.StackTrace}");
            }
            
            // Fallback response if API fails
            _engine?.Log($"Chat: Returning fallback response");
            return "Sorry, I'm having trouble thinking right now. Could you try asking me again?";
        }

        private void GenerateMarysResponse(string userMessage)
        {
            // Start thinking animation
            _isThinking = true;
            _thinkingTimer = 0f;
            _currentThinkingDot = 0;

            _engine?.Log($"Chat: GenerateMarysResponse called for: '{userMessage}'");

            // Use Task.Run to avoid blocking the game thread
            Task.Run(async () =>
            {
                try
                {
                    _engine?.Log($"Chat: Starting GPT response generation for: {userMessage}");
                    
                    string response;
                    
                    // Try to get GPT response, but have a simple fallback
                    try
                    {
                        response = await GetGptResponse(userMessage);
                        _engine?.Log($"Chat: Received GPT response: {response}");
                    }
                    catch (Exception apiEx)
                    {
                        _engine?.Log($"Chat: GPT API failed: {apiEx.Message}, using simple response");
                        // Simple fallback responses for testing
                        var lowerMessage = userMessage.ToLower();
                        if (lowerMessage.Contains("hello") || lowerMessage.Contains("hi"))
                        {
                            response = "Hello! Nice to meet you! How can I help you today?";
                        }
                        else if (lowerMessage.Contains("test"))
                        {
                            response = "This is a test response! The chat system is working correctly.";
                        }
                        else
                        {
                            response = $"I heard you say: '{userMessage}'. That's interesting! Tell me more about it.";
                        }
                    }
                    
                    // Add a small delay to show thinking animation
                    await Task.Delay(1500);
                    
                    _engine?.Log($"Chat: About to add Mary's response: '{response}'");
                    
                    // Stop thinking animation and add message
                    _isThinking = false;
                    
                    // Ensure we add the message
                    AddMessage("Mary", response);
                    
                    _engine?.Log($"Chat: Successfully added Mary's response to chat");
                }
                catch (Exception ex)
                {
                    _engine?.Log($"Chat: Critical error in GenerateMarysResponse: {ex.Message}");
                    _engine?.Log($"Chat: Stack trace: {ex.StackTrace}");
                    _isThinking = false;
                    AddMessage("Mary", "Sorry, I'm having trouble thinking right now. Could you try asking me again?");
                }
            });
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            try
            {
                if (_windowManagement?.IsVisible() != true)
                    return;

                _windowManagement.Draw(spriteBatch, "Chat");

                // Draw left panel (Mary Sue image)
                DrawLeftPanel(spriteBatch);

                // Draw right panel (chat area)
                DrawRightPanel(spriteBatch);
            }
            catch (Exception ex)
            {
                _engine?.Log($"Chat: Error in Draw: {ex.Message}");
            }
        }

        private void DrawLeftPanel(SpriteBatch spriteBatch)
        {
            // Draw left panel background
            spriteBatch.Draw(_pixel, _leftPanelBounds, CHAT_BACKGROUND);
            DrawBorder(spriteBatch, _leftPanelBounds, BORDER_COLOR);

            // Draw Mary Sue image if loaded (25% smaller than previous "twice as big" version)
            if (_marySueImage != null)
            {
                int imageSize = _leftPanelWidth; // Image fits exactly in panel width
                var imageBounds = new Rectangle(
                    _leftPanelBounds.X, // No padding - image starts at left edge
                    _leftPanelBounds.Y, // No padding - image starts at top edge
                    imageSize,
                    imageSize
                );

                spriteBatch.Draw(_marySueImage, imageBounds, Color.White);
            }

            // Draw Mary's name below the image with equal padding between image and window bottom
            if (_pixelFont != null)
            {
                var nameText = "Mary Sue";
                var nameSize = _pixelFont.MeasureString(nameText);
                
                // Calculate available space below image
                int spaceBelow = _leftPanelBounds.Height - _leftPanelWidth;
                
                // Center the name vertically in the remaining space
                var namePosition = new Vector2(
                    _leftPanelBounds.X + (_leftPanelBounds.Width - nameSize.X) / 2,
                    _leftPanelBounds.Y + _leftPanelWidth + (spaceBelow - nameSize.Y) / 2
                );

                spriteBatch.DrawString(_pixelFont, nameText, namePosition, TEXT_COLOR);
            }
        }

        private void DrawRightPanel(SpriteBatch spriteBatch)
        {
            // Draw chat area
            DrawChatArea(spriteBatch);

            // Draw input area
            DrawInputArea(spriteBatch);
        }

        private void DrawChatArea(SpriteBatch spriteBatch)
        {
            // Draw chat area background
            spriteBatch.Draw(_pixel, _chatAreaBounds, CHAT_BACKGROUND);
            DrawBorder(spriteBatch, _chatAreaBounds, BORDER_COLOR);

            // Draw chat messages as bubbles with scrolling
            if (_chatFont != null && _chatMessages.Count > 0)
            {
                int currentY = _chatAreaBounds.Y + PANEL_PADDING - _scrollY;
                
                for (int i = 0; i < _chatMessages.Count; i++)
                {
                    var message = _chatMessages[i];
                    
                    // Only draw if the bubble is visible in the chat area
                    if (currentY + message.TotalHeight >= _chatAreaBounds.Y && currentY <= _chatAreaBounds.Bottom)
                    {
                        DrawChatBubble(spriteBatch, message, currentY);
                    }
                    
                    currentY += message.TotalHeight + MESSAGE_PADDING;
                    
                    // Stop drawing if we're way below the visible area
                    if (currentY > _chatAreaBounds.Bottom + 100)
                        break;
                }
            }

            // Draw thinking animation if Mary is thinking
            if (_isThinking)
            {
                DrawThinkingAnimation(spriteBatch);
            }

            // Draw scrollbar if needed
            if (_needsScrollbar)
            {
                DrawScrollbar(spriteBatch);
            }
        }

        private void DrawChatBubble(SpriteBatch spriteBatch, ChatMessage message, int y)
        {
            if (message.WrappedLines == null || message.WrappedLines.Count == 0)
                return;

            // Calculate bubble dimensions based on wrapped text
            int lineHeight = (int)(_chatFont.LineSpacing * TEXT_SCALE);
            int lineSpacing = 2; // Same spacing as in AddMessage
            int maxLineWidth = 0;
            
            // Find the widest line to determine bubble width
            foreach (var line in message.WrappedLines)
            {
                var lineSize = _chatFont.MeasureString(line) * TEXT_SCALE;
                maxLineWidth = Math.Max(maxLineWidth, (int)lineSize.X);
            }
            
            int bubbleWidth = Math.Min(maxLineWidth + BUBBLE_PADDING * 2, _chatAreaBounds.Width - BUBBLE_MARGIN * 2);
            int bubbleHeight = message.TotalHeight;

            // Position bubble based on sender (user on right, Mary on left)
            bool isUser = message.Sender == "User";
            int bubbleX = isUser ? 
                _chatAreaBounds.Right - BUBBLE_MARGIN - bubbleWidth : 
                _chatAreaBounds.X + BUBBLE_MARGIN;

            var bubbleBounds = new Rectangle(bubbleX, y, bubbleWidth, bubbleHeight);

            // Draw rounded bubble background
            DrawRoundedRectangle(spriteBatch, bubbleBounds, message.BackgroundColor, BUBBLE_CORNER_RADIUS);
            
            // Draw bubble border
            DrawRoundedRectangleBorder(spriteBatch, bubbleBounds, BUBBLE_BORDER_COLOR, BUBBLE_CORNER_RADIUS, 1);

            // Draw each line of wrapped text
            var textPosition = new Vector2(
                bubbleBounds.X + BUBBLE_PADDING,
                bubbleBounds.Y + BUBBLE_PADDING
            );

            for (int i = 0; i < message.WrappedLines.Count; i++)
            {
                var linePosition = new Vector2(textPosition.X, textPosition.Y + (i * (lineHeight + lineSpacing)));
                spriteBatch.DrawString(_chatFont, message.WrappedLines[i], linePosition, Color.White, 0f, Vector2.Zero, TEXT_SCALE, SpriteEffects.None, 0f);
            }
        }

        private void DrawRoundedRectangle(SpriteBatch spriteBatch, Rectangle bounds, Color color, int cornerRadius)
        {
            // For simplicity, we'll create a rounded effect by drawing a main rectangle and corner pieces
            // Main rectangle (without corners)
            spriteBatch.Draw(_pixel, new Rectangle(bounds.X + cornerRadius, bounds.Y, bounds.Width - cornerRadius * 2, bounds.Height), color);
            spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y + cornerRadius, cornerRadius, bounds.Height - cornerRadius * 2), color);
            spriteBatch.Draw(_pixel, new Rectangle(bounds.Right - cornerRadius, bounds.Y + cornerRadius, cornerRadius, bounds.Height - cornerRadius * 2), color);

            // Corner approximation (small rectangles to simulate rounded corners)
            int cornerSize = Math.Max(1, cornerRadius / 2);
            
            // Top-left corner
            spriteBatch.Draw(_pixel, new Rectangle(bounds.X + cornerSize, bounds.Y, cornerRadius - cornerSize, cornerSize), color);
            spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y + cornerSize, cornerSize, cornerRadius - cornerSize), color);
            
            // Top-right corner
            spriteBatch.Draw(_pixel, new Rectangle(bounds.Right - cornerRadius, bounds.Y, cornerRadius - cornerSize, cornerSize), color);
            spriteBatch.Draw(_pixel, new Rectangle(bounds.Right - cornerSize, bounds.Y + cornerSize, cornerSize, cornerRadius - cornerSize), color);
            
            // Bottom-left corner
            spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Bottom - cornerRadius, cornerSize, cornerRadius - cornerSize), color);
            spriteBatch.Draw(_pixel, new Rectangle(bounds.X + cornerSize, bounds.Bottom - cornerSize, cornerRadius - cornerSize, cornerSize), color);
            
            // Bottom-right corner
            spriteBatch.Draw(_pixel, new Rectangle(bounds.Right - cornerRadius, bounds.Bottom - cornerSize, cornerRadius - cornerSize, cornerSize), color);
            spriteBatch.Draw(_pixel, new Rectangle(bounds.Right - cornerSize, bounds.Bottom - cornerRadius, cornerSize, cornerRadius - cornerSize), color);
        }

        private void DrawRoundedRectangleBorder(SpriteBatch spriteBatch, Rectangle bounds, Color color, int cornerRadius, int thickness)
        {
            // Simple border - just draw thin rectangles around the edges
            // Top
            spriteBatch.Draw(_pixel, new Rectangle(bounds.X + cornerRadius, bounds.Y, bounds.Width - cornerRadius * 2, thickness), color);
            // Bottom
            spriteBatch.Draw(_pixel, new Rectangle(bounds.X + cornerRadius, bounds.Bottom - thickness, bounds.Width - cornerRadius * 2, thickness), color);
            // Left
            spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y + cornerRadius, thickness, bounds.Height - cornerRadius * 2), color);
            // Right
            spriteBatch.Draw(_pixel, new Rectangle(bounds.Right - thickness, bounds.Y + cornerRadius, thickness, bounds.Height - cornerRadius * 2), color);
        }

        private void DrawInputArea(SpriteBatch spriteBatch)
        {
            // Draw input area background
            spriteBatch.Draw(_pixel, _inputAreaBounds, INPUT_BACKGROUND);
            DrawBorder(spriteBatch, _inputAreaBounds, BORDER_COLOR);

            // Draw input text
            if (_chatFont != null)
            {
                var inputText = _currentInput.ToString();
                var textPosition = new Vector2(
                    _inputAreaBounds.X + PANEL_PADDING,
                    _inputAreaBounds.Y + (_inputAreaBounds.Height - _chatFont.LineSpacing) / 2
                );

                spriteBatch.DrawString(_chatFont, inputText, textPosition, TEXT_COLOR, 0f, Vector2.Zero, TEXT_SCALE, SpriteEffects.None, 0f);

                // Draw cursor
                if (_showCursor && _isFocused)
                {
                    var cursorX = textPosition.X + _chatFont.MeasureString(inputText.Substring(0, _cursorPosition)).X * TEXT_SCALE;
                    var cursorBounds = new Rectangle(
                        (int)cursorX,
                        (int)textPosition.Y,
                        1,
                        (int)(_chatFont.LineSpacing * TEXT_SCALE)
                    );
                    spriteBatch.Draw(_pixel, cursorBounds, TEXT_COLOR);
                }
            }

            // Draw send button
            DrawSendButton(spriteBatch);
        }

        private void DrawSendButton(SpriteBatch spriteBatch)
        {
            bool isHovered = _sendButtonBounds.Contains(_currentMouseState.Position);
            Color buttonColor = isHovered ? SEND_BUTTON_HOVER : SEND_BUTTON_COLOR;
            Color borderColor = isHovered ? Color.White : BORDER_COLOR;

            // Draw button background with rounded corners
            DrawRoundedRectangle(spriteBatch, _sendButtonBounds, buttonColor, 4);
            DrawRoundedRectangleBorder(spriteBatch, _sendButtonBounds, borderColor, 4, 1);

            // Draw "Send" text with pixel font
            if (_pixelFont != null)
            {
                var sendText = "SEND";
                var textSize = _pixelFont.MeasureString(sendText);
                var textPosition = new Vector2(
                    _sendButtonBounds.X + (_sendButtonBounds.Width - textSize.X) / 2,
                    _sendButtonBounds.Y + (_sendButtonBounds.Height - textSize.Y) / 2
                );

                spriteBatch.DrawString(_pixelFont, sendText, textPosition, Color.White);
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

        private void DrawScrollbar(SpriteBatch spriteBatch)
        {
            if (!_needsScrollbar) return;

            // Draw scrollbar track
            Color trackColor = new Color(40, 40, 40);
            spriteBatch.Draw(_pixel, _scrollbarBounds, trackColor);

            // Calculate thumb position and size
            float contentRatio = (float)_chatAreaBounds.Height / _contentHeight;
            int thumbHeight = Math.Max(20, (int)(_scrollbarBounds.Height * contentRatio));
            
            float scrollRatio = (float)_scrollY / (_contentHeight - _chatAreaBounds.Height);
            int thumbY = _scrollbarBounds.Y + (int)((_scrollbarBounds.Height - thumbHeight) * scrollRatio);

            // Draw scrollbar thumb
            Rectangle thumbBounds = new Rectangle(
                _scrollbarBounds.X + 2,
                thumbY,
                _scrollbarBounds.Width - 4,
                thumbHeight
            );

            Color thumbColor = _isHoveringScrollbar ? new Color(100, 100, 100) : new Color(80, 80, 80);
            spriteBatch.Draw(_pixel, thumbBounds, thumbColor);
        }

        private void DrawThinkingAnimation(SpriteBatch spriteBatch)
        {
            // Position the thinking dots at the bottom of the chat area (like Mary's message position)
            int bubbleHeight = (int)(_chatFont.LineSpacing * TEXT_SCALE) + BUBBLE_PADDING * 2;
            int bubbleWidth = 60; // Fixed width for thinking bubble (smaller)
            
            int bubbleX = _chatAreaBounds.X + BUBBLE_MARGIN; // Left side like Mary's messages
            int bubbleY = _chatAreaBounds.Bottom - bubbleHeight - PANEL_PADDING;

            var bubbleBounds = new Rectangle(bubbleX, bubbleY, bubbleWidth, bubbleHeight);

            // Draw thinking bubble background
            DrawRoundedRectangle(spriteBatch, bubbleBounds, MESSAGE_MARY_COLOR, BUBBLE_CORNER_RADIUS);
            DrawRoundedRectangleBorder(spriteBatch, bubbleBounds, BUBBLE_BORDER_COLOR, BUBBLE_CORNER_RADIUS, 1);

            // Draw three dots with animation (smaller)
            int dotSize = 4;
            int dotSpacing = 8;
            int totalDotsWidth = (dotSize * 3) + (dotSpacing * 2);
            int startX = bubbleBounds.X + (bubbleBounds.Width - totalDotsWidth) / 2;
            int dotY = bubbleBounds.Y + (bubbleBounds.Height - dotSize) / 2;

            for (int i = 0; i < 3; i++)
            {
                int dotX = startX + i * (dotSize + dotSpacing);
                
                // Calculate scale based on current thinking dot and timer
                float scale = 1.0f;
                if (i == _currentThinkingDot)
                {
                    // Animate the current dot with a sine wave
                    float animationTime = _thinkingTimer / THINKING_DOT_INTERVAL;
                    scale = 1.0f + 0.5f * (float)Math.Sin(animationTime * Math.PI * THINKING_SCALE_SPEED);
                }

                int scaledSize = (int)(dotSize * scale);
                int scaledX = dotX - (scaledSize - dotSize) / 2;
                int scaledY = dotY - (scaledSize - dotSize) / 2;

                var dotBounds = new Rectangle(scaledX, scaledY, scaledSize, scaledSize);
                spriteBatch.Draw(_pixel, dotBounds, Color.White);
            }
        }

        public void UpdateWindowWidth(int newWidth)
        {
            _windowWidth = newWidth;
            _windowManagement?.UpdateWindowWidth(newWidth);
        }

        public void LoadContent(ContentManager content)
        {
            _windowManagement?.LoadContent(content);
            
            // Load chat font (use a clean font for chat)
            _chatFont = content.Load<SpriteFont>("Fonts/SpriteFonts/open_sans/regular");
            
            // Load pixel font for Mary Sue name and Send button
            _pixelFont = content.Load<SpriteFont>("Fonts/SpriteFonts/pixel_font/regular");

            // Load Mary Sue image
            try
            {
                _marySueImage = content.Load<Texture2D>("Modules/Chat/Mary Sue");
            }
            catch (Exception ex)
            {
                _engine?.Log($"Chat: Failed to load Mary Sue image: {ex.Message}");
                // Continue without image
            }
        }

        public void Dispose()
        {
            _pixel?.Dispose();
            _windowManagement?.Dispose();
            // Note: _httpClient is static and shared, so we don't dispose it here
        }
    }
}
