using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
using System;
using System.Collections.Generic;
using MarySGameEngine;
using MarySGameEngine.Modules.UIElements_essential;

namespace MarySGameEngine.Modules.FlashMessage_essential
{
    public enum FlashMessageType
    {
        Success,
        Warning,
        Info
    }

    public class FlashMessageData
    {
        public string Message { get; set; }
        public FlashMessageType Type { get; set; }
        public float DisplayTime { get; set; }
        public float ElapsedTime { get; set; }
        public float AnimationProgress { get; set; } // 0.0 to 1.0 for fade in/out

        public FlashMessageData(string message, FlashMessageType type, float displayTime = 3.0f)
        {
            Message = message;
            Type = type;
            DisplayTime = displayTime;
            ElapsedTime = 0f;
            AnimationProgress = 0f;
        }
    }

    public class FlashMessage : IModule
    {
        private GraphicsDevice _graphicsDevice;
        private SpriteFont _menuFont;
        private SpriteFont _flashMessageFont; // Different font for flash messages
        private int _windowWidth;
        private Texture2D _pixel;
        private ContentManager _content;
        private GameEngine _engine;

        // Static instance for global access
        private static FlashMessage _instance;
        public static FlashMessage Instance => _instance;

        // Message queue
        private List<FlashMessageData> _messages;
        private const float ANIMATION_DURATION = 0.3f; // 300ms fade in/out
        private const float DEFAULT_DISPLAY_TIME = 3.0f; // 3 seconds default
        private const int MESSAGE_PADDING = 20;
        private const int MESSAGE_SPACING = 10;
        private const int MESSAGE_MIN_WIDTH = 300;
        private const int MESSAGE_MAX_WIDTH = 1000;
        private const int MESSAGE_BORDER_THICKNESS = 2;
        private const int MESSAGE_CORNER_RADIUS = 8;
        private const float LINE_SPACING = 1.2f; // Line spacing multiplier

        // Colors - more purple/dark themed
        private readonly Color SUCCESS_BACKGROUND = new Color(80, 50, 120, 240); // Dark purple with transparency
        private readonly Color SUCCESS_BORDER = new Color(147, 112, 219); // Purple border
        private readonly Color WARNING_BACKGROUND = new Color(100, 60, 140, 240); // Darker purple with transparency
        private readonly Color WARNING_BORDER = new Color(180, 145, 250); // Light purple border
        private readonly Color INFO_BACKGROUND = new Color(60, 40, 100, 240); // Very dark purple with transparency
        private readonly Color INFO_BORDER = new Color(147, 112, 219); // Purple border
        private readonly Color TEXT_COLOR = new Color(255, 255, 255); // White text
        private readonly Color SHADOW_COLOR = new Color(0, 0, 0, 150); // Semi-transparent black shadow

        public FlashMessage(GraphicsDevice graphicsDevice, SpriteFont menuFont, int windowWidth)
        {
            _graphicsDevice = graphicsDevice;
            _menuFont = menuFont;
            _windowWidth = windowWidth;
            _engine = GameEngine.Instance;
            _messages = new List<FlashMessageData>();

            // Create pixel texture
            _pixel = new Texture2D(_graphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });

            // Set static instance
            _instance = this;

            System.Diagnostics.Debug.WriteLine("FlashMessage: Module initialized");
        }

        public void LoadContent(ContentManager content)
        {
            _content = content;
            
            // Load a different font for flash messages (try Roboto, then Inconsolata, fallback to menu font)
            // Match the same font loading logic as TopBar workspace font
            try
            {
                _flashMessageFont = content.Load<SpriteFont>("Fonts/SpriteFonts/roboto/regular");
                System.Diagnostics.Debug.WriteLine("FlashMessage: Successfully loaded Roboto font");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FlashMessage: Failed to load Roboto font: {ex.Message}");
                try
                {
                    // Try Inconsolata as fallback
                    _flashMessageFont = content.Load<SpriteFont>("Fonts/SpriteFonts/inconsolata/regular");
                    System.Diagnostics.Debug.WriteLine("FlashMessage: Using Inconsolata font as fallback");
                }
                catch (Exception ex2)
                {
                    System.Diagnostics.Debug.WriteLine($"FlashMessage: Failed to load Inconsolata font: {ex2.Message}");
                    try
                    {
                        // Try Open Sans as another fallback
                        _flashMessageFont = content.Load<SpriteFont>("Fonts/SpriteFonts/open_sans/regular");
                        System.Diagnostics.Debug.WriteLine("FlashMessage: Using Open Sans font as fallback");
                    }
                    catch (Exception ex3)
                    {
                        System.Diagnostics.Debug.WriteLine($"FlashMessage: Failed to load Open Sans font: {ex3.Message}");
                        // Use menu font as last resort
                        _flashMessageFont = _menuFont;
                        System.Diagnostics.Debug.WriteLine("FlashMessage: Using menu font as last resort");
                    }
                }
            }
            
            System.Diagnostics.Debug.WriteLine("FlashMessage: Content loaded");
        }

        public void Update()
        {
            // Update all messages
            for (int i = _messages.Count - 1; i >= 0; i--)
            {
                var message = _messages[i];
                message.ElapsedTime += (float)_engine.TargetElapsedTime.TotalSeconds;

                // Calculate animation progress
                if (message.ElapsedTime < ANIMATION_DURATION)
                {
                    // Fade in
                    message.AnimationProgress = message.ElapsedTime / ANIMATION_DURATION;
                }
                else if (message.ElapsedTime > message.DisplayTime - ANIMATION_DURATION)
                {
                    // Fade out
                    float fadeOutStart = message.DisplayTime - ANIMATION_DURATION;
                    float fadeOutProgress = (message.ElapsedTime - fadeOutStart) / ANIMATION_DURATION;
                    message.AnimationProgress = 1.0f - fadeOutProgress;
                }
                else
                {
                    // Fully visible
                    message.AnimationProgress = 1.0f;
                }

                // Remove expired messages
                if (message.ElapsedTime >= message.DisplayTime)
                {
                    _messages.RemoveAt(i);
                }
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            if (_pixel == null || _flashMessageFont == null || _messages.Count == 0)
                return;

            int screenHeight = _graphicsDevice.Viewport.Height;
            int currentY = screenHeight - 100; // Start 100 pixels from bottom

            // Draw messages from bottom to top (newest at bottom)
            for (int i = _messages.Count - 1; i >= 0; i--)
            {
                var message = _messages[i];
                DrawMessage(spriteBatch, message, currentY);
                currentY -= GetMessageHeight(message) + MESSAGE_SPACING;
            }
        }

        private void DrawMessage(SpriteBatch spriteBatch, FlashMessageData message, int y)
        {
            // Wrap text into multiple lines
            int maxTextWidth = MESSAGE_MAX_WIDTH - (MESSAGE_PADDING * 2);
            List<string> wrappedLines = WrapText(message.Message, maxTextWidth);

            // Calculate message dimensions based on wrapped text
            float lineHeight = _flashMessageFont.MeasureString("A").Y;
            float totalTextHeight = wrappedLines.Count * lineHeight * LINE_SPACING;
            
            // Find the widest line
            float maxLineWidth = 0;
            foreach (string line in wrappedLines)
            {
                Vector2 lineSize = _flashMessageFont.MeasureString(line);
                if (lineSize.X > maxLineWidth)
                    maxLineWidth = lineSize.X;
            }

            int messageWidth = Math.Max(MESSAGE_MIN_WIDTH, Math.Min(MESSAGE_MAX_WIDTH, (int)maxLineWidth + (MESSAGE_PADDING * 2)));
            int messageHeight = (int)totalTextHeight + (MESSAGE_PADDING * 2);
            int centerX = _windowWidth / 2;
            int messageX = centerX - (messageWidth / 2);

            Rectangle messageBounds = new Rectangle(messageX, y - messageHeight, messageWidth, messageHeight);

            // Get colors based on type
            Color backgroundColor;
            Color borderColor;
            GetMessageColors(message.Type, out backgroundColor, out borderColor);

            // Apply alpha based on animation progress
            backgroundColor = new Color(backgroundColor.R, backgroundColor.G, backgroundColor.B, (byte)(backgroundColor.A * message.AnimationProgress));
            borderColor = new Color(borderColor.R, borderColor.G, borderColor.B, (byte)(borderColor.A * message.AnimationProgress));
            Color textColor = new Color(TEXT_COLOR.R, TEXT_COLOR.G, TEXT_COLOR.B, (byte)(TEXT_COLOR.A * message.AnimationProgress));
            Color shadowColor = new Color(SHADOW_COLOR.R, SHADOW_COLOR.G, SHADOW_COLOR.B, (byte)(SHADOW_COLOR.A * message.AnimationProgress));

            // Draw shadow (offset slightly down and right)
            Rectangle shadowBounds = new Rectangle(messageBounds.X + 2, messageBounds.Y + 2, messageBounds.Width, messageBounds.Height);
            DrawRoundedRectangle(spriteBatch, shadowBounds, shadowColor, MESSAGE_CORNER_RADIUS);

            // Draw message background
            DrawRoundedRectangle(spriteBatch, messageBounds, backgroundColor, MESSAGE_CORNER_RADIUS);

            // Draw border
            DrawRoundedRectangleBorder(spriteBatch, messageBounds, borderColor, MESSAGE_BORDER_THICKNESS, MESSAGE_CORNER_RADIUS);

            // Draw text (multiline, centered)
            float startY = messageBounds.Y + MESSAGE_PADDING;
            for (int i = 0; i < wrappedLines.Count; i++)
            {
                string line = wrappedLines[i];
                Vector2 lineSize = _flashMessageFont.MeasureString(line);
                Vector2 textPosition = new Vector2(
                    messageBounds.X + (messageBounds.Width / 2) - (lineSize.X / 2),
                    startY + (i * lineHeight * LINE_SPACING)
                );
                spriteBatch.DrawString(_flashMessageFont, line, textPosition, textColor);
            }
        }

        private void GetMessageColors(FlashMessageType type, out Color backgroundColor, out Color borderColor)
        {
            switch (type)
            {
                case FlashMessageType.Success:
                    backgroundColor = SUCCESS_BACKGROUND;
                    borderColor = SUCCESS_BORDER;
                    break;
                case FlashMessageType.Warning:
                    backgroundColor = WARNING_BACKGROUND;
                    borderColor = WARNING_BORDER;
                    break;
                case FlashMessageType.Info:
                    backgroundColor = INFO_BACKGROUND;
                    borderColor = INFO_BORDER;
                    break;
                default:
                    backgroundColor = INFO_BACKGROUND;
                    borderColor = INFO_BORDER;
                    break;
            }
        }

        private List<string> WrapText(string text, int maxWidth)
        {
            List<string> lines = new List<string>();
            if (string.IsNullOrEmpty(text))
                return lines;

            string[] words = text.Split(' ');
            string currentLine = "";

            foreach (string word in words)
            {
                string testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                Vector2 testSize = _flashMessageFont.MeasureString(testLine);

                if (testSize.X <= maxWidth)
                {
                    currentLine = testLine;
                }
                else
                {
                    if (!string.IsNullOrEmpty(currentLine))
                    {
                        lines.Add(currentLine);
                    }
                    // If a single word is too long, break it (though this shouldn't happen often)
                    if (_flashMessageFont.MeasureString(word).X > maxWidth)
                    {
                        // Word is too long, add it anyway (will be clipped but better than nothing)
                        lines.Add(word);
                        currentLine = "";
                    }
                    else
                    {
                        currentLine = word;
                    }
                }
            }

            if (!string.IsNullOrEmpty(currentLine))
            {
                lines.Add(currentLine);
            }

            return lines;
        }

        private int GetMessageHeight(FlashMessageData message)
        {
            int maxWidth = MESSAGE_MAX_WIDTH - (MESSAGE_PADDING * 2);
            List<string> wrappedLines = WrapText(message.Message, maxWidth);
            float lineHeight = _flashMessageFont.MeasureString("A").Y;
            int totalHeight = (int)(wrappedLines.Count * lineHeight * LINE_SPACING) + (MESSAGE_PADDING * 2);
            return totalHeight;
        }

        private void DrawRoundedRectangle(SpriteBatch spriteBatch, Rectangle bounds, Color color, int cornerRadius)
        {
            // Draw main rectangle
            spriteBatch.Draw(_pixel, new Rectangle(bounds.X + cornerRadius, bounds.Y, bounds.Width - (cornerRadius * 2), bounds.Height), color);
            spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y + cornerRadius, bounds.Width, bounds.Height - (cornerRadius * 2)), color);

            // Draw corner circles (simplified as small rectangles)
            DrawCorner(spriteBatch, bounds.X, bounds.Y, cornerRadius, color);
            DrawCorner(spriteBatch, bounds.Right - cornerRadius, bounds.Y, cornerRadius, color);
            DrawCorner(spriteBatch, bounds.X, bounds.Bottom - cornerRadius, cornerRadius, color);
            DrawCorner(spriteBatch, bounds.Right - cornerRadius, bounds.Bottom - cornerRadius, cornerRadius, color);
        }

        private void DrawCorner(SpriteBatch spriteBatch, int x, int y, int radius, Color color)
        {
            // Simplified corner drawing - just draw a small square
            spriteBatch.Draw(_pixel, new Rectangle(x, y, radius, radius), color);
        }

        private void DrawRoundedRectangleBorder(SpriteBatch spriteBatch, Rectangle bounds, Color color, int thickness, int cornerRadius)
        {
            // Top border
            spriteBatch.Draw(_pixel, new Rectangle(bounds.X + cornerRadius, bounds.Y, bounds.Width - (cornerRadius * 2), thickness), color);
            // Bottom border
            spriteBatch.Draw(_pixel, new Rectangle(bounds.X + cornerRadius, bounds.Bottom - thickness, bounds.Width - (cornerRadius * 2), thickness), color);
            // Left border
            spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y + cornerRadius, thickness, bounds.Height - (cornerRadius * 2)), color);
            // Right border
            spriteBatch.Draw(_pixel, new Rectangle(bounds.Right - thickness, bounds.Y + cornerRadius, thickness, bounds.Height - (cornerRadius * 2)), color);

            // Corner borders (simplified)
            DrawCornerBorder(spriteBatch, bounds.X, bounds.Y, cornerRadius, thickness, color);
            DrawCornerBorder(spriteBatch, bounds.Right - cornerRadius, bounds.Y, cornerRadius, thickness, color);
            DrawCornerBorder(spriteBatch, bounds.X, bounds.Bottom - cornerRadius, cornerRadius, thickness, color);
            DrawCornerBorder(spriteBatch, bounds.Right - cornerRadius, bounds.Bottom - cornerRadius, cornerRadius, thickness, color);
        }

        private void DrawCornerBorder(SpriteBatch spriteBatch, int x, int y, int radius, int thickness, Color color)
        {
            // Simplified corner border
            spriteBatch.Draw(_pixel, new Rectangle(x, y, radius, thickness), color);
            spriteBatch.Draw(_pixel, new Rectangle(x, y, thickness, radius), color);
        }

        // Public static method to show messages from any module
        public static void Show(string message, FlashMessageType type = FlashMessageType.Info, float displayTime = 3.0f)
        {
            if (_instance != null)
            {
                _instance._messages.Add(new FlashMessageData(message, type, displayTime));
                System.Diagnostics.Debug.WriteLine($"FlashMessage: Showing {type} message: {message}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"FlashMessage: Instance not available, cannot show message: {message}");
            }
        }

        public void UpdateWindowWidth(int width)
        {
            _windowWidth = width;
        }

        public void Dispose()
        {
            _pixel?.Dispose();
        }
    }
}


