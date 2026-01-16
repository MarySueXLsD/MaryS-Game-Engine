using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using MarySGameEngine;
using MarySGameEngine.Modules.WindowManagement_essential;
using MarySGameEngine.Modules.TaskBar_essential;

namespace MarySGameEngine.Modules.CharacterCreation
{
    public class CharacterCreation : IModule
    {
        private WindowManagement _windowManagement;
        private GraphicsDevice _graphicsDevice;
        private SpriteFont _menuFont; // Used for left panel categories and window title bar
        private SpriteFont _pixelFont;
        private SpriteFont _uiFont; // Used for most UI elements (character name, stats, traits, etc.)
        private int _windowWidth;
        private TaskBar _taskBar;
        private Texture2D _pixel;
        private ContentManager _content;
        private MouseState _currentMouseState;
        private MouseState _previousMouseState;
        private GameEngine _engine;
        private bool _hasShownError = false; // Prevent showing error messages repeatedly

        // Scrolling properties
        private int _scrollY = 0;
        private int _contentHeight = 0;
        private bool _needsScrollbar = false;
        private Rectangle _scrollbarBounds = Rectangle.Empty;
        private bool _isDraggingScrollbar = false;
        private Vector2 _scrollbarDragStart;
        private const int SCROLLBAR_WIDTH = 16;
        private const int SCROLLBAR_PADDING = 2;
        private bool _isHoveringScrollbar = false;
        private bool _scrollingEnabled = true;

        // Panel bounds
        private Rectangle _leftPanelBounds = Rectangle.Empty;      // Asset Browser
        private Rectangle _centerPanelBounds = Rectangle.Empty;    // Character Details
        private Rectangle _rightPanelBounds = Rectangle.Empty;      // Inspector

        // Left Panel - Asset Browser
        private Rectangle _searchBarBounds = Rectangle.Empty;
        private string _searchText = "Search Assets...";
        private bool _isSearchFocused = false;
        private List<string> _assetCategories = new List<string> { "All", "Characters", "Traits", "Abilities", "Cards", "Effects" };
        private int _selectedCategoryIndex = 0;
        private List<Rectangle> _categoryButtonBounds = new List<Rectangle>();
        private List<TraitItem> _availableTraits = new List<TraitItem>();
        private List<TraitItem> _filteredTraits = new List<TraitItem>();
        private bool _isTraitsExpanded = true;
        private int _traitScrollOffset = 0;
        private const int TRAIT_ITEM_HEIGHT = 30;

        // Center Panel - Character Details
        private string _characterName = "Knight_Warrior";
        private string _characterTags = "Human, Melee";
        private bool _isEditingName = false;
        private bool _isEditingTags = false;
        private Rectangle _nameInputBounds = Rectangle.Empty;
        private Rectangle _tagsInputBounds = Rectangle.Empty;
        private Rectangle _characterImageBounds = Rectangle.Empty;
        private Texture2D _characterImagePlaceholder;
        private List<StatItem> _baseStats = new List<StatItem>();
        private List<TraitItem> _characterTraits = new List<TraitItem>();
        private List<AbilityItem> _characterAbilities = new List<AbilityItem>();

        // Right Panel - Inspector
        private Rectangle _inspectorHeaderBounds = Rectangle.Empty;
        private Rectangle _referencesSectionBounds = Rectangle.Empty;
        private Rectangle _linkedTraitsSectionBounds = Rectangle.Empty;
        private bool _isReferencesExpanded = true;
        private bool _isLinkedTraitsExpanded = true;

        // Colors
        private readonly Color PANEL_BACKGROUND = new Color(40, 40, 40);
        private readonly Color PANEL_BORDER = new Color(60, 60, 60);
        private readonly Color SEARCH_BACKGROUND = new Color(30, 30, 30);
        private readonly Color SEARCH_BORDER = new Color(80, 80, 80);
        private readonly Color BUTTON_COLOR = new Color(50, 50, 50);
        private readonly Color BUTTON_HOVER = new Color(70, 70, 70);
        private readonly Color BUTTON_ACTIVE = new Color(147, 112, 219);
        private readonly Color TEXT_COLOR = new Color(220, 220, 220);
        private readonly Color TEXT_SECONDARY = new Color(150, 150, 150);
        private readonly Color SECTION_HEADER = new Color(60, 60, 60);
        private readonly Color STAT_BAR_BACKGROUND = new Color(30, 30, 30);
        private readonly Color STAT_BAR_FILL = new Color(147, 112, 219);

        // Panel widths
        private const int LEFT_PANEL_WIDTH = 250;
        private const int RIGHT_PANEL_WIDTH = 300;
        private const int PANEL_PADDING = 10;
        private const int SECTION_SPACING = 20;

        // Helper classes
        private class TraitItem
        {
            public string Name { get; set; }
            public bool IsChecked { get; set; }
            public string Icon { get; set; } // Placeholder for icon type
        }

        private class StatItem
        {
            public string Name { get; set; }
            public int Value { get; set; }
            public int MaxValue { get; set; }
        }

        private class AbilityItem
        {
            public string Name { get; set; }
            public string Icon { get; set; }
        }

        public CharacterCreation(GraphicsDevice graphicsDevice, SpriteFont menuFont, int windowWidth)
        {
            _graphicsDevice = graphicsDevice;
            _menuFont = menuFont;
            _pixelFont = menuFont; // Will be updated in LoadContent
            _windowWidth = windowWidth;
            _engine = GameEngine.Instance;

            // Create pixel texture
            _pixel = new Texture2D(graphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });

            // Initialize window management
            var properties = new WindowProperties
            {
                IsVisible = false,
                IsMovable = true,
                IsResizable = true
            };

            _windowManagement = new WindowManagement(graphicsDevice, menuFont, windowWidth, properties);
            _windowManagement.SetWindowTitle("Character Creation");
            
            // Set default size using reflection (like Chat and Console modules do)
            var defaultWidthField = _windowManagement.GetType().GetField("_defaultWidth", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var defaultHeightField = _windowManagement.GetType().GetField("_defaultHeight", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (defaultWidthField != null)
                defaultWidthField.SetValue(_windowManagement, 1400);
            if (defaultHeightField != null)
                defaultHeightField.SetValue(_windowManagement, 350);
            
            // Also call SetDefaultSize to ensure UpdateWindowBounds is called
            _windowManagement.SetDefaultSize(1400, 350);
            _windowManagement.SetCustomMinimumSize(1000, 600);
            _windowManagement.SetPosition(new Vector2(100, 50)); // Set initial position
            _windowManagement.SetVisible(false); // Explicitly set to not visible on startup

            // Initialize sample data
            InitializeSampleData();
        }

        private void InitializeSampleData()
        {
            // Initialize available traits
            _availableTraits = new List<TraitItem>
            {
                new TraitItem { Name = "Berserker Rage", IsChecked = true, Icon = "fist" },
                new TraitItem { Name = "Sharpshooter", IsChecked = true, Icon = "target" },
                new TraitItem { Name = "Arcane Adept", IsChecked = false, Icon = "magic" }
            };
            _filteredTraits = _availableTraits.ToList();

            // Initialize base stats
            _baseStats = new List<StatItem>
            {
                new StatItem { Name = "Strength", Value = 7, MaxValue = 10 },
                new StatItem { Name = "Dexterity", Value = 5, MaxValue = 10 },
                new StatItem { Name = "Intelligence", Value = 3, MaxValue = 10 }
            };

            // Initialize character traits
            _characterTraits = new List<TraitItem>
            {
                new TraitItem { Name = "Battle Hardened", IsChecked = true, Icon = "crown" },
                new TraitItem { Name = "Heavy Hitter", IsChecked = true, Icon = "fist" }
            };

            // Initialize character abilities
            _characterAbilities = new List<AbilityItem>
            {
                new AbilityItem { Name = "Power Strike", Icon = "fist" },
                new AbilityItem { Name = "Shield Bash", Icon = "shield" },
                new AbilityItem { Name = "Chainmail Armor", Icon = "armor" }
            };
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

                bool wasVisible = _windowManagement?.IsVisible() ?? false;
                _windowManagement?.Update();
                bool isVisible = _windowManagement?.IsVisible() ?? false;

                // If window just became visible, reset size and position to defaults
                if (!wasVisible && isVisible)
                {
                    // Force reset to default size
                    var defaultWidthField = _windowManagement.GetType().GetField("_defaultWidth", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var defaultHeightField = _windowManagement.GetType().GetField("_defaultHeight", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (defaultWidthField != null)
                        defaultWidthField.SetValue(_windowManagement, 1400);
                    if (defaultHeightField != null)
                        defaultHeightField.SetValue(_windowManagement, 350);
                    
                    _windowManagement.SetDefaultSize(1400, 350);
                    _windowManagement.SetCustomMinimumSize(1000, 600);
                    _windowManagement.SetPosition(new Vector2(100, 50));
                    UpdateBounds();
                }

                if (_windowManagement == null || !isVisible)
                    return;

                UpdateBounds();
                UpdateScrolling();
                HandleScrollbarInteraction();
            }
            catch (Exception ex)
            {
                _engine?.Log($"CharacterCreation: Error in Update: {ex.Message}");
                _engine?.Log($"CharacterCreation: Stack trace: {ex.StackTrace}");
                
                // Show error message
                try
                {
                    var flashMessageType = typeof(MarySGameEngine.Modules.FlashMessage_essential.FlashMessage);
                    var showMethod = flashMessageType.GetMethod("Show", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (showMethod != null)
                    {
                        showMethod.Invoke(null, new object[] { $"Character Creation Error: {ex.Message}", 
                            MarySGameEngine.Modules.FlashMessage_essential.FlashMessageType.Warning, 5.0f });
                    }
                }
                catch { }
            }
        }

        private void UpdateBounds()
        {
            if (_windowManagement == null) return;

            try
            {
                var windowBounds = _windowManagement.GetWindowBounds();
                
                // Validate window bounds
                if (windowBounds.Width <= 0 || windowBounds.Height <= 0)
                    return;
                
                int titleBarHeight = 40;
                int contentY = windowBounds.Y + titleBarHeight;
                int contentHeight = windowBounds.Height - titleBarHeight;
                
                // Ensure content height is valid
                if (contentHeight <= 0)
                    contentHeight = 100; // Minimum height

            // Left Panel - Asset Browser
            _leftPanelBounds = new Rectangle(
                windowBounds.X + 2,
                contentY,
                LEFT_PANEL_WIDTH,
                contentHeight
            );

            // Right Panel - Inspector
            _rightPanelBounds = new Rectangle(
                windowBounds.Right - RIGHT_PANEL_WIDTH - 2,
                contentY,
                RIGHT_PANEL_WIDTH,
                contentHeight
            );

            // Center Panel - Character Details
            _centerPanelBounds = new Rectangle(
                _leftPanelBounds.Right,
                contentY,
                windowBounds.Width - LEFT_PANEL_WIDTH - RIGHT_PANEL_WIDTH - 4,
                contentHeight
            );

            // Update search bar bounds
            _searchBarBounds = new Rectangle(
                _leftPanelBounds.X + PANEL_PADDING,
                _leftPanelBounds.Y + PANEL_PADDING,
                _leftPanelBounds.Width - PANEL_PADDING * 2,
                30
            );

            // Update category button bounds
            _categoryButtonBounds.Clear();
            int categoryY = _searchBarBounds.Bottom + PANEL_PADDING;
            for (int i = 0; i < _assetCategories.Count; i++)
            {
                _categoryButtonBounds.Add(new Rectangle(
                    _leftPanelBounds.X + PANEL_PADDING,
                    categoryY + i * 35,
                    _leftPanelBounds.Width - PANEL_PADDING * 2,
                    30
                ));
            }

            // Update character input bounds
            int centerTop = _centerPanelBounds.Y + PANEL_PADDING;
            _nameInputBounds = new Rectangle(
                _centerPanelBounds.X + PANEL_PADDING,
                centerTop,
                300,
                30
            );
            _tagsInputBounds = new Rectangle(
                _nameInputBounds.Right + PANEL_PADDING,
                centerTop,
                200,
                30
            );

            // Update character image bounds
            _characterImageBounds = new Rectangle(
                _centerPanelBounds.X + PANEL_PADDING,
                _tagsInputBounds.Bottom + PANEL_PADDING * 2,
                400,
                500
            );

            // Update inspector sections
            int inspectorTop = _rightPanelBounds.Y + PANEL_PADDING;
            _inspectorHeaderBounds = new Rectangle(
                _rightPanelBounds.X + PANEL_PADDING,
                inspectorTop,
                _rightPanelBounds.Width - PANEL_PADDING * 2,
                100
            );
            _referencesSectionBounds = new Rectangle(
                _rightPanelBounds.X + PANEL_PADDING,
                _inspectorHeaderBounds.Bottom + PANEL_PADDING,
                _rightPanelBounds.Width - PANEL_PADDING * 2,
                150
            );
            _linkedTraitsSectionBounds = new Rectangle(
                _rightPanelBounds.X + PANEL_PADDING,
                _referencesSectionBounds.Bottom + PANEL_PADDING,
                _rightPanelBounds.Width - PANEL_PADDING * 2,
                300
            );
            }
            catch (Exception ex)
            {
                _engine?.Log($"CharacterCreation: Error in UpdateBounds: {ex.Message}");
                _engine?.Log($"CharacterCreation: Stack trace: {ex.StackTrace}");
            }
        }

        private void UpdateScrolling()
        {
            if (_windowManagement == null || !_windowManagement.IsVisible())
                return;

            // Calculate content height (approximate based on center panel content)
            _contentHeight = CalculateContentHeight();
            
            // Reserve space for resize handle
            int resizeHandleSize = 24;
            int availableHeight = _centerPanelBounds.Height - resizeHandleSize;
            
            // Check if scrollbar is needed
            _needsScrollbar = _contentHeight > availableHeight;
            
            if (_needsScrollbar)
            {
                // Update scrollbar bounds (on right side of center panel)
                _scrollbarBounds = new Rectangle(
                    _centerPanelBounds.Right - SCROLLBAR_WIDTH - 2,
                    _centerPanelBounds.Y,
                    SCROLLBAR_WIDTH,
                    availableHeight
                );
                
                // Clamp scroll position
                int maxScroll = Math.Max(0, _contentHeight - availableHeight);
                _scrollY = MathHelper.Clamp(_scrollY, 0, maxScroll);
            }
            else
            {
                _scrollY = 0;
                _scrollbarBounds = Rectangle.Empty;
            }
        }

        private int CalculateContentHeight()
        {
            // Calculate approximate content height based on center panel elements
            int height = 0;
            
            // Name and tags
            height += 30 + PANEL_PADDING;
            
            // Character image
            height += 500 + PANEL_PADDING * 2;
            
            // BASE STATS section
            height += 30 + PANEL_PADDING; // Header
            height += _baseStats.Count * 35; // Stats
            
            // TRAITS & PERKS section
            height += 30 + PANEL_PADDING; // Header
            height += _characterTraits.Count * 35; // Traits
            
            // ABILITIES & SKILLS section
            height += 30 + PANEL_PADDING; // Header
            height += _characterAbilities.Count * 35; // Abilities
            
            // Add some padding at bottom
            height += PANEL_PADDING * 2;
            
            return height;
        }

        private void HandleScrollbarInteraction()
        {
            if (!_needsScrollbar || !_scrollingEnabled) return;

            var mousePosition = _currentMouseState.Position;

            // Handle scrollbar dragging
            if (_currentMouseState.LeftButton == ButtonState.Pressed && 
                _previousMouseState.LeftButton == ButtonState.Released)
            {
                if (_scrollbarBounds.Contains(mousePosition))
                {
                    _isDraggingScrollbar = true;
                    _scrollbarDragStart = new Vector2(mousePosition.X, mousePosition.Y);
                }
            }

            if (_isDraggingScrollbar)
            {
                if (_currentMouseState.LeftButton == ButtonState.Pressed)
                {
                    float deltaY = mousePosition.Y - _scrollbarDragStart.Y;
                    float scrollbarHeight = _scrollbarBounds.Height;
                    int resizeHandleSize = 24;
                    int availableHeight = _centerPanelBounds.Height - resizeHandleSize;
                    
                    float scrollRatio = deltaY / scrollbarHeight;
                    _scrollY = (int)(scrollRatio * (_contentHeight - availableHeight));
                    
                    int maxScroll = Math.Max(0, _contentHeight - availableHeight);
                    _scrollY = MathHelper.Clamp(_scrollY, 0, maxScroll);
                }
                else
                {
                    _isDraggingScrollbar = false;
                }
            }

            // Handle mouse wheel scrolling
            if (_centerPanelBounds.Contains(mousePosition) && 
                _currentMouseState.ScrollWheelValue != _previousMouseState.ScrollWheelValue)
            {
                int delta = _currentMouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
                _scrollY -= delta / 3; // Adjust scroll speed
                
                int resizeHandleSize = 24;
                int availableHeight = _centerPanelBounds.Height - resizeHandleSize;
                int maxScroll = Math.Max(0, _contentHeight - availableHeight);
                _scrollY = MathHelper.Clamp(_scrollY, 0, maxScroll);
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            try
            {
                if (_windowManagement == null || !_windowManagement.IsVisible())
                    return;

                // Ensure resources are initialized
                if (_pixel == null)
                {
                    // Try to recreate pixel texture if it's null
                    try
                    {
                        _pixel = new Texture2D(_graphicsDevice, 1, 1);
                        _pixel.SetData(new[] { Color.White });
                    }
                    catch
                    {
                        return; // Can't draw without pixel texture
                    }
                }

                if (_menuFont == null)
                {
                    return; // Can't draw without font
                }

                _windowManagement.Draw(spriteBatch, "Character Creation");

                var windowBounds = _windowManagement.GetWindowBounds();
                
                // Validate window bounds
                if (windowBounds.Width <= 0 || windowBounds.Height <= 0)
                    return;
                
                // Ensure bounds are initialized and valid
                if (_leftPanelBounds.Width <= 0 || _centerPanelBounds.Width <= 0 || _rightPanelBounds.Width <= 0 ||
                    _leftPanelBounds.Height <= 0 || _centerPanelBounds.Height <= 0 || _rightPanelBounds.Height <= 0)
                {
                    UpdateBounds();
                    // Re-validate after updating bounds
                    if (_leftPanelBounds.Width <= 0 || _centerPanelBounds.Width <= 0 || _rightPanelBounds.Width <= 0 ||
                        _leftPanelBounds.Height <= 0 || _centerPanelBounds.Height <= 0 || _rightPanelBounds.Height <= 0)
                        return;
                }
                
                int titleBarHeight = 40;
                int resizeHandleSize = 24; // Reserve space for resize handle

                // Set up scissor rectangle for content area (leave space for resize handle)
                Rectangle originalScissor = spriteBatch.GraphicsDevice.ScissorRectangle;
                Rectangle contentScissor = new Rectangle(
                    windowBounds.X + 2,
                    windowBounds.Y + titleBarHeight,
                    windowBounds.Width - 4,
                    windowBounds.Height - titleBarHeight - 2 - resizeHandleSize // Reserve space for resize handle
                );

                spriteBatch.GraphicsDevice.ScissorRectangle = contentScissor;
                spriteBatch.GraphicsDevice.RasterizerState = new RasterizerState
                {
                    ScissorTestEnable = true
                };

                // Update bounds before drawing to ensure they match current window size
                UpdateBounds();
                
                // Draw panels (with scroll offset applied to center panel)
                DrawLeftPanel(spriteBatch);
                DrawCenterPanel(spriteBatch, _scrollY, windowBounds);
                DrawRightPanel(spriteBatch);

                // Restore scissor test before drawing scrollbar and resize handle
                spriteBatch.GraphicsDevice.ScissorRectangle = originalScissor;
                spriteBatch.GraphicsDevice.RasterizerState = new RasterizerState
                {
                    ScissorTestEnable = false
                };
                
                // Draw scrollbar if needed (outside scissor so it can extend)
                if (_needsScrollbar)
                {
                    DrawScrollbar(spriteBatch);
                }
                
                // Draw resize handle on top (ensure it's always visible)
                DrawResizeHandle(spriteBatch, windowBounds);
            }
            catch (Exception ex)
            {
                _engine?.Log($"CharacterCreation: Error in Draw: {ex.Message}");
                _engine?.Log($"CharacterCreation: Stack trace: {ex.StackTrace}");
                
                // Show error message only once to prevent spam
                if (!_hasShownError)
                {
                    try
                    {
                        var flashMessageType = typeof(MarySGameEngine.Modules.FlashMessage_essential.FlashMessage);
                        var showMethod = flashMessageType.GetMethod("Show", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        if (showMethod != null)
                        {
                            showMethod.Invoke(null, new object[] { $"Character Creation Draw Error: {ex.Message}", 
                                MarySGameEngine.Modules.FlashMessage_essential.FlashMessageType.Warning, 5.0f });
                            _hasShownError = true;
                        }
                    }
                    catch { }
                }
            }
        }

        private void DrawLeftPanel(SpriteBatch spriteBatch)
        {
            if (_pixel == null || _menuFont == null) return;
            
            // Draw panel background
            spriteBatch.Draw(_pixel, _leftPanelBounds, PANEL_BACKGROUND);
            DrawBorder(spriteBatch, _leftPanelBounds, PANEL_BORDER);

            // Draw search bar
            Color searchBgColor = _isSearchFocused ? SEARCH_BACKGROUND : SEARCH_BACKGROUND;
            spriteBatch.Draw(_pixel, _searchBarBounds, searchBgColor);
            DrawBorder(spriteBatch, _searchBarBounds, SEARCH_BORDER);
            
            Vector2 searchTextPos = new Vector2(_searchBarBounds.X + 10, _searchBarBounds.Y + 5);
            Color searchTextColor = _isSearchFocused ? TEXT_COLOR : TEXT_SECONDARY;
            // Search bar uses UI font
            SpriteFont searchFont = _uiFont ?? _menuFont;
            spriteBatch.DrawString(searchFont, _searchText, searchTextPos, searchTextColor);

            // Draw category buttons
            for (int i = 0; i < _assetCategories.Count && i < _categoryButtonBounds.Count; i++)
            {
                Rectangle bounds = _categoryButtonBounds[i];
                bool isSelected = i == _selectedCategoryIndex;
                Color buttonColor = isSelected ? BUTTON_ACTIVE : BUTTON_COLOR;
                
                spriteBatch.Draw(_pixel, bounds, buttonColor);
                if (isSelected)
                {
                    DrawBorder(spriteBatch, bounds, BUTTON_ACTIVE);
                }

                Vector2 textPos = new Vector2(bounds.X + 10, bounds.Y + 5);
                spriteBatch.DrawString(_menuFont, _assetCategories[i], textPos, TEXT_COLOR);
            }

            // Draw Traits section
            int traitsY = _categoryButtonBounds.Count > 0 ? _categoryButtonBounds.Last().Bottom + PANEL_PADDING : _searchBarBounds.Bottom + PANEL_PADDING * 2;
            Rectangle traitsHeaderBounds = new Rectangle(
                _leftPanelBounds.X + PANEL_PADDING,
                traitsY,
                _leftPanelBounds.Width - PANEL_PADDING * 2,
                30
            );

            spriteBatch.Draw(_pixel, traitsHeaderBounds, SECTION_HEADER);
            Vector2 traitsHeaderPos = new Vector2(traitsHeaderBounds.X + 10, traitsHeaderBounds.Y + 5);
            spriteBatch.DrawString(_menuFont, "Traits", traitsHeaderPos, TEXT_COLOR);

            // Draw trait items
            if (_isTraitsExpanded)
            {
                int traitY = traitsHeaderBounds.Bottom + 5;
                SpriteFont traitFont = _uiFont ?? _menuFont; // Traits use UI font
                for (int i = 0; i < _filteredTraits.Count; i++)
                {
                    Rectangle traitBounds = new Rectangle(
                        _leftPanelBounds.X + PANEL_PADDING + 20,
                        traitY + i * TRAIT_ITEM_HEIGHT,
                        _leftPanelBounds.Width - PANEL_PADDING * 2 - 20,
                        TRAIT_ITEM_HEIGHT - 2
                    );

                    // Draw checkbox
                    Rectangle checkboxBounds = new Rectangle(traitBounds.X, traitBounds.Y + 5, 15, 15);
                    spriteBatch.Draw(_pixel, checkboxBounds, BUTTON_COLOR);
                    DrawBorder(spriteBatch, checkboxBounds, PANEL_BORDER);
                    
                    if (_filteredTraits[i].IsChecked)
                    {
                        // Draw checkmark
                        spriteBatch.Draw(_pixel, new Rectangle(checkboxBounds.X + 3, checkboxBounds.Y + 3, 9, 9), BUTTON_ACTIVE);
                    }

                    // Draw trait name
                    Vector2 traitNamePos = new Vector2(checkboxBounds.Right + 10, traitBounds.Y + 5);
                    spriteBatch.DrawString(traitFont, _filteredTraits[i].Name, traitNamePos, TEXT_COLOR);
                }
            }
        }

        private bool IsVisible(Rectangle bounds, Rectangle scissorRect)
        {
            // Check if bounds intersect with scissor rectangle
            return bounds.Bottom > scissorRect.Y && bounds.Y < scissorRect.Bottom &&
                   bounds.Right > scissorRect.X && bounds.X < scissorRect.Right;
        }

        private void DrawCenterPanel(SpriteBatch spriteBatch, int scrollOffset, Rectangle windowBounds)
        {
            if (_pixel == null || _menuFont == null) return;
            if (_centerPanelBounds.Width <= 0 || _centerPanelBounds.Height <= 0) return;
            
            SpriteFont uiFont = _uiFont ?? _menuFont; // Use UI font for center panel
            
            // Apply scroll offset to all drawing positions
            int scrollY = -scrollOffset;
            
            // Set up scissor rectangle specifically for the center panel using CURRENT window bounds
            Rectangle originalScissor = spriteBatch.GraphicsDevice.ScissorRectangle;
            int resizeHandleSize = 24;
            
            // Calculate center panel scissor based on ACTUAL current window visible area
            // The scissor must match exactly what's visible in the window
            int centerPanelX = _centerPanelBounds.X;
            int centerPanelY = _centerPanelBounds.Y;
            int centerPanelWidth = _centerPanelBounds.Width - (_needsScrollbar ? SCROLLBAR_WIDTH + SCROLLBAR_PADDING : 0);
            
            // The visible height is the window height minus title bar and resize handle
            // This is the MAXIMUM visible area - content beyond this should be clipped
            int maxVisibleBottom = windowBounds.Y + windowBounds.Height - resizeHandleSize;
            int centerPanelScissorTop = centerPanelY;
            int centerPanelScissorBottom = Math.Min(centerPanelY + _centerPanelBounds.Height, maxVisibleBottom);
            int centerPanelScissorHeight = Math.Max(0, centerPanelScissorBottom - centerPanelScissorTop);
            
            Rectangle centerPanelScissor = new Rectangle(
                centerPanelX,
                centerPanelScissorTop,
                centerPanelWidth,
                centerPanelScissorHeight
            );
            
            // Intersect with the window-level scissor (originalScissor) to ensure proper clipping
            int scissorLeft = Math.Max(centerPanelScissor.X, originalScissor.X);
            int scissorTop = Math.Max(centerPanelScissor.Y, originalScissor.Y);
            int scissorRight = Math.Min(centerPanelScissor.Right, originalScissor.Right);
            int scissorBottom = Math.Min(centerPanelScissor.Bottom, originalScissor.Bottom);
            
            int scissorWidth = Math.Max(0, scissorRight - scissorLeft);
            int scissorHeight = Math.Max(0, scissorBottom - scissorTop);
            
            // Always set scissor for center panel to clip content properly
            // Even if dimensions are small, we need to set it to prevent drawing outside bounds
            Rectangle scissorRect = new Rectangle(scissorLeft, scissorTop, scissorWidth, scissorHeight);
            spriteBatch.GraphicsDevice.ScissorRectangle = scissorRect;
            spriteBatch.GraphicsDevice.RasterizerState = new RasterizerState
            {
                ScissorTestEnable = true,
                CullMode = CullMode.None
            };
            
            // Draw panel background
            spriteBatch.Draw(_pixel, _centerPanelBounds, PANEL_BACKGROUND);
            DrawBorder(spriteBatch, _centerPanelBounds, PANEL_BORDER);

            // Draw name and tags (apply scroll offset) - only if visible
            Rectangle nameBounds = new Rectangle(_nameInputBounds.X, _nameInputBounds.Y + scrollY, _nameInputBounds.Width, _nameInputBounds.Height);
            if (IsVisible(nameBounds, scissorRect))
            {
                spriteBatch.Draw(_pixel, nameBounds, SEARCH_BACKGROUND);
                DrawBorder(spriteBatch, nameBounds, SEARCH_BORDER);
                Vector2 namePos = new Vector2(nameBounds.X + 10, nameBounds.Y + 5);
                spriteBatch.DrawString(uiFont, $"Name: {_characterName}", namePos, TEXT_COLOR);
            }

            Rectangle tagsBounds = new Rectangle(_tagsInputBounds.X, _tagsInputBounds.Y + scrollY, _tagsInputBounds.Width, _tagsInputBounds.Height);
            if (IsVisible(tagsBounds, scissorRect))
            {
                spriteBatch.Draw(_pixel, tagsBounds, SEARCH_BACKGROUND);
                DrawBorder(spriteBatch, tagsBounds, SEARCH_BORDER);
                Vector2 tagsPos = new Vector2(tagsBounds.X + 10, tagsBounds.Y + 5);
                spriteBatch.DrawString(uiFont, $"Tags: {_characterTags}", tagsPos, TEXT_COLOR);
            }

            // Draw character image placeholder (apply scroll offset) - only if visible
            Rectangle imageBounds = new Rectangle(_characterImageBounds.X, _characterImageBounds.Y + scrollY, _characterImageBounds.Width, _characterImageBounds.Height);
            if (IsVisible(imageBounds, scissorRect))
            {
                spriteBatch.Draw(_pixel, imageBounds, new Color(30, 30, 30));
                DrawBorder(spriteBatch, imageBounds, PANEL_BORDER);
                
                // Draw placeholder text
                Vector2 imageTextPos = new Vector2(
                    imageBounds.X + imageBounds.Width / 2 - 50,
                    imageBounds.Y + imageBounds.Height / 2
                );
                spriteBatch.DrawString(uiFont, "Knight_Warrior", imageTextPos, TEXT_SECONDARY);
            }

            // Draw BASE STATS section (apply scroll offset) - only if visible
            int statsY = imageBounds.Bottom + PANEL_PADDING;
            Rectangle statsHeaderBounds = new Rectangle(
                _centerPanelBounds.X + PANEL_PADDING,
                statsY,
                200,
                30
            );
            if (IsVisible(statsHeaderBounds, scissorRect))
            {
                spriteBatch.Draw(_pixel, statsHeaderBounds, SECTION_HEADER);
                Vector2 statsHeaderPos = new Vector2(statsHeaderBounds.X + 10, statsHeaderBounds.Y + 5);
                spriteBatch.DrawString(uiFont, "BASE STATS", statsHeaderPos, TEXT_COLOR);
            }

            // Draw stats (apply scroll offset) - only if visible
            int statY = statsHeaderBounds.Bottom + 10;
            foreach (var stat in _baseStats)
            {
                Rectangle statBounds = new Rectangle(
                    _centerPanelBounds.X + PANEL_PADDING,
                    statY,
                    200,
                    25
                );

                if (IsVisible(statBounds, scissorRect))
                {
                    // Draw stat name and value
                    Vector2 statNamePos = new Vector2(statBounds.X + 10, statY);
                    spriteBatch.DrawString(uiFont, $"{stat.Name}: {stat.Value}", statNamePos, TEXT_COLOR);

                    // Draw stat bar
                    Rectangle barBounds = new Rectangle(statBounds.X + 10, statY + 20, 150, 8);
                    spriteBatch.Draw(_pixel, barBounds, STAT_BAR_BACKGROUND);
                    int fillWidth = (int)(barBounds.Width * ((float)stat.Value / stat.MaxValue));
                    Rectangle fillBounds = new Rectangle(barBounds.X, barBounds.Y, fillWidth, barBounds.Height);
                    spriteBatch.Draw(_pixel, fillBounds, STAT_BAR_FILL);
                }

                statY += 35;
            }

            // Draw Health, Action Points, Initiative (apply scroll offset) - check each individually
            int rightStatsY = statsHeaderBounds.Y;
            int rightStatsX = _centerPanelBounds.Right - 250;
            
            // Check visibility for each stat individually
            Rectangle healthBounds = new Rectangle(rightStatsX, rightStatsY, 250, 25);
            if (IsVisible(healthBounds, scissorRect))
            {
                DrawStatDisplay(spriteBatch, "Health: 120 / 120", rightStatsX, rightStatsY, Color.Red);
            }
            
            Rectangle actionPointsBounds = new Rectangle(rightStatsX, rightStatsY + 30, 250, 25);
            if (IsVisible(actionPointsBounds, scissorRect))
            {
                DrawStatDisplay(spriteBatch, "Action Points: 8", rightStatsX, rightStatsY + 30, Color.Gray);
            }
            
            Rectangle initiativeBounds = new Rectangle(rightStatsX, rightStatsY + 60, 250, 25);
            if (IsVisible(initiativeBounds, scissorRect))
            {
                DrawStatDisplay(spriteBatch, "Initiative: 12", rightStatsX, rightStatsY + 60, Color.Yellow);
            }

            // Draw TRAITS & PERKS section (apply scroll offset) - only if visible
            int traitsSectionY = statY + PANEL_PADDING;
            Rectangle traitsSectionHeader = new Rectangle(
                _centerPanelBounds.X + PANEL_PADDING,
                traitsSectionY,
                _centerPanelBounds.Width - PANEL_PADDING * 2,
                30
            );
            if (IsVisible(traitsSectionHeader, scissorRect))
            {
                spriteBatch.Draw(_pixel, traitsSectionHeader, SECTION_HEADER);
                Vector2 traitsSectionPos = new Vector2(traitsSectionHeader.X + 10, traitsSectionHeader.Y + 5);
                spriteBatch.DrawString(uiFont, "TRAITS & PERKS", traitsSectionPos, TEXT_COLOR);
            }

            // Draw character traits (apply scroll offset) - only if visible
            int charTraitY = traitsSectionHeader.Bottom + 10;
            foreach (var trait in _characterTraits)
            {
                Rectangle traitBounds = new Rectangle(
                    _centerPanelBounds.X + PANEL_PADDING,
                    charTraitY,
                    300,
                    30
                );

                if (IsVisible(traitBounds, scissorRect))
                {
                    // Draw trait name
                    Vector2 traitNamePos = new Vector2(traitBounds.X + 10, traitBounds.Y + 5);
                    spriteBatch.DrawString(uiFont, trait.Name, traitNamePos, TEXT_COLOR);

                    // Draw Edit button
                    Rectangle editButtonBounds = new Rectangle(traitBounds.Right - 100, traitBounds.Y, 50, 25);
                    spriteBatch.Draw(_pixel, editButtonBounds, BUTTON_COLOR);
                    DrawBorder(spriteBatch, editButtonBounds, PANEL_BORDER);
                    Vector2 editPos = new Vector2(editButtonBounds.X + 10, editButtonBounds.Y + 3);
                    spriteBatch.DrawString(uiFont, "Edit", editPos, TEXT_COLOR);
                }

                charTraitY += 35;
            }

            // Draw ABILITIES & SKILLS section (apply scroll offset) - only if visible
            int abilitiesSectionY = charTraitY + PANEL_PADDING;
            Rectangle abilitiesSectionHeader = new Rectangle(
                _centerPanelBounds.X + PANEL_PADDING,
                abilitiesSectionY,
                _centerPanelBounds.Width - PANEL_PADDING * 2,
                30
            );
            if (IsVisible(abilitiesSectionHeader, scissorRect))
            {
                spriteBatch.Draw(_pixel, abilitiesSectionHeader, SECTION_HEADER);
                Vector2 abilitiesSectionPos = new Vector2(abilitiesSectionHeader.X + 10, abilitiesSectionHeader.Y + 5);
                spriteBatch.DrawString(uiFont, "ABILITIES & SKILLS", abilitiesSectionPos, TEXT_COLOR);
            }

            // Draw character abilities (apply scroll offset) - only if visible
            int abilityY = abilitiesSectionHeader.Bottom + 10;
            foreach (var ability in _characterAbilities)
            {
                Rectangle abilityBounds = new Rectangle(
                    _centerPanelBounds.X + PANEL_PADDING,
                    abilityY,
                    300,
                    30
                );

                if (IsVisible(abilityBounds, scissorRect))
                {
                    // Draw ability name
                    Vector2 abilityNamePos = new Vector2(abilityBounds.X + 10, abilityBounds.Y + 5);
                    spriteBatch.DrawString(uiFont, ability.Name, abilityNamePos, TEXT_COLOR);

                    // Draw Pick Skill button
                    Rectangle pickButtonBounds = new Rectangle(abilityBounds.Right - 100, abilityBounds.Y, 80, 25);
                    spriteBatch.Draw(_pixel, pickButtonBounds, BUTTON_COLOR);
                    DrawBorder(spriteBatch, pickButtonBounds, PANEL_BORDER);
                    Vector2 pickPos = new Vector2(pickButtonBounds.X + 5, pickButtonBounds.Y + 3);
                    spriteBatch.DrawString(uiFont, "Pick Skill", pickPos, TEXT_COLOR);
                }

                abilityY += 35;
            }
            
            // Restore scissor rectangle
            spriteBatch.GraphicsDevice.ScissorRectangle = originalScissor;
            spriteBatch.GraphicsDevice.RasterizerState = new RasterizerState
            {
                ScissorTestEnable = false
            };
        }

        private void DrawRightPanel(SpriteBatch spriteBatch)
        {
            if (_pixel == null || _menuFont == null) return;
            if (_rightPanelBounds.Width <= 0 || _rightPanelBounds.Height <= 0) return;
            
            SpriteFont uiFont = _uiFont ?? _menuFont; // Use UI font for right panel
            
            // Draw panel background
            spriteBatch.Draw(_pixel, _rightPanelBounds, PANEL_BACKGROUND);
            DrawBorder(spriteBatch, _rightPanelBounds, PANEL_BORDER);

            // Draw Inspector header
            spriteBatch.Draw(_pixel, _inspectorHeaderBounds, SECTION_HEADER);
            Vector2 inspectorTitlePos = new Vector2(_inspectorHeaderBounds.X + 10, _inspectorHeaderBounds.Y + 10);
            spriteBatch.DrawString(uiFont, "Inspector", inspectorTitlePos, TEXT_COLOR);

            // Draw character info in header
            Vector2 charNamePos = new Vector2(_inspectorHeaderBounds.X + 10, _inspectorHeaderBounds.Y + 35);
            spriteBatch.DrawString(uiFont, _characterName, charNamePos, TEXT_COLOR);

            Vector2 charIdPos = new Vector2(_inspectorHeaderBounds.X + 10, _inspectorHeaderBounds.Y + 55);
            spriteBatch.DrawString(uiFont, $"ID: char.{_characterName.ToLower()}", charIdPos, TEXT_SECONDARY);

            Vector2 archetypePos = new Vector2(_inspectorHeaderBounds.X + 10, _inspectorHeaderBounds.Y + 75);
            spriteBatch.DrawString(uiFont, "Archetype: Warrior", archetypePos, TEXT_SECONDARY);

            Vector2 tagsPos = new Vector2(_inspectorHeaderBounds.X + 10, _inspectorHeaderBounds.Y + 95);
            spriteBatch.DrawString(uiFont, $"Tags: {_characterTags}", tagsPos, TEXT_SECONDARY);

            // Draw REFERENCES section
            DrawCollapsibleSection(spriteBatch, "REFERENCES", _referencesSectionBounds, _isReferencesExpanded, uiFont, () =>
            {
                Vector2 ref1Pos = new Vector2(_referencesSectionBounds.X + 20, _referencesSectionBounds.Y + 30);
                spriteBatch.DrawString(uiFont, "Uses: 12 Quests, 5 Encounters", ref1Pos, TEXT_COLOR);

                Vector2 ref2Pos = new Vector2(_referencesSectionBounds.X + 20, _referencesSectionBounds.Y + 50);
                spriteBatch.DrawString(uiFont, "Used By: Mutant_Boss, Arena_02", ref2Pos, TEXT_COLOR);
            });

            // Draw LINKED TRAITS section
            DrawCollapsibleSection(spriteBatch, "LINKED TRAITS", _linkedTraitsSectionBounds, _isLinkedTraitsExpanded, uiFont, () =>
            {
                int linkedTraitY = _linkedTraitsSectionBounds.Y + 30;
                foreach (var trait in _characterTraits)
                {
                    Vector2 linkedTraitNamePos = new Vector2(_linkedTraitsSectionBounds.X + 20, linkedTraitY);
                    spriteBatch.DrawString(uiFont, trait.Name, linkedTraitNamePos, TEXT_COLOR);

                    // Draw effects
                    Vector2 effectsPos = new Vector2(_linkedTraitsSectionBounds.X + 20, linkedTraitY + 20);
                    string effects = trait.Name == "Battle Hardened" 
                        ? "+20 Max HP, +10% DR" 
                        : "+25% Melee Damage, -10% Crit Chance";
                    spriteBatch.DrawString(uiFont, effects, effectsPos, TEXT_SECONDARY);

                    linkedTraitY += 60;
                }
            });

            // Draw action buttons at bottom
            int buttonY = _rightPanelBounds.Bottom - 120;
            DrawActionButton(spriteBatch, "Find References", _rightPanelBounds.X + PANEL_PADDING, buttonY, 120);
            DrawActionButton(spriteBatch, "Validate", _rightPanelBounds.X + PANEL_PADDING, buttonY + 35, 120);
            DrawActionButton(spriteBatch, "Export Data", _rightPanelBounds.X + PANEL_PADDING, buttonY + 70, 120);
        }

        private void DrawStatDisplay(SpriteBatch spriteBatch, string text, int x, int y, Color iconColor)
        {
            if (_pixel == null || _menuFont == null) return;
            
            SpriteFont uiFont = _uiFont ?? _menuFont;
            
            // Draw icon placeholder (small square)
            Rectangle iconBounds = new Rectangle(x, y, 20, 20);
            spriteBatch.Draw(_pixel, iconBounds, iconColor);

            // Draw text
            Vector2 textPos = new Vector2(x + 25, y + 2);
            spriteBatch.DrawString(uiFont, text, textPos, TEXT_COLOR);
        }

        private void DrawCollapsibleSection(SpriteBatch spriteBatch, string title, Rectangle bounds, bool isExpanded, SpriteFont font, Action drawContent)
        {
            if (_pixel == null || _menuFont == null) return;
            
            // Draw section header
            Rectangle headerBounds = new Rectangle(bounds.X, bounds.Y, bounds.Width, 25);
            spriteBatch.Draw(_pixel, headerBounds, SECTION_HEADER);
            
            // Draw collapse/expand indicator (using simple characters that all fonts support)
            Vector2 arrowPos = new Vector2(headerBounds.X + 5, headerBounds.Y + 5);
            string indicator = isExpanded ? "-" : "+";
            spriteBatch.DrawString(font, indicator, arrowPos, TEXT_COLOR);

            // Draw title
            Vector2 titlePos = new Vector2(headerBounds.X + 25, headerBounds.Y + 5);
            spriteBatch.DrawString(font, title, titlePos, TEXT_COLOR);

            // Draw content if expanded
            if (isExpanded)
            {
                drawContent();
            }
        }

        private void DrawActionButton(SpriteBatch spriteBatch, string text, int x, int y, int width)
        {
            if (_pixel == null || _menuFont == null) return;
            
            SpriteFont uiFont = _uiFont ?? _menuFont;
            
            Rectangle buttonBounds = new Rectangle(x, y, width, 30);
            spriteBatch.Draw(_pixel, buttonBounds, BUTTON_COLOR);
            DrawBorder(spriteBatch, buttonBounds, PANEL_BORDER);

            Vector2 textPos = new Vector2(buttonBounds.X + 10, buttonBounds.Y + 5);
            spriteBatch.DrawString(uiFont, text, textPos, TEXT_COLOR);
        }

        private void DrawScrollbar(SpriteBatch spriteBatch)
        {
            if (!_needsScrollbar || _scrollbarBounds.IsEmpty) return;
            if (_pixel == null) return;

            // Draw scrollbar background
            spriteBatch.Draw(_pixel, _scrollbarBounds, new Color(55, 65, 81));
            DrawBorder(spriteBatch, _scrollbarBounds, PANEL_BORDER);

            // Calculate thumb size and position
            int resizeHandleSize = 24;
            int availableHeight = _centerPanelBounds.Height - resizeHandleSize;
            float contentRatio = (float)availableHeight / Math.Max(1, _contentHeight);
            int thumbHeight = Math.Max(20, (int)(_scrollbarBounds.Height * contentRatio));
            
            int maxScroll = Math.Max(1, _contentHeight - availableHeight);
            int thumbY = _scrollbarBounds.Y + (int)((_scrollbarBounds.Height - thumbHeight) * (_scrollY / (float)maxScroll));

            var thumbBounds = new Rectangle(_scrollbarBounds.X + 2, thumbY + 2, _scrollbarBounds.Width - 4, thumbHeight - 4);
            bool isThumbHovered = thumbBounds.Contains(_currentMouseState.Position) || _isDraggingScrollbar;
            Color thumbColor = isThumbHovered ? BUTTON_HOVER : BUTTON_COLOR;

            spriteBatch.Draw(_pixel, thumbBounds, thumbColor);
            DrawBorder(spriteBatch, thumbBounds, PANEL_BORDER);
        }

        private void DrawResizeHandle(SpriteBatch spriteBatch, Rectangle windowBounds)
        {
            if (_pixel == null) return;
            if (!_windowManagement.IsVisible()) return;
            
            // Check if window is maximized using reflection
            var isMaximizedField = _windowManagement.GetType().GetField("_isMaximized",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            bool isMaximized = isMaximizedField?.GetValue(_windowManagement) as bool? ?? false;
            if (isMaximized) return;
            
            // Draw resize handle in bottom-right corner (on top of everything)
            const int handleSize = 24;
            Rectangle resizeHandleBounds = new Rectangle(
                windowBounds.Right - handleSize,
                windowBounds.Bottom - handleSize,
                handleSize,
                handleSize
            );
            
            // Draw resize handle pattern (same as WindowManagement)
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

        private void DrawBorder(SpriteBatch spriteBatch, Rectangle bounds, Color color)
        {
            if (_pixel == null) return;
            
            // Top
            spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), color);
            // Bottom
            spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), color);
            // Left
            spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), color);
            // Right
            spriteBatch.Draw(_pixel, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), color);
        }

        public void UpdateWindowWidth(int width)
        {
            _windowWidth = width;
            _windowManagement?.UpdateWindowWidth(width);
        }

        public void LoadContent(ContentManager content)
        {
            _content = content;
            try
            {
                // Load window management content (icons, fonts, etc.)
                if (_windowManagement != null)
                {
                    _windowManagement.LoadContent(content);
                }
                
                // Load UI font for most elements (try Roboto, then Open Sans, fallback to menu font)
                try
                {
                    _uiFont = content.Load<SpriteFont>("Fonts/SpriteFonts/roboto/regular");
                    _engine?.Log("CharacterCreation: Successfully loaded Roboto font");
                }
                catch (Exception ex)
                {
                    _engine?.Log($"CharacterCreation: Failed to load Roboto font: {ex.Message}");
                    try
                    {
                        _uiFont = content.Load<SpriteFont>("Fonts/SpriteFonts/open_sans/regular");
                        _engine?.Log("CharacterCreation: Using Open Sans font as fallback");
                    }
                    catch
                    {
                        _engine?.Log($"CharacterCreation: Failed to load Open Sans font: {ex.Message}");
                        _uiFont = _menuFont; // Fallback to menu font
                    }
                }
                
                // Load pixel font (for titles/headers if needed)
                try
                {
                    _pixelFont = content.Load<SpriteFont>("Fonts/SpriteFonts/pixel_font/regular");
                }
                catch
                {
                    _pixelFont = _menuFont; // Fallback to menu font
                }
            }
            catch (Exception ex)
            {
                _engine?.Log($"CharacterCreation: Error in LoadContent: {ex.Message}");
                _uiFont = _menuFont; // Fallback to menu font
                _pixelFont = _menuFont;
            }
        }

        public void Dispose()
        {
            _pixel?.Dispose();
        }
    }
}

