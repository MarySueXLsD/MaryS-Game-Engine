using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Content;
using System;
using System.Collections.Generic;

namespace MarySGameEngine.Modules.PopUp_essential
{
    public enum PopUpType
    {
        Confirm
    }

    public abstract class PopUpData
    {
        public string Title { get; set; }
        public PopUpType Type { get; set; }

        protected PopUpData(string title, PopUpType type)
        {
            Title = title;
            Type = type;
        }
    }

    public class ConfirmPopUpData : PopUpData
    {
        public string Message { get; set; }
        public string ConfirmText { get; set; }
        public string CancelText { get; set; }
        public Action OnConfirm { get; set; }
        public Action OnCancel { get; set; }

        public ConfirmPopUpData(string title, string message, Action onConfirm, Action onCancel,
            string confirmText = "Confirm", string cancelText = "Cancel")
            : base(title, PopUpType.Confirm)
        {
            Message = message;
            ConfirmText = confirmText;
            CancelText = cancelText;
            OnConfirm = onConfirm;
            OnCancel = onCancel;
        }
    }

    public class PopUp : IModule
    {
        private GraphicsDevice _graphicsDevice;
        private SpriteFont _menuFont;
        private SpriteFont _contentFont;
        private int _windowWidth;
        private int _windowHeight;
        private Texture2D _pixel;
        private Texture2D _closeIcon;
        private GameEngine _engine;
        private KeyboardState _previousKeyboardState;
        private MouseState _previousMouseState;

        private static PopUp _instance;
        public static PopUp Instance => _instance;

        /// <summary>True when a popup button (Confirm/Cancel/Close) was clicked this frame. Other modules should not process the same click.</summary>
        public static bool ConsumedClickThisFrame { get; set; }

        private readonly Queue<PopUpData> _popUpQueue = new Queue<PopUpData>();
        private PopUpData _currentPopUp;

        private const int TITLE_BAR_HEIGHT = 40;
        private const int BUTTON_HEIGHT = 36;
        private const int BUTTON_WIDTH = 100;
        private const int BUTTON_SPACING = 16;
        private const int PADDING = 20;
        private const int CLOSE_BUTTON_SIZE = 30;
        private const int CLOSE_BUTTON_PADDING = 5;
        private const int POPUP_MIN_WIDTH = 320;
        private const int POPUP_MAX_WIDTH = 500;
        private const int POPUP_BORDER_THICKNESS = 2;

        private readonly Color BACKDROP_COLOR = new Color(0, 0, 0, 160);
        private readonly Color POPUP_BG_COLOR = new Color(40, 40, 45);
        private readonly Color TITLE_BAR_COLOR = new Color(147, 112, 219);
        private readonly Color BORDER_COLOR = new Color(70, 68, 85);
        private readonly Color BUTTON_COLOR = new Color(55, 58, 68);
        private readonly Color BUTTON_HOVER_COLOR = new Color(75, 78, 90);
        private readonly Color CONFIRM_BUTTON_TOP = new Color(60, 110, 60);
        private readonly Color CONFIRM_BUTTON_BOTTOM = new Color(85, 145, 85);
        private readonly Color CONFIRM_BUTTON_HOVER_TOP = new Color(75, 140, 75);
        private readonly Color CONFIRM_BUTTON_HOVER_BOTTOM = new Color(105, 175, 105);
        private readonly Color CLOSE_BUTTON_HOVER_COLOR = new Color(232, 17, 35);
        private readonly Color TEXT_COLOR = Color.White;

        public PopUp(GraphicsDevice graphicsDevice, SpriteFont menuFont, int windowWidth)
        {
            _graphicsDevice = graphicsDevice;
            _menuFont = menuFont;
            _windowWidth = windowWidth;
            _windowHeight = graphicsDevice.Viewport.Height;
            _engine = GameEngine.Instance;

            _pixel = new Texture2D(_graphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });

            _instance = this;
            _previousKeyboardState = Keyboard.GetState();
            _previousMouseState = Mouse.GetState();
        }

        public void LoadContent(ContentManager content)
        {
            try
            {
                _closeIcon = content.Load<Texture2D>("Modules/WindowManagement_essential/close");
            }
            catch
            {
                _closeIcon = null;
            }

            try
            {
                _contentFont = content.Load<SpriteFont>("Fonts/SpriteFonts/roboto/regular");
            }
            catch
            {
                try
                {
                    _contentFont = content.Load<SpriteFont>("Fonts/SpriteFonts/pixel_font/regular");
                }
                catch
                {
                    _contentFont = _menuFont;
                }
            }
        }

        public void Update()
        {
            var keyboardState = Keyboard.GetState();
            var mouseState = Mouse.GetState();

            if (_currentPopUp != null)
            {
                if (_currentPopUp is ConfirmPopUpData confirmPopUp)
                {
                    bool escapePressed = keyboardState.IsKeyDown(Keys.Escape) && !_previousKeyboardState.IsKeyDown(Keys.Escape);
                    if (escapePressed)
                    {
                        DismissCurrent(true);
                        _previousKeyboardState = keyboardState;
                        return;
                    }

                    bool enterPressed = (keyboardState.IsKeyDown(Keys.Enter) || keyboardState.IsKeyDown(Keys.Space)) &&
                        !_previousKeyboardState.IsKeyDown(Keys.Enter) && !_previousKeyboardState.IsKeyDown(Keys.Space);
                    if (enterPressed)
                    {
                        DismissCurrent(false);
                        confirmPopUp.OnConfirm?.Invoke();
                        _previousKeyboardState = keyboardState;
                        return;
                    }

                    Rectangle popupBounds = GetPopupBounds(confirmPopUp);
                    Rectangle closeButtonBounds = GetCloseButtonBounds(popupBounds);
                    Rectangle confirmButtonBounds = GetConfirmButtonBounds(popupBounds);
                    Rectangle cancelButtonBounds = GetCancelButtonBounds(popupBounds);

                    bool mouseClicked = mouseState.LeftButton == ButtonState.Pressed &&
                                        _previousMouseState.LeftButton == ButtonState.Released;

                    if (mouseClicked)
                    {
                        if (!popupBounds.Contains(mouseState.Position))
                        {
                            DismissCurrent(true);
                            confirmPopUp.OnCancel?.Invoke();
                            ConsumedClickThisFrame = true;
                        }
                        else if (closeButtonBounds.Contains(mouseState.Position))
                        {
                            DismissCurrent(true);
                            ConsumedClickThisFrame = true;
                        }
                        else if (confirmButtonBounds.Contains(mouseState.Position))
                        {
                            DismissCurrent(false);
                            confirmPopUp.OnConfirm?.Invoke();
                            ConsumedClickThisFrame = true;
                        }
                        else if (cancelButtonBounds.Contains(mouseState.Position))
                        {
                            DismissCurrent(true);
                            confirmPopUp.OnCancel?.Invoke();
                            ConsumedClickThisFrame = true;
                        }
                    }
                }
            }
            else
            {
                if (_popUpQueue.Count > 0)
                {
                    _currentPopUp = _popUpQueue.Dequeue();
                }
            }

            _previousKeyboardState = keyboardState;
            _previousMouseState = mouseState;
        }

        private void DismissCurrent(bool invokeCancel)
        {
            if (_currentPopUp is ConfirmPopUpData confirmPopUp && invokeCancel)
            {
                confirmPopUp.OnCancel?.Invoke();
            }
            _currentPopUp = null;
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            if (_currentPopUp == null) return;

            int viewportWidth = _graphicsDevice.Viewport.Width;
            int viewportHeight = _graphicsDevice.Viewport.Height;

            Rectangle backdrop = new Rectangle(0, 0, viewportWidth, viewportHeight);
            spriteBatch.Draw(_pixel, backdrop, BACKDROP_COLOR);

            if (_currentPopUp is ConfirmPopUpData confirmPopUp)
            {
                DrawConfirmPopUp(spriteBatch, confirmPopUp);
            }
        }

        private void DrawPopupBorder(SpriteBatch spriteBatch, Rectangle bounds, Color color)
        {
            int t = POPUP_BORDER_THICKNESS;
            spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, t), color);
            spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Bottom - t, bounds.Width, t), color);
            spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, t, bounds.Height), color);
            spriteBatch.Draw(_pixel, new Rectangle(bounds.Right - t, bounds.Y, t, bounds.Height), color);
        }

        private void DrawConfirmPopUp(SpriteBatch spriteBatch, ConfirmPopUpData data)
        {
            Rectangle bounds = GetPopupBounds(data);

            spriteBatch.Draw(_pixel, bounds, POPUP_BG_COLOR);

            Rectangle titleBarBounds = new Rectangle(bounds.X, bounds.Y, bounds.Width, TITLE_BAR_HEIGHT);
            spriteBatch.Draw(_pixel, titleBarBounds, TITLE_BAR_COLOR);

            float titleScale = 1.0f;
            Vector2 titleSize = _menuFont.MeasureString(data.Title) * titleScale;
            Vector2 titlePos = new Vector2(
                bounds.X + PADDING,
                bounds.Y + (TITLE_BAR_HEIGHT - titleSize.Y) / 2
            );
            spriteBatch.DrawString(_menuFont, data.Title, titlePos, TEXT_COLOR, 0f, Vector2.Zero, titleScale, SpriteEffects.None, 0f);

            Rectangle closeButtonBounds = GetCloseButtonBounds(bounds);
            var mouseState = Mouse.GetState();
            bool closeHovered = closeButtonBounds.Contains(mouseState.Position);
            Color closeColor = closeHovered ? CLOSE_BUTTON_HOVER_COLOR : TITLE_BAR_COLOR;
            spriteBatch.Draw(_pixel, closeButtonBounds, closeColor);
            DrawCloseIcon(spriteBatch, closeButtonBounds);

            float lineHeight = _contentFont.MeasureString("A").Y * 1.2f;
            List<string> lines = WrapText(data.Message, bounds.Width - PADDING * 2);
            float contentY = bounds.Y + TITLE_BAR_HEIGHT + PADDING;
            foreach (string line in lines)
            {
                spriteBatch.DrawString(_contentFont, line, new Vector2(bounds.X + PADDING, contentY), TEXT_COLOR);
                contentY += lineHeight;
            }

            Rectangle confirmButtonBounds = GetConfirmButtonBounds(bounds);
            Rectangle cancelButtonBounds = GetCancelButtonBounds(bounds);
            bool confirmHovered = confirmButtonBounds.Contains(mouseState.Position);
            bool cancelHovered = cancelButtonBounds.Contains(mouseState.Position);

            // Confirm button: gradient + border + optional top highlight
            int confirmSplit = confirmButtonBounds.Y + confirmButtonBounds.Height * 50 / 100;
            if (confirmHovered)
            {
                spriteBatch.Draw(_pixel, new Rectangle(confirmButtonBounds.X, confirmButtonBounds.Y, confirmButtonBounds.Width, confirmSplit - confirmButtonBounds.Y), CONFIRM_BUTTON_HOVER_TOP);
                spriteBatch.Draw(_pixel, new Rectangle(confirmButtonBounds.X, confirmSplit, confirmButtonBounds.Width, confirmButtonBounds.Bottom - confirmSplit), CONFIRM_BUTTON_HOVER_BOTTOM);
                spriteBatch.Draw(_pixel, new Rectangle(confirmButtonBounds.X, confirmButtonBounds.Y, confirmButtonBounds.Width, 1), new Color(255, 255, 255, 55));
            }
            else
            {
                spriteBatch.Draw(_pixel, new Rectangle(confirmButtonBounds.X, confirmButtonBounds.Y, confirmButtonBounds.Width, confirmSplit - confirmButtonBounds.Y), CONFIRM_BUTTON_TOP);
                spriteBatch.Draw(_pixel, new Rectangle(confirmButtonBounds.X, confirmSplit, confirmButtonBounds.Width, confirmButtonBounds.Bottom - confirmSplit), CONFIRM_BUTTON_BOTTOM);
                spriteBatch.Draw(_pixel, new Rectangle(confirmButtonBounds.X, confirmButtonBounds.Y, confirmButtonBounds.Width, 1), new Color(255, 255, 255, 40));
            }
            DrawPopupBorder(spriteBatch, confirmButtonBounds, new Color(60, 140, 60));
            Vector2 confirmTextSize = _menuFont.MeasureString(data.ConfirmText);
            spriteBatch.DrawString(_menuFont, data.ConfirmText,
                new Vector2(confirmButtonBounds.Center.X - confirmTextSize.X / 2, confirmButtonBounds.Center.Y - confirmTextSize.Y / 2), TEXT_COLOR);

            // Cancel button: gradient + border
            int cancelSplit = cancelButtonBounds.Y + cancelButtonBounds.Height * 50 / 100;
            Color cancelTop = cancelHovered ? new Color(68, 72, 85) : new Color(48, 52, 62);
            Color cancelBottom = cancelHovered ? new Color(85, 88, 100) : new Color(62, 65, 75);
            spriteBatch.Draw(_pixel, new Rectangle(cancelButtonBounds.X, cancelButtonBounds.Y, cancelButtonBounds.Width, cancelSplit - cancelButtonBounds.Y), cancelTop);
            spriteBatch.Draw(_pixel, new Rectangle(cancelButtonBounds.X, cancelSplit, cancelButtonBounds.Width, cancelButtonBounds.Bottom - cancelSplit), cancelBottom);
            DrawPopupBorder(spriteBatch, cancelButtonBounds, BORDER_COLOR);
            Vector2 cancelTextSize = _menuFont.MeasureString(data.CancelText);
            spriteBatch.DrawString(_menuFont, data.CancelText,
                new Vector2(cancelButtonBounds.Center.X - cancelTextSize.X / 2, cancelButtonBounds.Center.Y - cancelTextSize.Y / 2), TEXT_COLOR);

            DrawPopupBorder(spriteBatch, bounds, BORDER_COLOR);
        }

        private void DrawCloseIcon(SpriteBatch spriteBatch, Rectangle bounds)
        {
            if (_closeIcon != null)
            {
                spriteBatch.Draw(_closeIcon, bounds, Color.White);
            }
            else
            {
                int iconPadding = 6;
                int lineThickness = 2;
                Vector2 center = new Vector2(bounds.Center.X, bounds.Center.Y);
                float length = (bounds.Width - iconPadding * 2) / 2;
                DrawLine(spriteBatch, center - new Vector2(length, length), center + new Vector2(length, length), lineThickness);
                DrawLine(spriteBatch, center + new Vector2(length, -length), center + new Vector2(-length, length), lineThickness);
            }
        }

        private void DrawLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, int thickness)
        {
            Vector2 dir = end - start;
            float len = dir.Length();
            dir.Normalize();
            Rectangle rect = new Rectangle((int)start.X, (int)start.Y, (int)len, thickness);
            spriteBatch.Draw(_pixel, rect, null, Color.White, (float)Math.Atan2(dir.Y, dir.X), Vector2.Zero, SpriteEffects.None, 0);
        }

        private Rectangle GetPopupBounds(ConfirmPopUpData data)
        {
            List<string> lines = WrapText(data.Message, POPUP_MAX_WIDTH - PADDING * 2);
            float lineHeight = _contentFont.MeasureString("A").Y * 1.2f;
            int lineCount = Math.Max(lines.Count, 1);
            float contentHeight = lineCount * lineHeight;
            int buttonAreaHeight = BUTTON_HEIGHT + PADDING;
            int totalHeight = TITLE_BAR_HEIGHT + PADDING + (int)contentHeight + PADDING + buttonAreaHeight;

            float maxLineWidth = 0;
            foreach (var line in lines)
            {
                float w = _contentFont.MeasureString(line).X;
                if (w > maxLineWidth) maxLineWidth = w;
            }
            int popupWidth = Math.Clamp((int)maxLineWidth + PADDING * 2, POPUP_MIN_WIDTH, POPUP_MAX_WIDTH);

            int x = (_windowWidth - popupWidth) / 2;
            int y = (_windowHeight - totalHeight) / 2;

            return new Rectangle(x, y, popupWidth, totalHeight);
        }

        private Rectangle GetCloseButtonBounds(Rectangle popupBounds)
        {
            return new Rectangle(
                popupBounds.Right - CLOSE_BUTTON_SIZE - CLOSE_BUTTON_PADDING,
                popupBounds.Y + (TITLE_BAR_HEIGHT - CLOSE_BUTTON_SIZE) / 2,
                CLOSE_BUTTON_SIZE,
                CLOSE_BUTTON_SIZE
            );
        }

        private Rectangle GetConfirmButtonBounds(Rectangle popupBounds)
        {
            int buttonsTotalWidth = BUTTON_WIDTH * 2 + BUTTON_SPACING;
            int startX = popupBounds.Center.X - buttonsTotalWidth / 2;
            int buttonY = popupBounds.Bottom - PADDING - BUTTON_HEIGHT;
            return new Rectangle(startX, buttonY, BUTTON_WIDTH, BUTTON_HEIGHT);
        }

        private Rectangle GetCancelButtonBounds(Rectangle popupBounds)
        {
            int buttonsTotalWidth = BUTTON_WIDTH * 2 + BUTTON_SPACING;
            int startX = popupBounds.Center.X - buttonsTotalWidth / 2;
            int buttonY = popupBounds.Bottom - PADDING - BUTTON_HEIGHT;
            return new Rectangle(startX + BUTTON_WIDTH + BUTTON_SPACING, buttonY, BUTTON_WIDTH, BUTTON_HEIGHT);
        }

        private List<string> WrapText(string text, int maxWidth)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text)) return lines;

            // Split by newlines first so "ID:\nName:" creates separate lines; then wrap each paragraph by width.
            string[] paragraphs = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string para in paragraphs)
            {
                string[] words = para.Split(' ');
                string currentLine = "";

                foreach (string word in words)
                {
                    string testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                    float w = _contentFont.MeasureString(testLine).X;
                    if (w <= maxWidth)
                    {
                        currentLine = testLine;
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(currentLine))
                            lines.Add(currentLine);
                        currentLine = _contentFont.MeasureString(word).X > maxWidth ? word : word;
                    }
                }
                if (!string.IsNullOrEmpty(currentLine))
                    lines.Add(currentLine);
            }
            return lines;
        }

        public void UpdateWindowWidth(int width)
        {
            _windowWidth = width;
            _windowHeight = _graphicsDevice.Viewport.Height;
        }

        public void Dispose()
        {
            _pixel?.Dispose();
        }

        public bool HasActivePopUp => _currentPopUp != null;

        public void QueuePopUp(PopUpData data)
        {
            _popUpQueue.Enqueue(data);
        }

        public static void ShowConfirm(string title, string message, Action onConfirm, Action onCancel = null,
            string confirmText = "Confirm", string cancelText = "Cancel")
        {
            if (_instance == null) return;
            var data = new ConfirmPopUpData(title, message, onConfirm, onCancel ?? (() => { }), confirmText, cancelText);
            _instance.QueuePopUp(data);
        }

        public static void ShowConfirmCloseEngine()
        {
            if (_instance == null) return;
            ShowConfirm(
                "Close Engine",
                "Do you want to close the engine?",
                () => GameEngine.Instance.Exit(),
                () => { },
                "Yes",
                "No"
            );
        }
    }
}
