using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using MarySGameEngine.Modules.TaskBar_essential;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Content;

namespace MarySGameEngine.Modules.WindowManagement_essential
{
    public class WindowProperties
    {
        [JsonPropertyName("is_visible")]
        public bool IsVisible { get; set; }

        [JsonPropertyName("is_movable")]
        public bool IsMovable { get; set; }

        [JsonPropertyName("is_resizable")]
        public bool IsResizable { get; set; }
    }

    public class WindowManagement
    {
        // Add static z-order tracking
        private static int _nextZOrder = 0;
        private static List<WindowManagement> _activeWindows = new List<WindowManagement>();
        private int _zOrder;
        
        // Window properties
        private int _windowWidth;
        private int _windowHeight;
        private Rectangle _windowBounds;
        private Vector2 _position;
        private bool _isDragging;
        private Vector2 _dragOffset;
        private bool _isResizing;
        private Vector2 _resizeStartPos;
        private Vector2 _resizeStartSize;
        private bool _isMaximized;
        private Vector2 _preMaximizePosition;
        private Vector2 _preMaximizeSize;
        private bool _isMinimized;
        private Vector2 _preMinimizePosition;
        private Vector2 _preMinimizeSize;
        private bool _isClosing;
        private Vector2 _closeAnimationCenter;
        private float _closeAnimationScale;
        private int _titleBarHeight = 40;
        private int _defaultWidth = 250;
        private int _defaultHeight = 400;
        private int _buttonSize = 30;
        private int _buttonSpacing = 5;
        private const int MIN_WINDOW_WIDTH = 200;
        private const int MIN_WINDOW_HEIGHT = 200;
        private const int TOP_BAR_HEIGHT = 35;
        private const float UNSTICK_THRESHOLD = 100f;
        private const int TITLE_BUTTON_SPACING = 20;  // Spacing between title and first button
        private const int TITLE_LEFT_PADDING = 10;    // Left padding for title text
        private Vector2 _dragStartPosition;
        private bool _isUnsticking;
        private TaskBar _taskBar;
        private string _windowTitle;
        private GameEngine _engine;

        // Colors
        private Color _windowColor = new Color(40, 40, 40);
        private Color _titleBarColor = new Color(147, 112, 219); // Purple title bar
        private Color _hoverColor = new Color(60, 60, 60);
        private Color _buttonHoverColor = new Color(180, 145, 250); // Lighter purple for button hover
        private Color _closeButtonHoverColor = new Color(232, 17, 35);
        private Color ACTIVE_INDICATOR_COLOR = new Color(147, 112, 219); // Purple color for highlight
        private Color WINDOW_BORDER_COLOR = Color.Black; // Black border color
        private const int WINDOW_BORDER_THICKNESS = 2; // Thicker border for 80s style
        private const float HIGHLIGHT_DURATION = 1.5f; // Reduced duration to 1.5 seconds
        private const float HIGHLIGHT_BLINK_SPEED = 2.0f; // Reduced to 2 pulses per second
        private const float HIGHLIGHT_MIN_ALPHA = 0.3f; // Increased minimum alpha for less contrast
        private const float HIGHLIGHT_MAX_ALPHA = 0.7f; // Reduced maximum alpha for less intensity
        private float _highlightTimer = 0f;
        private bool _isHighlighted = false;
        private static WindowManagement _currentlyHighlightedWindow = null; // Track currently highlighted window

        // Resources
        private SpriteFont _menuFont;
        private SpriteFont _titleFont;
        private Texture2D _pixel;
        private Texture2D _closeIcon;
        private Texture2D _maximiseIcon;
        private Texture2D _restoreIcon;
        private Texture2D _minimiseIcon;
        private Texture2D _lockedIcon;
        private Texture2D _unlockedIcon;
        private Texture2D _pinIcon;
        private Texture2D _unpinIcon;
        private MouseState _currentMouseState;
        private MouseState _previousMouseState;
        private GraphicsDevice _graphicsDevice;
        private WindowProperties _properties;
        private bool _isAnimating;
        private Vector2 _animationStartPosition;
        private Vector2 _animationStartSize;
        private Vector2 _animationTargetPosition;
        private Vector2 _animationTargetSize;
        private float _animationProgress;
        private const float ANIMATION_SPEED = 0.05f; // Reduced from 0.15f for slower animation
        private const float MINIMIZE_SCALE_START = 1.0f;
        private const float MINIMIZE_SCALE_END = 0.15f; // Changed from 0.3f to 0.2f for a middle ground
        private const float MINIMIZE_VISIBILITY_THRESHOLD = 0.4f; // New constant for visibility threshold
        private const float CLOSE_ANIMATION_SPEED = 0.08f; // Speed of close animation
        private bool _isPinned;
        private bool _isLocked;
        private string _currentTooltip = string.Empty;
        private Vector2 _tooltipPosition;
        private const int TOOLTIP_PADDING = 5;
        private const int TOOLTIP_FONT_SIZE = 12;
        private const int TOOLTIP_DELAY = 500; // milliseconds
        private float _tooltipTimer = 0f;
        private bool _showTooltip = false;

        public WindowManagement(GraphicsDevice graphicsDevice, SpriteFont menuFont, int windowWidth, WindowProperties properties)
        {
            _menuFont = menuFont;
            _titleFont = menuFont;  // Will be updated in LoadContent
            _windowWidth = windowWidth;
            _graphicsDevice = graphicsDevice;
            _windowHeight = graphicsDevice.Viewport.Height;
            _position = new Vector2(100, TOP_BAR_HEIGHT);
            _properties = properties;
            _animationProgress = 0f;
            _isAnimating = false;
            _isClosing = false;
            _closeAnimationScale = 1.0f;
            _engine = (GameEngine)GameEngine.Instance;
            
            // Add to active windows list and assign z-order
            _activeWindows.Add(this);
            _zOrder = _nextZOrder++;
            
            // Create a 1x1 white texture for drawing rectangles
            _pixel = new Texture2D(graphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });

            // Initialize window bounds
            UpdateWindowBounds();
        }

        public void SetTaskBar(TaskBar taskBar)
        {
            _taskBar = taskBar;
        }

        private int CalculateMinimumWidth(string title)
        {
            // Measure title text width using the title font
            Vector2 titleSize = _titleFont.MeasureString(title);
            
            // Calculate total width needed for buttons
            int totalButtonWidth = (_buttonSize * 5) + (_buttonSpacing * 4);  // 5 buttons with 4 spaces between them
            
            // Calculate minimum width:
            // - Left padding for title
            // - Title text width
            // - Spacing between title and first button
            // - Total width of all buttons
            return (int)(TITLE_LEFT_PADDING + titleSize.X + TITLE_BUTTON_SPACING + totalButtonWidth);
        }

        private void UpdateWindowBounds()
        {
            try
            {
                int gameWindowWidth = _graphicsDevice.Viewport.Width;
                int gameWindowHeight = _graphicsDevice.Viewport.Height;

                // Calculate minimum width based on title and buttons
                int minWidth = CalculateMinimumWidth(_windowTitle);

                if (_isMaximized)
                {
                    _windowBounds = new Rectangle(
                        0,
                        _taskBar != null && _taskBar.GetCurrentPosition() == TaskBarPosition.Top ? _taskBar.GetTaskBarBounds().Height : TOP_BAR_HEIGHT,
                        Math.Max(minWidth, Math.Min(_defaultWidth, gameWindowWidth)),
                        Math.Min(gameWindowHeight - TOP_BAR_HEIGHT, gameWindowHeight)
                    );
                }
                else
                {
                    int x = Math.Max(0, Math.Min((int)_position.X, gameWindowWidth - minWidth));
                    int y = _taskBar != null && _taskBar.GetCurrentPosition() == TaskBarPosition.Top ? 
                        _taskBar.GetTaskBarBounds().Height : TOP_BAR_HEIGHT;
                    y = Math.Max(y, Math.Min((int)_position.Y, gameWindowHeight - MIN_WINDOW_HEIGHT));
                    
                    _windowBounds = new Rectangle(
                        x,
                        y,
                        Math.Max(minWidth, Math.Min(_defaultWidth, gameWindowWidth - x)),
                        Math.Min(_defaultHeight, gameWindowHeight - y)
                    );
                }

                // Adjust window position if it overlaps with TaskBar
                if (_taskBar != null)
                {
                    Rectangle taskBarBounds = _taskBar.GetTaskBarBounds();
                    TaskBarPosition taskBarPosition = _taskBar.GetCurrentPosition();

                    switch (taskBarPosition)
                    {
                        case TaskBarPosition.Left:
                            if (_windowBounds.Left < taskBarBounds.Right)
                            {
                                _windowBounds.X = taskBarBounds.Right;
                                _position.X = taskBarBounds.Right;
                            }
                            break;
                        case TaskBarPosition.Right:
                            if (_windowBounds.Right > taskBarBounds.Left)
                            {
                                _windowBounds.X = taskBarBounds.Left - _windowBounds.Width;
                                _position.X = taskBarBounds.Left - _windowBounds.Width;
                            }
                            break;
                        case TaskBarPosition.Top:
                            if (_windowBounds.Top < taskBarBounds.Bottom)
                            {
                                _windowBounds.Y = taskBarBounds.Bottom;
                                _position.Y = taskBarBounds.Bottom;
                            }
                            break;
                        case TaskBarPosition.Bottom:
                            if (_windowBounds.Bottom > taskBarBounds.Top)
                            {
                                _windowBounds.Y = taskBarBounds.Top - _windowBounds.Height;
                                _position.Y = taskBarBounds.Top - _windowBounds.Height;
                            }
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                _windowBounds = new Rectangle(10, TOP_BAR_HEIGHT, MIN_WINDOW_WIDTH, MIN_WINDOW_HEIGHT);
                System.Diagnostics.Debug.WriteLine($"Error updating window bounds: {ex.Message}");
            }
        }

        private Rectangle GetMaximizeButtonBounds(Rectangle? bounds = null)
        {
            try
            {
                Rectangle targetBounds = bounds ?? _windowBounds;
                return new Rectangle(
                    targetBounds.Right - (_buttonSize * 2) - (_buttonSpacing * 2),
                    targetBounds.Y + (_titleBarHeight - _buttonSize) / 2,
                    _buttonSize,
                    _buttonSize
                );
            }
            catch
            {
                return Rectangle.Empty;
            }
        }

        private Rectangle GetMinimizeButtonBounds(Rectangle? bounds = null)
        {
            try
            {
                Rectangle targetBounds = bounds ?? _windowBounds;
                return new Rectangle(
                    targetBounds.Right - (_buttonSize * 3) - (_buttonSpacing * 3),
                    targetBounds.Y + (_titleBarHeight - _buttonSize) / 2,
                    _buttonSize,
                    _buttonSize
                );
            }
            catch
            {
                return Rectangle.Empty;
            }
        }

        private Rectangle GetCloseButtonBounds(Rectangle? bounds = null)
        {
            try
            {
                Rectangle targetBounds = bounds ?? _windowBounds;
                return new Rectangle(
                    targetBounds.Right - _buttonSize - _buttonSpacing,
                    targetBounds.Y + (_titleBarHeight - _buttonSize) / 2,
                    _buttonSize,
                    _buttonSize
                );
            }
            catch
            {
                return Rectangle.Empty;
            }
        }

        private Rectangle GetLockButtonBounds(Rectangle? bounds = null)
        {
            try
            {
                Rectangle targetBounds = bounds ?? _windowBounds;
                return new Rectangle(
                    targetBounds.Right - (_buttonSize * 4) - (_buttonSpacing * 4),
                    targetBounds.Y + (_titleBarHeight - _buttonSize) / 2,
                    _buttonSize,
                    _buttonSize
                );
            }
            catch
            {
                return Rectangle.Empty;
            }
        }

        private Rectangle GetPinButtonBounds(Rectangle? bounds = null)
        {
            try
            {
                Rectangle targetBounds = bounds ?? _windowBounds;
                return new Rectangle(
                    targetBounds.Right - (_buttonSize * 5) - (_buttonSpacing * 5),
                    targetBounds.Y + (_titleBarHeight - _buttonSize) / 2,
                    _buttonSize,
                    _buttonSize
                );
            }
            catch
            {
                return Rectangle.Empty;
            }
        }

        private Rectangle GetResizeHandleBounds(Rectangle? bounds = null)
        {
            try
            {
                Rectangle targetBounds = bounds ?? _windowBounds;
                const int handleSize = 24; // Increased to 24 for better pattern alignment
                return new Rectangle(
                    targetBounds.Right - handleSize,
                    targetBounds.Bottom - handleSize,
                    handleSize,
                    handleSize
                );
            }
            catch
            {
                return Rectangle.Empty;
            }
        }

        private void DrawCloseIcon(SpriteBatch spriteBatch, Rectangle bounds)
        {
            int iconPadding = 6;
            int lineThickness = 2;

            Vector2 center = new Vector2(
                bounds.X + bounds.Width / 2,
                bounds.Y + bounds.Height / 2
            );

            float length = (bounds.Width - (iconPadding * 2)) / 2;

            // Draw first diagonal line (top-left to bottom-right)
            Vector2 start1 = center - new Vector2(length, length);
            Vector2 end1 = center + new Vector2(length, length);
            DrawRotatedLine(spriteBatch, start1, end1, lineThickness, 0);

            // Draw second diagonal line (top-right to bottom-left)
            Vector2 start2 = center + new Vector2(length, -length);
            Vector2 end2 = center + new Vector2(-length, length);
            DrawRotatedLine(spriteBatch, start2, end2, lineThickness, 0);
        }

        private void DrawMinimizeIcon(SpriteBatch spriteBatch, Rectangle bounds)
        {
            int iconPadding = 6;
            int lineThickness = 2;

            // Draw horizontal line for minimize icon
            Rectangle lineRect = new Rectangle(
                bounds.X + iconPadding,
                bounds.Y + (bounds.Height - lineThickness) / 2,
                bounds.Width - (iconPadding * 2),
                lineThickness
            );

            spriteBatch.Draw(_pixel, lineRect, Color.White);
        }

        private void DrawRotatedLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, int thickness, float angle)
        {
            Vector2 direction = end - start;
            float length = direction.Length();
            direction.Normalize();

            Rectangle lineRect = new Rectangle(
                (int)start.X,
                (int)start.Y,
                (int)length,
                thickness
            );

            spriteBatch.Draw(_pixel, lineRect, null, Color.White, (float)Math.Atan2(direction.Y, direction.X), Vector2.Zero, SpriteEffects.None, 0);
        }

        private void SmartAdjustWindowPosition()
        {
            Rectangle currentBounds = _windowBounds;
            bool needsAdjustment = false;
            int newX = currentBounds.X;
            int newY = currentBounds.Y;
            int newWidth = currentBounds.Width;
            int newHeight = currentBounds.Height;

            if (currentBounds.Left < 0)
            {
                newX = 0;
                needsAdjustment = true;
            }

            if (currentBounds.Right > _windowWidth)
            {
                newX = _windowWidth - currentBounds.Width;
                needsAdjustment = true;
            }

            if (currentBounds.Top < 0)
            {
                newY = 0;
                needsAdjustment = true;
            }

            if (currentBounds.Bottom > _windowHeight)
            {
                newY = _windowHeight - currentBounds.Height;
                needsAdjustment = true;
            }

            if (currentBounds.Width > _windowWidth)
            {
                newWidth = _windowWidth;
                newX = 0;
                needsAdjustment = true;
            }

            if (currentBounds.Height > _windowHeight)
            {
                newHeight = _windowHeight;
                newY = 0;
                needsAdjustment = true;
            }

            // Check for TaskBar overlap
            if (_taskBar != null)
            {
                Rectangle taskBarBounds = _taskBar.GetTaskBarBounds();
                TaskBarPosition taskBarPosition = _taskBar.GetCurrentPosition();

                switch (taskBarPosition)
                {
                    case TaskBarPosition.Left:
                        if (currentBounds.Left < taskBarBounds.Right)
                        {
                            newX = taskBarBounds.Right;
                            needsAdjustment = true;
                        }
                        break;
                    case TaskBarPosition.Right:
                        if (currentBounds.Right > taskBarBounds.Left)
                        {
                            newX = taskBarBounds.Left - currentBounds.Width;
                            needsAdjustment = true;
                        }
                        break;
                    case TaskBarPosition.Top:
                        if (currentBounds.Top < taskBarBounds.Bottom)
                        {
                            newY = taskBarBounds.Bottom;
                            needsAdjustment = true;
                        }
                        break;
                    case TaskBarPosition.Bottom:
                        if (currentBounds.Bottom > taskBarBounds.Top)
                        {
                            newY = taskBarBounds.Top - currentBounds.Height;
                            needsAdjustment = true;
                        }
                        break;
                }
            }

            if (needsAdjustment)
            {
                _position = new Vector2(newX, newY);
                _defaultWidth = newWidth;
                _defaultHeight = newHeight;
                UpdateWindowBounds();
            }
        }

        public void Update()
        {
            if (!_properties.IsVisible) return;

            _previousMouseState = _currentMouseState;
            _currentMouseState = Mouse.GetState();

            // Update highlight timer
            if (_isHighlighted)
            {
                _highlightTimer += (float)GameEngine.Instance.TargetElapsedTime.TotalSeconds;
                if (_highlightTimer >= HIGHLIGHT_DURATION)
                {
                    _isHighlighted = false;
                    _highlightTimer = 0f;
                    if (_currentlyHighlightedWindow == this)
                    {
                        _currentlyHighlightedWindow = null;
                    }
                    _engine.Log($"WindowManagement: Highlight ended for window {_windowTitle}");
                }
            }

            // Handle animation
            if (_isAnimating)
            {
                _animationProgress += ANIMATION_SPEED;
                if (_animationProgress >= 1f)
                {
                    _animationProgress = 1f;
                    _isAnimating = false;
                    _position = _animationTargetPosition;
                    _defaultWidth = (int)_animationTargetSize.X;
                    _defaultHeight = (int)_animationTargetSize.Y;
                    
                    // If we just finished minimizing, hide the window
                    if (_isMinimized)
                    {
                        _properties.IsVisible = false;
                    }
                }
                else
                {
                    // Use a more dramatic easing function for minimize animation
                    float smoothProgress;
                    if (_isMinimized)
                    {
                        // Accelerate quickly at start, slow down at end
                        smoothProgress = (float)(1 - Math.Cos(_animationProgress * Math.PI / 2));
                        
                        // Add scale effect for minimize
                        float scale = MathHelper.Lerp(MINIMIZE_SCALE_START, MINIMIZE_SCALE_END, smoothProgress);
                        
                        // Calculate center point for scaling
                        Vector2 windowCenter = new Vector2(
                            _position.X + (_windowBounds.Width / 2),
                            _position.Y + (_windowBounds.Height / 2)
                        );
                        
                        // Interpolate position and size with scale effect
                        _position = Vector2.Lerp(_animationStartPosition, _animationTargetPosition, smoothProgress);
                        _defaultWidth = (int)(MathHelper.Lerp(_animationStartSize.X, _animationTargetSize.X, smoothProgress) * scale);
                        _defaultHeight = (int)(MathHelper.Lerp(_animationStartSize.Y, _animationTargetSize.Y, smoothProgress) * scale);
                    }
                    else
                    {
                        // Start slow, accelerate at end for restore
                        smoothProgress = (float)Math.Sin(_animationProgress * Math.PI / 2);
                        
                        // Add scale effect for restore
                        float scale = MathHelper.Lerp(MINIMIZE_SCALE_END, MINIMIZE_SCALE_START, smoothProgress);
                        
                        // Interpolate position and size with scale effect
                        _position = Vector2.Lerp(_animationStartPosition, _animationTargetPosition, smoothProgress);
                        _defaultWidth = (int)(MathHelper.Lerp(_animationStartSize.X, _animationTargetSize.X, smoothProgress) * scale);
                        _defaultHeight = (int)(MathHelper.Lerp(_animationStartSize.Y, _animationTargetSize.Y, smoothProgress) * scale);
                    }
                            }
            UpdateWindowBounds();
            return;
        }

        // Handle close animation
        if (_isClosing)
        {
            _closeAnimationScale -= CLOSE_ANIMATION_SPEED;
            if (_closeAnimationScale <= 0f)
            {
                _closeAnimationScale = 0f;
                _isClosing = false;
                _properties.IsVisible = false;
                
                // Remove from TaskBar
                if (_taskBar != null)
                {
                    _taskBar.SetModuleActive(_windowTitle, false);
                }
                
                // Remove from active windows list
                _activeWindows.Remove(this);
            }
            return;
        }

        // Only process input if not minimized and not animating
        if (_isMinimized) return;

            int gameWindowWidth = _graphicsDevice.Viewport.Width;
            int gameWindowHeight = _graphicsDevice.Viewport.Height;

            // Calculate scaled bounds for close animation
            Rectangle scaledBounds = _windowBounds;
            if (_isClosing)
            {
                // Calculate scaled size
                int scaledWidth = (int)(_windowBounds.Width * _closeAnimationScale);
                int scaledHeight = (int)(_windowBounds.Height * _closeAnimationScale);
                
                // Calculate scaled position to keep center point
                int scaledX = (int)(_closeAnimationCenter.X - scaledWidth / 2);
                int scaledY = (int)(_closeAnimationCenter.Y - scaledHeight / 2);
                
                scaledBounds = new Rectangle(scaledX, scaledY, scaledWidth, scaledHeight);
            }

            Rectangle titleBarBounds = new Rectangle(
                scaledBounds.X,
                scaledBounds.Y,
                scaledBounds.Width - (_buttonSize * 3) - (_buttonSpacing * 3),
                _titleBarHeight
            );

            Rectangle maximizeButtonBounds = GetMaximizeButtonBounds(scaledBounds);
            Rectangle minimizeButtonBounds = GetMinimizeButtonBounds(scaledBounds);
            Rectangle closeButtonBounds = GetCloseButtonBounds(scaledBounds);
            Rectangle resizeHandleBounds = GetResizeHandleBounds(scaledBounds);

            // Check if mouse is over any part of the window
            bool isMouseOverWindow = scaledBounds.Contains(_currentMouseState.Position);

            if (_currentMouseState.LeftButton == ButtonState.Pressed && 
                _previousMouseState.LeftButton == ButtonState.Released)
            {
                if (isMouseOverWindow)
                {
                    // Bring window to front when clicked
                    BringToFront();

                    if (_properties.IsMovable && titleBarBounds.Contains(_currentMouseState.Position))
                    {
                        _isDragging = true;
                        _dragStartPosition = new Vector2(_currentMouseState.Position.X, _currentMouseState.Position.Y);
                        _dragOffset = new Vector2(
                            _currentMouseState.Position.X - _position.X,
                            _currentMouseState.Position.Y - _position.Y
                        );
                        _isUnsticking = false;
                    }
                    else if (maximizeButtonBounds.Contains(_currentMouseState.Position))
                    {
                        ToggleMaximize();
                    }
                    else if (minimizeButtonBounds.Contains(_currentMouseState.Position))
                    {
                        ToggleMinimize();
                    }
                    else if (closeButtonBounds.Contains(_currentMouseState.Position))
                    {
                        StartCloseAnimation();
                    }
                    else if (_properties.IsResizable && resizeHandleBounds.Contains(_currentMouseState.Position))
                    {
                        _isResizing = true;
                        _resizeStartPos = new Vector2(_currentMouseState.Position.X, _currentMouseState.Position.Y);
                        _resizeStartSize = new Vector2(_windowBounds.Width, _windowBounds.Height);
                    }
                }
            }
            else if (_currentMouseState.LeftButton == ButtonState.Released)
            {
                if (_isDragging || _isResizing)
                {
                    SmartAdjustWindowPosition();
                }
                _isDragging = false;
                _isResizing = false;
                _isUnsticking = false;
            }

            if (_isDragging && _properties.IsMovable)
            {
                float dragDistance = Vector2.Distance(_dragStartPosition, new Vector2(_currentMouseState.Position.X, _currentMouseState.Position.Y));

                if (_isMaximized && !_isUnsticking && dragDistance > UNSTICK_THRESHOLD)
                {
                    _isUnsticking = true;
                    _isMaximized = false;
                    _defaultWidth = (int)_preMaximizeSize.X;
                    _defaultHeight = (int)_preMaximizeSize.Y;
                    _position = new Vector2(
                        _currentMouseState.Position.X - _dragOffset.X,
                        _currentMouseState.Position.Y - _dragOffset.Y
                    );
                }

                if (!_isMaximized || _isUnsticking)
                {
                    float newX = _currentMouseState.Position.X - _dragOffset.X;
                    float newY = _currentMouseState.Position.Y - _dragOffset.Y;

                    newY = Math.Max(TOP_BAR_HEIGHT, newY);
                    newX = Math.Max(0, Math.Min(newX, _windowWidth - _windowBounds.Width));
                    newY = Math.Min(newY, _windowHeight - _windowBounds.Height);

                    // Check for TaskBar overlap during drag
                    if (_taskBar != null)
                    {
                        Rectangle taskBarBounds = _taskBar.GetTaskBarBounds();
                        TaskBarPosition taskBarPosition = _taskBar.GetCurrentPosition();

                        switch (taskBarPosition)
                        {
                            case TaskBarPosition.Left:
                                newX = Math.Max(newX, taskBarBounds.Right);
                                break;
                            case TaskBarPosition.Right:
                                newX = Math.Min(newX, taskBarBounds.Left - _windowBounds.Width);
                                break;
                            case TaskBarPosition.Top:
                                newY = Math.Max(newY, taskBarBounds.Bottom);
                                break;
                            case TaskBarPosition.Bottom:
                                newY = Math.Min(newY, taskBarBounds.Top - _windowBounds.Height);
                                break;
                        }
                    }

                    _position = new Vector2(newX, newY);
                    UpdateWindowBounds();
                }
            }
            else if (_isResizing && _properties.IsResizable && !_isMaximized)
            {
                float newWidth = _resizeStartSize.X + (_currentMouseState.Position.X - _resizeStartPos.X);
                float newHeight = _resizeStartSize.Y + (_currentMouseState.Position.Y - _resizeStartPos.Y);

                newWidth = Math.Max(MIN_WINDOW_WIDTH, newWidth);
                newHeight = Math.Max(MIN_WINDOW_HEIGHT, newHeight);

                newWidth = Math.Min(newWidth, _windowWidth - _windowBounds.X);
                newHeight = Math.Min(newHeight, _windowHeight - _windowBounds.Y);

                if (_windowBounds.Y < TOP_BAR_HEIGHT)
                {
                    newHeight = Math.Min(newHeight, _windowHeight - TOP_BAR_HEIGHT);
                }

                // Check for TaskBar overlap during resize
                if (_taskBar != null)
                {
                    Rectangle taskBarBounds = _taskBar.GetTaskBarBounds();
                    TaskBarPosition taskBarPosition = _taskBar.GetCurrentPosition();

                    switch (taskBarPosition)
                    {
                        case TaskBarPosition.Left:
                            newWidth = Math.Min(newWidth, _windowWidth - taskBarBounds.Right);
                            break;
                        case TaskBarPosition.Right:
                            newWidth = Math.Min(newWidth, taskBarBounds.Left - _windowBounds.X);
                            break;
                        case TaskBarPosition.Top:
                            newHeight = Math.Min(newHeight, _windowHeight - taskBarBounds.Bottom);
                            break;
                        case TaskBarPosition.Bottom:
                            newHeight = Math.Min(newHeight, taskBarBounds.Top - _windowBounds.Y);
                            break;
                    }
                }

                _defaultWidth = (int)newWidth;
                _defaultHeight = (int)newHeight;
                UpdateWindowBounds();
            }

            // Update tooltip
            UpdateTooltip(_currentMouseState);
        }

        private void ToggleMaximize()
        {
            try
            {
                if (_isMaximized)
                {
                    _position = _preMaximizePosition;
                    _defaultWidth = (int)_preMaximizeSize.X;
                    _defaultHeight = (int)_preMaximizeSize.Y;
                }
                else
                {
                    _preMaximizePosition = _position;
                    _preMaximizeSize = new Vector2(_defaultWidth, _defaultHeight);
                }
                _isMaximized = !_isMaximized;
                UpdateWindowBounds();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ToggleMaximize: {ex.Message}");
            }
        }

        private void ToggleMinimize()
        {
            try
            {
                if (_isMinimized)
                {
                    _engine.Log($"WindowManagement: Window {_windowTitle} is already minimized");
                    return;
                }

                _engine.Log($"WindowManagement: Minimizing window {_windowTitle}");
                
                // Store current state before minimizing
                _preMinimizePosition = _position;
                _preMinimizeSize = new Vector2(_defaultWidth, _defaultHeight);
                
                // Store state in TaskBar
                if (_taskBar != null)
                {
                    _taskBar.StoreMinimizeState(_windowTitle, _preMinimizePosition, _preMinimizeSize);
                    _engine.Log($"WindowManagement: Stored minimize state in TaskBar for {_windowTitle}");
                }
                
                // Find the target position (TaskBar icon position)
                if (_taskBar != null)
                {
                    var iconPosition = _taskBar.GetModuleIconPosition(_windowTitle);
                    if (iconPosition != Vector2.Zero)
                    {
                        _isAnimating = true;
                        _animationStartPosition = _position;
                        _animationStartSize = new Vector2(_windowBounds.Width, _windowBounds.Height);
                        
                        // Calculate center of the icon for better animation
                        Vector2 iconCenter = new Vector2(
                            iconPosition.X + 20, // Half of icon width (40/2)
                            iconPosition.Y + 20  // Half of icon height (40/2)
                        );
                        
                        // Calculate center of the window
                        Vector2 windowCenter = new Vector2(
                            _position.X + (_windowBounds.Width / 2),
                            _position.Y + (_windowBounds.Height / 2)
                        );
                        
                        // Set target position to be the icon center minus half the final size
                        _animationTargetPosition = new Vector2(
                            iconCenter.X - 20, // Half of final width
                            iconCenter.Y - 20  // Half of final height
                        );
                        
                        _animationTargetSize = new Vector2(40, 40); // Icon size
                        _animationProgress = 0f;
                        _engine.Log($"WindowManagement: Starting minimize animation for {_windowTitle}");
                    }
                }
                
                _isMinimized = true;
                
                // Update TaskBar indicator - minimize keeps icon visible but removes active indicator
                if (_taskBar != null)
                {
                    _taskBar.SetModuleMinimized(_windowTitle, true);
                    _engine.Log($"WindowManagement: Updated TaskBar indicator for {_windowTitle} (minimized)");
                }
            }
            catch (Exception ex)
            {
                _engine.Log($"WindowManagement: Error in ToggleMinimize: {ex.Message}");
            }
        }

        private void StartCloseAnimation()
        {
            try
            {
                _engine.Log($"WindowManagement: Starting close animation for window {_windowTitle}");
                
                // Calculate the center of the window for the shrink animation
                _closeAnimationCenter = new Vector2(
                    _position.X + (_windowBounds.Width / 2),
                    _position.Y + (_windowBounds.Height / 2)
                );
                
                // Start the close animation
                _isClosing = true;
                _closeAnimationScale = 1.0f;
                
                _engine.Log($"WindowManagement: Close animation started for {_windowTitle}");
            }
            catch (Exception ex)
            {
                _engine.Log($"WindowManagement: Error in StartCloseAnimation: {ex.Message}");
            }
        }

        public void Draw(SpriteBatch spriteBatch, string title)
        {
            // Don't draw if not visible or if minimized and not animating
            if (!_properties.IsVisible || (_isMinimized && !_isAnimating)) return;
            
            // Don't draw if minimized and scale is below threshold
            if (_isMinimized && _isAnimating && _animationProgress > 0.85f) return;
            
            // Don't draw if closing and scale is 0
            if (_isClosing && _closeAnimationScale <= 0f) return;
            
            _windowTitle = title;

            // Calculate scaled bounds for close animation
            Rectangle scaledBounds = _windowBounds;
            if (_isClosing)
            {
                // Calculate scaled size
                int scaledWidth = (int)(_windowBounds.Width * _closeAnimationScale);
                int scaledHeight = (int)(_windowBounds.Height * _closeAnimationScale);
                
                // Calculate scaled position to keep center point
                int scaledX = (int)(_closeAnimationCenter.X - scaledWidth / 2);
                int scaledY = (int)(_closeAnimationCenter.Y - scaledHeight / 2);
                
                scaledBounds = new Rectangle(scaledX, scaledY, scaledWidth, scaledHeight);
            }

            // Draw window background
            spriteBatch.Draw(_pixel, scaledBounds, _windowColor);

            // Draw title bar
            Rectangle titleBarBounds = new Rectangle(
                scaledBounds.X,
                scaledBounds.Y,
                scaledBounds.Width,
                _titleBarHeight
            );
            spriteBatch.Draw(_pixel, titleBarBounds, _titleBarColor);

            // Draw title text with the title font
            Vector2 titleTextPos = new Vector2(
                scaledBounds.X + TITLE_LEFT_PADDING,
                scaledBounds.Y + (_titleBarHeight - _titleFont.LineSpacing) / 2
            );
            spriteBatch.DrawString(_titleFont, title, titleTextPos, Color.White);

            // Draw maximize/restore button
            Rectangle maximizeButtonBounds = GetMaximizeButtonBounds(scaledBounds);
            if (maximizeButtonBounds != Rectangle.Empty)
            {
                bool isMaximizeHovered = maximizeButtonBounds.Contains(_currentMouseState.Position);
                spriteBatch.Draw(_pixel, maximizeButtonBounds, isMaximizeHovered ? _buttonHoverColor : _titleBarColor);
                spriteBatch.Draw(_isMaximized ? _restoreIcon : _maximiseIcon, maximizeButtonBounds, Color.White);
            }

            // Draw minimize button
            Rectangle minimizeButtonBounds = GetMinimizeButtonBounds(scaledBounds);
            if (minimizeButtonBounds != Rectangle.Empty)
            {
                bool isMinimizeHovered = minimizeButtonBounds.Contains(_currentMouseState.Position);
                spriteBatch.Draw(_pixel, minimizeButtonBounds, isMinimizeHovered ? _buttonHoverColor : _titleBarColor);
                spriteBatch.Draw(_minimiseIcon, minimizeButtonBounds, Color.White);
            }

            // Draw close button
            Rectangle closeButtonBounds = GetCloseButtonBounds(scaledBounds);
            if (closeButtonBounds != Rectangle.Empty)
            {
                bool isCloseHovered = closeButtonBounds.Contains(_currentMouseState.Position);
                spriteBatch.Draw(_pixel, closeButtonBounds, isCloseHovered ? _closeButtonHoverColor : _titleBarColor);
                spriteBatch.Draw(_closeIcon, closeButtonBounds, Color.White);
            }

            // Draw lock/unlock button
            Rectangle lockButtonBounds = GetLockButtonBounds(scaledBounds);
            if (lockButtonBounds != Rectangle.Empty)
            {
                bool isLockHovered = lockButtonBounds.Contains(_currentMouseState.Position);
                spriteBatch.Draw(_pixel, lockButtonBounds, isLockHovered ? _buttonHoverColor : _titleBarColor);
                spriteBatch.Draw(_isLocked ? _lockedIcon : _unlockedIcon, lockButtonBounds, Color.White);
            }

            // Draw pin/unpin button
            Rectangle pinButtonBounds = GetPinButtonBounds(scaledBounds);
            if (pinButtonBounds != Rectangle.Empty)
            {
                bool isPinHovered = pinButtonBounds.Contains(_currentMouseState.Position);
                spriteBatch.Draw(_pixel, pinButtonBounds, isPinHovered ? _buttonHoverColor : _titleBarColor);
                spriteBatch.Draw(_isPinned ? _unpinIcon : _pinIcon, pinButtonBounds, Color.White);
            }

            // Draw resize handle if not maximized and resizable
            if (!_isMaximized && _properties.IsResizable)
            {
                Rectangle resizeHandleBounds = GetResizeHandleBounds(scaledBounds);
                if (resizeHandleBounds != Rectangle.Empty)
                {
                    // Removed hover effect, just draw the pattern
                    int patternSize = 6;
                    for (int i = 0; i < 4; i++)
                    {
                        for (int j = 0; j < 4; j++)
                        {
                            if (i + j >= 3)
                            {
                                Rectangle dot = new Rectangle(
                                    resizeHandleBounds.Right - ((4 - i) * patternSize),
                                    resizeHandleBounds.Bottom - ((4 - j) * patternSize),
                                    patternSize - 1,
                                    patternSize - 1
                                );
                                spriteBatch.Draw(_pixel, dot, Color.White);
                            }
                        }
                    }
                }
            }

            // Draw window border (black border)
            // Top border
            spriteBatch.Draw(_pixel, new Rectangle(scaledBounds.X - WINDOW_BORDER_THICKNESS, scaledBounds.Y - WINDOW_BORDER_THICKNESS, 
                scaledBounds.Width + (WINDOW_BORDER_THICKNESS * 2), WINDOW_BORDER_THICKNESS), WINDOW_BORDER_COLOR);
            // Bottom border
            spriteBatch.Draw(_pixel, new Rectangle(scaledBounds.X - WINDOW_BORDER_THICKNESS, scaledBounds.Bottom, 
                scaledBounds.Width + (WINDOW_BORDER_THICKNESS * 2), WINDOW_BORDER_THICKNESS), WINDOW_BORDER_COLOR);
            // Left border
            spriteBatch.Draw(_pixel, new Rectangle(scaledBounds.X - WINDOW_BORDER_THICKNESS, scaledBounds.Y - WINDOW_BORDER_THICKNESS, 
                WINDOW_BORDER_THICKNESS, scaledBounds.Height + (WINDOW_BORDER_THICKNESS * 2)), WINDOW_BORDER_COLOR);
            // Right border
            spriteBatch.Draw(_pixel, new Rectangle(scaledBounds.Right, scaledBounds.Y - WINDOW_BORDER_THICKNESS, 
                WINDOW_BORDER_THICKNESS, scaledBounds.Height + (WINDOW_BORDER_THICKNESS * 2)), WINDOW_BORDER_COLOR);

            // Draw highlight if active (after borders to ensure it's visible)
            if (_isHighlighted)
            {
                // Calculate pulse effect using a sine wave with faster oscillation
                float pulseValue = (float)(Math.Sin(_highlightTimer * Math.PI * 2 * HIGHLIGHT_BLINK_SPEED) + 1) / 2;
                float alpha = MathHelper.Lerp(HIGHLIGHT_MIN_ALPHA, HIGHLIGHT_MAX_ALPHA, pulseValue);
                
                // Create a pulsing purple color
                Color highlightColor = new Color(
                    (byte)(ACTIVE_INDICATOR_COLOR.R * alpha),
                    (byte)(ACTIVE_INDICATOR_COLOR.G * alpha),
                    (byte)(ACTIVE_INDICATOR_COLOR.B * alpha),
                    (byte)(255 * alpha)
                );

                // Draw highlight borders with same width as window borders
                // Top border
                spriteBatch.Draw(_pixel, new Rectangle(scaledBounds.X - WINDOW_BORDER_THICKNESS, scaledBounds.Y - WINDOW_BORDER_THICKNESS, 
                    scaledBounds.Width + (WINDOW_BORDER_THICKNESS * 2), WINDOW_BORDER_THICKNESS), highlightColor);
                // Bottom border
                spriteBatch.Draw(_pixel, new Rectangle(scaledBounds.X - WINDOW_BORDER_THICKNESS, scaledBounds.Bottom, 
                    scaledBounds.Width + (WINDOW_BORDER_THICKNESS * 2), WINDOW_BORDER_THICKNESS), highlightColor);
                // Left border
                spriteBatch.Draw(_pixel, new Rectangle(scaledBounds.X - WINDOW_BORDER_THICKNESS, scaledBounds.Y - WINDOW_BORDER_THICKNESS, 
                    WINDOW_BORDER_THICKNESS, scaledBounds.Height + (WINDOW_BORDER_THICKNESS * 2)), highlightColor);
                // Right border
                spriteBatch.Draw(_pixel, new Rectangle(scaledBounds.Right, scaledBounds.Y - WINDOW_BORDER_THICKNESS, 
                    WINDOW_BORDER_THICKNESS, scaledBounds.Height + (WINDOW_BORDER_THICKNESS * 2)), highlightColor);

                // Log the current alpha value for debugging
                if (_highlightTimer % 0.25f < 0.016f) // Log roughly every quarter second
                {
                    _engine.Log($"WindowManagement: Highlight alpha for {_windowTitle}: {alpha:F2}");
                }
            }

            // Draw tooltip
            DrawTooltip(spriteBatch);
        }

        public void UpdateWindowWidth(int newWidth)
        {
            int oldWidth = _windowWidth;
            int oldHeight = _windowHeight;
            
            _windowWidth = newWidth;
            _windowHeight = _graphicsDevice.Viewport.Height;
            
            // Calculate proportional sizes
            float widthRatio = (float)newWidth / oldWidth;
            float heightRatio = (float)_windowHeight / oldHeight;
            
            // Update default dimensions proportionally
            _defaultWidth = (int)(_defaultWidth * widthRatio);
            _defaultHeight = (int)(_defaultHeight * heightRatio);
            
            // Ensure minimum sizes
            _defaultWidth = Math.Max(_defaultWidth, MIN_WINDOW_WIDTH);
            _defaultHeight = Math.Max(_defaultHeight, MIN_WINDOW_HEIGHT);
            
            // Update position to maintain relative position
            float relativeX = _position.X / oldWidth;
            float relativeY = (_position.Y - TOP_BAR_HEIGHT) / (oldHeight - TOP_BAR_HEIGHT);
            
            _position = new Vector2(
                relativeX * newWidth,
                TOP_BAR_HEIGHT + (relativeY * (_windowHeight - TOP_BAR_HEIGHT))
            );
            
            UpdateWindowBounds();
        }

        public void Dispose()
        {
            _activeWindows.Remove(this);
            _pixel?.Dispose();
        }

        public Rectangle GetWindowBounds()
        {
            return _windowBounds;
        }

        public bool IsVisible()
        {
            return _properties.IsVisible;
        }

        public void SetVisible(bool visible)
        {
            _properties.IsVisible = visible;
            if (!visible)
            {
                _activeWindows.Remove(this);
            }
            else if (!_activeWindows.Contains(this))
            {
                _activeWindows.Add(this);
                BringToFront();
            }
        }

        public string GetWindowTitle()
        {
            return _windowTitle;
        }

        public void SetPosition(Vector2 newPosition)
        {
            _position = newPosition;
            UpdateWindowBounds();
        }

        public void HandleTaskBarClick()
        {
            try
            {
                // Remove highlight from previously highlighted window
                if (_currentlyHighlightedWindow != null && _currentlyHighlightedWindow != this)
                {
                    _currentlyHighlightedWindow._isHighlighted = false;
                    _currentlyHighlightedWindow._highlightTimer = 0f;
                }

                if (_isMinimized)
                {
                    _engine.Log($"WindowManagement: Restoring window {_windowTitle} from minimized state");
                    
                    // Store current state for animation
                    _isAnimating = true;
                    _animationStartPosition = _position;
                    _animationStartSize = new Vector2(_windowBounds.Width, _windowBounds.Height);
                    
                    // Get the stored position and size from TaskBar
                    if (_taskBar != null)
                    {
                        var preMinimizePos = _taskBar.GetPreMinimizePosition(_windowTitle);
                        var preMinimizeSize = _taskBar.GetPreMinimizeSize(_windowTitle);
                        
                        if (preMinimizePos.HasValue && preMinimizeSize.HasValue)
                        {
                            _animationTargetPosition = preMinimizePos.Value;
                            _animationTargetSize = preMinimizeSize.Value;
                            _engine.Log($"WindowManagement: Restoring to position {_animationTargetPosition} and size {_animationTargetSize}");
                        }
                        else
                        {
                            // Fallback to default position if stored values aren't available
                            _animationTargetPosition = new Vector2(10, TOP_BAR_HEIGHT);
                            _animationTargetSize = new Vector2(_defaultWidth, _defaultHeight);
                            _engine.Log($"WindowManagement: Using default position {_animationTargetPosition} and size {_animationTargetSize}");
                        }
                    }
                    
                    _animationProgress = 0f;
                    _isMinimized = false;
                    _properties.IsVisible = true;
                    
                    // Update TaskBar indicator - restore from minimized state
                    if (_taskBar != null)
                    {
                        _taskBar.SetModuleMinimized(_windowTitle, false);
                    }
                    
                    _engine.Log($"WindowManagement: Window {_windowTitle} restored successfully");
                }
                else
                {
                    _engine.Log($"WindowManagement: Window {_windowTitle} is not minimized, no action needed");
                }

                // Always bring to front and highlight when clicked
                BringToFront();
                _isHighlighted = true;
                _highlightTimer = 0f;
                _currentlyHighlightedWindow = this;
                _engine.Log($"WindowManagement: Highlighting window {_windowTitle}");
            }
            catch (Exception ex)
            {
                _engine.Log($"WindowManagement: Error restoring window {_windowTitle}: {ex.Message}");
            }
        }

        public int GetZOrder()
        {
            return _zOrder;
        }

        public void BringToFront()
        {
            // Remove from current position
            _activeWindows.Remove(this);
            
            // Add to end of list (top)
            _activeWindows.Add(this);
            
            // Update z-order
            _zOrder = _nextZOrder++;
            
            // Ensure all windows have proper z-order
            for (int i = 0; i < _activeWindows.Count; i++)
            {
                _activeWindows[i]._zOrder = i;
            }

            // Force a redraw to ensure proper z-order
            _engine.Log($"WindowManagement: Brought window {_windowTitle} to front with z-order {_zOrder}");
        }

        public bool IsTopMost()
        {
            return _activeWindows.Count > 0 && _activeWindows[_activeWindows.Count - 1] == this;
        }

        public bool IsDragging()
        {
            return _isDragging;
        }

        public bool IsResizing()
        {
            return _isResizing;
        }

        public void LoadContent(ContentManager content)
        {
            _closeIcon = content.Load<Texture2D>("Modules/WindowManagement_essential/close");
            _maximiseIcon = content.Load<Texture2D>("Modules/WindowManagement_essential/maximise");
            _restoreIcon = content.Load<Texture2D>("Modules/WindowManagement_essential/restore");
            _minimiseIcon = content.Load<Texture2D>("Modules/WindowManagement_essential/minimise");
            _lockedIcon = content.Load<Texture2D>("Modules/WindowManagement_essential/locked");
            _unlockedIcon = content.Load<Texture2D>("Modules/WindowManagement_essential/unlocked");
            _pinIcon = content.Load<Texture2D>("Modules/WindowManagement_essential/pin");
            _unpinIcon = content.Load<Texture2D>("Modules/WindowManagement_essential/unpin");
            _titleFont = content.Load<SpriteFont>("Fonts/SpriteFonts/window_title_font");
        }

        private string GetButtonTooltip(Rectangle bounds)
        {
            if (bounds == GetCloseButtonBounds())
                return "Close";
            else if (bounds == GetMaximizeButtonBounds())
                return _isMaximized ? "Restore" : "Maximize";
            else if (bounds == GetMinimizeButtonBounds())
                return "Minimize";
            else if (bounds == GetLockButtonBounds())
                return _isLocked ? "Unlock" : "Lock";
            else if (bounds == GetPinButtonBounds())
                return _isPinned ? "Unpin" : "Pin";
            return string.Empty;
        }

        private void UpdateTooltip(MouseState mouseState)
        {
            Rectangle[] buttonBounds = new[]
            {
                GetCloseButtonBounds(),
                GetMaximizeButtonBounds(),
                GetMinimizeButtonBounds(),
                GetLockButtonBounds(),
                GetPinButtonBounds()
            };

            bool isOverButton = false;
            foreach (var bounds in buttonBounds)
            {
                if (bounds.Contains(mouseState.Position))
                {
                    string tooltip = GetButtonTooltip(bounds);
                    if (tooltip != _currentTooltip)
                    {
                        _currentTooltip = tooltip;
                        _tooltipTimer = 0f;
                        _showTooltip = false;
                    }
                    isOverButton = true;
                    _tooltipPosition = new Vector2(mouseState.Position.X, mouseState.Position.Y + 20);
                    break;
                }
            }

            if (!isOverButton)
            {
                _currentTooltip = string.Empty;
                _tooltipTimer = 0f;
                _showTooltip = false;
            }
            else if (!_showTooltip)
            {
                _tooltipTimer += (float)GameEngine.Instance.TargetElapsedTime.TotalMilliseconds;
                if (_tooltipTimer >= TOOLTIP_DELAY)
                {
                    _showTooltip = true;
                }
            }
        }

        private void DrawTooltip(SpriteBatch spriteBatch)
        {
            if (!_showTooltip || string.IsNullOrEmpty(_currentTooltip))
                return;

            Vector2 textSize = _menuFont.MeasureString(_currentTooltip);
            Rectangle tooltipBounds = new Rectangle(
                (int)_tooltipPosition.X,
                (int)_tooltipPosition.Y,
                (int)textSize.X + (TOOLTIP_PADDING * 2),
                (int)textSize.Y + (TOOLTIP_PADDING * 2)
            );

            // Draw tooltip background
            spriteBatch.Draw(_pixel, tooltipBounds, new Color(0, 0, 0, 200));
            
            // Draw tooltip border
            spriteBatch.Draw(_pixel, new Rectangle(tooltipBounds.X, tooltipBounds.Y, tooltipBounds.Width, 1), Color.White);
            spriteBatch.Draw(_pixel, new Rectangle(tooltipBounds.X, tooltipBounds.Bottom - 1, tooltipBounds.Width, 1), Color.White);
            spriteBatch.Draw(_pixel, new Rectangle(tooltipBounds.X, tooltipBounds.Y, 1, tooltipBounds.Height), Color.White);
            spriteBatch.Draw(_pixel, new Rectangle(tooltipBounds.Right - 1, tooltipBounds.Y, 1, tooltipBounds.Height), Color.White);

            // Draw tooltip text
            spriteBatch.DrawString(
                _menuFont,
                _currentTooltip,
                new Vector2(tooltipBounds.X + TOOLTIP_PADDING, tooltipBounds.Y + TOOLTIP_PADDING),
                Color.White
            );
        }
    }
} 