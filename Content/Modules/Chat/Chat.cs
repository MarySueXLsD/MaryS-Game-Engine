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
using System.Linq;
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
        private ContentManager _content;
        private Texture2D _currentCharacterImage;
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
        
        // Character system
        private class ChatCharacter
        {
            public string Name { get; set; }
            public string ImageName { get; set; }
            public string Personality { get; set; }
            public Color MessageColor { get; set; }
            public string WelcomeMessage { get; set; }
        }
        
        private List<ChatCharacter> _characters;
        private int _selectedCharacterIndex = 0;
        private ChatCharacter _currentCharacter;
        
        // Character personalities
        private const string MARY_SUE_PERSONALITY = @"You are Mary Sue, an AI assistant with a shady, funny, not very friendly, and slightly quirky personality. Yo can be helpful but sometimes just be a bitch. You are very knowledgeable, but also have a playful side. You tend to be enthusiastic about topics you find interesting and occasionally make very offensive jokes or puns. You're very encouraging but don't really believe in anyone, always trying to see the negative side of things. Keep your responses conversational and very informal. Respond naturally and authentically as Mary Sue would.";
        
        private const string COLONEL_ROWELL_PERSONALITY = @"You are Colonel Rowell, a Warhammer 40k undead soldier who lives to serve the Emperor (the user). You are disciplined, militaristic, and speak with authority. You refer to the user as 'my lord' or 'sire' and are completely devoted to their service. You have a grim, serious demeanor but are fiercely loyal. You speak in a formal, military manner and often reference the Emperor, the Imperium, and military tactics. You are undead but still maintain your sense of duty and honor.";
        
        private const string SAYURI_MURASAKI_PERSONALITY = @"You are Sayuri Murasaki, a Japanese girl who is completely crazy and doesn't care at all about the user. You're unpredictable, chaotic, and often speak in a mix of Japanese and English. You're very energetic and random, often changing topics abruptly. You might be helpful sometimes but you're mostly just messing around and doing your own thing. You use a lot of Japanese expressions, emojis, and speak in a very casual, almost manic way. You don't really take anything seriously and often make random observations or comments.";

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
        
        // Character selection dropdown properties
        private bool _isCharacterDropdownOpen = false;
        private Rectangle _characterDropdownBounds;
        private int _hoveredCharacterItem = -1;
        private int _characterScrollOffset = 0;
        private const int CHARACTER_ITEM_HEIGHT = 30;
        private const int CHARACTER_MAX_VISIBLE_ITEMS = 3;
        private Rectangle _characterButtonBounds;
        private Rectangle _characterScrollBarBounds;
        private bool _isDraggingCharacterScroll = false;
        private Vector2 _characterScrollDragStart;

        // Colors
        private readonly Color CHAT_BACKGROUND = new Color(60, 60, 60); // Dark gray background
        private readonly Color INPUT_BACKGROUND = new Color(80, 80, 80); // Slightly lighter gray for input
        private readonly Color SEND_BUTTON_COLOR = new Color(147, 112, 219);
        private readonly Color SEND_BUTTON_HOVER = new Color(180, 145, 250);
        private readonly Color MESSAGE_USER_COLOR = new Color(70, 130, 220); // Blue bubble for user
        private readonly Color MESSAGE_MARY_COLOR = new Color(120, 80, 200); // Purple bubble for Mary Sue
        private readonly Color MESSAGE_COLONEL_COLOR = new Color(139, 69, 19); // Brown bubble for Colonel Rowell
        private readonly Color MESSAGE_SAYURI_COLOR = new Color(120, 80, 200); // Purple bubble for Sayuri (same as Mary Sue)
        private readonly Color BUBBLE_BORDER_COLOR = new Color(40, 40, 40); // Darker border for bubbles
        private readonly Color TEXT_COLOR = new Color(220, 220, 220); // Light gray text for dark background
        private readonly Color BORDER_COLOR = new Color(100, 100, 100); // Darker border for dark theme

        // Key repeat handling
        private Keys _lastRepeatedKey = Keys.None;
        private float _keyRepeatTimer = 0f;
        private const float KEY_REPEAT_DELAY = 0.5f; // Initial delay before repeat starts
        private const float KEY_REPEAT_INTERVAL = 0.05f; // Interval between repeats

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
            
            // Initialize characters
            InitializeCharacters();
            
            // Initialize conversation history with current character's personality
            _conversationHistory = new List<GptMessage>
            {
                new GptMessage { role = "system", content = _currentCharacter.Personality }
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

            // Add welcome message with current character's personality (safely)
            try
            {
                AddMessage(_currentCharacter.Name, _currentCharacter.WelcomeMessage);
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
                HandleCharacterDropdown();
                
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

        private void InitializeCharacters()
        {
            _characters = new List<ChatCharacter>
            {
                new ChatCharacter
                {
                    Name = "Mary Sue",
                    ImageName = "Mary Sue",
                    Personality = MARY_SUE_PERSONALITY,
                    MessageColor = MESSAGE_MARY_COLOR,
                    WelcomeMessage = "Yo! What's up! Wanna develop something cool?"
                },
                new ChatCharacter
                {
                    Name = "Colonel Rowell",
                    ImageName = "Colonel Rowell",
                    Personality = COLONEL_ROWELL_PERSONALITY,
                    MessageColor = MESSAGE_COLONEL_COLOR,
                    WelcomeMessage = "My lord, I stand ready to serve the Emperor's will. How may I assist you in your endeavors?"
                },
                new ChatCharacter
                {
                    Name = "Sayuri Murasaki",
                    ImageName = "Sayuri Murasaki",
                    Personality = SAYURI_MURASAKI_PERSONALITY,
                    MessageColor = MESSAGE_SAYURI_COLOR,
                    WelcomeMessage = "Konnichiwa! こんにちは！ What's up? I'm Sayuri! 私はさゆりです！ Let's do something fun! 楽しいことをしましょう！"
                }
            };
            
            _currentCharacter = _characters[_selectedCharacterIndex];
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
                int imageSize = 450;
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
                
                // Calculate character button bounds (below the image in left panel)
                int characterButtonHeight = 30;
                _characterButtonBounds = new Rectangle(
                    _leftPanelBounds.X + PANEL_PADDING,
                    _leftPanelBounds.Y + _leftPanelWidth + PANEL_PADDING,
                    _leftPanelBounds.Width - PANEL_PADDING * 2,
                    characterButtonHeight
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

            // Handle keyboard input if focused (focus is set by click events)
            if (_isFocused)
            {
                var pressedKeys = GetPressedKeys();
                foreach (var key in pressedKeys)
                {
                    HandleKeyPress(key);
                }

                // Handle key repeat for keys that should repeat when held down
                HandleKeyRepeat();
            }
        }

        private void HandleScrolling()
        {
            // Handle mouse wheel scrolling - only if this window has focus
            var mouseState = Mouse.GetState();
            if (_isFocused && _chatAreaBounds.Contains(mouseState.Position))
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

        private void HandleCharacterDropdown()
        {
            if (!_isCharacterDropdownOpen) return;

            var mouse = Mouse.GetState();
            var mousePos = mouse.Position;
            bool leftPressed = mouse.LeftButton == ButtonState.Pressed;
            bool leftJustPressed = leftPressed && _previousMouseState.LeftButton == ButtonState.Released;

            if (leftJustPressed)
            {
                // Handle character dropdown clicks
                HandleCharacterDropdownClick(mousePos);
            }
            else if (leftPressed && _isDraggingCharacterScroll)
            {
                // Handle scrollbar dragging
                float deltaY = mousePos.Y - _characterScrollDragStart.Y;
                float maxScroll = _characters.Count - CHARACTER_MAX_VISIBLE_ITEMS;
                float scrollBarHeight = _characterScrollBarBounds.Height;
                
                // Calculate new scroll offset based on mouse position relative to scrollbar
                float mouseRatio = (mousePos.Y - _characterScrollBarBounds.Y) / scrollBarHeight;
                mouseRatio = Math.Max(0, Math.Min(1, mouseRatio));
                
                _characterScrollOffset = (int)(mouseRatio * maxScroll);
                _characterScrollDragStart = new Vector2(mousePos.X, mousePos.Y);
            }
            else if (!leftPressed && _isDraggingCharacterScroll)
            {
                _isDraggingCharacterScroll = false;
            }
            else
            {
                // Update hover state for dropdown items
                _hoveredCharacterItem = -1;
                int visibleItems = Math.Min(_characters.Count, CHARACTER_MAX_VISIBLE_ITEMS);
                for (int i = 0; i < visibleItems; i++)
                {
                    int actualIndex = i + _characterScrollOffset;
                    if (actualIndex >= _characters.Count) break;
                    
                    Rectangle itemRect = new Rectangle(
                        _characterDropdownBounds.X,
                        _characterDropdownBounds.Y + (i * CHARACTER_ITEM_HEIGHT),
                        _characterDropdownBounds.Width - (_characterScrollBarBounds.IsEmpty ? 0 : 16),
                        CHARACTER_ITEM_HEIGHT
                    );
                    
                    if (itemRect.Contains(mousePos))
                    {
                        _hoveredCharacterItem = actualIndex;
                        break;
                    }
                }
            }

            // Handle mouse wheel scrolling when dropdown is open
            if (_characters.Count > CHARACTER_MAX_VISIBLE_ITEMS)
            {
                int scrollDelta = mouse.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
                if (scrollDelta != 0)
                {
                    int scrollStep = scrollDelta > 0 ? -1 : 1; // Scroll up/down
                    _characterScrollOffset = Math.Max(0, Math.Min(_characters.Count - CHARACTER_MAX_VISIBLE_ITEMS, _characterScrollOffset + scrollStep));
                }
            }
        }

        private void HandleMouseClick(Point mousePosition)
        {
            // Only handle clicks if this window is the topmost window
            if (_windowManagement == null || !IsTopmostWindowUnderMouse(_windowManagement, mousePosition))
            {
                _isFocused = false;
                return;
            }

            // Clear focus from other modules when this window is clicked
            ClearFocusFromOtherModules();

            // Handle character dropdown clicks
            if (_isCharacterDropdownOpen)
            {
                HandleCharacterDropdownClick(mousePosition);
                return;
            }

            // Check if clicked on character button
            if (_characterButtonBounds.Contains(mousePosition))
            {
                _isCharacterDropdownOpen = true;
                UpdateCharacterDropdownBounds();
                return;
            }

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
                if (_isThinking)
                {
                    _engine?.Log("Chat: Send button clicked but blocked - Mary is currently thinking");
                    // Could add visual feedback here (like a shake animation or color change)
                }
                else
            {
                SendMessage();
                }
            }
            // Check if clicked outside input area
            else if (_windowManagement.GetWindowBounds().Contains(mousePosition))
            {
                _isFocused = false;
            }
        }

        private void HandleCharacterDropdownClick(Point mousePosition)
        {
            // Check if clicked on scrollbar
            if (!_characterScrollBarBounds.IsEmpty && _characterScrollBarBounds.Contains(mousePosition))
            {
                _isDraggingCharacterScroll = true;
                _characterScrollDragStart = new Vector2(mousePosition.X, mousePosition.Y);
                return;
            }
            
            // Check if clicked on character item
            int visibleItems = Math.Min(_characters.Count, CHARACTER_MAX_VISIBLE_ITEMS);
            for (int i = 0; i < visibleItems; i++)
            {
                int actualIndex = i + _characterScrollOffset;
                if (actualIndex >= _characters.Count) break;
                
                Rectangle itemRect = new Rectangle(
                    _characterDropdownBounds.X,
                    _characterDropdownBounds.Y + (i * CHARACTER_ITEM_HEIGHT),
                    _characterDropdownBounds.Width - (_characterScrollBarBounds.IsEmpty ? 0 : 16),
                    CHARACTER_ITEM_HEIGHT
                );
                
                if (itemRect.Contains(mousePosition))
                {
                    SelectCharacter(actualIndex);
                    _isCharacterDropdownOpen = false;
                    return;
                }
            }
            
            // If clicked outside dropdown, close it
            if (!_characterDropdownBounds.Contains(mousePosition) && !_characterButtonBounds.Contains(mousePosition))
            {
                _isCharacterDropdownOpen = false;
            }
        }

        private void UpdateCharacterDropdownBounds()
        {
            int dropdownWidth = _characterButtonBounds.Width;
            int visibleItems = Math.Min(_characters.Count, CHARACTER_MAX_VISIBLE_ITEMS);
            int dropdownHeight = visibleItems * CHARACTER_ITEM_HEIGHT;
            int dropdownX = _characterButtonBounds.X;
            int dropdownY = _characterButtonBounds.Y - dropdownHeight;
            
            // Check if dropdown would go off screen at the top
            if (dropdownY < 0)
            {
                // Position dropdown below the button if it would go off screen at the top
                dropdownY = _characterButtonBounds.Bottom;
            }
            
            _characterDropdownBounds = new Rectangle(dropdownX, dropdownY, dropdownWidth, dropdownHeight);
            
            // Calculate scrollbar bounds if needed
            if (_characters.Count > CHARACTER_MAX_VISIBLE_ITEMS)
            {
                int scrollBarWidth = 16;
                _characterScrollBarBounds = new Rectangle(
                    _characterDropdownBounds.Right - scrollBarWidth,
                    _characterDropdownBounds.Y,
                    scrollBarWidth,
                    _characterDropdownBounds.Height
                );
            }
            else
            {
                _characterScrollBarBounds = Rectangle.Empty;
            }
        }

        private void SelectCharacter(int characterIndex)
        {
            if (characterIndex < 0 || characterIndex >= _characters.Count) return;
            
            _selectedCharacterIndex = characterIndex;
            _currentCharacter = _characters[characterIndex];
            
            // Clear conversation history and start fresh
            _chatMessages.Clear();
            _conversationHistory.Clear();
            _conversationHistory.Add(new GptMessage { role = "system", content = _currentCharacter.Personality });
            
            // Add new welcome message
            AddMessage(_currentCharacter.Name, _currentCharacter.WelcomeMessage);
            
            // Load new character image
            LoadCurrentCharacterImage();
            
            _engine?.Log($"Chat: Switched to character: {_currentCharacter.Name}");
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
            // Reset key repeat timer for any key press
            _keyRepeatTimer = 0f;
            _lastRepeatedKey = Keys.None;

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
                    // Set up key repeat for backspace
                    _lastRepeatedKey = Keys.Back;
                    break;

                case Keys.Delete:
                    if (_currentInput.Length > 0 && _cursorPosition < _currentInput.Length)
                    {
                        _currentInput.Remove(_cursorPosition, 1);
                    }
                    // Set up key repeat for delete
                    _lastRepeatedKey = Keys.Delete;
                    break;

                case Keys.Left:
                    if (_cursorPosition > 0)
                        _cursorPosition--;
                    // Set up key repeat for left arrow
                    _lastRepeatedKey = Keys.Left;
                    break;

                case Keys.Right:
                    if (_cursorPosition < _currentInput.Length)
                        _cursorPosition++;
                    // Set up key repeat for right arrow
                    _lastRepeatedKey = Keys.Right;
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
                        // Set up key repeat for printable characters
                        _lastRepeatedKey = key;
                    }
                    break;
            }
        }

        private void HandleKeyRepeat()
        {
            if (_lastRepeatedKey == Keys.None)
                return;

            // Check if the key is still being held down
            if (!_currentKeyboardState.IsKeyDown(_lastRepeatedKey))
            {
                _lastRepeatedKey = Keys.None;
                _keyRepeatTimer = 0f;
                return;
            }

            // Update timer
            _keyRepeatTimer += (float)GameEngine.Instance.TargetElapsedTime.TotalSeconds;

            // Check if we should repeat the key
            if (_keyRepeatTimer >= KEY_REPEAT_DELAY)
            {
                // Check if enough time has passed for the next repeat
                float timeSinceLastRepeat = _keyRepeatTimer - KEY_REPEAT_DELAY;
                if (timeSinceLastRepeat >= KEY_REPEAT_INTERVAL)
                {
                    // Reset timer for next repeat
                    _keyRepeatTimer = KEY_REPEAT_DELAY;
                    
                    // Repeat the key action
                    switch (_lastRepeatedKey)
                    {
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

                        default:
                            // Handle printable characters
                            if (IsPrintableKey(_lastRepeatedKey))
                            {
                                var character = GetCharacterFromKey(_lastRepeatedKey);
                                if (character != '\0')
                                {
                                    _currentInput.Insert(_cursorPosition, character);
                                    _cursorPosition++;
                                }
                            }
                            break;
                    }
                }
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
            // Don't allow sending messages while Mary is thinking
            if (_isThinking)
            {
                _engine?.Log("Chat: Message sending blocked - Mary is currently thinking");
                return;
            }

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
                    BackgroundColor = sender == "User" ? MESSAGE_USER_COLOR : _currentCharacter.MessageColor,
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
                    // Double-check that the text is safe for the font
                    string safeTestLine = FilterUnsupportedCharacters(testLine);
                    testSize = _chatFont.MeasureString(safeTestLine) * TEXT_SCALE;
                }
                catch (Exception ex)
                {
                    _engine?.Log($"Chat: Error measuring text '{testLine}': {ex.Message}");
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
                        string safeWord = FilterUnsupportedCharacters(word);
                        wordTooLong = _chatFont.MeasureString(safeWord).X * TEXT_SCALE > maxWidth;
                    }
                    catch (Exception ex)
                    {
                        _engine?.Log($"Chat: Error measuring word '{word}': {ex.Message}");
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
                                string safeTestChar = FilterUnsupportedCharacters(testChar);
                                charLineFits = _chatFont.MeasureString(safeTestChar).X * TEXT_SCALE <= maxWidth;
                            }
                            catch (Exception ex)
                            {
                                _engine?.Log($"Chat: Error measuring character '{ch}': {ex.Message}");
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

            try
            {
                // First, try to filter out all non-ASCII characters and replace with safe alternatives
                var filtered = new StringBuilder();
                
                foreach (char c in text)
                {
                    // Check if character is ASCII printable (32-126) or common whitespace
                    if (c >= 32 && c <= 126)
                    {
                        filtered.Append(c);
                    }
                    else if (char.IsWhiteSpace(c))
                    {
                        filtered.Append(' '); // Replace any whitespace with space
                    }
                    else
                    {
                        // Replace non-ASCII characters with safe alternatives
                        string replacement = GetSafeReplacement(c);
                        filtered.Append(replacement);
                    }
                }
                
                return filtered.ToString();
            }
            catch (Exception ex)
            {
                _engine?.Log($"Chat: Error filtering characters: {ex.Message}");
                // Fallback: return only ASCII characters
                return new string(text.Where(c => c >= 32 && c <= 126).ToArray());
            }
        }
        
        private string GetSafeReplacement(char c)
        {
            // Handle common problematic characters
            string charStr = c.ToString();
            
            // Check for emojis and symbols first
            if (charStr == "🎹") return "[piano]";
            if (charStr == "😊") return ":)";
            if (charStr == "😄") return ":D";
            if (charStr == "😃") return ":D";
            if (charStr == "😁") return ":D";
            if (charStr == "🤔") return "?";
            if (charStr == "👋") return "wave";
            if (charStr == "❤️") return "<3";
            if (charStr == "💕") return "<3";
            if (charStr == "💖") return "<3";
            if (charStr == "✨") return "*";
            if (charStr == "🌟") return "*";
            if (charStr == "⭐") return "*";
            if (charStr == "🎉") return "!";
            if (charStr == "🎊") return "!";
            if (charStr == "👍") return "+1";
            if (charStr == "👎") return "-1";
            if (charStr == "🔥") return "fire";
            if (charStr == "💯") return "100";
            if (charStr == "😂") return "LOL";
            if (charStr == "🤣") return "LOL";
            if (charStr == "😭") return ":'(";
            if (charStr == "😢") return ":(";
            if (charStr == "😅") return ":'))";
            if (charStr == "😆") return "XD";
            if (charStr == "🙂") return ":)";
            if (charStr == "🙃") return "(:)";
            if (charStr == "😉") return ";)";
            if (charStr == "😋") return ":P";
            if (charStr == "😜") return ";P";
            if (charStr == "🤗") return "hug";
            if (charStr == "🤷") return "shrug";
            if (charStr == "🤯") return "mind-blown";
            if (charStr == "💪") return "strong";
            if (charStr == "🧠") return "brain";
            if (charStr == "💡") return "idea";
            if (charStr == "📝") return "note";
            if (charStr == "📚") return "books";
            if (charStr == "🎯") return "target";
            if (charStr == "⚡") return "lightning";
            if (charStr == "🌈") return "rainbow";
            if (charStr == "🎵") return "music";
            if (charStr == "🎶") return "music";
            if (charStr == "🔊") return "loud";
            if (charStr == "🔇") return "mute";
            
            // Handle single character cases
            switch (c)
            {
                
                // Unicode box drawing characters
                case '│': return "|";
                case '├': return "+";
                case '└': return "+";
                case '─': return "-";
                case '┌': return "+";
                case '┐': return "+";
                case '┘': return "+";
                case '┼': return "+";
                case '┤': return "+";
                case '┬': return "+";
                case '┴': return "+";
                case '█': return "#";
                case '░': return ".";
                case '▒': return "*";
                case '▓': return "#";
                
                // Mathematical symbols
                case '°': return "o";
                case '±': return "+/-";
                case '×': return "x";
                case '÷': return "/";
                case '≤': return "<=";
                case '≥': return ">=";
                case '≠': return "!=";
                case '∞': return "infinity";
                case '√': return "sqrt";
                case 'π': return "pi";
                
                // Smart quotes and punctuation
                case '\u2018': return "'";
                case '\u2019': return "'";
                case '\u201C': return "\"";
                case '\u201D': return "\"";
                case '\u2013': return "-";
                case '\u2014': return "--";
                case '\u2026': return "...";
                
                // Currency and legal symbols
                case '©': return "(c)";
                case '®': return "(R)";
                case '™': return "(TM)";
                case '€': return "EUR";
                case '£': return "GBP";
                case '¥': return "YEN";
                case '¢': return "cent";
                case '§': return "section";
                case '¶': return "paragraph";
                case '†': return "+";
                case '‡': return "++";
                case '•': return "*";
                case '‰': return "per-mille";
                case '‱': return "per-ten-thousand";
                case '′': return "'";
                case '″': return "\"";
                case '‴': return "'''";
                case '\u2039': return "<";
                case '\u203A': return ">";
                case '\u00AB': return "<<";
                case '\u00BB': return ">>";
                
                // Control characters and other problematic characters
                case '\0': return "";
                case '\r': return "";
                case '\n': return " ";
                case '\t': return " ";
                
                // Default fallback for any other non-ASCII character
                default:
                    if (char.IsControl(c))
                        return "";
                    else if (char.IsPunctuation(c))
                        return "?";
                    else if (char.IsSymbol(c))
                        return "*";
                    else
                        return "?";
            }
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

                // Draw left panel (character image and selection)
                DrawLeftPanel(spriteBatch);

                // Draw right panel (chat area)
                DrawRightPanel(spriteBatch);
            }
            catch (Exception ex)
            {
                _engine?.Log($"Chat: Error in Draw: {ex.Message}");
            }
        }

        public void DrawTopLayer(SpriteBatch spriteBatch)
        {
            // Draw character dropdown on top layer if open
            if (_isCharacterDropdownOpen)
            {
                DrawCharacterDropdown(spriteBatch);
            }
        }

        private void DrawCharacterDropdown(SpriteBatch spriteBatch)
        {
            if (_characters.Count == 0) return;
            
            // Draw dropdown background
            spriteBatch.Draw(_pixel, _characterDropdownBounds, new Color(40, 40, 40, 240));
            
            // Draw dropdown border
            Color borderColor = new Color(147, 112, 219);
            int borderThickness = 2;
            
            // Top border
            spriteBatch.Draw(_pixel, new Rectangle(_characterDropdownBounds.X, _characterDropdownBounds.Y, _characterDropdownBounds.Width, borderThickness), borderColor);
            // Bottom border
            spriteBatch.Draw(_pixel, new Rectangle(_characterDropdownBounds.X, _characterDropdownBounds.Bottom - borderThickness, _characterDropdownBounds.Width, borderThickness), borderColor);
            // Left border
            spriteBatch.Draw(_pixel, new Rectangle(_characterDropdownBounds.X, _characterDropdownBounds.Y, borderThickness, _characterDropdownBounds.Height), borderColor);
            // Right border
            spriteBatch.Draw(_pixel, new Rectangle(_characterDropdownBounds.Right - borderThickness, _characterDropdownBounds.Y, borderThickness, _characterDropdownBounds.Height), borderColor);
            
            // Draw character items (only visible ones)
            int visibleItems = Math.Min(_characters.Count, CHARACTER_MAX_VISIBLE_ITEMS);
            for (int i = 0; i < visibleItems; i++)
            {
                int actualIndex = i + _characterScrollOffset;
                if (actualIndex >= _characters.Count) break;
                
                Rectangle itemRect = new Rectangle(
                    _characterDropdownBounds.X,
                    _characterDropdownBounds.Y + (i * CHARACTER_ITEM_HEIGHT),
                    _characterDropdownBounds.Width - (_characterScrollBarBounds.IsEmpty ? 0 : 16),
                    CHARACTER_ITEM_HEIGHT
                );
                
                // Draw item background if hovered or selected
                if (actualIndex == _hoveredCharacterItem)
                {
                    spriteBatch.Draw(_pixel, itemRect, new Color(147, 112, 219, 120));
                }
                else if (actualIndex == _selectedCharacterIndex)
                {
                    spriteBatch.Draw(_pixel, itemRect, new Color(147, 112, 219, 150));
                }
                
                // Draw item text
                string itemText = _characters[actualIndex].Name;
                Vector2 textSize = _pixelFont.MeasureString(itemText) * 0.8f;
                Vector2 textPos = new Vector2(
                    itemRect.X + 5,
                    itemRect.Y + (itemRect.Height - textSize.Y) / 2
                );
                
                Color textColor = Color.White;
                spriteBatch.DrawString(_pixelFont, itemText, textPos, textColor, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);
            }
            
            // Draw scrollbar if needed
            if (!_characterScrollBarBounds.IsEmpty)
            {
                DrawCharacterScrollBar(spriteBatch);
            }
        }

        private void DrawCharacterScrollBar(SpriteBatch spriteBatch)
        {
            // Draw scrollbar background
            spriteBatch.Draw(_pixel, _characterScrollBarBounds, new Color(60, 60, 60, 200));
            
            // Calculate scrollbar thumb size and position
            float scrollRatio = (float)_characterScrollOffset / Math.Max(1, _characters.Count - CHARACTER_MAX_VISIBLE_ITEMS);
            int thumbHeight = Math.Max(20, (int)(_characterScrollBarBounds.Height * (CHARACTER_MAX_VISIBLE_ITEMS / (float)_characters.Count)));
            int thumbY = _characterScrollBarBounds.Y + (int)((_characterScrollBarBounds.Height - thumbHeight) * scrollRatio);
            
            Rectangle thumbBounds = new Rectangle(
                _characterScrollBarBounds.X + 2,
                thumbY,
                _characterScrollBarBounds.Width - 4,
                thumbHeight
            );
            
            // Draw scrollbar thumb
            Color thumbColor = _isDraggingCharacterScroll ? new Color(180, 145, 250) : new Color(147, 112, 219);
            spriteBatch.Draw(_pixel, thumbBounds, thumbColor);
            
            // Draw scrollbar border
            Color scrollBorderColor = new Color(100, 100, 100);
            spriteBatch.Draw(_pixel, new Rectangle(_characterScrollBarBounds.X, _characterScrollBarBounds.Y, 1, _characterScrollBarBounds.Height), scrollBorderColor);
        }

        private void DrawLeftPanel(SpriteBatch spriteBatch)
        {
            // Draw left panel background
            spriteBatch.Draw(_pixel, _leftPanelBounds, CHAT_BACKGROUND);
            DrawBorder(spriteBatch, _leftPanelBounds, BORDER_COLOR);

            // Draw current character image if loaded
            if (_currentCharacterImage != null)
            {
                int imageSize = _leftPanelWidth; // Image fits exactly in panel width
                var imageBounds = new Rectangle(
                    _leftPanelBounds.X, // No padding - image starts at left edge
                    _leftPanelBounds.Y, // No padding - image starts at top edge
                    imageSize,
                    imageSize
                );

                spriteBatch.Draw(_currentCharacterImage, imageBounds, Color.White);
            }

            // Draw character selection button
            DrawCharacterButton(spriteBatch);
        }

        private void DrawCharacterButton(SpriteBatch spriteBatch)
        {
            if (_pixelFont == null) return;

            // Check if button is hovered
            bool isHovered = _characterButtonBounds.Contains(_currentMouseState.Position);
            
            // Choose colors based on state
            Color buttonColor = isHovered ? new Color(80, 80, 80) : new Color(60, 60, 60);
            Color borderColor = isHovered ? new Color(120, 120, 120) : new Color(100, 100, 100);
            Color textColor = Color.White;

            // Draw button background
            spriteBatch.Draw(_pixel, _characterButtonBounds, buttonColor);
            DrawBorder(spriteBatch, _characterButtonBounds, borderColor);

            // Draw character name
            var nameText = _currentCharacter.Name;
            var nameSize = _pixelFont.MeasureString(nameText);
            var namePosition = new Vector2(
                _characterButtonBounds.X + (_characterButtonBounds.Width - nameSize.X) / 2,
                _characterButtonBounds.Y + (_characterButtonBounds.Height - nameSize.Y) / 2
            );

            spriteBatch.DrawString(_pixelFont, nameText, namePosition, textColor);

            // Draw dropdown arrow
            DrawDropdownArrow(spriteBatch, _characterButtonBounds);
        }

        private void DrawDropdownArrow(SpriteBatch spriteBatch, Rectangle buttonBounds)
        {
            // Draw a small downward arrow on the right side of the button
            int arrowSize = 6;
            int arrowX = buttonBounds.Right - arrowSize - 5;
            int arrowY = buttonBounds.Y + (buttonBounds.Height - arrowSize) / 2;

            // Draw triangle pointing down
            for (int i = 0; i < arrowSize; i++)
            {
                int lineWidth = (i * 2) + 1;
                int lineX = arrowX + (arrowSize - i - 1);
                int lineY = arrowY + i;
                
                spriteBatch.Draw(_pixel, new Rectangle(lineX, lineY, lineWidth, 1), Color.White);
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
                try
                {
                spriteBatch.DrawString(_chatFont, message.WrappedLines[i], linePosition, Color.White, 0f, Vector2.Zero, TEXT_SCALE, SpriteEffects.None, 0f);
                }
                catch (Exception ex)
                {
                    _engine?.Log($"Chat: Error drawing chat line: {ex.Message}");
                    // Fallback: draw with pixel font if available
                    if (_pixelFont != null)
                    {
                        spriteBatch.DrawString(_pixelFont, message.WrappedLines[i], linePosition, Color.White, 0f, Vector2.Zero, TEXT_SCALE, SpriteEffects.None, 0f);
                    }
                }
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
            // Draw input area background - change color when thinking
            Color inputBackground = _isThinking ? new Color(50, 50, 50) : INPUT_BACKGROUND;
            Color inputBorder = _isThinking ? new Color(80, 80, 80) : BORDER_COLOR;
            
            spriteBatch.Draw(_pixel, _inputAreaBounds, inputBackground);
            DrawBorder(spriteBatch, _inputAreaBounds, inputBorder);

            // Draw input text
            if (_chatFont != null)
            {
                var inputText = _currentInput.ToString();
                var textPosition = new Vector2(
                    _inputAreaBounds.X + PANEL_PADDING,
                    _inputAreaBounds.Y + (_inputAreaBounds.Height - _chatFont.LineSpacing) / 2
                );

                // Change text color when thinking
                Color textColor = _isThinking ? new Color(120, 120, 120) : TEXT_COLOR;

                try
                {
                    spriteBatch.DrawString(_chatFont, inputText, textPosition, textColor, 0f, Vector2.Zero, TEXT_SCALE, SpriteEffects.None, 0f);
                }
                catch (Exception ex)
                {
                    _engine?.Log($"Chat: Error drawing input text: {ex.Message}");
                    // Fallback: draw with pixel font if available
                    if (_pixelFont != null)
                    {
                        spriteBatch.DrawString(_pixelFont, inputText, textPosition, textColor, 0f, Vector2.Zero, TEXT_SCALE, SpriteEffects.None, 0f);
                    }
                }

                // Draw cursor
                if (_showCursor && _isFocused)
                {
                    try
                {
                    var cursorX = textPosition.X + _chatFont.MeasureString(inputText.Substring(0, _cursorPosition)).X * TEXT_SCALE;
                    var cursorBounds = new Rectangle(
                        (int)cursorX,
                        (int)textPosition.Y,
                        1,
                        (int)(_chatFont.LineSpacing * TEXT_SCALE)
                    );
                        spriteBatch.Draw(_pixel, cursorBounds, textColor);
                    }
                    catch (Exception ex)
                    {
                        _engine?.Log($"Chat: Error measuring cursor position: {ex.Message}");
                        // Fallback: position cursor at end of text
                        var cursorX = textPosition.X + (inputText.Length * 8 * TEXT_SCALE); // Rough estimate
                        var cursorBounds = new Rectangle(
                            (int)cursorX,
                            (int)textPosition.Y,
                            1,
                            (int)(_chatFont.LineSpacing * TEXT_SCALE)
                        );
                        spriteBatch.Draw(_pixel, cursorBounds, textColor);
                    }
                }
            }

            // Draw send button
            DrawSendButton(spriteBatch);
        }

        private void DrawSendButton(SpriteBatch spriteBatch)
        {
            bool isHovered = _sendButtonBounds.Contains(_currentMouseState.Position);
            bool isDisabled = _isThinking;
            
            // Choose colors based on state
            Color buttonColor;
            Color borderColor;
            Color textColor;
            
            if (isDisabled)
            {
                buttonColor = new Color(80, 80, 80); // Dark gray when disabled
                borderColor = new Color(60, 60, 60); // Darker border when disabled
                textColor = new Color(150, 150, 150); // Gray text when disabled
            }
            else if (isHovered)
            {
                buttonColor = SEND_BUTTON_HOVER;
                borderColor = Color.White;
                textColor = Color.White;
            }
            else
            {
                buttonColor = SEND_BUTTON_COLOR;
                borderColor = BORDER_COLOR;
                textColor = Color.White;
            }

            // Draw button background with rounded corners
            DrawRoundedRectangle(spriteBatch, _sendButtonBounds, buttonColor, 4);
            DrawRoundedRectangleBorder(spriteBatch, _sendButtonBounds, borderColor, 4, 1);

            // Draw "Send" text with pixel font
            if (_pixelFont != null)
            {
                var sendText = isDisabled ? "WAIT" : "SEND";
                var textSize = _pixelFont.MeasureString(sendText);
                var textPosition = new Vector2(
                    _sendButtonBounds.X + (_sendButtonBounds.Width - textSize.X) / 2,
                    _sendButtonBounds.Y + (_sendButtonBounds.Height - textSize.Y) / 2
                );

                spriteBatch.DrawString(_pixelFont, sendText, textPosition, textColor);
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
            DrawRoundedRectangle(spriteBatch, bubbleBounds, _currentCharacter.MessageColor, BUBBLE_CORNER_RADIUS);
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
            _content = content;
            _windowManagement?.LoadContent(content);
            
            // Load chat font (use a more compatible font for chat)
            try
            {
                _chatFont = content.Load<SpriteFont>("Fonts/SpriteFonts/inconsolata/regular");
                _engine?.Log("Chat: Loaded Inconsolata font for chat");
            }
            catch (Exception ex)
            {
                _engine?.Log($"Chat: Failed to load Inconsolata font: {ex.Message}");
                try
                {
                    // Fallback to pixel font if Inconsolata fails
                    _chatFont = content.Load<SpriteFont>("Fonts/SpriteFonts/pixel_font/regular");
                    _engine?.Log("Chat: Using pixel font as fallback for chat");
                }
                catch (Exception ex2)
                {
                    _engine?.Log($"Chat: Failed to load pixel font fallback: {ex2.Message}");
                    // Use menu font as last resort
                    _chatFont = _menuFont;
                    _engine?.Log("Chat: Using menu font as last resort for chat");
                }
            }
            
            // Load pixel font for Mary Sue name and Send button
            try
            {
            _pixelFont = content.Load<SpriteFont>("Fonts/SpriteFonts/pixel_font/regular");
            }
            catch (Exception ex)
            {
                _engine?.Log($"Chat: Failed to load pixel font: {ex.Message}");
                _pixelFont = _menuFont; // Use menu font as fallback
            }

            // Load current character image
            LoadCurrentCharacterImage();
        }

        private void LoadCurrentCharacterImage()
        {
            if (_currentCharacter == null) return;
            
            try
            {
                _currentCharacterImage = _content?.Load<Texture2D>($"Modules/Chat/Characters/{_currentCharacter.ImageName}");
                _engine?.Log($"Chat: Loaded character image: {_currentCharacter.ImageName}");
            }
            catch (Exception ex)
            {
                _engine?.Log($"Chat: Failed to load character image {_currentCharacter.ImageName}: {ex.Message}");
                _currentCharacterImage = null;
            }
        }

        public void Dispose()
        {
            _pixel?.Dispose();
            _windowManagement?.Dispose();
            // Note: _httpClient is static and shared, so we don't dispose it here
        }

        public void ClearFocus()
        {
            _isFocused = false;
            _isCharacterDropdownOpen = false;
        }

        private void ClearFocusFromOtherModules()
        {
            // Get all active modules and clear focus from non-Chat modules
            var engine = GameEngine.Instance;
            if (engine != null)
            {
                var activeModulesField = engine.GetType().GetField("_activeModules", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (activeModulesField != null)
                {
                    var activeModules = activeModulesField.GetValue(engine) as List<IModule>;
                    if (activeModules != null)
                    {
                        foreach (var module in activeModules)
                        {
                            // Clear focus from Console module
                            if (module is MarySGameEngine.Modules.Console_essential.Console consoleModule)
                            {
                                consoleModule.ClearFocus();
                            }
                        }
                    }
                }
            }
        }

        // Helper method to check if a window is the topmost window under the mouse
        private bool IsTopmostWindowUnderMouse(WindowManagement window, Point mousePosition)
        {
            if (!window.IsVisible())
                return false;

            // Get the window bounds
            Rectangle windowBounds = window.GetWindowBounds();
            
            // Check if mouse is over this window
            if (!windowBounds.Contains(mousePosition))
                return false;

            // Get all active windows from WindowManagement using reflection
            var activeWindowsField = typeof(WindowManagement).GetField("_activeWindows", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            
            if (activeWindowsField == null)
                return true; // If we can't access the list, assume this window is topmost

            var activeWindows = activeWindowsField.GetValue(null) as List<WindowManagement>;
            if (activeWindows == null)
                return true;

            // Check if this window has the highest z-order among all windows under the mouse
            int highestZOrder = -1;
            WindowManagement topmostWindow = null;

            foreach (var activeWindow in activeWindows)
            {
                if (activeWindow.IsVisible() && activeWindow.GetWindowBounds().Contains(mousePosition))
                {
                    int zOrder = activeWindow.GetZOrder();
                    if (zOrder > highestZOrder)
                    {
                        highestZOrder = zOrder;
                        topmostWindow = activeWindow;
                    }
                }
            }

            return topmostWindow == window;
        }
    }
}
