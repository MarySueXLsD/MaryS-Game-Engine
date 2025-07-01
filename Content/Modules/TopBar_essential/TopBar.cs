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

        public DropdownItem(string text, string shortcut = "")
        {
            Text = text;
            Shortcut = shortcut;
        }
    }

    public class TopBar : IModule
    {
        // Window and UI dimensions
        private int _windowWidth;
        private int _dropdownItemHeight = 25;
        private int _buttonLeftPadding = 10; // Left padding for text
        private int _buttonRightPadding = 10; // Right padding for text
        private int _dropdownLeftPadding = 25; // Padding from left edge for dropdowns
        private Vector2 _menuStartPosition = new Vector2(10, 5);
        private int _buttonHeight = 35;     // Height of the button area
        private int _shortcutRightPadding = 10; // Padding from right edge for shortcuts
        private int _dropdownTextSpacing = 40; // Spacing between menu text and shortcut
        

        // Colors
        private Color _topBarColor;
        private Color _dropdownColor;
        private Color _hoverColor;
        private Color _buttonHoverColor = new Color(60, 60, 60); // Color for button hover state
        private Color _shortcutColor = new Color(150, 150, 150); // Gray color for shortcuts
        
        // Resources
        private SpriteFont _menuFont;
        private List<MenuItem> _menuItems;
        private Texture2D _pixel;
        private MouseState _currentMouseState;
        private MouseState _previousMouseState;

        public TopBar(GraphicsDevice graphicsDevice, SpriteFont menuFont, int windowWidth)
        {
            _menuFont = menuFont;
            _windowWidth = windowWidth;
            
            // Set colors
            _topBarColor = new Color(40, 40, 40); // Dark grey
            _dropdownColor = new Color(50, 50, 50); // Slightly lighter grey
            _hoverColor = new Color(70, 70, 70); // Even lighter grey for hover
            
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
                    modulesDropdownItems.Add(new DropdownItem(moduleInfo.Name, moduleInfo.Shortcut));
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

            foreach (var item in menuItem.DropdownItems)
            {
                float textWidth = _menuFont.MeasureString(item.Text).X;
                maxTextWidth = Math.Max(maxTextWidth, textWidth);

                if (!string.IsNullOrEmpty(item.Shortcut))
                {
                    float shortcutWidth = _menuFont.MeasureString(item.Shortcut).X;
                    maxShortcutWidth = Math.Max(maxShortcutWidth, shortcutWidth);
                }
            }

            // Total width = text width + spacing + shortcut width + left/right padding
            return (int)(maxTextWidth + _dropdownTextSpacing + maxShortcutWidth + 10);
        }

        public void Update()
        {
            _previousMouseState = _currentMouseState;
            _currentMouseState = Mouse.GetState();

            // Update menu items
            for (int menuIndex = 0; menuIndex < _menuItems.Count; menuIndex++)
            {
                var menuItem = _menuItems[menuIndex];
                // Check if mouse is over the button area
                menuItem.IsHovered = menuItem.ButtonBounds.Contains(_currentMouseState.Position);

                // Handle dropdown visibility
                if (menuItem.IsHovered && _currentMouseState.LeftButton == ButtonState.Released && 
                    _previousMouseState.LeftButton == ButtonState.Pressed)
                {
                    menuItem.IsDropdownVisible = !menuItem.IsDropdownVisible;
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

                // Close dropdown if clicking outside
                if (menuItem.IsDropdownVisible && _currentMouseState.LeftButton == ButtonState.Pressed && 
                    _previousMouseState.LeftButton == ButtonState.Released)
                {
                    bool clickedInside = false;
                    foreach (var bound in menuItem.DropdownBounds)
                    {
                        if (bound.Contains(_currentMouseState.Position))
                        {
                            clickedInside = true;
                            break;
                        }
                    }
                    if (!clickedInside && !menuItem.ButtonBounds.Contains(_currentMouseState.Position))
                    {
                        menuItem.IsDropdownVisible = false;
                    }
                }
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            // Draw top bar background
            spriteBatch.Draw(_pixel, new Rectangle(0, 0, _windowWidth, _buttonHeight), _topBarColor);

            // Draw menu items
            foreach (var menuItem in _menuItems)
            {
                // Draw button background if hovered
                if (menuItem.IsHovered)
                {
                    spriteBatch.Draw(_pixel, menuItem.ButtonBounds, _buttonHoverColor);
                }

                // Draw menu text
                Vector2 textSize = _menuFont.MeasureString(menuItem.Text);
                Vector2 textPosition = new Vector2(
                    menuItem.ButtonBounds.X + _buttonLeftPadding,
                    menuItem.ButtonBounds.Y + (_buttonHeight - textSize.Y) / 2
                );
                System.Diagnostics.Debug.WriteLine($"Drawing text '{menuItem.Text}' at {textPosition} with size {textSize} in bounds {menuItem.ButtonBounds}");
                spriteBatch.DrawString(_menuFont, menuItem.Text, textPosition, Color.White);
            }
        }

        public void DrawDropdowns(SpriteBatch spriteBatch)
        {
            // Draw dropdowns if visible
            foreach (var menuItem in _menuItems)
            {
                if (menuItem.IsDropdownVisible)
                {
                    for (int i = 0; i < menuItem.DropdownItems.Count; i++)
                    {
                        var bound = menuItem.DropdownBounds[i];
                        bool isHovered = bound.Contains(_currentMouseState.Position);
                        
                        // Draw dropdown background
                        spriteBatch.Draw(_pixel, bound, isHovered ? _hoverColor : _dropdownColor);
                        
                        // Draw dropdown text
                        Vector2 textPos = new Vector2(bound.X + 5, bound.Y + 5);
                        spriteBatch.DrawString(_menuFont, menuItem.DropdownItems[i].Text, textPos, Color.White);

                        // Draw shortcut if exists
                        if (!string.IsNullOrEmpty(menuItem.DropdownItems[i].Shortcut))
                        {
                            Vector2 shortcutPos = new Vector2(
                                bound.X + bound.Width - _shortcutRightPadding - _menuFont.MeasureString(menuItem.DropdownItems[i].Shortcut).X,
                                bound.Y + 5
                            );
                            spriteBatch.DrawString(_menuFont, menuItem.DropdownItems[i].Shortcut, shortcutPos, _shortcutColor);
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
            // No content to load for now
        }

        public void Dispose()
        {
            _pixel.Dispose();
        }
    }
} 