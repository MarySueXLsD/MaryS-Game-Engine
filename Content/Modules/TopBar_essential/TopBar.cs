using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Content;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;
using System;
using MarySGameEngine;
using MarySGameEngine.Modules.WindowManagement_essential;

namespace MarySGameEngine.Modules.TopBar_essential
{
    public class ModuleInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Shortcut { get; set; } = string.Empty;
        public string[] Dependencies { get; set; } = Array.Empty<string>();
        public string Category { get; set; } = string.Empty;
        public bool IsEssential { get; set; }
        public string MinEngineVersion { get; set; } = string.Empty;
        public string LastUpdated { get; set; } = string.Empty;
    }

    public class MenuItem
    {
        public string Text { get; set; }
        public List<DropdownItem> DropdownItems { get; set; }
        public Vector2 TextPosition { get; set; }
        public Rectangle ButtonBounds { get; set; }
        public bool IsHovered { get; set; }
        public bool IsDropdownVisible { get; set; }
        public List<Rectangle> DropdownBounds { get; set; }

        public MenuItem(string text, List<DropdownItem> dropdownItems, Vector2 textPosition)
        {
            Text = text;
            DropdownItems = dropdownItems;
            TextPosition = textPosition;
            IsHovered = false;
            IsDropdownVisible = false;
            DropdownBounds = new List<Rectangle>();
        }
    }

    public class DropdownItem
    {
        public string Text { get; set; }
        public string Shortcut { get; set; }
        public bool HasSettingsIcon { get; set; }

        public DropdownItem(string text, string shortcut = "", bool hasSettingsIcon = false)
        {
            Text = text;
            Shortcut = shortcut;
            HasSettingsIcon = hasSettingsIcon;
        }
    }

    public class TopBar : IModule
    {
        // Window and UI dimensions
        private int _windowWidth;
        private int _dropdownItemHeight = 35; // Increased from 30 to 35 to accommodate larger settings icon
        private int _buttonLeftPadding = 10; // Left padding for text
        private int _buttonRightPadding = 10; // Right padding for text
        private int _dropdownLeftPadding = 25; // Padding from left edge for dropdowns
        private Vector2 _menuStartPosition = new Vector2(10, 5);
        private int _buttonHeight = 35;     // Height of the button area
        private int _shortcutRightPadding = 15; // Increased from 10 to 15 for more padding
        private int _dropdownTextSpacing = 40; // Spacing between menu text and shortcut
        private const int SETTINGS_ICON_SIZE = 30; // Size of settings icon (same as dropdown item height minus small spacing)
        private const int SETTINGS_ICON_SPACING = 2; // Small spacing around settings icon
        

        // Colors
        private Color _topBarColor;
        private Color _dropdownColor;
        private Color _hoverColor;
        private Color _buttonHoverColor;
        private Color _shortcutColor = new Color(150, 150, 150); // Gray color for shortcuts
        
        // 80s Miami retro violet style colors
        private readonly Color MIAMI_PURPLE = new Color(147, 112, 219); // Main purple color
        private readonly Color MIAMI_PURPLE_DARK = new Color(100, 75, 150); // Darker purple
        private readonly Color MIAMI_PURPLE_LIGHT = new Color(180, 145, 250); // Lighter purple
        private readonly Color MIAMI_BACKGROUND = new Color(40, 40, 40); // Dark background
        private readonly Color MIAMI_BORDER = new Color(147, 112, 219); // Purple border
        private readonly Color MIAMI_SHADOW = new Color(0, 0, 0, 100); // Shadow color
        private readonly Color MIAMI_TEXT = new Color(220, 220, 220); // Light text
        private readonly Color MIAMI_HOVER = new Color(147, 112, 219, 180); // Semi-transparent purple hover
        
        // Resources
        private SpriteFont _menuFont;
        private SpriteFont _dropdownFont;
        private List<MenuItem> _menuItems;
        private Texture2D _pixel;
        private Texture2D _settingsIcon;
        private MouseState _currentMouseState;
        private MouseState _previousMouseState;
        private bool _isHoveringOverInteractive = false;
        // Highlight effect
        private bool _isHighlighted = false;
        private float _highlightTimer = 0f;
        private const float HIGHLIGHT_DURATION = 1.5f; // Match WindowManagement
        private const float HIGHLIGHT_BLINK_SPEED = 2.0f; // Match WindowManagement
        private const float HIGHLIGHT_MIN_ALPHA = 0.3f; // Match WindowManagement
        private const float HIGHLIGHT_MAX_ALPHA = 0.7f; // Match WindowManagement

        public TopBar(GraphicsDevice graphicsDevice, SpriteFont menuFont, SpriteFont dropdownFont, int windowWidth)
        {
            _menuFont = menuFont;
            _dropdownFont = dropdownFont;
            _windowWidth = windowWidth;
            
            // Set colors to match 80s Miami retro violet style
            _topBarColor = MIAMI_BACKGROUND; // Dark background
            _dropdownColor = MIAMI_BACKGROUND; // Dark background for dropdowns
            _hoverColor = MIAMI_HOVER; // Semi-transparent purple for hover
            _buttonHoverColor = MIAMI_HOVER; // Purple hover for buttons
            
            // Initialize menu items
            _menuItems = new List<MenuItem>();

            // Create a 1x1 white texture for drawing rectangles
            _pixel = new Texture2D(graphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });

            InitializeMenuItems();
        }

        private ModuleInfo LoadModuleInfo(string modulePath)
        {
            try
            {
                string jsonPath = Path.Combine(modulePath, "bridge.json");
                if (!File.Exists(jsonPath))
                {
                    System.Diagnostics.Debug.WriteLine($"bridge.json not found in {modulePath}");
                    return null;
                }

                string jsonContent = File.ReadAllText(jsonPath);
                if (string.IsNullOrEmpty(jsonContent))
                {
                    System.Diagnostics.Debug.WriteLine($"Empty bridge.json in {modulePath}");
                    return null;
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                };

                var moduleInfo = JsonSerializer.Deserialize<ModuleInfo>(jsonContent, options);
                
                if (moduleInfo == null)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to deserialize bridge.json in {modulePath}");
                    return null;
                }

                // Validate required fields
                if (string.IsNullOrEmpty(moduleInfo.Name))
                {
                    System.Diagnostics.Debug.WriteLine($"Module name is missing in {modulePath}");
                    return null;
                }

                return moduleInfo;
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"JSON parsing error in {modulePath}: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading module info from {modulePath}: {ex.Message}");
                return null;
            }
        }

        private void InitializeMenuItems()
        {
            _menuItems.Clear(); // Clear any existing items
            Vector2 currentTextPosition = _menuStartPosition;
            
            // Get all module directories
            string modulesPath = Path.Combine(Directory.GetCurrentDirectory(), "Content", "Modules");
            if (!Directory.Exists(modulesPath))
            {
                System.Diagnostics.Debug.WriteLine($"Modules directory not found at {modulesPath}");
                return;
            }

            var moduleDirectories = Directory.GetDirectories(modulesPath);
            System.Diagnostics.Debug.WriteLine($"Found {moduleDirectories.Length} module directories");

            // First pass: calculate button widths and positions
            List<float> buttonWidths = new List<float>();
            List<float> buttonStartPositions = new List<float>();
            float currentButtonX = _menuStartPosition.X;

            // Add standard menu items
            List<string> menuTexts = new List<string> { "Game", "Edit", "View", "Assets", "Modules", "Help" };
            
            foreach (var text in menuTexts)
            {
                float textWidth = _menuFont.MeasureString(text).X;
                float buttonWidth = textWidth + _buttonLeftPadding + _buttonRightPadding; // Add padding on both sides
                buttonWidths.Add(buttonWidth);
                buttonStartPositions.Add(currentButtonX);
                currentButtonX += buttonWidth; // No spacing between buttons
                System.Diagnostics.Debug.WriteLine($"Menu item '{text}': width={textWidth}, buttonWidth={buttonWidth}, x={currentButtonX}");
            }

            // Reset position for second pass
            currentTextPosition = _menuStartPosition;

            // Game menu
            var gameMenuItem = new MenuItem("Game", new List<DropdownItem>
            {
                new DropdownItem("New Scene", "Ctrl N"),
                new DropdownItem("Open Scene", "Ctrl O"),
                new DropdownItem("Save Scene", "Ctrl S"),
                new DropdownItem("Build Project", "Ctrl B"),
                new DropdownItem("Exit", "Alt F4")
            }, currentTextPosition);
            gameMenuItem.ButtonBounds = new Rectangle(
                (int)buttonStartPositions[0], 0, 
                (int)buttonWidths[0], 
                _buttonHeight);
            _menuItems.Add(gameMenuItem);
            currentTextPosition.X += buttonWidths[0];
            System.Diagnostics.Debug.WriteLine($"Game menu: textPos={currentTextPosition}, bounds={gameMenuItem.ButtonBounds}, textWidth={_menuFont.MeasureString("Game").X}");

            // Edit menu
            var editMenuItem = new MenuItem("Edit", new List<DropdownItem>
            {
                new DropdownItem("Undo", "Ctrl Z"),
                new DropdownItem("Redo", "Ctrl Y"),
                new DropdownItem("Cut", "Ctrl X"),
                new DropdownItem("Copy", "Ctrl C"),
                new DropdownItem("Paste", "Ctrl V"),
                new DropdownItem("Delete", "Del")
            }, currentTextPosition);
            editMenuItem.ButtonBounds = new Rectangle(
                (int)buttonStartPositions[1], 0, 
                (int)buttonWidths[1], 
                _buttonHeight);
            _menuItems.Add(editMenuItem);
            currentTextPosition.X += buttonWidths[1];
            System.Diagnostics.Debug.WriteLine($"Edit menu: textPos={currentTextPosition}, bounds={editMenuItem.ButtonBounds}");

            // View menu
            var viewMenuItem = new MenuItem("View", new List<DropdownItem>
            {
                new DropdownItem("Scene View", "Ctrl 1"),
                new DropdownItem("Game View", "Ctrl 2"),
                new DropdownItem("Inspector", "Ctrl 3"),
                new DropdownItem("Project", "Ctrl 4"),
                new DropdownItem("Console", "Ctrl 5")
            }, currentTextPosition);
            viewMenuItem.ButtonBounds = new Rectangle(
                (int)buttonStartPositions[2], 0, 
                (int)buttonWidths[2], 
                _buttonHeight);
            _menuItems.Add(viewMenuItem);
            currentTextPosition.X += buttonWidths[2];
            System.Diagnostics.Debug.WriteLine($"View menu: textPos={currentTextPosition}, bounds={viewMenuItem.ButtonBounds}");

            // Assets menu
            var assetsMenuItem = new MenuItem("Assets", new List<DropdownItem>
            {
                new DropdownItem("Import Asset", "Ctrl I"),
                new DropdownItem("Create Material", "Ctrl M"),
                new DropdownItem("Create Prefab", "Ctrl P"),
                new DropdownItem("Create Script", "Ctrl Shift N")
            }, currentTextPosition);
            assetsMenuItem.ButtonBounds = new Rectangle(
                (int)buttonStartPositions[3], 0, 
                (int)buttonWidths[3], 
                _buttonHeight);
            _menuItems.Add(assetsMenuItem);
            currentTextPosition.X += buttonWidths[3];
            System.Diagnostics.Debug.WriteLine($"Assets menu: textPos={currentTextPosition}, bounds={assetsMenuItem.ButtonBounds}");

            // Modules menu - dynamically load modules
            var modulesDropdownItems = new List<DropdownItem>();
            foreach (var moduleDir in moduleDirectories)
            {
                System.Diagnostics.Debug.WriteLine($"Loading module from {moduleDir}");
                var moduleInfo = LoadModuleInfo(moduleDir);
                if (moduleInfo != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Successfully loaded module: {moduleInfo.Name}");
                    modulesDropdownItems.Add(new DropdownItem(moduleInfo.Name, moduleInfo.Shortcut, true)); // Add settings icon for modules
                }
            }

            var modulesMenuItem = new MenuItem("Modules", modulesDropdownItems, currentTextPosition);
            modulesMenuItem.ButtonBounds = new Rectangle(
                (int)buttonStartPositions[4], 0, 
                (int)buttonWidths[4], 
                _buttonHeight);
            _menuItems.Add(modulesMenuItem);
            currentTextPosition.X += buttonWidths[4];
            System.Diagnostics.Debug.WriteLine($"Modules menu: textPos={currentTextPosition}, bounds={modulesMenuItem.ButtonBounds}");

            // Help menu
            var helpMenuItem = new MenuItem("Help", new List<DropdownItem>
            {
                new DropdownItem("Documentation", "F1"),
                new DropdownItem("Tutorials", "F2"),
                new DropdownItem("About", "")
            }, currentTextPosition);
            helpMenuItem.ButtonBounds = new Rectangle(
                (int)buttonStartPositions[5], 0, 
                (int)buttonWidths[5], 
                _buttonHeight);
            _menuItems.Add(helpMenuItem);
            System.Diagnostics.Debug.WriteLine($"Help menu: textPos={currentTextPosition}, bounds={helpMenuItem.ButtonBounds}");
        }

        private int CalculateDropdownWidth(MenuItem menuItem)
        {
            float maxTextWidth = 0;
            float maxShortcutWidth = 0;
            bool hasSettingsIcons = false;

            foreach (var item in menuItem.DropdownItems)
            {
                float textWidth = _dropdownFont.MeasureString(item.Text).X;
                maxTextWidth = Math.Max(maxTextWidth, textWidth);

                if (!string.IsNullOrEmpty(item.Shortcut))
                {
                    float shortcutWidth = _dropdownFont.MeasureString(item.Shortcut).X;
                    maxShortcutWidth = Math.Max(maxShortcutWidth, shortcutWidth);
                }

                if (item.HasSettingsIcon)
                {
                    hasSettingsIcons = true;
                }
            }

            // Total width = left padding + text width + spacing + shortcut width + settings icon space + right padding
            int leftPadding = 15; // Increased from implicit 10
            int rightPadding = 15; // Increased from implicit 10
            int settingsIconSpace = hasSettingsIcons ? SETTINGS_ICON_SIZE + SETTINGS_ICON_SPACING * 2 : 0; // Space for settings icon + spacing
            return (int)(leftPadding + maxTextWidth + _dropdownTextSpacing + maxShortcutWidth + settingsIconSpace + rightPadding);
        }

        public void Update()
        {
            _previousMouseState = _currentMouseState;
            _currentMouseState = Mouse.GetState();

            // Track if we're hovering over any interactive element
            bool wasHoveringOverInteractive = _isHoveringOverInteractive;
            _isHoveringOverInteractive = false;

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

            // Update menu items
            for (int menuIndex = 0; menuIndex < _menuItems.Count; menuIndex++)
            {
                var menuItem = _menuItems[menuIndex];
                // Check if mouse is over the button area
                menuItem.IsHovered = menuItem.ButtonBounds.Contains(_currentMouseState.Position);

                // Check for interactive elements
                if (menuItem.IsHovered)
                {
                    _isHoveringOverInteractive = true;
                }

                // Check for dropdown items if dropdown is visible
                if (menuItem.IsDropdownVisible)
                {
                    for (int i = 0; i < menuItem.DropdownBounds.Count; i++)
                    {
                        var bound = menuItem.DropdownBounds[i];
                        if (bound.Contains(_currentMouseState.Position))
                        {
                            _isHoveringOverInteractive = true;
                            break;
                        }
                    }
                }

                // Handle dropdown visibility
                if (menuItem.IsHovered && _currentMouseState.LeftButton == ButtonState.Released && 
                    _previousMouseState.LeftButton == ButtonState.Pressed)
                {
                    menuItem.IsDropdownVisible = !menuItem.IsDropdownVisible;
                    System.Diagnostics.Debug.WriteLine($"TopBar: Toggled dropdown for {menuItem.Text} to {menuItem.IsDropdownVisible}");
                }

                // Calculate dropdown bounds
                menuItem.DropdownBounds.Clear();
                if (menuItem.IsDropdownVisible)
                {
                    // Apply left padding only to the Game menu (first item)
                    int leftPadding = menuIndex == 0 ? _dropdownLeftPadding : 0;
                    int dropdownWidth = CalculateDropdownWidth(menuItem);
                    
                    for (int i = 0; i < menuItem.DropdownItems.Count; i++)
                    {
                        menuItem.DropdownBounds.Add(new Rectangle(
                            menuItem.ButtonBounds.X + leftPadding,
                            menuItem.ButtonBounds.Y + menuItem.ButtonBounds.Height + (i * _dropdownItemHeight),
                            dropdownWidth,
                            _dropdownItemHeight
                        ));
                    }
                }

                // Handle clicks on dropdown items and settings icons
                if (menuItem.IsDropdownVisible && _currentMouseState.LeftButton == ButtonState.Pressed && 
                    _previousMouseState.LeftButton == ButtonState.Released)
                {
                    System.Diagnostics.Debug.WriteLine($"TopBar: Dropdown click detected for menu: {menuItem.Text}");
                    System.Diagnostics.Debug.WriteLine($"TopBar: Mouse position: {_currentMouseState.Position}");
                    
                    bool clickedInside = false;
                    for (int i = 0; i < menuItem.DropdownBounds.Count; i++)
                    {
                        var bound = menuItem.DropdownBounds[i];
                        System.Diagnostics.Debug.WriteLine($"TopBar: Checking dropdown bound {i}: {bound}");
                        
                        if (bound.Contains(_currentMouseState.Position))
                        {
                            clickedInside = true;
                            _isHoveringOverInteractive = true;
                            System.Diagnostics.Debug.WriteLine($"TopBar: Click inside dropdown bound {i} for item: {menuItem.DropdownItems[i].Text}");
                            
                            // Set the flag to prevent other modules from processing this click
                            GameEngine.Instance.SetTopBarHandledClick(true);
                            
                            // Check if click was on settings icon
                            if (menuItem.DropdownItems[i].HasSettingsIcon && _settingsIcon != null)
                            {
                                // Calculate settings icon bounds with new positioning
                                Rectangle settingsIconBounds = new Rectangle(
                                    (int)(bound.X + bound.Width - SETTINGS_ICON_SIZE - SETTINGS_ICON_SPACING),
                                    bound.Y + SETTINGS_ICON_SPACING,
                                    SETTINGS_ICON_SIZE,
                                    SETTINGS_ICON_SIZE
                                );
                                
                                System.Diagnostics.Debug.WriteLine($"TopBar: Settings icon bounds: {settingsIconBounds}");
                                System.Diagnostics.Debug.WriteLine($"TopBar: HasSettingsIcon: {menuItem.DropdownItems[i].HasSettingsIcon}, SettingsIcon: {_settingsIcon != null}");
                                
                                if (settingsIconBounds.Contains(_currentMouseState.Position))
                                {
                                    // Settings icon was clicked
                                    System.Diagnostics.Debug.WriteLine($"TopBar: Settings icon clicked for module: {menuItem.DropdownItems[i].Text}");
                                    System.Diagnostics.Debug.WriteLine($"TopBar: Settings icon bounds: {settingsIconBounds}, Mouse position: {_currentMouseState.Position}");
                                    OpenModuleSettings(menuItem.DropdownItems[i].Text);
                                    // Close the dropdown after handling the settings action
                                    menuItem.IsDropdownVisible = false;
                                    break;
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"TopBar: Click not on settings icon for {menuItem.DropdownItems[i].Text}");
                                }
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"TopBar: No settings icon for {menuItem.DropdownItems[i].Text}");
                            }
                            
                            // Regular dropdown item was clicked
                            System.Diagnostics.Debug.WriteLine($"TopBar: Regular dropdown item clicked: {menuItem.DropdownItems[i].Text}");
                            
                            // Handle module action
                            HandleModuleAction(menuItem.DropdownItems[i].Text);
                            
                            // Close the dropdown after handling the action
                            menuItem.IsDropdownVisible = false;
                            break;
                        }
                    }
                    if (!clickedInside && !menuItem.ButtonBounds.Contains(_currentMouseState.Position))
                    {
                        System.Diagnostics.Debug.WriteLine($"TopBar: Click outside dropdown, closing");
                        menuItem.IsDropdownVisible = false;
                    }
                }
            }

            // Update cursor based on hover state
            UpdateCursor(wasHoveringOverInteractive);
        }

        private void UpdateCursor(bool wasHoveringOverInteractive)
        {
            try
            {
                // Only change cursor if the hover state actually changed
                if (_isHoveringOverInteractive != wasHoveringOverInteractive)
                {
                    if (_isHoveringOverInteractive)
                    {
                        // Request hand cursor when hovering over interactive elements
                        GameEngine.Instance.RequestHandCursor();
                        System.Diagnostics.Debug.WriteLine("TopBar: Requested hand cursor (hovering over interactive element)");
                    }
                    else
                    {
                        // Release hand cursor when not hovering over interactive elements
                        GameEngine.Instance.ReleaseHandCursor();
                        System.Diagnostics.Debug.WriteLine("TopBar: Released hand cursor (not hovering over interactive element)");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TopBar: Error updating cursor: {ex.Message}");
            }
        }

        private void HandleModuleAction(string moduleName)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"TopBar: Handling action for module: {moduleName}");
                
                // Get the game engine instance
                var engine = GameEngine.Instance;
                
                // Handle special cases first
                switch (moduleName)
                {
                    case "Desktop":
                        // Highlight the desktop
                        HighlightDesktop();
                        return;
                        
                    case "Window Management":
                        // Highlight all currently active and open windows
                        HighlightAllWindows();
                        return;
                        
                    case "Task Bar":
                        // Highlight the taskbar
                        HighlightTaskBar();
                        return;
                        
                    case "Top Bar":
                        // Highlight the topbar area
                        HighlightTopBar();
                        return;
                        
                    case "Module Settings":
                        // Open the Module Settings window
                        System.Diagnostics.Debug.WriteLine($"TopBar: Opening Module Settings from HandleModuleAction");
                        OpenModuleSettings(moduleName);
                        return;
                }
                
                // For regular modules, find their WindowManagement and handle accordingly
                var windowManagement = GetWindowManagementForModule(moduleName);
                if (windowManagement != null)
                {
                    System.Diagnostics.Debug.WriteLine($"TopBar: Found window management for {moduleName}");
                    
                    // Check if the window is currently visible and active
                    if (windowManagement.IsVisible())
                    {
                        // Window is open - highlight it (same as TaskBar click)
                        System.Diagnostics.Debug.WriteLine($"TopBar: Window {moduleName} is open, highlighting it");
                        windowManagement.HandleTaskBarClick();
                    }
                    else
                    {
                        // Window is closed - open it
                        System.Diagnostics.Debug.WriteLine($"TopBar: Window {moduleName} is closed, opening it");
                        OpenModule(moduleName, windowManagement);
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"TopBar: No window management found for {moduleName}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TopBar: Error handling module action for {moduleName}: {ex.Message}");
            }
        }

        private WindowManagement GetWindowManagementForModule(string moduleName)
        {
            try
            {
                // Get all modules from the game engine
                var modules = GameEngine.Instance.GetActiveModules();
                System.Diagnostics.Debug.WriteLine($"TopBar: Searching for window management for {moduleName} among {modules.Count} modules");

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
                            System.Diagnostics.Debug.WriteLine($"TopBar: Found window management with title: {windowTitle}");
                            
                            if (windowTitle == moduleName)
                            {
                                System.Diagnostics.Debug.WriteLine($"TopBar: Found matching window management for {moduleName}");
                                return windowManagement;
                            }
                        }
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"TopBar: No window management found for {moduleName}");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TopBar: Error finding window management: {ex.Message}");
                return null;
            }
        }

        private void OpenModule(string moduleName, WindowManagement windowManagement)
        {
            try
            {
                // Set the window to visible
                windowManagement.SetVisible(true);
                
                // Bring to front
                windowManagement.BringToFront();
                
                // Highlight the window
                windowManagement.HandleTaskBarClick();
                
                // Ensure TaskBar has an icon for this module
                var modules = GameEngine.Instance.GetActiveModules();
                foreach (var module in modules)
                {
                    if (module is MarySGameEngine.Modules.TaskBar_essential.TaskBar taskBar)
                    {
                        taskBar.EnsureModuleIconExists(moduleName);
                        break;
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"TopBar: Successfully opened module {moduleName}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TopBar: Error opening module {moduleName}: {ex.Message}");
            }
        }

        private void HighlightDesktop()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("TopBar: Highlighting desktop");
                // Find the Desktop module and trigger a highlight effect
                var modules = GameEngine.Instance.GetActiveModules();
                foreach (var module in modules)
                {
                    if (module is MarySGameEngine.Modules.Desktop_essential.Desktop desktop)
                    {
                        desktop.Highlight();
                        System.Diagnostics.Debug.WriteLine("TopBar: Found Desktop module for highlighting");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TopBar: Error highlighting desktop: {ex.Message}");
            }
        }

        private void HighlightAllWindows()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("TopBar: Highlighting all windows");
                
                // Find all WindowManagement instances and highlight them
                var modules = GameEngine.Instance.GetActiveModules();
                foreach (var module in modules)
                {
                    var windowManagementField = module.GetType().GetField("_windowManagement", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (windowManagementField != null)
                    {
                        var windowManagement = windowManagementField.GetValue(module) as WindowManagement;
                        if (windowManagement != null && windowManagement.IsVisible())
                        {
                            // Highlight each visible window
                            windowManagement.HandleTaskBarClick();
                            System.Diagnostics.Debug.WriteLine($"TopBar: Highlighted window: {windowManagement.GetWindowTitle()}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TopBar: Error highlighting all windows: {ex.Message}");
            }
        }

        private void HighlightTaskBar()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("TopBar: Highlighting taskbar");
                // Find the TaskBar module and trigger a highlight effect
                var modules = GameEngine.Instance.GetActiveModules();
                foreach (var module in modules)
                {
                    if (module is MarySGameEngine.Modules.TaskBar_essential.TaskBar taskBar)
                    {
                        taskBar.Highlight();
                        System.Diagnostics.Debug.WriteLine("TopBar: Found TaskBar module for highlighting");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TopBar: Error highlighting taskbar: {ex.Message}");
            }
        }

        private void HighlightTopBar()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("TopBar: Highlighting topbar");
                this.Highlight();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TopBar: Error highlighting topbar: {ex.Message}");
            }
        }

        private void OpenModuleSettings(string moduleName)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"TopBar: Opening settings for module: {moduleName}");
                
                // Find the ModuleSettings module
                var modules = GameEngine.Instance.GetActiveModules();
                System.Diagnostics.Debug.WriteLine($"TopBar: Found {modules.Count} active modules");
                
                foreach (var module in modules)
                {
                    System.Diagnostics.Debug.WriteLine($"TopBar: Checking module type: {module.GetType().FullName}");
                    if (module is MarySGameEngine.Modules.ModuleSettings_essential.ModuleSettings moduleSettings)
                    {
                        System.Diagnostics.Debug.WriteLine($"TopBar: Found ModuleSettings module, calling Open()");
                        // Use the new Open() method
                        moduleSettings.Open();
                        System.Diagnostics.Debug.WriteLine($"TopBar: Successfully opened Module Settings for {moduleName}");
                        return;
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"TopBar: ModuleSettings module not found among {modules.Count} modules");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TopBar: Error opening Module Settings for {moduleName}: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"TopBar: Stack trace: {ex.StackTrace}");
            }
        }

        public void Highlight()
        {
            _isHighlighted = true;
            _highlightTimer = 0f;
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            // Draw top bar background with Miami style
            spriteBatch.Draw(_pixel, new Rectangle(0, 0, _windowWidth, _buttonHeight), _topBarColor);

            // Draw top bar border (2px thick like windows)
            const int BORDER_THICKNESS = 2;
            // Bottom border only for top bar
            spriteBatch.Draw(_pixel, new Rectangle(0, _buttonHeight - BORDER_THICKNESS, _windowWidth, BORDER_THICKNESS), MIAMI_BORDER);

            // Draw menu items
            foreach (var menuItem in _menuItems)
            {
                // Draw button background if hovered
                if (menuItem.IsHovered)
                {
                    spriteBatch.Draw(_pixel, menuItem.ButtonBounds, _buttonHoverColor);
                }

                // Draw menu text with Miami style
                Vector2 textSize = _menuFont.MeasureString(menuItem.Text);
                Vector2 textPosition = new Vector2(
                    menuItem.ButtonBounds.X + _buttonLeftPadding,
                    menuItem.ButtonBounds.Y + (_buttonHeight - textSize.Y) / 2
                );
                System.Diagnostics.Debug.WriteLine($"Drawing text '{menuItem.Text}' at {textPosition} with size {textSize} in bounds {menuItem.ButtonBounds}");
                spriteBatch.DrawString(_menuFont, menuItem.Text, textPosition, MIAMI_TEXT);
            }
        }

        public void DrawHighlight(SpriteBatch spriteBatch)
        {
            // Draw highlight border if needed
            if (_isHighlighted)
            {
                float pulseValue = (float)(Math.Sin(_highlightTimer * Math.PI * 2 * HIGHLIGHT_BLINK_SPEED) + 1) / 2;
                float alpha = MathHelper.Lerp(HIGHLIGHT_MIN_ALPHA, HIGHLIGHT_MAX_ALPHA, pulseValue);
                Color highlightColor = new Color((byte)147, (byte)112, (byte)219, (byte)(255 * alpha));
                int borderThickness = 4;
                // Top
                spriteBatch.Draw(_pixel, new Rectangle(0, 0, _windowWidth, borderThickness), highlightColor);
                // Bottom
                spriteBatch.Draw(_pixel, new Rectangle(0, _buttonHeight - borderThickness, _windowWidth, borderThickness), highlightColor);
                // Left
                spriteBatch.Draw(_pixel, new Rectangle(0, 0, borderThickness, _buttonHeight), highlightColor);
                // Right
                spriteBatch.Draw(_pixel, new Rectangle(_windowWidth - borderThickness, 0, borderThickness, _buttonHeight), highlightColor);
            }
        }

        public void DrawDropdowns(SpriteBatch spriteBatch)
        {
            // Draw dropdowns if visible
            foreach (var menuItem in _menuItems)
            {
                if (menuItem.IsDropdownVisible)
                {
                    // Calculate dropdown bounds for shadow and border
                    int dropdownWidth = CalculateDropdownWidth(menuItem);
                    int dropdownHeight = menuItem.DropdownItems.Count * _dropdownItemHeight;
                    Rectangle dropdownBounds = new Rectangle(
                        menuItem.ButtonBounds.X,
                        menuItem.ButtonBounds.Y + menuItem.ButtonBounds.Height,
                        dropdownWidth,
                        dropdownHeight
                    );

                    // Draw dropdown shadow
                    Rectangle shadowBounds = new Rectangle(
                        dropdownBounds.X + 2,
                        dropdownBounds.Y + 2,
                        dropdownBounds.Width,
                        dropdownBounds.Height
                    );
                    spriteBatch.Draw(_pixel, shadowBounds, MIAMI_SHADOW);

                    // Draw dropdown background
                    spriteBatch.Draw(_pixel, dropdownBounds, MIAMI_BACKGROUND);

                    // Draw dropdown border (2px thick like windows)
                    const int BORDER_THICKNESS = 2;
                    // Top border
                    spriteBatch.Draw(_pixel, new Rectangle(dropdownBounds.X - BORDER_THICKNESS, dropdownBounds.Y - BORDER_THICKNESS, 
                        dropdownBounds.Width + (BORDER_THICKNESS * 2), BORDER_THICKNESS), MIAMI_BORDER);
                    // Bottom border
                    spriteBatch.Draw(_pixel, new Rectangle(dropdownBounds.X - BORDER_THICKNESS, dropdownBounds.Bottom, 
                        dropdownBounds.Width + (BORDER_THICKNESS * 2), BORDER_THICKNESS), MIAMI_BORDER);
                    // Left border
                    spriteBatch.Draw(_pixel, new Rectangle(dropdownBounds.X - BORDER_THICKNESS, dropdownBounds.Y - BORDER_THICKNESS, 
                        BORDER_THICKNESS, dropdownBounds.Height + (BORDER_THICKNESS * 2)), MIAMI_BORDER);
                    // Right border
                    spriteBatch.Draw(_pixel, new Rectangle(dropdownBounds.Right, dropdownBounds.Y - BORDER_THICKNESS, 
                        BORDER_THICKNESS, dropdownBounds.Height + (BORDER_THICKNESS * 2)), MIAMI_BORDER);

                    for (int i = 0; i < menuItem.DropdownItems.Count; i++)
                    {
                        var bound = menuItem.DropdownBounds[i];
                        bool isHovered = bound.Contains(_currentMouseState.Position);
                        
                        // Draw dropdown item background if hovered
                        if (isHovered)
                        {
                            spriteBatch.Draw(_pixel, bound, MIAMI_HOVER);
                        }
                        
                        // Draw dropdown text with proper padding and vertical centering
                        Vector2 textSize = _dropdownFont.MeasureString(menuItem.DropdownItems[i].Text);
                        Vector2 textPos = new Vector2(
                            bound.X + 15, // Increased left padding from 10 to 15
                            bound.Y + (bound.Height - textSize.Y) / 2 // Center vertically
                        );
                        spriteBatch.DrawString(_dropdownFont, menuItem.DropdownItems[i].Text, textPos, MIAMI_TEXT);

                        // Calculate settings icon position (after shortcuts)
                        Rectangle settingsIconBounds = Rectangle.Empty;
                        if (menuItem.DropdownItems[i].HasSettingsIcon && _settingsIcon != null)
                        {
                            // Position the settings icon to fill the right side of the dropdown element
                            // with minimal spacing from the borders
                            int iconX = bound.X + bound.Width - SETTINGS_ICON_SIZE - SETTINGS_ICON_SPACING;
                            int iconY = bound.Y + SETTINGS_ICON_SPACING;
                            
                            settingsIconBounds = new Rectangle(
                                iconX,
                                iconY,
                                SETTINGS_ICON_SIZE,
                                SETTINGS_ICON_SIZE
                            );
                        }

                        // Draw shortcut if exists with proper padding and vertical centering
                        if (!string.IsNullOrEmpty(menuItem.DropdownItems[i].Shortcut))
                        {
                            Vector2 shortcutSize = _dropdownFont.MeasureString(menuItem.DropdownItems[i].Shortcut);
                            Vector2 shortcutPos = new Vector2(
                                bound.X + bound.Width - _shortcutRightPadding - shortcutSize.X - (menuItem.DropdownItems[i].HasSettingsIcon ? SETTINGS_ICON_SIZE + SETTINGS_ICON_SPACING * 2 : 0),
                                bound.Y + (bound.Height - shortcutSize.Y) / 2 // Center vertically
                            );
                            spriteBatch.DrawString(_dropdownFont, menuItem.DropdownItems[i].Shortcut, shortcutPos, _shortcutColor);
                        }

                        // Draw settings icon if item has one
                        if (menuItem.DropdownItems[i].HasSettingsIcon && _settingsIcon != null && settingsIconBounds != Rectangle.Empty)
                        {
                            // Check if mouse is hovering over the settings icon specifically
                            bool isSettingsIconHovered = settingsIconBounds.Contains(_currentMouseState.Position);
                            
                            // Draw settings icon background if hovered
                            if (isSettingsIconHovered)
                            {
                                spriteBatch.Draw(_pixel, settingsIconBounds, MIAMI_HOVER);
                            }
                            
                            // Draw settings icon
                            spriteBatch.Draw(_settingsIcon, settingsIconBounds, Color.White);
                            
                            // Debug: Log settings icon drawing occasionally
                            if (_currentMouseState.Position.X % 100 == 0 && _currentMouseState.Position.Y % 100 == 0)
                            {
                                System.Diagnostics.Debug.WriteLine($"TopBar: Drawing settings icon for {menuItem.DropdownItems[i].Text} at {settingsIconBounds}");
                            }
                        }
                        else
                        {
                            // Debug: Log when settings icon is not drawn
                            if (_currentMouseState.Position.X % 200 == 0 && _currentMouseState.Position.Y % 200 == 0)
                            {
                                System.Diagnostics.Debug.WriteLine($"TopBar: Not drawing settings icon for {menuItem.DropdownItems[i].Text} - HasSettingsIcon: {menuItem.DropdownItems[i].HasSettingsIcon}, SettingsIcon: {_settingsIcon != null}, Bounds: {settingsIconBounds}");
                            }
                        }
                    }
                }
            }
        }

        public void UpdateWindowWidth(int newWidth)
        {
            _windowWidth = newWidth;
            InitializeMenuItems();
        }

        public void LoadContent(ContentManager content)
        {
            // Load settings icon from WindowManagement module
            _settingsIcon = content.Load<Texture2D>("Modules/WindowManagement_essential/settings");
        }

        public void Dispose()
        {
            // Reset cursor when disposing
            ResetCursor();
            
            _pixel.Dispose();
            _settingsIcon?.Dispose();
        }

        public void ResetCursor()
        {
            try
            {
                GameEngine.Instance.ReleaseHandCursor();
                _isHoveringOverInteractive = false;
                System.Diagnostics.Debug.WriteLine("TopBar: Reset cursor to arrow");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TopBar: Error resetting cursor: {ex.Message}");
            }
        }

        public List<MenuItem> GetMenuItems()
        {
            return _menuItems;
        }
    }
} 