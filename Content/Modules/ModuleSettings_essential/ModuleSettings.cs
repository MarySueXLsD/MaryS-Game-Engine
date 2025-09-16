using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Content;
using System.Collections.Generic;
using System;
using MarySGameEngine.Modules.WindowManagement_essential;
using MarySGameEngine.Modules.TaskBar_essential;
using MarySGameEngine.Modules.UIElements_essential;
using System.IO;
using System.Text.Json;
using System.Linq;

namespace MarySGameEngine.Modules.ModuleSettings_essential
{
    public class ModuleSettings : IModule
    {
        private WindowManagement _windowManagement;
        private GraphicsDevice _graphicsDevice;
        private SpriteFont _menuFont;
        private SpriteFont _uiFont; // Separate font for UI elements
        private int _windowWidth;
        private TaskBar _taskBar;
        private ContentManager _content;
        private UIElements _uiElements;
        private GameEngine _engine;
        private Texture2D _pixel;
        private MouseState _prevMouseState;
        private MouseState _currentMouseState;
        private bool _isFocused = false;

        private class ModuleTab
        {
            public string Name;
            public string SettingsPath;
            public string BridgePath;
            public string FolderName;
        }
        private List<ModuleTab> _tabs = new List<ModuleTab>();
        private int _activeTabIndex = 0;
        private int _firstVisibleTabIndex = 0;
        private int _maxVisibleTabs = 1;
        private SpriteFont _tabFont;
        private int _tabBarHeight = 38; // 30px + padding
        private int _tabPadding = 10;
        private int _tabOverlap = 18; // How much tabs overlap each other
        private Rectangle _tabBarBounds;
        private int _tabBarSpacing = 8; // Spacing between title bar and tab bar, and between tab bar and UI
        private int _titleBarHeight = 40; // Should match WindowManagement's title bar height

        // Dropdown state
        private bool _isDropdownOpen = false;
        private Rectangle _dropdownBounds;
        private int _hoveredDropdownItem = -1;
        private int _scrollOffset = 0;
        private int _maxVisibleItems = 6;
        private int _itemHeight = 36;
        private bool _isScrolling = false;
        private Rectangle _scrollBarBounds;
        private bool _isDraggingScroll = false;
        private Vector2 _scrollDragStart;

        public ModuleSettings(GraphicsDevice graphicsDevice, SpriteFont menuFont, int windowWidth)
        {
            _graphicsDevice = graphicsDevice;
            _menuFont = menuFont;
            _windowWidth = windowWidth;
            _engine = (GameEngine)GameEngine.Instance;
            
            // Create 1x1 white pixel texture
            _pixel = new Texture2D(graphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
            
            // Create window properties from bridge.json
            var properties = new WindowProperties
            {
                IsVisible = false, // Start closed
                IsMovable = true,
                IsResizable = true
            };

            // Initialize window management
            _windowManagement = new WindowManagement(graphicsDevice, menuFont, windowWidth, properties);
            _windowManagement.SetVisible(false);  // Explicitly set to closed

            // Position the window in the center of the screen
            UpdateBounds();

            // Don't initialize UI elements yet - wait until window is opened
            _uiElements = null;
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
                // Update mouse state
                _prevMouseState = _currentMouseState;
                _currentMouseState = Mouse.GetState();
                
                // Track window visibility changes
                bool isCurrentlyVisible = _windowManagement.IsVisible();
                
                // Close dropdown if window is not visible
                if (!isCurrentlyVisible)
                {
                    CloseDropdown();
                }
                
                // Handle focus - only if window is visible
                if (isCurrentlyVisible)
                {
                    // Check if this window was clicked (gains focus)
                    if (_currentMouseState.LeftButton == ButtonState.Pressed && 
                        _prevMouseState.LeftButton == ButtonState.Released)
                    {
                        var mousePos = _currentMouseState.Position;
                        var windowRect = _windowManagement.GetWindowBounds();
                        
                        // Check if click is within this window
                        if (windowRect.Contains(mousePos))
                        {
                            // Clear focus from other modules
                            ClearFocusFromOtherModules();
                            _isFocused = true;
                        }
                        else
                        {
                            // Click outside window - lose focus
                            _isFocused = false;
                        }
                    }
                }
                
                // Update TaskBar about window state changes
                if (_taskBar != null)
                {
                    if (!isCurrentlyVisible)
                    {
                        // Window is minimized - set as minimized but keep icon visible
                        _taskBar.SetModuleMinimized("Module Settings", true);
                        System.Diagnostics.Debug.WriteLine("ModuleSettings: Window not visible, set as minimized in TaskBar");
                    }
                    else
                    {
                        // Window is visible - ensure module icon exists and is not marked as minimized
                        _taskBar.EnsureModuleIconExists("Module Settings", _content);
                        _taskBar.SetModuleMinimized("Module Settings", false);
                        System.Diagnostics.Debug.WriteLine("ModuleSettings: Window visible, ensured icon exists and not minimized in TaskBar");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("ModuleSettings: WARNING - TaskBar is null in Update method!");
                }
                
                _windowManagement.Update();
                Rectangle windowBounds = _windowManagement.GetWindowBounds();
                // Tab bar is now inside the window, just below the title bar
                _tabBarBounds = new Rectangle(
                    windowBounds.X,
                    windowBounds.Y + _titleBarHeight + _tabBarSpacing,
                    windowBounds.Width,
                    _tabBarHeight
                );
                UpdateTabBarInput();

                // If window is visible and UIElements is null, load the current tab's settings_ui.md
                if (_windowManagement.IsVisible() && _uiElements == null)
                {
                    LoadTabUI(_activeTabIndex);
                }
                
                // Update UI elements if window is visible and initialized
                if (_windowManagement.IsVisible() && _uiElements != null)
                {
                    // UI area starts below the tab bar
                    // Account for scrollbar width if needed
                    int uiWidth = windowBounds.Width;
                    Rectangle uiBounds = new Rectangle(
                        windowBounds.X,
                        windowBounds.Y + _titleBarHeight + _tabBarSpacing + _tabBarHeight + _tabBarSpacing,
                        uiWidth,
                        windowBounds.Height - _titleBarHeight - _tabBarHeight - 2 * _tabBarSpacing
                    );
                    Rectangle currentBounds = _uiElements.GetBounds();
                    if (currentBounds != uiBounds)
                    {
                        _uiElements.SetBounds(uiBounds);
                    }
                    
                    // Disable scrolling when dropdown is open
                    _uiElements.SetScrollingEnabled(!_isDropdownOpen);
                    
                    _uiElements.Update(_isFocused);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ModuleSettings: Error in Update: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"ModuleSettings: Stack trace: {ex.StackTrace}");
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            try
            {
                _windowManagement.Draw(spriteBatch, "Module Settings");
                if (_windowManagement.IsVisible())
                {
                    DrawTabBar(spriteBatch);
                    if (_uiElements != null)
                    _uiElements.Draw(spriteBatch);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ModuleSettings: Error in Draw: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"ModuleSettings: Stack trace: {ex.StackTrace}");
            }
        }
        
        public void DrawTopLayer(SpriteBatch spriteBatch)
        {
            // Draw dropdown menu on top layer if open
            if (_isDropdownOpen)
            {
                DrawDropdownMenu(spriteBatch);
            }
        }

        public void UpdateWindowWidth(int newWidth)
        {
            // Close dropdown when window is being resized to avoid positioning confusion
            CloseDropdown();
            
            _windowWidth = newWidth;
            _windowManagement.UpdateWindowWidth(newWidth);
            UpdateBounds(); // Maintain center positioning
            
            // Update UI elements bounds
            if (_uiElements != null)
            {
                Rectangle windowBounds = _windowManagement.GetWindowBounds();
                _uiElements.SetBounds(windowBounds);
            }
            
            // Ensure TaskBar communication is maintained after window resize
            if (_taskBar != null)
            {
                bool isCurrentlyVisible = _windowManagement.IsVisible();
                if (isCurrentlyVisible)
                {
                    // Ensure the module icon exists and is properly registered
                    _taskBar.EnsureModuleIconExists("Module Settings", _content);
                    // Ensure the module is not marked as minimized in TaskBar after resize
                    _taskBar.SetModuleMinimized("Module Settings", false);
                    System.Diagnostics.Debug.WriteLine("ModuleSettings: Ensured module icon exists and is not minimized in TaskBar after window resize");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("ModuleSettings: WARNING - TaskBar is null during window resize!");
            }
        }

        public void OnTaskBarPositionChanged(TaskBarPosition newPosition)
        {
            try
            {
                _engine.Log($"ModuleSettings: TaskBar position changed to {newPosition}, updating window bounds");
                
                // Update window management bounds
                _windowManagement.OnTaskBarPositionChanged(newPosition);
                
                // Update UI elements bounds if they exist
                if (_uiElements != null)
                {
                    Rectangle windowBounds = _windowManagement.GetWindowBounds();
                    _uiElements.SetBounds(windowBounds);
                }
                
                _engine.Log($"ModuleSettings: Updated window bounds after TaskBar position change");
            }
            catch (Exception ex)
            {
                _engine.Log($"ModuleSettings: Error handling TaskBar position change: {ex.Message}");
            }
        }
        
        private void CloseDropdown()
        {
            if (_isDropdownOpen)
            {
                _isDropdownOpen = false;
                _hoveredDropdownItem = -1;
                _scrollOffset = 0;
            }
        }
        
        public void OnWindowStateChanged()
        {
            // Close dropdown when window state changes (minimize, maximize, etc.)
            CloseDropdown();
        }
        
        public void OnWindowVisibilityChanged(bool isVisible)
        {
            // Close dropdown when window becomes invisible (minimized, etc.)
            if (!isVisible)
            {
                CloseDropdown();
            }
            
            // Update TaskBar about window state changes
            if (_taskBar != null)
            {
                if (!isVisible)
                {
                    // Window is minimized - set as minimized but keep icon visible
                    _taskBar.SetModuleMinimized("Module Settings", true);
                    System.Diagnostics.Debug.WriteLine("ModuleSettings: Set module as minimized in TaskBar");
                }
                else
                {
                    // Window is restored - ensure it's not marked as minimized
                    _taskBar.SetModuleMinimized("Module Settings", false);
                    System.Diagnostics.Debug.WriteLine("ModuleSettings: Set module as not minimized in TaskBar");
                }
            }
        }

        public void LoadContent(ContentManager content)
        {
            _content = content;
            _windowManagement.LoadContent(content);
            // Try to load Roboto for UIElements
            try {
                _uiFont = content.Load<SpriteFont>("Fonts/SpriteFonts/roboto/regular");
            } catch {
                _uiFont = _menuFont;
            }
            _tabFont = _menuFont;
            BuildTabs();
        }

        public void Dispose()
        {
            // Reset cursor when disposing
            _uiElements?.ResetCursor();
            
            _windowManagement.Dispose();
            _uiElements?.Dispose();
        }

        private void UpdateBounds()
        {
            if (_windowManagement != null)
            {
                Rectangle windowBounds = _windowManagement.GetWindowBounds();
                _windowWidth = windowBounds.Width;
            }
        }

        private void BuildTabs()
        {
            _tabs.Clear();
            string modulesRoot = Path.Combine(Directory.GetCurrentDirectory(), "Content", "Modules");
            foreach (var dir in Directory.GetDirectories(modulesRoot))
            {
                string folderName = Path.GetFileName(dir);
                string bridgePath = Path.Combine(dir, "bridge.json");
                string settingsPath = Path.Combine(dir, "settings_ui.md");
                if (File.Exists(bridgePath) && File.Exists(settingsPath))
                {
                    string json = File.ReadAllText(bridgePath);
                    string name = "";
                    try
                    {
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("name", out var n))
                            name = n.GetString();
                    }
                    catch { name = folderName; }
                    _tabs.Add(new ModuleTab { Name = name, SettingsPath = settingsPath, BridgePath = bridgePath, FolderName = folderName });
                }
            }
            // Always include ModuleSettings itself, even if not found above
            if (!_tabs.Any(t => t.FolderName == "ModuleSettings_essential"))
            {
                string dir = Path.Combine(modulesRoot, "ModuleSettings_essential");
                string bridgePath = Path.Combine(dir, "bridge.json");
                string settingsPath = Path.Combine(dir, "settings_ui.md");
                string name = "Module Settings";
                if (File.Exists(bridgePath))
                {
                    string json = File.ReadAllText(bridgePath);
                    try
                    {
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("name", out var n))
                            name = n.GetString();
                    }
                    catch { }
                }
                _tabs.Insert(0, new ModuleTab { Name = name, SettingsPath = settingsPath, BridgePath = bridgePath, FolderName = "ModuleSettings_essential" });
            }
            _engine.Log($"[ModuleSettingsTabs] Built tab list. Count: {_tabs.Count}");
            foreach (var t in _tabs)
                _engine.Log($"[ModuleSettingsTabs] Tab: {t.Name} | Path: {t.SettingsPath}");
            if (_tabs.Count == 0)
                _engine.Log("[ModuleSettingsTabs] WARNING: No tabs found!");
            _activeTabIndex = 0;
            _firstVisibleTabIndex = 0;
            LoadTabUI(_activeTabIndex);
        }

        private void LoadTabUI(int tabIndex)
        {
            if (tabIndex < 0 || tabIndex >= _tabs.Count) return;
            var tab = _tabs[tabIndex];
            _engine.Log($"[ModuleSettingsTabs] Loading tab {tabIndex}: {tab.Name} | File: {tab.SettingsPath}");
            string markdown = File.Exists(tab.SettingsPath) ? File.ReadAllText(tab.SettingsPath) : "# No settings found";
            if (!File.Exists(tab.SettingsPath))
                _engine.Log($"[ModuleSettingsTabs] WARNING: settings_ui.md not found for {tab.Name} at {tab.SettingsPath}");
            if (string.IsNullOrWhiteSpace(markdown))
                _engine.Log($"[ModuleSettingsTabs] WARNING: settings_ui.md is empty for {tab.Name} at {tab.SettingsPath}");
            // UI area starts below the tab bar
            Rectangle windowBounds = _windowManagement.GetWindowBounds();
            int uiWidth = windowBounds.Width;
            Rectangle uiBounds = new Rectangle(
                windowBounds.X,
                windowBounds.Y + _titleBarHeight + _tabBarSpacing + _tabBarHeight + _tabBarSpacing,
                uiWidth,
                windowBounds.Height - _titleBarHeight - _tabBarHeight - 2 * _tabBarSpacing
            );
            _uiElements = new UIElements(_graphicsDevice, _uiFont, uiBounds, markdown, tab.SettingsPath);
            _uiElements.SetSaveSettingsCallback(OnSaveSettings);
            _uiElements.SetResetToDefaultsCallback(OnResetToDefaults);
            if (_uiElements == null)
                _engine.Log($"[ModuleSettingsTabs] ERROR: UIElements is null after loading tab {tab.Name}");
        }

        private void UpdateTabBarInput()
        {
            var mouse = Mouse.GetState();
            var mousePos = mouse.Position;
            bool leftPressed = mouse.LeftButton == ButtonState.Pressed;
            bool leftJustPressed = leftPressed && _prevMouseState.LeftButton == ButtonState.Released;
            
            // Store current mouse state for scroll wheel
            _currentMouseState = mouse;
            
            if (!_windowManagement.IsVisible()) 
            { 
                _prevMouseState = mouse; 
                return; 
            }
            
            // Handle dropdown menu clicks
            if (_isDropdownOpen)
            {
                if (leftJustPressed)
                {
                    // Check if clicked on scrollbar
                    if (!_scrollBarBounds.IsEmpty && _scrollBarBounds.Contains(mousePos))
                    {
                        _isDraggingScroll = true;
                        _scrollDragStart = new Vector2(mousePos.X, mousePos.Y);
                        _prevMouseState = mouse;
                        return;
                    }
                    
                    // Check if clicked on dropdown item
                    int visibleItems = Math.Min(_tabs.Count, _maxVisibleItems);
                    for (int i = 0; i < visibleItems; i++)
                    {
                        int actualIndex = i + _scrollOffset;
                        if (actualIndex >= _tabs.Count) break;
                        
                        Rectangle itemRect = new Rectangle(
                            _dropdownBounds.X,
                            _dropdownBounds.Y + (i * _itemHeight),
                            _dropdownBounds.Width - (_scrollBarBounds.IsEmpty ? 0 : 16),
                            _itemHeight
                        );
                        
                        if (itemRect.Contains(mousePos))
                        {
                            _activeTabIndex = actualIndex;
                            LoadTabUI(actualIndex);
                            CloseDropdown();
                        _prevMouseState = mouse;
                        return;
                    }
                }
                    
                    // If clicked outside dropdown, close it
                    if (!_dropdownBounds.Contains(mousePos) && !_tabBarBounds.Contains(mousePos))
                    {
                        CloseDropdown();
                    }
                }
                else if (leftPressed && _isDraggingScroll)
                {
                    // Handle scrollbar dragging
                    float deltaY = mousePos.Y - _scrollDragStart.Y;
                    float maxScroll = _tabs.Count - _maxVisibleItems;
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
                    _hoveredDropdownItem = -1;
                    int visibleItems = Math.Min(_tabs.Count, _maxVisibleItems);
                    for (int i = 0; i < visibleItems; i++)
                    {
                        int actualIndex = i + _scrollOffset;
                        if (actualIndex >= _tabs.Count) break;
                        
                        Rectangle itemRect = new Rectangle(
                            _dropdownBounds.X,
                            _dropdownBounds.Y + (i * _itemHeight),
                            _dropdownBounds.Width - (_scrollBarBounds.IsEmpty ? 0 : 16),
                            _itemHeight
                        );
                        
                        if (itemRect.Contains(mousePos))
                        {
                            _hoveredDropdownItem = actualIndex;
                    break;
                        }
                    }
                }
            }
            else
            {
                // Calculate label width for click detection
                string labelText = "Module:";
                Vector2 labelSize = _uiFont.MeasureString(labelText) * 1.0f;
                int labelWidth = (int)labelSize.X + 10;
                
                // Create dropdown click area (excluding label)
                Rectangle dropdownClickArea = new Rectangle(
                    _tabBarBounds.X + labelWidth,
                    _tabBarBounds.Y,
                    _tabBarBounds.Width - labelWidth,
                    _tabBarBounds.Height
                );
                
                // Handle dropdown button click
                if (leftJustPressed && dropdownClickArea.Contains(mousePos))
                {
                    _isDropdownOpen = true;
                    UpdateDropdownBounds();
                }
            }
            
            // Handle mouse wheel scrolling when dropdown is open (regardless of other conditions)
            if (_isDropdownOpen && _tabs.Count > _maxVisibleItems)
            {
                int scrollDelta = _currentMouseState.ScrollWheelValue - _prevMouseState.ScrollWheelValue;
                if (scrollDelta != 0)
                {
                    int scrollStep = scrollDelta > 0 ? -1 : 1; // Scroll up/down
                    _scrollOffset = Math.Max(0, Math.Min(_tabs.Count - _maxVisibleItems, _scrollOffset + scrollStep));
                }
            }
            
            _prevMouseState = mouse;
        }

        private void UpdateDropdownBounds()
        {
            // Calculate label width using UI font
            string labelText = "Module:";
            Vector2 labelSize = _uiFont.MeasureString(labelText) * 1.0f;
            int labelWidth = (int)labelSize.X + 10; // Add some padding
            
            // Calculate dropdown menu bounds (accounting for label)
            int dropdownWidth = _tabBarBounds.Width - labelWidth;
            int visibleItems = Math.Min(_tabs.Count, _maxVisibleItems);
            int dropdownHeight = visibleItems * _itemHeight;
            int dropdownX = _tabBarBounds.X + labelWidth;
            int dropdownY = _tabBarBounds.Bottom;
            
            // Check if dropdown would go off screen
            if (dropdownY + dropdownHeight > _graphicsDevice.Viewport.Height)
            {
                // Position dropdown above the selector
                dropdownY = _tabBarBounds.Y - dropdownHeight;
            }
            
            _dropdownBounds = new Rectangle(dropdownX, dropdownY, dropdownWidth, dropdownHeight);
            
            // Calculate scrollbar bounds if needed
            if (_tabs.Count > _maxVisibleItems)
            {
                int scrollBarWidth = 16;
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
        
        private void DrawDropdownMenu(SpriteBatch spriteBatch)
        {
            if (_tabs.Count == 0) return;
            
            // Draw dropdown background
            spriteBatch.Draw(_pixel, _dropdownBounds, new Color(40, 40, 40, 240));
            
            // Draw dropdown border
            Color borderColor = new Color(147, 112, 219);
            int borderThickness = 2;
            
            // Top border
            spriteBatch.Draw(_pixel, new Rectangle(_dropdownBounds.X, _dropdownBounds.Y, _dropdownBounds.Width, borderThickness), borderColor);
            // Bottom border
            spriteBatch.Draw(_pixel, new Rectangle(_dropdownBounds.X, _dropdownBounds.Bottom - borderThickness, _dropdownBounds.Width, borderThickness), borderColor);
            // Left border
            spriteBatch.Draw(_pixel, new Rectangle(_dropdownBounds.X, _dropdownBounds.Y, borderThickness, _dropdownBounds.Height), borderColor);
            // Right border
            spriteBatch.Draw(_pixel, new Rectangle(_dropdownBounds.Right - borderThickness, _dropdownBounds.Y, borderThickness, _dropdownBounds.Height), borderColor);
            
            // Draw dropdown items (only visible ones)
            int visibleItems = Math.Min(_tabs.Count, _maxVisibleItems);
            for (int i = 0; i < visibleItems; i++)
            {
                int actualIndex = i + _scrollOffset;
                if (actualIndex >= _tabs.Count) break;
                
                Rectangle itemRect = new Rectangle(
                    _dropdownBounds.X,
                    _dropdownBounds.Y + (i * _itemHeight),
                    _dropdownBounds.Width - (_scrollBarBounds.IsEmpty ? 0 : 16),
                    _itemHeight
                );
                
                // Draw item background if hovered or selected
                if (actualIndex == _hoveredDropdownItem)
                {
                    spriteBatch.Draw(_pixel, itemRect, new Color(147, 112, 219, 120));
                }
                else if (actualIndex == _activeTabIndex)
                {
                    spriteBatch.Draw(_pixel, itemRect, new Color(147, 112, 219, 150));
                }
                
                // Draw item text with normal font size
                string itemText = _tabs[actualIndex].Name;
                Vector2 textSize = _tabFont.MeasureString(itemText) * 0.9f; // Normal font size
                Vector2 textPos = new Vector2(
                    itemRect.X + _tabPadding,
                    itemRect.Y + (itemRect.Height - textSize.Y) / 2
                );
                
                // Use high contrast colors for better readability
                Color textColor = (actualIndex == _activeTabIndex) ? Color.White : Color.White;
                if (actualIndex == _hoveredDropdownItem)
                {
                    textColor = Color.White;
                }
                spriteBatch.DrawString(_tabFont, itemText, textPos, textColor, 0f, Vector2.Zero, 0.9f, SpriteEffects.None, 0f);
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
            float scrollRatio = (float)_scrollOffset / Math.Max(1, _tabs.Count - _maxVisibleItems);
            int thumbHeight = Math.Max(20, (int)(_scrollBarBounds.Height * (_maxVisibleItems / (float)_tabs.Count)));
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
        
        private void DrawTabBar(SpriteBatch spriteBatch)
        {
            if (_tabFont == null || _tabs.Count == 0) return;
            _engine.Log($"[ModuleSettingsTabs] Drawing module selector. Tab count: {_tabs.Count}, Active: {_activeTabIndex}");
            
            // Calculate label width using UI font
            string labelText = "Module:";
            Vector2 labelSize = _uiFont.MeasureString(labelText) * 1.0f;
            int labelWidth = (int)labelSize.X + 10; // Add some padding
            
            // Adjust dropdown bounds to make room for label
            Rectangle dropdownBounds = new Rectangle(
                _tabBarBounds.X + labelWidth,
                _tabBarBounds.Y,
                _tabBarBounds.Width - labelWidth,
                _tabBarBounds.Height
            );
            
            // Draw label using UI font
            Vector2 labelPos = new Vector2(
                _tabBarBounds.X + 5,
                _tabBarBounds.Y + (_tabBarBounds.Height - labelSize.Y) / 2
            );
            spriteBatch.DrawString(_uiFont, labelText, labelPos, Color.White, 0f, Vector2.Zero, 1.0f, SpriteEffects.None, 0f);
            
            // Draw dropdown background
            spriteBatch.Draw(_pixel, dropdownBounds, new Color(60, 40, 90, 220));
            
            // Draw dropdown border
            Color borderColor = new Color(147, 112, 219);
            int borderThickness = 2;
            
            // Top border
            spriteBatch.Draw(_pixel, new Rectangle(dropdownBounds.X, dropdownBounds.Y, dropdownBounds.Width, borderThickness), borderColor);
            // Bottom border
            spriteBatch.Draw(_pixel, new Rectangle(dropdownBounds.X, dropdownBounds.Bottom - borderThickness, dropdownBounds.Width, borderThickness), borderColor);
            // Left border
            spriteBatch.Draw(_pixel, new Rectangle(dropdownBounds.X, dropdownBounds.Y, borderThickness, dropdownBounds.Height), borderColor);
            // Right border
            spriteBatch.Draw(_pixel, new Rectangle(dropdownBounds.Right - borderThickness, dropdownBounds.Y, borderThickness, dropdownBounds.Height), borderColor);
            
            // Draw current selection with larger font
            if (_activeTabIndex >= 0 && _activeTabIndex < _tabs.Count)
            {
                string currentModule = _tabs[_activeTabIndex].Name;
                Vector2 textSize = _uiFont.MeasureString(currentModule) * 1.0f;
                Vector2 textPos = new Vector2(
                    dropdownBounds.X + _tabPadding,
                    dropdownBounds.Y + (dropdownBounds.Height - textSize.Y) / 2
                );
                
                spriteBatch.DrawString(_uiFont, currentModule, textPos, Color.White, 0f, Vector2.Zero, 1.0f, SpriteEffects.None, 0f);
            }
            
            // Draw dropdown burger icon (three horizontal lines) to indicate it's clickable
            DrawBurgerIcon(spriteBatch, dropdownBounds);
        }

        private void DrawBurgerIcon(SpriteBatch spriteBatch, Rectangle dropdownBounds)
        {
            // Calculate icon position (right side of dropdown, centered vertically)
            int iconWidth = 24; // Increased from 16 to 24 for wider lines
            int iconHeight = 12;
            int lineThickness = 2;
            int lineSpacing = 2;
            
            int iconX = dropdownBounds.Right - iconWidth - 12; // Increased from 8 to 12px padding from right edge (moves icon left)
            int iconY = dropdownBounds.Y + (dropdownBounds.Height - iconHeight) / 2;
            
            // Draw three horizontal lines (burger icon)
            for (int i = 0; i < 3; i++)
            {
                int lineY = iconY + (i * (lineThickness + lineSpacing));
                Rectangle lineRect = new Rectangle(
                    iconX,
                    lineY,
                    iconWidth,
                    lineThickness
                );
                spriteBatch.Draw(_pixel, lineRect, Color.White);
            }
        }


        

        


        private void OnSaveSettings(Dictionary<string, object> values)
        {
            try
            {
                _engine.Log($"ModuleSettings: Save Settings called with {values.Count} values");
                
                // Save the current values to the ui_layout.md file
                string layoutPath = Path.Combine(Directory.GetCurrentDirectory(), "Content", "Modules", "ModuleSettings_essential", "ui_layout.md");
                
                // Get the current markdown content from UIElements
                if (_uiElements != null)
                {
                    // Reconstruct the markdown content with current values
                    string updatedMarkdown = ReconstructMarkdownWithValues(values);
                    
                    if (updatedMarkdown != null)
                    {
                        // Write the updated content to the file
                        File.WriteAllText(layoutPath, updatedMarkdown);
                        
                        // Update the UIElements markdown content
                        _uiElements.LoadFromMarkdown(updatedMarkdown);
                        
                        _engine.Log($"ModuleSettings: Successfully updated ui_layout.md with {values.Count} values");
                    }
                    else
                    {
                        _engine.Log($"ModuleSettings: Failed to reconstruct markdown, cannot save settings");
                    }
                }
                else
                {
                    _engine.Log($"ModuleSettings: UIElements is null, cannot save settings");
                }
                
                // Log the values for debugging
                foreach (var kvp in values)
                {
                    _engine.Log($"ModuleSettings: {kvp.Key} = {kvp.Value}");
                }
            }
            catch (Exception ex)
            {
                _engine.Log($"ModuleSettings: Error saving settings: {ex.Message}");
                _engine.Log($"ModuleSettings: Stack trace: {ex.StackTrace}");
            }
        }

        private string ReconstructMarkdownWithValues(Dictionary<string, object> values)
        {
            try
            {
                // Load the current markdown content to get the structure
                string currentMarkdown = File.Exists(_tabs[_activeTabIndex].SettingsPath) ? File.ReadAllText(_tabs[_activeTabIndex].SettingsPath) : "";
                if (string.IsNullOrEmpty(currentMarkdown))
                {
                    _engine.Log("ModuleSettings: ERROR - No current markdown layout found, cannot save settings");
                    return null;
                }

                string[] lines = currentMarkdown.Split('\n');
                var updatedLines = new List<string>();

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    string trimmedLine = line.Trim();

                    if (trimmedLine.StartsWith("- [x] ") || trimmedLine.StartsWith("- [ ] "))
                    {
                        // Checkbox line
                        string text = trimmedLine.Substring(6);
                        string key = $"checkbox_{i}";
                        
                        if (values.ContainsKey(key))
                        {
                            bool isChecked = (bool)values[key];
                            string checkboxPrefix = isChecked ? "- [x] " : "- [ ] ";
                            updatedLines.Add(line.Replace(trimmedLine, checkboxPrefix + text));
                        }
                        else
                        {
                            updatedLines.Add(line);
                        }
                    }
                    else if (trimmedLine.StartsWith("Input: "))
                    {
                        // Text input line
                        string placeholder = trimmedLine.Substring(7);
                        string key = $"input_{i}";
                        
                        if (values.ContainsKey(key))
                        {
                            string text = values[key]?.ToString() ?? "";
                            updatedLines.Add(line.Replace(trimmedLine, $"Input: {text}"));
                        }
                        else
                        {
                            updatedLines.Add(line);
                        }
                    }
                    else if (trimmedLine.StartsWith("Color: "))
                    {
                        // Color picker line
                        string colorText = trimmedLine.Substring(7);
                        string key = $"color_{i}";
                        
                        if (values.ContainsKey(key))
                        {
                            Color color = (Color)values[key];
                            string hexColor = ColorToHex(color);
                            string label = colorText.Contains(" ") ? colorText.Substring(colorText.IndexOf(' ') + 1) : colorText;
                            updatedLines.Add(line.Replace(trimmedLine, $"Color: {hexColor} {label}"));
                        }
                        else
                        {
                            updatedLines.Add(line);
                        }
                    }
                    else if (trimmedLine.StartsWith("Scale ["))
                    {
                        // Slider line
                        string key = $"slider_{i}";
                        
                        if (values.ContainsKey(key))
                        {
                            float value = (float)values[key];
                            // Reconstruct the slider line with the new value
                            string updatedSliderLine = ReconstructSliderLine(trimmedLine, value);
                            updatedLines.Add(line.Replace(trimmedLine, updatedSliderLine));
                        }
                        else
                        {
                            updatedLines.Add(line);
                        }
                    }
                    else
                    {
                        // Keep other lines unchanged
                        updatedLines.Add(line);
                    }
                }

                string result = string.Join("\n", updatedLines);
                _engine.Log($"ModuleSettings: Reconstructed markdown with {values.Count} values");
                return result;
            }
            catch (Exception ex)
            {
                _engine.Log($"ModuleSettings: Error reconstructing markdown: {ex.Message}");
                return null;
            }
        }

        private string ReconstructSliderLine(string originalLine, float value)
        {
            try
            {
                // Parse the original slider line to extract parameters
                // Format: "Scale [min=X, max=Y, step=Z, value=W] Label"
                
                // Find the label part (after the closing bracket)
                int closingBracketIndex = originalLine.IndexOf(']');
                if (closingBracketIndex == -1) return originalLine;
                
                // The label starts after the closing bracket and a space
                int labelStartIndex = closingBracketIndex + 1;
                if (labelStartIndex < originalLine.Length && originalLine[labelStartIndex] == ' ')
                {
                    labelStartIndex++; // Skip the space after the bracket
                }
                
                string label = originalLine.Substring(labelStartIndex);
                string configPart = originalLine.Substring(0, closingBracketIndex + 1);
                
                // Parse the configuration part
                if (!configPart.StartsWith("Scale [")) return originalLine;
                
                // Extract the parameters from [min=X, max=Y, step=Z, value=W]
                string paramsPart = configPart.Substring(7, configPart.Length - 8); // Remove "Scale [" and "]"
                var paramArray = paramsPart.Split(',');
                
                int min = 0, max = 100, step = 1;
                
                foreach (var param in paramArray)
                {
                    var trimmed = param.Trim();
                    if (trimmed.StartsWith("min="))
                        min = int.Parse(trimmed.Substring(4));
                    else if (trimmed.StartsWith("max="))
                        max = int.Parse(trimmed.Substring(4));
                    else if (trimmed.StartsWith("step="))
                        step = int.Parse(trimmed.Substring(5));
                }
                
                // Convert the float value (0-1) back to the actual range
                int actualValue = min + (int)(value * (max - min));
                
                // Apply step increments
                actualValue = (actualValue / step) * step;
                
                // Reconstruct the line
                return $"Scale [min={min}, max={max}, step={step}, value={actualValue}] {label}";
            }
            catch (Exception ex)
            {
                _engine.Log($"ModuleSettings: Error reconstructing slider line: {ex.Message}");
                return originalLine;
            }
        }

        private string ColorToHex(Color color)
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        private void OnResetToDefaults()
        {
            try
            {
                _engine.Log("ModuleSettings: Reset to Defaults called");
                
                // Load the default layout
                string defaultLayoutPath = Path.Combine(Directory.GetCurrentDirectory(), "Content", "Modules", "ModuleSettings_essential", "default_ui_layout.md");
                
                if (File.Exists(defaultLayoutPath))
                {
                    string defaultLayout = File.ReadAllText(defaultLayoutPath);
                    
                    // Copy default layout to current layout
                    string currentLayoutPath = Path.Combine(Directory.GetCurrentDirectory(), "Content", "Modules", "ModuleSettings_essential", "ui_layout.md");
                    File.WriteAllText(currentLayoutPath, defaultLayout);
                    
                    // Reload the UI elements
                    if (_uiElements != null)
                    {
                        _uiElements.LoadFromMarkdown(defaultLayout);
                    }
                    
                    _engine.Log("ModuleSettings: Reset to defaults completed successfully");
                }
                else
                {
                    _engine.Log("ModuleSettings: Default layout file not found");
                }
            }
            catch (Exception ex)
            {
                _engine.Log($"ModuleSettings: Error resetting to defaults: {ex.Message}");
            }
        }

        // Method to open the Module Settings window
        public void Open()
        {
            System.Diagnostics.Debug.WriteLine("ModuleSettings: Open() method called");
            
            // Set the window title first before making it visible
            _windowManagement.SetWindowTitle("Module Settings");
            
            if (_taskBar != null)
            {
                System.Diagnostics.Debug.WriteLine("ModuleSettings: TaskBar is available, ensuring icon exists");
                // Ensure TaskBar has an icon for this module with logo loading
                _taskBar.EnsureModuleIconExists("Module Settings", _content);
                // Ensure the module is not marked as minimized in TaskBar
                _taskBar.SetModuleMinimized("Module Settings", false);
                System.Diagnostics.Debug.WriteLine("ModuleSettings: Set module as not minimized in TaskBar");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("ModuleSettings: TaskBar is null!");
            }
            
            // Set the window to visible
            _windowManagement.SetVisible(true);
            System.Diagnostics.Debug.WriteLine("ModuleSettings: Window set to visible");
            
            // Bring to front
            _windowManagement.BringToFront();
            System.Diagnostics.Debug.WriteLine("ModuleSettings: Window brought to front");
            
            // Highlight the window
            _windowManagement.HandleTaskBarClick();
            System.Diagnostics.Debug.WriteLine("ModuleSettings: Window highlighted");
        }
        
        public void Close()
        {
            System.Diagnostics.Debug.WriteLine("ModuleSettings: Close() method called");
            
            // Remove icon from TaskBar
            if (_taskBar != null)
            {
                _taskBar.RemoveModuleIcon("Module Settings");
                System.Diagnostics.Debug.WriteLine("ModuleSettings: Removed icon from TaskBar");
            }
            
            // Set the window to invisible
            _windowManagement.SetVisible(false);
            System.Diagnostics.Debug.WriteLine("ModuleSettings: Window set to invisible");
        }

        public void ClearFocus()
        {
            _isFocused = false;
        }

        private void ClearFocusFromOtherModules()
        {
            // Get all active modules and clear focus from non-ModuleSettings modules
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
                            // Clear focus from Chat module
                            else if (module is MarySGameEngine.Modules.Chat.Chat chatModule)
                            {
                                chatModule.ClearFocus();
                            }
                        }
                    }
                }
            }
        }
    }
} 