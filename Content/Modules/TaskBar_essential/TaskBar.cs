using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using System;
using System.IO;
using System.Text.Json;
using Microsoft.Xna.Framework.Content;
using MarySGameEngine;
using System.Linq;
using MarySGameEngine.Modules.WindowManagement_essential;

namespace MarySGameEngine.Modules.TaskBar_essential
{
    public enum TaskBarPosition
    {
        Left,
        Top,
        Right,
        Bottom
    }

    public class TaskBar : IModule
    {
        private GraphicsDevice _graphicsDevice;
        private SpriteFont _menuFont;
        private Texture2D _pixel;
        private Texture2D _logo;
        private MouseState _currentMouseState;
        private MouseState _previousMouseState;
        private bool _isDragging;
        private Vector2 _dragStartPosition;
        private const float UNSTICK_THRESHOLD = 100f;
        private TaskBarPosition _currentPosition;
        private int _iconSize = 40;
        private int _spacing = 5;
        private List<ModuleIcon> _moduleIcons;
        private int _windowWidth;
        private int _windowHeight;
        private Rectangle _taskBarBounds;
        private const int TOP_BAR_HEIGHT = 35;
        private const int TASK_BAR_SIZE = 60;
        private const int SQUARE_ICON_SIZE = 40;
        private const int ACTIVE_INDICATOR_WIDTH = 3; // Width of the purple line indicator
        private const int ACTIVE_INDICATOR_LENGTH = 45; // Length of the indicator line
        private const int DOT_SIZE = 4; // Size of the separator dots
        private const int DOT_SPACING = 6; // Space between dots
        private const int DOT_SEPARATOR_SPACING = 15; // Space between TaskBar icon and dots
        private Color ACTIVE_INDICATOR_COLOR = new Color(147, 112, 219); // Purple color for active indicator
        private GameEngine _engine;
        private Dictionary<string, Texture2D> _moduleLogos; // Store module logos
        private const float TOOLTIP_DELAY = 1.0f; // Show tooltip after 1 second
        private float _hoverTime;
        private string _currentHoveredModule;
        private Vector2 _tooltipPosition;
        private const int TOOLTIP_PADDING = 5;
        private const int TOOLTIP_OFFSET = 10; // Distance from icon to tooltip
        private bool _isMouseDown;
        private Point _lastMousePosition;
        private bool _isMouseOverTaskBar;
        private const float HIGHLIGHT_DURATION = 1.0f; // Duration of highlight effect in seconds
        private const float HIGHLIGHT_BLINK_SPEED = 0.25f; // Speed of blink effect
        private const float ICON_ANIMATION_SPEED = 0.08f; // Speed of icon position animation
        private const float ICON_SHRINK_SPEED = 0.12f; // Speed of icon shrink animation
        private float _highlightTimer = 0f;
        private bool _isHighlighted = false;

        private class ModuleIcon
        {
            public string Name { get; set; }
            public Rectangle Bounds { get; set; }
            public Rectangle TargetBounds { get; set; } // Target position for animation
            public bool IsHovered { get; set; }
            public bool IsVisible { get; set; }
            public Texture2D Logo { get; set; }
            public bool IsActive { get; set; }
            public bool IsAnimating { get; set; } // Whether this icon is currently animating
            public float AnimationProgress { get; set; } // Animation progress (0.0 to 1.0)
            public float Scale { get; set; } = 1.0f; // Scale for shrink animation
            public bool IsShrinking { get; set; } // Whether this icon is shrinking to disappear
            public Vector2? PreMinimizePosition { get; set; }
            public Vector2? PreMinimizeSize { get; set; }
        }

        public TaskBar(GraphicsDevice graphicsDevice, SpriteFont menuFont, int windowWidth)
        {
            try
            {
                _engine = (GameEngine)GameEngine.Instance;
                _engine.Log("TaskBar: Starting constructor");
                
                _graphicsDevice = graphicsDevice;
                _menuFont = menuFont;
                _windowWidth = windowWidth;
                _windowHeight = graphicsDevice.Viewport.Height;
                _currentPosition = TaskBarPosition.Left;
                _moduleIcons = new List<ModuleIcon>();
                _moduleLogos = new Dictionary<string, Texture2D>(); // Initialize logo dictionary

                _engine.Log($"TaskBar: Initialized with window width: {windowWidth}, height: {_windowHeight}");

                // Create a 1x1 white texture for drawing rectangles
                _pixel = new Texture2D(graphicsDevice, 1, 1);
                _pixel.SetData(new[] { Color.White });
                _engine.Log("TaskBar: Created pixel texture");

                UpdateTaskBarBounds();
                _engine.Log($"TaskBar: Updated bounds: {_taskBarBounds}");

                UpdateModuleIcons();
                _engine.Log($"TaskBar: Updated module icons. Count: {_moduleIcons.Count}");
            }
            catch (Exception ex)
            {
                _engine.Log($"TaskBar: ERROR in constructor: {ex.Message}");
                _engine.Log($"TaskBar: Stack trace: {ex.StackTrace}");
            }
        }

        private void UpdateTaskBarBounds()
        {
            switch (_currentPosition)
            {
                case TaskBarPosition.Left:
                    _taskBarBounds = new Rectangle(0, TOP_BAR_HEIGHT, TASK_BAR_SIZE, _windowHeight - TOP_BAR_HEIGHT);
                    break;
                case TaskBarPosition.Right:
                    _taskBarBounds = new Rectangle(_windowWidth - TASK_BAR_SIZE, TOP_BAR_HEIGHT, TASK_BAR_SIZE, _windowHeight - TOP_BAR_HEIGHT);
                    break;
                case TaskBarPosition.Top:
                    _taskBarBounds = new Rectangle(0, TOP_BAR_HEIGHT, _windowWidth, TASK_BAR_SIZE);
                    break;
                case TaskBarPosition.Bottom:
                    _taskBarBounds = new Rectangle(0, _windowHeight - TASK_BAR_SIZE - 1, _windowWidth, TASK_BAR_SIZE + 1);
                    break;
            }
        }

        public Rectangle GetTaskBarBounds()
        {
            return _taskBarBounds;
        }

        public TaskBarPosition GetCurrentPosition()
        {
            return _currentPosition;
        }

        private void UpdateModuleIcons()
        {
            try
            {
                _moduleIcons.Clear();
                int currentX = _taskBarBounds.X;
                int currentY = _taskBarBounds.Y;

                // Use consistent square size for all positions
                int squareSize = TASK_BAR_SIZE;

                // Add TaskBar's own icon first
                Rectangle taskBarBounds;
                switch (_currentPosition)
                {
                    case TaskBarPosition.Left:
                    case TaskBarPosition.Right:
                        taskBarBounds = new Rectangle(currentX, currentY, squareSize, squareSize);
                        currentY += squareSize + _spacing;
                        break;
                    default: // Top or Bottom
                        taskBarBounds = new Rectangle(currentX, currentY, squareSize, squareSize);
                        currentX += squareSize + _spacing;
                        break;
                }

                var taskBarLogo = _moduleLogos.ContainsKey("Task Bar") ? _moduleLogos["Task Bar"] : null;
                _moduleIcons.Add(new ModuleIcon
                {
                    Name = "Task Bar",
                    Bounds = taskBarBounds,
                    TargetBounds = taskBarBounds,
                    IsHovered = false,
                    IsVisible = true,
                    Logo = taskBarLogo,
                    IsActive = true,
                    IsAnimating = false,
                    AnimationProgress = 0f,
                    Scale = 1.0f,
                    IsShrinking = false
                });

                // Add separator dots
                switch (_currentPosition)
                {
                    case TaskBarPosition.Left:
                    case TaskBarPosition.Right:
                        currentY += DOT_SEPARATOR_SPACING + (3 * DOT_SIZE) + (2 * DOT_SPACING);
                        break;
                    default: // Top or Bottom
                        currentX += DOT_SEPARATOR_SPACING + (3 * DOT_SIZE) + (2 * DOT_SPACING);
                        break;
                }

                // Get all module directories
                string modulesPath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "Content", "Modules"
                );

                if (!Directory.Exists(modulesPath))
                {
                    _engine.Log($"TaskBar: ERROR: Modules directory not found at: {modulesPath}");
                    return;
                }

                string[] moduleDirectories = Directory.GetDirectories(modulesPath);

                foreach (var moduleDir in moduleDirectories)
                {
                    string moduleName = Path.GetFileName(moduleDir);
                    string bridgeJsonPath = Path.Combine(moduleDir, "bridge.json");

                    if (!File.Exists(bridgeJsonPath))
                    {
                        continue;
                    }

                    try
                    {
                        string jsonContent = File.ReadAllText(bridgeJsonPath);
                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        };
                        var moduleInfo = JsonSerializer.Deserialize<ModuleInfo>(jsonContent, options);

                        if (moduleInfo == null)
                        {
                            continue;
                        }

                        if (!moduleInfo.IsVisible)
                        {
                            continue;
                        }

                        // Skip TaskBar since we already added it
                        if (moduleInfo.Name == "Task Bar")
                        {
                            continue;
                        }

                        // Skip TopBar module
                        if (moduleInfo.Name == "Top Bar")
                        {
                            continue;
                        }

                        Rectangle bounds;
                        switch (_currentPosition)
                        {
                            case TaskBarPosition.Left:
                            case TaskBarPosition.Right:
                                bounds = new Rectangle(currentX, currentY, squareSize, squareSize);
                                currentY += squareSize + _spacing;
                                break;
                            default: // Top or Bottom
                                bounds = new Rectangle(currentX, currentY, squareSize, squareSize);
                                currentX += squareSize + _spacing;
                                break;
                        }

                        var moduleLogo = _moduleLogos.ContainsKey(moduleInfo.Name) ? _moduleLogos[moduleInfo.Name] : null;
                        var moduleIcon = new ModuleIcon
                        {
                            Name = moduleInfo.Name,
                            Bounds = bounds,
                            TargetBounds = bounds,
                            IsHovered = false,
                            IsVisible = true,
                            Logo = moduleLogo,
                            IsActive = true,
                            IsAnimating = false,
                            AnimationProgress = 0f,
                            Scale = 1.0f,
                            IsShrinking = false
                        };

                        _moduleIcons.Add(moduleIcon);
                    }
                    catch (Exception ex)
                    {
                        _engine.Log($"TaskBar: ERROR loading module info: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _engine.Log($"TaskBar: ERROR in UpdateModuleIcons: {ex.Message}");
            }
        }

        public void Update()
        {
            _previousMouseState = _currentMouseState;
            _currentMouseState = Mouse.GetState();
            Point currentMousePosition = _currentMouseState.Position;

            // Update highlight timer
            if (_isHighlighted)
            {
                _highlightTimer += (float)GameEngine.Instance.TargetElapsedTime.TotalSeconds;
                if (_highlightTimer >= HIGHLIGHT_DURATION)
                {
                    _isHighlighted = false;
                    _highlightTimer = 0f;
                }
            }

            // Handle mouse down
            if (_currentMouseState.LeftButton == ButtonState.Pressed && 
                _previousMouseState.LeftButton == ButtonState.Released)
            {
                _isMouseDown = true;
                _lastMousePosition = currentMousePosition;

                // Check if we clicked on any icon
                bool clickedOnIcon = false;
                foreach (var icon in _moduleIcons)
                {
                    // Skip icons that are closed (not active, not visible) or shrunk to nothing
                    // But allow minimized icons (not active but still visible)
                    if ((!icon.IsActive && !icon.IsVisible) || icon.Scale <= 0f)
                    {
                        continue;
                    }
                    
                    if (icon.Bounds.Contains(currentMousePosition))
                    {
                        clickedOnIcon = true;
                        _engine.Log($"TaskBar: Click detected on icon: {icon.Name}");
                        
                        // Only highlight if it's the TaskBar's own icon
                        if (icon.Name == "Task Bar")
                        {
                            _isHighlighted = true;
                            _highlightTimer = 0f;
                        }
                        
                        var windowManagement = GetWindowManagementForModule(icon.Name);
                        if (windowManagement != null)
                        {
                            _engine.Log($"TaskBar: Found window management for {icon.Name}");
                            windowManagement.HandleTaskBarClick();
                            
                            // Ensure the window is brought to front after handling the click
                            windowManagement.BringToFront();
                        }
                        break;
                    }
                }

                // If we didn't click on an icon and we're in the TaskBar area, start dragging
                if (!clickedOnIcon && _taskBarBounds.Contains(currentMousePosition))
                {
                    _isDragging = true;
                    _dragStartPosition = new Vector2(currentMousePosition.X, currentMousePosition.Y);
                    _engine.Log("TaskBar: Started dragging TaskBar");
                }
            }

            // Handle mouse up
            if (_currentMouseState.LeftButton == ButtonState.Released && 
                _previousMouseState.LeftButton == ButtonState.Pressed)
            {
                _isMouseDown = false;
                if (_isDragging)
                {
                    _isDragging = false;
                    _engine.Log("TaskBar: Stopped dragging TaskBar");
                }
            }

            // Handle dragging
            if (_isDragging && _isMouseDown)
            {
                // Calculate drag distance
                float dragDistance = Vector2.Distance(
                    _dragStartPosition,
                    new Vector2(currentMousePosition.X, currentMousePosition.Y)
                );

                if (dragDistance > UNSTICK_THRESHOLD)
                {
                    // Calculate drag direction
                    Vector2 dragDirection = new Vector2(
                        currentMousePosition.X - _dragStartPosition.X,
                        currentMousePosition.Y - _dragStartPosition.Y
                    );

                    // Determine new position based on drag direction
                    TaskBarPosition newPosition;
                    if (Math.Abs(dragDirection.X) > Math.Abs(dragDirection.Y))
                    {
                        newPosition = dragDirection.X > 0 ? TaskBarPosition.Right : TaskBarPosition.Left;
                    }
                    else
                    {
                        newPosition = dragDirection.Y > 0 ? TaskBarPosition.Bottom : TaskBarPosition.Top;
                    }

                    // Only update if position changed
                    if (newPosition != _currentPosition)
                    {
                        _currentPosition = newPosition;
                        UpdateTaskBarBounds();
                        UpdateModuleIcons();
                        _engine.Log($"TaskBar: Moved to {_currentPosition} position");
                        
                        // Update drag start position to prevent continuous movement
                        _dragStartPosition = new Vector2(currentMousePosition.X, currentMousePosition.Y);
                    }
                }
            }

            // Update module icons hover state and tooltip
            bool foundHoveredIcon = false;
            foreach (var icon in _moduleIcons)
            {
                // Skip icons that are closed (not active, not visible) or shrunk to nothing
                // But allow minimized icons (not active but still visible)
                if ((!icon.IsActive && !icon.IsVisible) || icon.Scale <= 0f)
                {
                    icon.IsHovered = false; // Ensure inactive icons are not hovered
                    continue;
                }
                
                bool wasHovered = icon.IsHovered;
                icon.IsHovered = icon.Bounds.Contains(currentMousePosition);
                
                if (icon.IsHovered)
                {
                    foundHoveredIcon = true;
                    if (!wasHovered)
                    {
                        _hoverTime = 0;
                        _currentHoveredModule = icon.Name;
                    }
                    else
                    {
                        _hoverTime += (float)GameEngine.Instance.TargetElapsedTime.TotalSeconds;
                    }
                }
            }

            if (!foundHoveredIcon)
            {
                _hoverTime = 0;
                _currentHoveredModule = null;
            }

            _lastMousePosition = currentMousePosition;
            
            // Update icon animations
            UpdateIconAnimations();
        }

        private WindowManagement GetWindowManagementForModule(string moduleName)
        {
            try
            {
                // Get all modules from the game engine
                var modules = GameEngine.Instance.GetActiveModules();
                _engine.Log($"TaskBar: Searching for window management for {moduleName} among {modules.Count} modules");

                foreach (var module in modules)
                {
                    // Use reflection to get the WindowManagement instance
                    var windowManagementField = module.GetType().GetField("_windowManagement", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (windowManagementField != null)
                    {
                        var windowManagement = windowManagementField.GetValue(module) as WindowManagement;
                        if (windowManagement != null)
                        {
                            var windowTitle = windowManagement.GetWindowTitle();
                            _engine.Log($"TaskBar: Found window management with title: {windowTitle}");
                            
                            if (windowTitle == moduleName)
                            {
                                _engine.Log($"TaskBar: Found matching window management for {moduleName}");
                                return windowManagement;
                            }
                        }
                    }
                }
                
                _engine.Log($"TaskBar: No window management found for {moduleName}");
                return null;
            }
            catch (Exception ex)
            {
                _engine.Log($"TaskBar: Error finding window management: {ex.Message}");
                return null;
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            try
            {
                // Draw task bar background
                spriteBatch.Draw(_pixel, _taskBarBounds, new Color(40, 40, 40));

                // Draw module icons
                foreach (var icon in _moduleIcons)
                {
                    // Skip drawing if icon is closed (not active, not visible) or shrunk to nothing
                    // But allow minimized icons (not active but still visible)
                    if ((!icon.IsActive && !icon.IsVisible) || icon.Scale <= 0f)
                    {
                        continue;
                    }
                    
                    // Calculate scaled bounds for shrinking animation
                    Rectangle drawBounds = icon.Bounds;
                    if (icon.IsShrinking && icon.Scale < 1.0f)
                    {
                        // Calculate center point for scaling
                        Vector2 center = new Vector2(
                            icon.Bounds.X + icon.Bounds.Width / 2f,
                            icon.Bounds.Y + icon.Bounds.Height / 2f
                        );
                        
                        // Calculate scaled size
                        int scaledWidth = (int)(icon.Bounds.Width * icon.Scale);
                        int scaledHeight = (int)(icon.Bounds.Height * icon.Scale);
                        
                        // Calculate scaled position to keep center point
                        int scaledX = (int)(center.X - scaledWidth / 2f);
                        int scaledY = (int)(center.Y - scaledHeight / 2f);
                        
                        drawBounds = new Rectangle(scaledX, scaledY, scaledWidth, scaledHeight);
                    }
                    
                    // Draw icon background
                    Color iconColor = icon.IsHovered ? new Color(70, 70, 70) : new Color(50, 50, 50);
                    spriteBatch.Draw(_pixel, drawBounds, iconColor);

                    // Draw active indicator if this module is active
                    if (icon.IsActive)
                    {
                        Rectangle indicatorBounds;
                        int centerOffset = (drawBounds.Height - ACTIVE_INDICATOR_LENGTH) / 2;
                        switch (_currentPosition)
                        {
                            case TaskBarPosition.Left:
                                indicatorBounds = new Rectangle(drawBounds.X, drawBounds.Y + centerOffset, ACTIVE_INDICATOR_WIDTH, ACTIVE_INDICATOR_LENGTH);
                                break;
                            case TaskBarPosition.Right:
                                indicatorBounds = new Rectangle(drawBounds.Right - ACTIVE_INDICATOR_WIDTH, drawBounds.Y + centerOffset, ACTIVE_INDICATOR_WIDTH, ACTIVE_INDICATOR_LENGTH);
                                break;
                            case TaskBarPosition.Top:
                                centerOffset = (drawBounds.Width - ACTIVE_INDICATOR_LENGTH) / 2;
                                indicatorBounds = new Rectangle(drawBounds.X + centerOffset, drawBounds.Y, ACTIVE_INDICATOR_LENGTH, ACTIVE_INDICATOR_WIDTH);
                                break;
                            default: // Bottom
                                centerOffset = (drawBounds.Width - ACTIVE_INDICATOR_LENGTH) / 2;
                                indicatorBounds = new Rectangle(drawBounds.X + centerOffset, drawBounds.Bottom - ACTIVE_INDICATOR_WIDTH, ACTIVE_INDICATOR_LENGTH, ACTIVE_INDICATOR_WIDTH);
                                break;
                        }
                        spriteBatch.Draw(_pixel, indicatorBounds, ACTIVE_INDICATOR_COLOR);
                    }

                    // Draw separator dots only for TaskBar icon
                    if (icon.Name == "Task Bar")
                    {
                        // Draw separator dots
                        int dotX, dotY;
                        switch (_currentPosition)
                        {
                            case TaskBarPosition.Left:
                            case TaskBarPosition.Right:
                                // Calculate total width of dots and spacing
                                int totalDotsWidth = (3 * DOT_SIZE) + (2 * DOT_SPACING);
                                dotX = drawBounds.X + (drawBounds.Width - totalDotsWidth) / 2;
                                dotY = drawBounds.Bottom + DOT_SEPARATOR_SPACING;
                                for (int i = 0; i < 3; i++)
                                {
                                    // Draw circular dot
                                    int centerX = dotX + (i * (DOT_SIZE + DOT_SPACING)) + (DOT_SIZE / 2);
                                    int centerY = dotY + (DOT_SIZE / 2);
                                    for (int dx = -DOT_SIZE/2; dx <= DOT_SIZE/2; dx++)
                                    {
                                        for (int dy = -DOT_SIZE/2; dy <= DOT_SIZE/2; dy++)
                                        {
                                            if (dx*dx + dy*dy <= (DOT_SIZE/2)*(DOT_SIZE/2))
                                            {
                                                spriteBatch.Draw(_pixel, 
                                                    new Rectangle(centerX + dx, centerY + dy, 1, 1), 
                                                    ACTIVE_INDICATOR_COLOR);
                                            }
                                        }
                                    }
                                }
                                break;
                            default: // Top or Bottom
                                dotX = drawBounds.Right + DOT_SEPARATOR_SPACING;
                                // Calculate total height of dots and spacing
                                int totalDotsHeight = (3 * DOT_SIZE) + (2 * DOT_SPACING);
                                dotY = drawBounds.Y + (drawBounds.Height - totalDotsHeight) / 2;
                                for (int i = 0; i < 3; i++)
                                {
                                    // Draw circular dot
                                    int centerX = dotX + (DOT_SIZE / 2);
                                    int centerY = dotY + (i * (DOT_SIZE + DOT_SPACING)) + (DOT_SIZE / 2);
                                    for (int dx = -DOT_SIZE/2; dx <= DOT_SIZE/2; dx++)
                                    {
                                        for (int dy = -DOT_SIZE/2; dy <= DOT_SIZE/2; dy++)
                                        {
                                            if (dx*dx + dy*dy <= (DOT_SIZE/2)*(DOT_SIZE/2))
                                            {
                                                spriteBatch.Draw(_pixel, 
                                                    new Rectangle(centerX + dx, centerY + dy, 1, 1), 
                                                    ACTIVE_INDICATOR_COLOR);
                                            }
                                        }
                                    }
                                }
                                break;
                        }
                    }

                    // Draw logo if available
                    if (icon.Logo != null)
                    {
                        try
                        {
                            spriteBatch.Draw(icon.Logo, drawBounds, Color.White);
                        }
                        catch (Exception ex)
                        {
                            _engine.Log($"TaskBar: Error drawing logo for {icon.Name}: {ex.Message}");
                            DrawModuleName(spriteBatch, icon, drawBounds);
                        }
                    }
                    else
                    {
                        DrawModuleName(spriteBatch, icon, drawBounds);
                    }
                }

                // Draw highlight if active (after module icons to ensure it's on top)
                if (_isHighlighted)
                {
                    // Calculate blink effect
                    float blinkAlpha = (float)(Math.Sin(_highlightTimer * Math.PI * 2 / HIGHLIGHT_BLINK_SPEED) + 1) / 2;
                    Color highlightColor = new Color(ACTIVE_INDICATOR_COLOR, (byte)(blinkAlpha * 255));

                    // Draw highlight borders (1 pixel width) on the inside of the taskbar
                    // Top border
                    spriteBatch.Draw(_pixel, new Rectangle(_taskBarBounds.X, _taskBarBounds.Y, _taskBarBounds.Width, 1), highlightColor);
                    // Bottom border
                    spriteBatch.Draw(_pixel, new Rectangle(_taskBarBounds.X, _taskBarBounds.Bottom - 1, _taskBarBounds.Width, 1), highlightColor);
                    // Left border
                    spriteBatch.Draw(_pixel, new Rectangle(_taskBarBounds.X, _taskBarBounds.Y, 1, _taskBarBounds.Height), highlightColor);
                    // Right border
                    spriteBatch.Draw(_pixel, new Rectangle(_taskBarBounds.Right - 1, _taskBarBounds.Y, 1, _taskBarBounds.Height), highlightColor);
                }

                // Draw tooltip if hovering long enough
                if (_currentHoveredModule != null && _hoverTime >= TOOLTIP_DELAY)
                {
                    Vector2 textSize = _menuFont.MeasureString(_currentHoveredModule);
                    Rectangle tooltipBounds = new Rectangle(
                        (int)_currentMouseState.Position.X + TOOLTIP_OFFSET,
                        (int)_currentMouseState.Position.Y + TOOLTIP_OFFSET,
                        (int)textSize.X + (TOOLTIP_PADDING * 2),
                        (int)textSize.Y + (TOOLTIP_PADDING * 2)
                    );

                    // Draw tooltip background
                    spriteBatch.Draw(_pixel, tooltipBounds, new Color(60, 60, 60));
                    
                    // Draw tooltip border
                    Rectangle borderRect = new Rectangle(
                        tooltipBounds.X - 1,
                        tooltipBounds.Y - 1,
                        tooltipBounds.Width + 2,
                        tooltipBounds.Height + 2
                    );
                    spriteBatch.Draw(_pixel, borderRect, new Color(100, 100, 100));

                    // Draw tooltip text
                    Vector2 textPosition = new Vector2(
                        tooltipBounds.X + TOOLTIP_PADDING,
                        tooltipBounds.Y + TOOLTIP_PADDING
                    );
                    spriteBatch.DrawString(_menuFont, _currentHoveredModule, textPosition, Color.White);
                }
            }
            catch (Exception ex)
            {
                _engine.Log($"TaskBar: ERROR in Draw: {ex.Message}");
            }
        }

        private void DrawModuleName(SpriteBatch spriteBatch, ModuleIcon icon, Rectangle drawBounds)
        {
            // Calculate text position to be centered in the icon bounds
            Vector2 textSize = _menuFont.MeasureString(icon.Name);
            float textX = drawBounds.X + (drawBounds.Width - textSize.X) / 2;
            float textY = drawBounds.Y + (drawBounds.Height - textSize.Y) / 2;

            // Draw text with a slight shadow for better visibility
            Vector2 shadowOffset = new Vector2(1, 1);
            spriteBatch.DrawString(_menuFont, icon.Name, new Vector2(textX + shadowOffset.X, textY + shadowOffset.Y), Color.Black);
            spriteBatch.DrawString(_menuFont, icon.Name, new Vector2(textX, textY), Color.White);
        }

        public void UpdateWindowWidth(int newWidth)
        {
            _windowWidth = newWidth;
            _windowHeight = _graphicsDevice.Viewport.Height;
            UpdateTaskBarBounds();
            UpdateModuleIcons();
        }

        public void LoadContent(ContentManager content)
        {
            try
            {
                _engine.Log("TaskBar: Starting LoadContent");
                
                // First ensure we have all module icons created
                UpdateModuleIcons();
                
                // Load TaskBar logo
                try
                {
                    _logo = content.Load<Texture2D>("Modules/TaskBar_essential/logo");
                    _moduleLogos["Task Bar"] = _logo;
                }
                catch (Exception ex)
                {
                    _engine.Log($"TaskBar: ERROR loading TaskBar logo: {ex.Message}");
                }

                // Load module logos
                foreach (var icon in _moduleIcons)
                {
                    if (icon.Name != "Task Bar")
                    {
                        try
                        {
                            string moduleName = icon.Name.Replace(" ", "") + "_essential";
                            string logoPath = $"Modules/{moduleName}/logo";
                            
                            try
                            {
                                var logo = content.Load<Texture2D>(logoPath);
                                _moduleLogos[icon.Name] = logo;
                                icon.Logo = logo;
                            }
                            catch (ContentLoadException ex)
                            {
                                _engine.Log($"TaskBar: Content not found for {icon.Name} at path: {logoPath}");
                            }
                        }
                        catch (Exception ex)
                        {
                            _engine.Log($"TaskBar: Error loading logo for {icon.Name}: {ex.Message}");
                        }
                    }
                    else
                    {
                        icon.Logo = _logo;
                    }
                }
                
                // Update module icons one final time to ensure all logos are properly set
                UpdateModuleIcons();
            }
            catch (Exception ex)
            {
                _engine.Log($"TaskBar: ERROR in LoadContent: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _pixel?.Dispose();
        }

        // Add method to set module active state
        public void SetModuleActive(string moduleName, bool isActive)
        {
            try
            {
                var module = _moduleIcons.FirstOrDefault(m => m.Name == moduleName);
                if (module != null)
                {
                    if (!isActive && module.IsActive)
                    {
                        // Starting shrink animation for closing
                        StartIconShrinkAnimation(module);
                    }
                    else if (isActive && !module.IsActive)
                    {
                        // Restoring icon
                        module.IsActive = true;
                        module.IsShrinking = false;
                        module.Scale = 1.0f;
                        module.IsAnimating = false;
                        module.AnimationProgress = 0f;
                        _engine.Log($"TaskBar: Restored {moduleName} icon");
                    }
                    else
                    {
                        module.IsActive = isActive;
                        _engine.Log($"TaskBar: Set {moduleName} active state to {isActive}");
                    }
                }
                else
                {
                    _engine.Log($"TaskBar: Could not find module {moduleName} to set active state");
                }
            }
            catch (Exception ex)
            {
                _engine.Log($"TaskBar: Error setting module active state: {ex.Message}");
            }
        }

        public Vector2 GetModuleIconPosition(string moduleName)
        {
            var module = _moduleIcons.FirstOrDefault(m => m.Name == moduleName);
            if (module != null)
            {
                return new Vector2(module.Bounds.X, module.Bounds.Y);
            }
            return Vector2.Zero;
        }

        public void StoreMinimizeState(string moduleName, Vector2 position, Vector2 size)
        {
            try
            {
                var module = _moduleIcons.FirstOrDefault(m => m.Name == moduleName);
                if (module != null)
                {
                    module.PreMinimizePosition = position;
                    module.PreMinimizeSize = size;
                    _engine.Log($"TaskBar: Stored minimize state for {moduleName} - Position: {position}, Size: {size}");
                }
                else
                {
                    _engine.Log($"TaskBar: Could not find module {moduleName} to store minimize state");
                }
            }
            catch (Exception ex)
            {
                _engine.Log($"TaskBar: Error storing minimize state: {ex.Message}");
            }
        }

        public Vector2? GetPreMinimizePosition(string moduleName)
        {
            try
            {
                var module = _moduleIcons.FirstOrDefault(m => m.Name == moduleName);
                if (module != null && module.PreMinimizePosition.HasValue)
                {
                    _engine.Log($"TaskBar: Retrieved pre-minimize position for {moduleName}: {module.PreMinimizePosition}");
                    return module.PreMinimizePosition;
                }
                _engine.Log($"TaskBar: No pre-minimize position found for {moduleName}");
                return null;
            }
            catch (Exception ex)
            {
                _engine.Log($"TaskBar: Error getting pre-minimize position: {ex.Message}");
                return null;
            }
        }

        public Vector2? GetPreMinimizeSize(string moduleName)
        {
            try
            {
                var module = _moduleIcons.FirstOrDefault(m => m.Name == moduleName);
                if (module != null && module.PreMinimizeSize.HasValue)
                {
                    _engine.Log($"TaskBar: Retrieved pre-minimize size for {moduleName}: {module.PreMinimizeSize}");
                    return module.PreMinimizeSize;
                }
                _engine.Log($"TaskBar: No pre-minimize size found for {moduleName}");
                return null;
            }
            catch (Exception ex)
            {
                _engine.Log($"TaskBar: Error getting pre-minimize size: {ex.Message}");
                return null;
            }
        }

        public bool IsDragging()
        {
            return _isDragging;
        }

        public bool IsMouseOver()
        {
            return _isMouseOverTaskBar;
        }

        private void StartIconShrinkAnimation(ModuleIcon icon)
        {
            try
            {
                _engine.Log($"TaskBar: Starting shrink animation for {icon.Name}");
                
                // Store current bounds as target for other icons
                icon.TargetBounds = icon.Bounds;
                
                // Start shrink animation
                icon.IsShrinking = true;
                icon.IsAnimating = true;
                icon.AnimationProgress = 0f;
                icon.Scale = 1.0f;
                
                // Calculate target positions for all icons that come after this one
                CalculateIconRepositioning(icon);
                
                _engine.Log($"TaskBar: Shrink animation started for {icon.Name}");
            }
            catch (Exception ex)
            {
                _engine.Log($"TaskBar: Error starting shrink animation: {ex.Message}");
            }
        }

        // Add method to set module minimized state (different from active state)
        public void SetModuleMinimized(string moduleName, bool isMinimized)
        {
            try
            {
                var module = _moduleIcons.FirstOrDefault(m => m.Name == moduleName);
                if (module != null)
                {
                    // For minimize, we keep the icon visible but remove the active indicator
                    // The icon stays in place, only the active state changes
                    module.IsActive = !isMinimized; // Active = not minimized
                    _engine.Log($"TaskBar: Set {moduleName} minimized state to {isMinimized} (active: {!isMinimized})");
                }
                else
                {
                    _engine.Log($"TaskBar: Could not find module {moduleName} to set minimized state");
                }
            }
            catch (Exception ex)
            {
                _engine.Log($"TaskBar: Error setting module minimized state: {ex.Message}");
            }
        }

        private void CalculateIconRepositioning(ModuleIcon closingIcon)
        {
            try
            {
                // Find the index of the closing icon
                int closingIndex = _moduleIcons.IndexOf(closingIcon);
                if (closingIndex == -1) return;

                // Calculate the space that will be freed up
                int freedSpace = TASK_BAR_SIZE + _spacing; // Icon size + spacing

                // Update target positions for all icons that come after the closing icon
                for (int i = closingIndex + 1; i < _moduleIcons.Count; i++)
                {
                    var icon = _moduleIcons[i];
                    // Animate icons that are either active or visible (minimized windows are visible but not active)
                    if ((icon.IsActive || icon.IsVisible) && !icon.IsShrinking)
                    {
                        // Calculate new target position
                        Rectangle newTargetBounds;
                        switch (_currentPosition)
                        {
                            case TaskBarPosition.Left:
                            case TaskBarPosition.Right:
                                newTargetBounds = new Rectangle(
                                    icon.Bounds.X,
                                    icon.Bounds.Y - freedSpace,
                                    icon.Bounds.Width,
                                    icon.Bounds.Height
                                );
                                break;
                            default: // Top or Bottom
                                newTargetBounds = new Rectangle(
                                    icon.Bounds.X - freedSpace,
                                    icon.Bounds.Y,
                                    icon.Bounds.Width,
                                    icon.Bounds.Height
                                );
                                break;
                        }
                        
                        icon.TargetBounds = newTargetBounds;
                        icon.IsAnimating = true;
                        icon.AnimationProgress = 0f;
                        
                        _engine.Log($"TaskBar: Set target position for {icon.Name} to {newTargetBounds} (Active: {icon.IsActive}, Visible: {icon.IsVisible})");
                    }
                }
            }
            catch (Exception ex)
            {
                _engine.Log($"TaskBar: Error calculating icon repositioning: {ex.Message}");
            }
        }

        private void UpdateIconAnimations()
        {
            try
            {
                bool anyAnimating = false;
                
                foreach (var icon in _moduleIcons)
                {
                    if (icon.IsAnimating)
                    {
                        anyAnimating = true;
                        
                        if (icon.IsShrinking)
                        {
                            // Update shrink animation
                            icon.AnimationProgress += ICON_SHRINK_SPEED;
                            if (icon.AnimationProgress >= 1.0f)
                            {
                                // Shrink animation complete
                                icon.AnimationProgress = 1.0f;
                                icon.IsAnimating = false;
                                icon.IsShrinking = false;
                                icon.IsActive = false; // Now actually deactivate
                                icon.Scale = 0f;
                                _engine.Log($"TaskBar: Shrink animation completed for {icon.Name}");
                            }
                            else
                            {
                                // Update scale
                                icon.Scale = 1.0f - icon.AnimationProgress;
                            }
                        }
                        else
                        {
                            // Update position animation
                            icon.AnimationProgress += ICON_ANIMATION_SPEED;
                            if (icon.AnimationProgress >= 1.0f)
                            {
                                // Position animation complete
                                icon.AnimationProgress = 1.0f;
                                icon.IsAnimating = false;
                                icon.Bounds = icon.TargetBounds;
                                _engine.Log($"TaskBar: Position animation completed for {icon.Name}");
                            }
                            else
                            {
                                // Interpolate position
                                Rectangle oldBounds = icon.Bounds;
                                icon.Bounds = new Rectangle(
                                    (int)MathHelper.Lerp(icon.Bounds.X, icon.TargetBounds.X, icon.AnimationProgress),
                                    (int)MathHelper.Lerp(icon.Bounds.Y, icon.TargetBounds.Y, icon.AnimationProgress),
                                    icon.Bounds.Width,
                                    icon.Bounds.Height
                                );
                                

                            }
                        }
                    }
                }
                
                if (!anyAnimating)
                {
                    _engine.Log("TaskBar: All icon animations completed");
                }
            }
            catch (Exception ex)
            {
                _engine.Log($"TaskBar: Error updating icon animations: {ex.Message}");
            }
        }
    }
} 