using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using MarySGameEngine;

namespace MarySGameEngine.Modules.NotificationCenter_essential
{
    public class NotificationCenter : IModule
    {
        /// <summary>Static instance for push notifications (e.g. from GameManager when workspace changes).</summary>
        private static NotificationCenter _instance;

        private GraphicsDevice _graphicsDevice;
        private SpriteFont _menuFont;
        private SpriteFont _dropdownFont;
        private SpriteFont _notificationFont; // Font for notifications (same as FlashMessage)
        private int _windowWidth;
        private GameEngine _engine;
        private Texture2D _pixel;
        private Texture2D _logo;
        // Notification bell icons (read state and unread counts 1-5, 5+)
        private Texture2D _iconNotificationBell;
        private Texture2D _iconNew1;
        private Texture2D _iconNew2;
        private Texture2D _iconNew3;
        private Texture2D _iconNew4;
        private Texture2D _iconNew5;
        private Texture2D _iconNew5Plus;
        private MouseState _currentMouseState;
        private MouseState _previousMouseState;
        private bool _isHoveringOverInteractive = false;

        // Notification properties
        private class Notification
        {
            [JsonPropertyName("message")]
            public string Message { get; set; }
            
            [JsonPropertyName("timestamp")]
            public DateTime Timestamp { get; set; }
            
            [JsonPropertyName("type")]
            public string Type { get; set; } // "workspace", "system", etc.
        }

        private class NotificationsData
        {
            [JsonPropertyName("notifications")]
            public List<Notification> Notifications { get; set; } = new List<Notification>();
            [JsonPropertyName("lastReadNotificationCount")]
            public int LastReadNotificationCount { get; set; }
        }

        private List<Notification> _notifications = new List<Notification>();
        private const int MAX_NOTIFICATIONS = 30; // Keep last 30 notifications
        private string _notificationsFilePath;
        /// <summary>Number of notifications considered "read" (user opened the dropdown at that count).</summary>
        private int _lastReadNotificationCount = 0;
        /// <summary>When dropdown was opened, how many items were unread (show red dot on that many top items until closed).</summary>
        private int _unreadCountWhenOpened = 0;

        // TopBar integration
        private Rectangle _iconBounds;
        private const int ICON_SIZE = 46;   // Larger so the bell is clearly visible (matches TopBar)
        private const int ICON_PADDING = 6;
        private bool _isIconHovered = false;

        // Dropdown properties
        private bool _isDropdownOpen = false;
        private Rectangle _dropdownBounds;
        private int _hoveredNotificationIndex = -1;
        private int _scrollOffset = 0;
        private const int MAX_VISIBLE_ITEMS = 6; // Show 6 notifications visible
        private const int ITEM_HEIGHT = 50;
        private const int DROPDOWN_WIDTH = 650;
        private Rectangle _scrollBarBounds;
        private bool _isDraggingScroll = false;
        private Vector2 _scrollDragStart;
        private const int SCROLLBAR_WIDTH = 16;

        // Colors (matching TopBar style)
        private readonly Color MIAMI_BACKGROUND = new Color(40, 40, 40);
        private readonly Color MIAMI_BORDER = new Color(147, 112, 219);
        private readonly Color MIAMI_PURPLE = new Color(147, 112, 219); // Main purple color
        private readonly Color MIAMI_PURPLE_LIGHT = new Color(180, 145, 250); // Lighter purple
        private readonly Color MIAMI_HOVER = new Color(147, 112, 219, 180);
        private readonly Color MIAMI_TEXT = new Color(220, 220, 220);
        private readonly Color MIAMI_SHADOW = new Color(0, 0, 0, 100);
        private readonly Color NOTIFICATION_BACKGROUND = new Color(50, 50, 50);
        private readonly Color NOTIFICATION_HOVER = new Color(60, 60, 60);
        private readonly Color PINK_COLOR = new Color(255, 192, 203); // Pink color for workspace names

        // Previous workspace tracking
        private string _previousWorkspaceText = "";

        public NotificationCenter(GraphicsDevice graphicsDevice, SpriteFont menuFont, SpriteFont dropdownFont, int windowWidth)
        {
            _graphicsDevice = graphicsDevice;
            _menuFont = menuFont;
            _dropdownFont = dropdownFont;
            _windowWidth = windowWidth;
            _engine = (GameEngine)GameEngine.Instance;

            // Create 1x1 white pixel texture
            _pixel = new Texture2D(graphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });

            // Initialize icon bounds (will be set by TopBar)
            _iconBounds = new Rectangle(_windowWidth - ICON_SIZE - ICON_PADDING, ICON_PADDING, ICON_SIZE, ICON_SIZE);

            // Set static instance for push notifications
            _instance = this;

            // Set notifications file path
            string dataDir = Path.Combine(Directory.GetCurrentDirectory(), "Data");
            if (!Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
            }
            _notificationsFilePath = Path.Combine(dataDir, "notifications.json");

            // Load notifications from file
            LoadNotifications();
        }

        public void Update()
        {
            _previousMouseState = _currentMouseState;
            _currentMouseState = Mouse.GetState();

            // Track if we're hovering over any interactive element
            bool wasHoveringOverInteractive = _isHoveringOverInteractive;
            _isHoveringOverInteractive = false;

            // Check for workspace changes
            CheckWorkspaceChanges();

            // Update icon hover state
            _isIconHovered = _iconBounds.Contains(_currentMouseState.Position);
            if (_isIconHovered)
            {
                _isHoveringOverInteractive = true;
            }

            // Handle dropdown
            if (_isDropdownOpen)
            {
                HandleDropdown();
            }

            // Update cursor based on hover state
            UpdateCursor(wasHoveringOverInteractive);
        }

        private void CheckWorkspaceChanges()
        {
            try
            {
                string currentWorkspaceText = GetActiveWorkspaceText();
                
                // Only trigger notification if workspace actually changed (not just initialized)
                if (!string.IsNullOrEmpty(_previousWorkspaceText) &&
                    !string.IsNullOrEmpty(currentWorkspaceText) && 
                    currentWorkspaceText != _previousWorkspaceText &&
                    currentWorkspaceText != "No workspace chosen" &&
                    _previousWorkspaceText != "No workspace chosen")
                {
                    // Workspace changed, add notification
                    AddNotification($"Workspace changed to {currentWorkspaceText}", "workspace");
                }
                
                // Update previous workspace text
                _previousWorkspaceText = currentWorkspaceText;
            }
            catch (Exception ex)
            {
                _engine.Log($"NotificationCenter: Error checking workspace changes: {ex.Message}");
            }
        }

        private string GetActiveWorkspaceText()
        {
            try
            {
                var modules = GameEngine.Instance.GetActiveModules();
                foreach (var module in modules)
                {
                    if (module is MarySGameEngine.Modules.GameManager_essential.GameManager gameManager)
                    {
                        var activeWorkspacePathField = gameManager.GetType().GetField("_activeWorkspacePath",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        
                        if (activeWorkspacePathField != null)
                        {
                            string activeWorkspacePath = activeWorkspacePathField.GetValue(gameManager) as string ?? "";
                            
                            if (!string.IsNullOrEmpty(activeWorkspacePath))
                            {
                                var projectsField = gameManager.GetType().GetField("_projects",
                                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                
                                if (projectsField != null)
                                {
                                    var projects = projectsField.GetValue(gameManager) as System.Collections.Generic.List<MarySGameEngine.Modules.GameManager_essential.GameProject>;
                                    
                                    if (projects != null)
                                    {
                                        foreach (var project in projects)
                                        {
                                            string projectPath = !string.IsNullOrEmpty(project.Path) 
                                                ? project.Path 
                                                : System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Projects", project.Name);
                                            
                                            string normalizedProjectPath = System.IO.Path.GetFullPath(projectPath);
                                            string normalizedActivePath = System.IO.Path.GetFullPath(activeWorkspacePath);
                                            
                                            if (normalizedProjectPath == normalizedActivePath)
                                            {
                                                string gameType = !string.IsNullOrEmpty(project.Genre) ? project.Genre : "Unknown";
                                                return $"{project.Name} ({gameType})";
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _engine.Log($"NotificationCenter: Error getting active workspace: {ex.Message}");
            }
            
            return "No workspace chosen";
        }

        private void AddNotification(string message, string type)
        {
            var notification = new Notification
            {
                Message = message,
                Timestamp = DateTime.Now,
                Type = type
            };

            _notifications.Insert(0, notification); // Add to beginning

            // Limit to max notifications (keep only last 15, remove older ones)
            if (_notifications.Count > MAX_NOTIFICATIONS)
            {
                _notifications.RemoveRange(MAX_NOTIFICATIONS, _notifications.Count - MAX_NOTIFICATIONS);
            }

            // Save to file
            SaveNotifications();

            _engine.Log($"NotificationCenter: Added notification: {message}");
        }

        /// <summary>Call from GameManager (or elsewhere) when workspace changes so the notification appears immediately.</summary>
        public static void NotifyWorkspaceChanged(string workspaceText)
        {
            if (string.IsNullOrEmpty(workspaceText) || workspaceText == "No workspace chosen")
                return;
            _instance?.AddNotification($"Workspace changed to {workspaceText}", "workspace");
            _instance?.SetPreviousWorkspaceText(workspaceText);
        }

        /// <summary>Keeps polling in sync so we don't add a duplicate when CheckWorkspaceChanges runs later in the same frame.</summary>
        private void SetPreviousWorkspaceText(string workspaceText)
        {
            _previousWorkspaceText = workspaceText ?? "";
        }

        private void LoadNotifications()
        {
            try
            {
                if (File.Exists(_notificationsFilePath))
                {
                    string jsonContent = File.ReadAllText(_notificationsFilePath);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip
                    };
                    var data = JsonSerializer.Deserialize<NotificationsData>(jsonContent, options);
                    if (data != null && data.Notifications != null)
                    {
                        _notifications = data.Notifications;
                        _lastReadNotificationCount = data.LastReadNotificationCount;
                        // Ensure we only have max notifications
                        if (_notifications.Count > MAX_NOTIFICATIONS)
                        {
                            _notifications = _notifications.Take(MAX_NOTIFICATIONS).ToList();
                        }
                        _engine.Log($"NotificationCenter: Loaded {_notifications.Count} notifications from file");
                    }
                }
            }
            catch (Exception ex)
            {
                _engine.Log($"NotificationCenter: Error loading notifications: {ex.Message}");
                _notifications = new List<Notification>();
            }
        }

        private void SaveNotifications()
        {
            try
            {
                var data = new NotificationsData
                {
                    Notifications = _notifications,
                    LastReadNotificationCount = _lastReadNotificationCount
                };
                
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                
                string jsonContent = JsonSerializer.Serialize(data, options);
                File.WriteAllText(_notificationsFilePath, jsonContent);
                _engine.Log($"NotificationCenter: Saved {_notifications.Count} notifications to file");
            }
            catch (Exception ex)
            {
                _engine.Log($"NotificationCenter: Error saving notifications: {ex.Message}");
            }
        }

        private void HandleDropdown()
        {
            var mousePos = _currentMouseState.Position;
            bool leftPressed = _currentMouseState.LeftButton == ButtonState.Pressed;
            bool leftJustPressed = leftPressed && _previousMouseState.LeftButton == ButtonState.Released;

            if (leftJustPressed)
            {
                // Check if clicked on scrollbar
                if (!_scrollBarBounds.IsEmpty && _scrollBarBounds.Contains(mousePos))
                {
                    _isDraggingScroll = true;
                    _scrollDragStart = new Vector2(mousePos.X, mousePos.Y);
                    return;
                }

                // Check if clicked on notification item
                int visibleItems = Math.Min(_notifications.Count, MAX_VISIBLE_ITEMS);
                for (int i = 0; i < visibleItems; i++)
                {
                    int actualIndex = i + _scrollOffset;
                    if (actualIndex >= _notifications.Count) break;

                    Rectangle itemRect = new Rectangle(
                        _dropdownBounds.X,
                        _dropdownBounds.Y + (i * ITEM_HEIGHT),
                        _dropdownBounds.Width - (_scrollBarBounds.IsEmpty ? 0 : SCROLLBAR_WIDTH),
                        ITEM_HEIGHT
                    );

                    if (itemRect.Contains(mousePos))
                    {
                        // Notification clicked (could add action here)
                        _engine.Log($"NotificationCenter: Clicked notification: {_notifications[actualIndex].Message}");
                        break;
                    }
                }

                // If clicked outside dropdown, close it
                if (!_dropdownBounds.Contains(mousePos) && !_iconBounds.Contains(mousePos))
                {
                    CloseDropdown();
                }
            }
            else if (leftPressed && _isDraggingScroll)
            {
                // Handle scrollbar dragging
                float deltaY = mousePos.Y - _scrollDragStart.Y;
                int maxScroll = _notifications.Count - MAX_VISIBLE_ITEMS;
                float scrollBarHeight = _scrollBarBounds.Height;

                // Calculate new scroll offset based on mouse position relative to scrollbar
                float mouseRatio = (mousePos.Y - _scrollBarBounds.Y) / scrollBarHeight;
                mouseRatio = Math.Max(0, Math.Min(1, mouseRatio));

                _scrollOffset = (int)(mouseRatio * maxScroll);
                _scrollDragStart = new Vector2(mousePos.X, mousePos.Y);
            }
            else if (!leftPressed && _isDraggingScroll)
            {
                _isDraggingScroll = false;
            }
            else
            {
                // Update hover state for dropdown items
                _hoveredNotificationIndex = -1;
                int visibleItems = Math.Min(_notifications.Count, MAX_VISIBLE_ITEMS);
                for (int i = 0; i < visibleItems; i++)
                {
                    int actualIndex = i + _scrollOffset;
                    if (actualIndex >= _notifications.Count) break;

                    Rectangle itemRect = new Rectangle(
                        _dropdownBounds.X,
                        _dropdownBounds.Y + (i * ITEM_HEIGHT),
                        _dropdownBounds.Width - (_scrollBarBounds.IsEmpty ? 0 : SCROLLBAR_WIDTH),
                        ITEM_HEIGHT
                    );

                    if (itemRect.Contains(mousePos))
                    {
                        _hoveredNotificationIndex = actualIndex;
                        _isHoveringOverInteractive = true;
                        break;
                    }
                }
            }

            // Handle mouse wheel scrolling when dropdown is open
            if (_notifications.Count > MAX_VISIBLE_ITEMS)
            {
                int scrollDelta = _currentMouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
                if (scrollDelta != 0 && _dropdownBounds.Contains(mousePos))
                {
                    int scrollStep = scrollDelta > 0 ? -1 : 1;
                    int maxScroll = Math.Max(0, _notifications.Count - MAX_VISIBLE_ITEMS);
                    _scrollOffset = Math.Max(0, Math.Min(maxScroll, _scrollOffset + scrollStep));
                }
            }
        }

        private void UpdateDropdownBounds()
        {
            // Calculate dropdown position (above the icon, aligned to right)
            // Always show 4 notifications at a time (or fewer if less available)
            int visibleItems = Math.Min(_notifications.Count, MAX_VISIBLE_ITEMS);
            int dropdownHeight = visibleItems * ITEM_HEIGHT;
            
            // If no notifications, show a small empty dropdown
            if (_notifications.Count == 0)
            {
                dropdownHeight = ITEM_HEIGHT * 2; // Show empty state with some height
            }

            int dropdownX = _windowWidth - DROPDOWN_WIDTH - ICON_PADDING;
            int dropdownY = _iconBounds.Bottom + ICON_PADDING;

            // Check if dropdown would go off screen, position above icon instead
            if (dropdownY + dropdownHeight > _graphicsDevice.Viewport.Height)
            {
                dropdownY = _iconBounds.Y - dropdownHeight - ICON_PADDING;
            }

            _dropdownBounds = new Rectangle(dropdownX, dropdownY, DROPDOWN_WIDTH, dropdownHeight);
            UpdateScrollBar();
        }

        private void UpdateScrollBar()
        {
            int visibleItems = Math.Min(_notifications.Count, MAX_VISIBLE_ITEMS);
            if (_notifications.Count > visibleItems)
            {
                int scrollBarWidth = SCROLLBAR_WIDTH;
                _scrollBarBounds = new Rectangle(
                    _dropdownBounds.Right - scrollBarWidth,
                    _dropdownBounds.Y,
                    scrollBarWidth,
                    _dropdownBounds.Height
                );
            }
            else
            {
                _scrollBarBounds = Rectangle.Empty;
            }
        }

        private void CloseDropdown()
        {
            _isDropdownOpen = false;
            _scrollOffset = 0;
            _unreadCountWhenOpened = 0; // Next time we open, don't show red dots for already-read items
        }

        private string FormatNotificationDateTime(DateTime timestamp)
        {
            DateTime now = DateTime.Now;
            DateTime today = now.Date;
            DateTime yesterday = today.AddDays(-1);
            
            DateTime notificationDate = timestamp.Date;
            
            if (notificationDate == today)
            {
                // Today: show time only
                return $"Today, {timestamp:HH:mm}";
            }
            else if (notificationDate == yesterday)
            {
                // Yesterday: show "Yesterday" and time
                return $"Yesterday, {timestamp:HH:mm}";
            }
            else
            {
                // Older: show date in format "08 Apr", "21 Dec", "02 Jan"
                string[] monthAbbreviations = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", 
                                                "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
                int day = timestamp.Day;
                string month = monthAbbreviations[timestamp.Month - 1];
                return $"{day:D2} {month}, {timestamp:HH:mm}";
            }
        }

        private void DrawNotificationText(SpriteBatch spriteBatch, Notification notification, Rectangle itemRect)
        {
            // Use notification font (same as FlashMessage)
            SpriteFont fontToUse = _notificationFont ?? _dropdownFont;
            
            string dateTimeText = FormatNotificationDateTime(notification.Timestamp);
            string bracketText = $"[{dateTimeText}] ";
            string message = notification.Message;
            
            // Calculate starting position
            Vector2 currentPos = new Vector2(
                itemRect.X + 10,
                itemRect.Y + (itemRect.Height - fontToUse.LineSpacing) / 2
            );
            
            // Draw bracketed date/time in purple
            Vector2 bracketSize = fontToUse.MeasureString(bracketText);
            spriteBatch.DrawString(fontToUse, bracketText, currentPos, MIAMI_PURPLE);
            currentPos.X += bracketSize.X;
            
            // For workspace notifications, extract workspace name and make it pink
            if (notification.Type == "workspace" && message.StartsWith("Workspace changed to "))
            {
                // Extract workspace name (everything after "Workspace changed to ")
                string prefix = "Workspace changed to ";
                string workspaceName = message.Substring(prefix.Length);
                
                // Draw prefix in white
                Vector2 prefixSize = fontToUse.MeasureString(prefix);
                spriteBatch.DrawString(fontToUse, prefix, currentPos, MIAMI_TEXT);
                currentPos.X += prefixSize.X;
                
                // Draw workspace name in pink
                Vector2 workspaceSize = fontToUse.MeasureString(workspaceName);
                
                // Check if text would overflow
                float totalWidth = currentPos.X - (itemRect.X + 10) + workspaceSize.X;
                int maxWidth = itemRect.Width - 20 - (_scrollBarBounds.IsEmpty ? 0 : SCROLLBAR_WIDTH);
                
                if (totalWidth > maxWidth)
                {
                    // Truncate workspace name
                    string truncated = workspaceName;
                    while (fontToUse.MeasureString(truncated + "...").X + (currentPos.X - (itemRect.X + 10)) > maxWidth && truncated.Length > 0)
                    {
                        truncated = truncated.Substring(0, truncated.Length - 1);
                    }
                    workspaceName = truncated + "...";
                    workspaceSize = fontToUse.MeasureString(workspaceName);
                }
                
                spriteBatch.DrawString(fontToUse, workspaceName, currentPos, PINK_COLOR);
            }
            else
            {
                // Draw regular message in white
                string displayText = message;
                Vector2 messageSize = fontToUse.MeasureString(displayText);
                
                // Check if text would overflow
                float totalWidth = currentPos.X - (itemRect.X + 10) + messageSize.X;
                int maxWidth = itemRect.Width - 20 - (_scrollBarBounds.IsEmpty ? 0 : SCROLLBAR_WIDTH);
                
                if (totalWidth > maxWidth)
                {
                    // Truncate message
                    string truncated = displayText;
                    while (fontToUse.MeasureString(truncated + "...").X + (currentPos.X - (itemRect.X + 10)) > maxWidth && truncated.Length > 0)
                    {
                        truncated = truncated.Substring(0, truncated.Length - 1);
                    }
                    displayText = truncated + "...";
                }
                
                spriteBatch.DrawString(fontToUse, displayText, currentPos, MIAMI_TEXT);
            }
        }

        private void UpdateCursor(bool wasHoveringOverInteractive)
        {
            try
            {
                if (_isHoveringOverInteractive != wasHoveringOverInteractive)
                {
                    if (_isHoveringOverInteractive)
                    {
                        GameEngine.Instance.RequestHandCursor();
                    }
                    else
                    {
                        GameEngine.Instance.ReleaseHandCursor();
                    }
                }
            }
            catch (Exception ex)
            {
                _engine.Log($"NotificationCenter: Error updating cursor: {ex.Message}");
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            Texture2D iconTexture = GetIconTexture();
            if (iconTexture != null)
            {
                Color iconColor = _isIconHovered ? Color.White : new Color(200, 200, 200);
                spriteBatch.Draw(iconTexture, _iconBounds, iconColor);

                // Draw hover background
                if (_isIconHovered)
                {
                    spriteBatch.Draw(_pixel, _iconBounds, MIAMI_HOVER);
                }
            }
            else
            {
                // Fallback: draw a simple rectangle if no icon loaded
                Color iconColor = _isIconHovered ? MIAMI_PURPLE_LIGHT : MIAMI_PURPLE;
                spriteBatch.Draw(_pixel, _iconBounds, iconColor);
            }
        }

        public void DrawTopLayer(SpriteBatch spriteBatch)
        {
            // Draw dropdown if open
            if (_isDropdownOpen)
            {
                DrawDropdown(spriteBatch);
            }
        }

        private void DrawDropdown(SpriteBatch spriteBatch)
        {
            const int BORDER_THICKNESS = 2;

            // Draw empty state if no notifications
            if (_notifications.Count == 0)
            {
                // Draw dropdown background
                spriteBatch.Draw(_pixel, _dropdownBounds, MIAMI_BACKGROUND);

                // Draw dropdown border (outer border, like TopBar)
                // Top border
                spriteBatch.Draw(_pixel, new Rectangle(_dropdownBounds.X - BORDER_THICKNESS, _dropdownBounds.Y - BORDER_THICKNESS, 
                    _dropdownBounds.Width + (BORDER_THICKNESS * 2), BORDER_THICKNESS), MIAMI_BORDER);
                // Bottom border
                spriteBatch.Draw(_pixel, new Rectangle(_dropdownBounds.X - BORDER_THICKNESS, _dropdownBounds.Bottom, 
                    _dropdownBounds.Width + (BORDER_THICKNESS * 2), BORDER_THICKNESS), MIAMI_BORDER);
                // Left border
                spriteBatch.Draw(_pixel, new Rectangle(_dropdownBounds.X - BORDER_THICKNESS, _dropdownBounds.Y - BORDER_THICKNESS, 
                    BORDER_THICKNESS, _dropdownBounds.Height + (BORDER_THICKNESS * 2)), MIAMI_BORDER);
                // Right border
                spriteBatch.Draw(_pixel, new Rectangle(_dropdownBounds.Right, _dropdownBounds.Y - BORDER_THICKNESS, 
                    BORDER_THICKNESS, _dropdownBounds.Height + (BORDER_THICKNESS * 2)), MIAMI_BORDER);

                // Draw "No notifications" text
                string emptyText = "No notifications";
                SpriteFont fontToUse = _notificationFont ?? _dropdownFont;
                Vector2 textSize = fontToUse.MeasureString(emptyText);
                Vector2 textPos = new Vector2(
                    _dropdownBounds.X + (_dropdownBounds.Width - textSize.X) / 2,
                    _dropdownBounds.Y + (_dropdownBounds.Height - textSize.Y) / 2
                );
                spriteBatch.DrawString(fontToUse, emptyText, textPos, new Color((byte)MIAMI_TEXT.R, (byte)MIAMI_TEXT.G, (byte)MIAMI_TEXT.B, (byte)150));
                return;
            }

            // Draw dropdown shadow
            Rectangle shadowBounds = new Rectangle(
                _dropdownBounds.X + 2,
                _dropdownBounds.Y + 2,
                _dropdownBounds.Width,
                _dropdownBounds.Height
            );
            spriteBatch.Draw(_pixel, shadowBounds, MIAMI_SHADOW);

            // Draw dropdown background
            spriteBatch.Draw(_pixel, _dropdownBounds, MIAMI_BACKGROUND);

            // Draw dropdown border (outer border, like TopBar)
            // Top border
            spriteBatch.Draw(_pixel, new Rectangle(_dropdownBounds.X - BORDER_THICKNESS, _dropdownBounds.Y - BORDER_THICKNESS, 
                _dropdownBounds.Width + (BORDER_THICKNESS * 2), BORDER_THICKNESS), MIAMI_BORDER);
            // Bottom border
            spriteBatch.Draw(_pixel, new Rectangle(_dropdownBounds.X - BORDER_THICKNESS, _dropdownBounds.Bottom, 
                _dropdownBounds.Width + (BORDER_THICKNESS * 2), BORDER_THICKNESS), MIAMI_BORDER);
            // Left border
            spriteBatch.Draw(_pixel, new Rectangle(_dropdownBounds.X - BORDER_THICKNESS, _dropdownBounds.Y - BORDER_THICKNESS, 
                BORDER_THICKNESS, _dropdownBounds.Height + (BORDER_THICKNESS * 2)), MIAMI_BORDER);
            // Right border
            spriteBatch.Draw(_pixel, new Rectangle(_dropdownBounds.Right, _dropdownBounds.Y - BORDER_THICKNESS, 
                BORDER_THICKNESS, _dropdownBounds.Height + (BORDER_THICKNESS * 2)), MIAMI_BORDER);

            // Draw notification items
            int visibleItems = Math.Min(_notifications.Count, MAX_VISIBLE_ITEMS);
            for (int i = 0; i < visibleItems; i++)
            {
                int actualIndex = i + _scrollOffset;
                if (actualIndex >= _notifications.Count) break;

                Rectangle itemRect = new Rectangle(
                    _dropdownBounds.X,
                    _dropdownBounds.Y + (i * ITEM_HEIGHT),
                    _dropdownBounds.Width - (_scrollBarBounds.IsEmpty ? 0 : SCROLLBAR_WIDTH),
                    ITEM_HEIGHT
                );

                // Draw item background if hovered
                if (actualIndex == _hoveredNotificationIndex)
                {
                    spriteBatch.Draw(_pixel, itemRect, NOTIFICATION_HOVER);
                }
                else
                {
                    spriteBatch.Draw(_pixel, itemRect, NOTIFICATION_BACKGROUND);
                }

                // Draw notification text with colored segments
                var notification = _notifications[actualIndex];
                DrawNotificationText(spriteBatch, notification, itemRect);

                // Red dot for notifications that were "new" when we opened (only this session)
                if (actualIndex < _unreadCountWhenOpened)
                {
                    const int RED_DOT_SIZE = 8;
                    int contentWidth = itemRect.Width - (_scrollBarBounds.IsEmpty ? 0 : SCROLLBAR_WIDTH);
                    int dotX = itemRect.X + contentWidth - RED_DOT_SIZE - 6;
                    int dotY = itemRect.Y + (ITEM_HEIGHT - RED_DOT_SIZE) / 2;
                    spriteBatch.Draw(_pixel, new Rectangle(dotX, dotY, RED_DOT_SIZE, RED_DOT_SIZE), Color.Red);
                }

                // Draw separator line at bottom of item (except for last item)
                if (i < visibleItems - 1 && actualIndex < _notifications.Count - 1)
                {
                    Color separatorColor = new Color(80, 80, 80); // Dark gray separator
                    int separatorY = itemRect.Bottom - 1;
                    int separatorWidth = itemRect.Width - (_scrollBarBounds.IsEmpty ? 0 : SCROLLBAR_WIDTH);
                    spriteBatch.Draw(_pixel, new Rectangle(itemRect.X, separatorY, separatorWidth, 1), separatorColor);
                }
            }

            // Draw scrollbar if needed
            if (!_scrollBarBounds.IsEmpty)
            {
                DrawScrollBar(spriteBatch);
            }
        }

        private void DrawScrollBar(SpriteBatch spriteBatch)
        {
            // Draw scrollbar background
            spriteBatch.Draw(_pixel, _scrollBarBounds, new Color(60, 60, 60, 200));

            // Calculate scrollbar thumb size and position
            int visibleItems = Math.Min(_notifications.Count, MAX_VISIBLE_ITEMS);
            float scrollRatio = (float)_scrollOffset / Math.Max(1, _notifications.Count - visibleItems);
            int thumbHeight = Math.Max(20, (int)(_scrollBarBounds.Height * (visibleItems / (float)_notifications.Count)));
            int thumbY = _scrollBarBounds.Y + (int)((_scrollBarBounds.Height - thumbHeight) * scrollRatio);

            Rectangle thumbBounds = new Rectangle(
                _scrollBarBounds.X + 2,
                thumbY,
                _scrollBarBounds.Width - 4,
                thumbHeight
            );

            // Draw scrollbar thumb
            Color thumbColor = _isDraggingScroll ? new Color(180, 145, 250) : new Color(147, 112, 219);
            spriteBatch.Draw(_pixel, thumbBounds, thumbColor);

            // Draw scrollbar border
            Color scrollBorderColor = new Color(100, 100, 100);
            spriteBatch.Draw(_pixel, new Rectangle(_scrollBarBounds.X, _scrollBarBounds.Y, 1, _scrollBarBounds.Height), scrollBorderColor);
        }

        public void LoadContent(ContentManager content)
        {
            try
            {
                _iconNotificationBell = content.Load<Texture2D>("Modules/NotificationCenter_essential/Images/NotificationBell");
            }
            catch (Exception ex) { _engine.Log($"NotificationCenter: Failed to load NotificationBell: {ex.Message}"); }
            try
            {
                _iconNew1 = content.Load<Texture2D>("Modules/NotificationCenter_essential/Images/NewNotification_1");
            }
            catch (Exception ex) { _engine.Log($"NotificationCenter: Failed to load NewNotification_1: {ex.Message}"); }
            try
            {
                _iconNew2 = content.Load<Texture2D>("Modules/NotificationCenter_essential/Images/NewNotification_2");
            }
            catch (Exception ex) { _engine.Log($"NotificationCenter: Failed to load NewNotification_2: {ex.Message}"); }
            try
            {
                _iconNew3 = content.Load<Texture2D>("Modules/NotificationCenter_essential/Images/NewNotification_3");
            }
            catch (Exception ex) { _engine.Log($"NotificationCenter: Failed to load NewNotification_3: {ex.Message}"); }
            try
            {
                _iconNew4 = content.Load<Texture2D>("Modules/NotificationCenter_essential/Images/NewNotification_4");
            }
            catch (Exception ex) { _engine.Log($"NotificationCenter: Failed to load NewNotification_4: {ex.Message}"); }
            try
            {
                _iconNew5 = content.Load<Texture2D>("Modules/NotificationCenter_essential/Images/NewNotification_5");
            }
            catch (Exception ex) { _engine.Log($"NotificationCenter: Failed to load NewNotification_5: {ex.Message}"); }
            try
            {
                _iconNew5Plus = content.Load<Texture2D>("Modules/NotificationCenter_essential/Images/NewNotification_5+");
            }
            catch (Exception ex) { _engine.Log($"NotificationCenter: Failed to load NewNotification_5+: {ex.Message}"); }
            try
            {
                _logo = content.Load<Texture2D>("Modules/NotificationCenter_essential/logo");
            }
            catch (Exception ex)
            {
                _engine.Log($"NotificationCenter: Failed to load logo (optional): {ex.Message}");
                _logo = null;
            }

            // Load notification font (same as FlashMessage - try Roboto, then Inconsolata, fallback to menu font)
            try
            {
                _notificationFont = content.Load<SpriteFont>("Fonts/SpriteFonts/roboto/regular");
                _engine.Log("NotificationCenter: Successfully loaded Roboto font");
            }
            catch (Exception ex)
            {
                _engine.Log($"NotificationCenter: Failed to load Roboto font: {ex.Message}");
                try
                {
                    // Try Inconsolata as fallback
                    _notificationFont = content.Load<SpriteFont>("Fonts/SpriteFonts/inconsolata/regular");
                    _engine.Log("NotificationCenter: Using Inconsolata font as fallback");
                }
                catch (Exception ex2)
                {
                    _engine.Log($"NotificationCenter: Failed to load Inconsolata font: {ex2.Message}");
                    try
                    {
                        // Try Open Sans as another fallback
                        _notificationFont = content.Load<SpriteFont>("Fonts/SpriteFonts/open_sans/regular");
                        _engine.Log("NotificationCenter: Using Open Sans font as fallback");
                    }
                    catch (Exception ex3)
                    {
                        _engine.Log($"NotificationCenter: Failed to load Open Sans font: {ex3.Message}");
                        // Use menu font as last resort
                        _notificationFont = _menuFont;
                        _engine.Log("NotificationCenter: Using menu font as last resort");
                    }
                }
            }
        }

        public void UpdateWindowWidth(int newWidth)
        {
            _windowWidth = newWidth;
            // Keep the same Y position, just update X
            _iconBounds = new Rectangle(_windowWidth - ICON_SIZE - ICON_PADDING, _iconBounds.Y, ICON_SIZE, ICON_SIZE);
            if (_isDropdownOpen)
            {
                UpdateDropdownBounds();
            }
        }

        public void SetIconBounds(Rectangle bounds)
        {
            _iconBounds = bounds;
            _engine.Log($"NotificationCenter: Icon bounds set to {bounds}");
        }

        public Rectangle GetIconBounds()
        {
            return _iconBounds;
        }

        public void ToggleDropdown()
        {
            if (_isDropdownOpen)
            {
                _engine.Log("NotificationCenter: Closing dropdown");
                CloseDropdown();
            }
            else
            {
                _engine.Log("NotificationCenter: Opening dropdown");
                // Mark all current notifications as read and show red dot on items that were unread
                int unreadCount = GetUnreadCount();
                _unreadCountWhenOpened = unreadCount;
                _lastReadNotificationCount = _notifications.Count;
                SaveNotifications();
                _isDropdownOpen = true;
                UpdateDropdownBounds();
            }
        }

        /// <summary>Number of notifications added after the last time the user opened the dropdown.</summary>
        private int GetUnreadCount()
        {
            return Math.Max(0, _notifications.Count - _lastReadNotificationCount);
        }

        private Texture2D GetIconTexture()
        {
            int unread = GetUnreadCount();
            if (unread <= 0) return _iconNotificationBell ?? _logo;
            if (unread == 1) return _iconNew1 ?? _iconNotificationBell ?? _logo;
            if (unread == 2) return _iconNew2 ?? _iconNotificationBell ?? _logo;
            if (unread == 3) return _iconNew3 ?? _iconNotificationBell ?? _logo;
            if (unread == 4) return _iconNew4 ?? _iconNotificationBell ?? _logo;
            if (unread == 5) return _iconNew5 ?? _iconNotificationBell ?? _logo;
            return _iconNew5Plus ?? _iconNew5 ?? _iconNotificationBell ?? _logo;
        }

        public void Dispose()
        {
            if (_instance == this)
                _instance = null;
            _pixel?.Dispose();
            _logo?.Dispose();
            _iconNotificationBell?.Dispose();
            _iconNew1?.Dispose();
            _iconNew2?.Dispose();
            _iconNew3?.Dispose();
            _iconNew4?.Dispose();
            _iconNew5?.Dispose();
            _iconNew5Plus?.Dispose();
        }
    }
}

