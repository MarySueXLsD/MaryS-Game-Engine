using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
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

        // Scrolling properties (center panel)
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
        
        // Right panel scrolling properties
        private int _rightPanelScrollY = 0;
        private int _rightPanelContentHeight = 0;
        private bool _rightPanelNeedsScrollbar = false;
        private Rectangle _rightPanelScrollbarBounds = Rectangle.Empty;
        private bool _isDraggingRightPanelScrollbar = false;
        private Vector2 _rightPanelScrollbarDragStart;

        // Panel bounds
        private Rectangle _leftPanelBounds = Rectangle.Empty;      // Asset Browser
        private Rectangle _centerPanelBounds = Rectangle.Empty;    // Character Details
        private Rectangle _rightPanelBounds = Rectangle.Empty;      // Inspector

        // Left Panel - Asset Browser
        private Rectangle _searchBarBounds = Rectangle.Empty;
        private string _searchText = "Search Assets...";
        private bool _isSearchFocused = false;
        private List<string> _assetCategories = new List<string> { "All", "Characters", "Traits", "Skills", "Effects", "Stats", "Tags" };
        private int _selectedCategoryIndex = 0;
        private List<Rectangle> _categoryButtonBounds = new List<Rectangle>();
        
        // Screen/View system
        private string _currentView = "All"; // Default to "All" tab
        private string _previousView = "All"; // Track previous view to reset scroll on tab change
        private bool _isInspectingCharacter = false; // Whether we're inspecting a character (shows character details)
        
        // Workspace tracking
        private string _currentWorkspaceName = "";
        private string _previousWorkspaceName = "";

        // Character list (shown in Characters tab); click an entry to open inspection
        private List<string> _characters = new List<string> { "Knight_Warrior" };
        private List<Rectangle> _characterListBounds = new List<Rectangle>();
        private List<Rectangle> _characterDeleteButtonBounds = new List<Rectangle>();

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

        // Entity type descriptions (loaded from JSON)
        private Dictionary<string, string> _entityDescriptionsShort = new Dictionary<string, string>();
        private Dictionary<string, string> _entityDescriptionsLong = new Dictionary<string, string>();
        
        // Map tab names to entity type names
        private Dictionary<string, string> _tabToEntityType = new Dictionary<string, string>
        {
            { "Characters", "Character" },
            { "Traits", "Trait" },
            { "Skills", "Skill" },
            { "Effects", "Effect" },
            { "Stats", "Stat" },
            { "Tags", "Tag" }
        };

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
        private readonly Color PROJECT_NAME_COLOR = new Color(147, 112, 219); // Purple for project names

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
                defaultHeightField.SetValue(_windowManagement, 600);
            
            // Also call SetDefaultSize to ensure UpdateWindowBounds is called
            _windowManagement.SetDefaultSize(1400, 600);  // 600 matches min - 350 was too small on typical screens
            _windowManagement.SetCustomMinimumSize(1400, 500);
            _windowManagement.SetPosition(new Vector2(100, 50)); // Set initial position
            _windowManagement.SetVisible(false); // Explicitly set to not visible on startup

            // Initialize sample data
            InitializeSampleData();
            
            // Initialize default descriptions (will be overridden by JSON if available)
            InitializeDefaultDescriptions();
        }
        
        private void InitializeDefaultDescriptions()
        {
            // Short descriptions for "All" tab
            _entityDescriptionsShort["Character"] = "Defines a character template that composes stats, traits, skills, tags, and loadout into a playable or simulated entity.";
            _entityDescriptionsShort["Trait"] = "Defines a passive rule modifier that alters character behavior by modifying stats, adding triggers, granting skills, or enforcing constraints.";
            _entityDescriptionsShort["Skill"] = "Defines an active or reactive ability that a character can use, specifying costs, targeting, and which effects are executed.";
            _entityDescriptionsShort["Effect"] = "Defines an atomic gameplay operation (damage, heal, apply status, modify stat, etc.) that changes game state and is reused by skills and traits.";
            _entityDescriptionsShort["Stat"] = "Defines a numerical attribute schema (base, derived, or resource) that characters possess and that other systems reference and modify.";
            _entityDescriptionsShort["Tag"] = "Defines a semantic label used for filtering, conditions, targeting, and rule logic across characters, skills, traits, and effects.";
            
            // Long descriptions for individual tabs (formatted with subtitles)
            _entityDescriptionsLong["Character"] = "A complete, composable entity that can exist in the game world, combat simulation, or any other gameplay context. It acts as the central aggregation point where stats, traits, skills, tags, and starting state are brought together into a single definition. Characters are usually authored as templates or blueprints, which are then instantiated at runtime to create individual units with current health, cooldowns, statuses, and other mutable state.\n\nTo provide structure and ownership: traits attach to a character, skills belong to a character, and stats are evaluated in the context of a character. Without this entity, all other systems would exist in isolation with no authoritative place to resolve conflicts, compute derived values, or apply effects. Characters also define the boundary for rule evaluation, meaning that triggers, conditions, and effects always execute relative to a specific character instance.\n\nAll other entities through the Character, the engine ensures consistency and scalability. Whether the game is a turn-based RPG, a top-down action game, or a tactical simulation, the Character remains the stable anchor that systems reference when applying rules, resolving abilities, and advancing game state.";
            _entityDescriptionsLong["Trait"] = "A passive feature that permanently or semi-permanently modifies how a character behaves. Traits can adjust stats, inject new skills, register triggers, impose restrictions, or alter core rules such as costs, cooldowns, or targeting logic. Unlike skills, traits do not require explicit player activation; instead, they react to game events or continuously affect the character.\n\nTo encode identity, specialization, and long-term mechanical differences without duplicating logic across skills or characters. By attaching behavior to traits rather than hardcoding it into characters, the engine allows designers to recombine the same trait across many characters while maintaining consistent behavior. Traits also serve as a clean place to express trade-offs, synergies, and mutually exclusive design choices.\n\nHeavily to effects and skills: a trait may apply effects directly through triggers or grant access to specific skills based on conditions. This makes traits a powerful composition layer that bridges character definition and moment-to-moment gameplay logic, enabling complex behavior through data rather than code.";
            _entityDescriptionsLong["Skill"] = "An action or ability that a character can attempt to use, typically during gameplay. It specifies how the action is initiated, what it targets, what it costs, and which effects it executes when resolved. Skills can represent attacks, spells, abilities, reactions, or any other deliberate action available to a character.\n\nTo separate intent from outcome. The skill defines when and how an action can be used, while the actual gameplay impact is handled by effects. This separation allows the same effect logic to be reused across multiple skills while still allowing each skill to have unique costs, targeting rules, and conditions. Skills also provide a clear interface for AI, UI, and input systems to interact with gameplay mechanics.\n\nTo characters and traits. Characters own skills, traits may grant or restrict skills, and skills apply effects to characters or the environment. This structure allows skills to remain modular and reusable while still being deeply integrated into character progression and specialization systems.";
            _entityDescriptionsLong["Effect"] = "A single, atomic change to the game state. It represents the smallest unit of gameplay logic, such as dealing damage, healing, applying a status, modifying a stat, or spawning an entity. Effects are intentionally narrow in scope so they can be reused, combined, and sequenced without ambiguity.\n\nTo centralize and standardize game state changes. Instead of embedding damage formulas or stat changes directly into skills or traits, effects provide a shared language for all gameplay outcomes. This makes balancing, debugging, and extending the system significantly easier, as changes to an effect propagate consistently wherever it is used.\n\nTo skills, traits, and triggers, always in the context of a character or target. They rely on stats, tags, and conditions to resolve their parameters, and they may themselves apply additional traits or statuses. In this way, effects form the execution layer of the engine, turning abstract rules into concrete results.";
            _entityDescriptionsLong["Stat"] = "A numeric attribute schema that characters possess, such as health, strength, power, or initiative. It establishes the valid range, default value, and interpretation of a number, without embedding any specific gameplay behavior by itself. Stats can be base values, derived values, or resources depending on how they are configured.\n\nTo provide a flexible numerical foundation that can be referenced consistently across the engine. Skills, traits, and effects all rely on stats for scaling, conditions, and calculations, so defining stats as first-class entities prevents hardcoded assumptions and allows different games to define entirely different stat systems. This makes the engine adaptable to many genres without rewriting core logic.\n\nTo nearly every other entity: characters own stat values, traits modify them, skills consume or scale from them, and effects read or change them at runtime. By separating stat definitions from their usage, the engine ensures that numerical systems remain data-driven and extensible.";
            _entityDescriptionsLong["Tag"] = "A semantic label that can be attached to characters, traits, skills, effects, or stats to convey meaning without enforcing structure. Tags do not directly change gameplay values; instead, they are used for filtering, conditions, targeting, and rule evaluation. Examples include tags like Melee, Fire, Undead, Human, or Magical.\n\nTo decouple logic from specific entity IDs and enable flexible rule definitions. Instead of checking for exact skill or trait names, conditions can operate on tags, making systems more reusable and expressive. This allows designers to create broad interactions, such as bonuses against all Fire abilities or restrictions that apply to all Undead characters, without special-case code.\n\nThe entire system together by acting as a shared vocabulary. Skills can target by tag, traits can react to tagged events, and effects can apply differently based on tag presence. This makes tags one of the most important tools for scaling complexity while keeping the engine clean and maintainable.";
            
            _engine?.Log($"CharacterCreation: Initialized {_entityDescriptionsLong.Count} default long descriptions");
        }

        private void InitializeSampleData()
        {
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
        
        private void HandleInput()
        {
            if (_currentMouseState.LeftButton == ButtonState.Pressed && 
                _previousMouseState.LeftButton == ButtonState.Released)
            {
                // Handle category button clicks
                for (int i = 0; i < _categoryButtonBounds.Count && i < _assetCategories.Count; i++)
                {
                    if (_categoryButtonBounds[i].Contains(_currentMouseState.Position))
                    {
                        _selectedCategoryIndex = i;
                        _currentView = _assetCategories[i];
                        _isInspectingCharacter = false; // Reset inspection when switching tabs
                        
                        // Reset scroll position when switching tabs
                        if (_previousView != _currentView)
                        {
                            _rightPanelScrollY = 0;
                            _previousView = _currentView;
                        }
                        break;
                    }
                }

                // Handle character list click (Characters tab)
                if (_currentView == "Characters" && !_isInspectingCharacter && _centerPanelBounds.Contains(_currentMouseState.Position))
                {
                    bool hitDelete = false;
                    for (int i = 0; i < _characterDeleteButtonBounds.Count && i < _characters.Count; i++)
                    {
                        if (_characterDeleteButtonBounds[i].Contains(_currentMouseState.Position))
                        {
                            hitDelete = true;
                            string nameToDelete = _characters[i];
                            MarySGameEngine.Modules.PopUp_essential.PopUp.ShowConfirm(
                                "Delete Character",
                                $"Delete character \"{nameToDelete}\"?",
                                onConfirm: () =>
                                {
                                    _characters.Remove(nameToDelete);
                                    if (_isInspectingCharacter && _characterName == nameToDelete)
                                    {
                                        _isInspectingCharacter = false;
                                    }
                                },
                                onCancel: () => { },
                                confirmText: "Delete",
                                cancelText: "Cancel"
                            );
                            break;
                        }
                    }
                    if (!hitDelete)
                    {
                        for (int i = 0; i < _characterListBounds.Count && i < _characters.Count; i++)
                        {
                            if (_characterListBounds[i].Contains(_currentMouseState.Position) &&
                                !_characterDeleteButtonBounds[i].Contains(_currentMouseState.Position))
                            {
                                _characterName = _characters[i];
                                _characterTags = "Human, Melee";
                                _isInspectingCharacter = true;
                                _scrollY = 0;
                                break;
                            }
                        }
                    }
                }
            }
        }
        
        private void UpdateWorkspaceDisplay()
        {
            try
            {
                string newWorkspaceName = GetActiveWorkspaceName();
                
                if (newWorkspaceName != _previousWorkspaceName)
                {
                    _currentWorkspaceName = newWorkspaceName;
                    _previousWorkspaceName = newWorkspaceName;
                }
            }
            catch (Exception ex)
            {
                _engine?.Log($"CharacterCreation: Error updating workspace display: {ex.Message}");
            }
        }
        
        private string GetActiveWorkspaceName()
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
                                                return project.Name;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _engine?.Log($"CharacterCreation: Error getting workspace name: {ex.Message}");
            }
            
            return "No workspace";
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
                    _windowManagement.SetCustomMinimumSize(1400, 600);
                    _windowManagement.SetPosition(new Vector2(100, 50));
                    
                    // Reset to "All" tab when opening
                    _selectedCategoryIndex = 0;
                    _currentView = "All";
                    _previousView = "All";
                    _isInspectingCharacter = false;
                    _rightPanelScrollY = 0; // Reset scroll position
                    
                    UpdateBounds();
                }

                if (_windowManagement == null || !isVisible)
                    return;

                UpdateWorkspaceDisplay();
                HandleInput();
                
                // Reset scroll position if view changed
                if (_previousView != _currentView)
                {
                    _rightPanelScrollY = 0;
                    _previousView = _currentView;
                }

                UpdateBounds();
                UpdateScrolling();
                HandleScrollbarInteraction();
                UpdateRightPanelScrolling();
                HandleRightPanelScrollbarInteraction();
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

            // Only enable scrolling for character inspection view
            if (!_isInspectingCharacter)
            {
                _scrollY = 0;
                _needsScrollbar = false;
                _scrollbarBounds = Rectangle.Empty;
                return;
            }

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
            // Calculate content height based on current view
            if (_isInspectingCharacter)
            {
                // Character inspection view
            int height = 0;
                height += 30 + PANEL_PADDING; // Name and tags
                height += 500 + PANEL_PADDING * 2; // Character image
                height += 30 + PANEL_PADDING; // BASE STATS header
            height += _baseStats.Count * 35; // Stats
                height += 30 + PANEL_PADDING; // TRAITS & PERKS header
            height += _characterTraits.Count * 35; // Traits
                height += 30 + PANEL_PADDING; // SKILLS header
            height += _characterAbilities.Count * 35; // Abilities
            height += PANEL_PADDING * 2;
            return height;
            }
            else
            {
                // Tab view - minimal height needed
                return _centerPanelBounds.Height;
            }
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
            
            // Set up scissor rectangle specifically for the center panel using CURRENT window bounds
            Rectangle originalScissor = spriteBatch.GraphicsDevice.ScissorRectangle;
            int resizeHandleSize = 24;
            
            // Calculate center panel scissor based on ACTUAL current window visible area
            int centerPanelX = _centerPanelBounds.X;
            int centerPanelY = _centerPanelBounds.Y;
            int centerPanelWidth = _centerPanelBounds.Width - (_needsScrollbar ? SCROLLBAR_WIDTH + SCROLLBAR_PADDING : 0);
            
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
            
            int scissorLeft = Math.Max(centerPanelScissor.X, originalScissor.X);
            int scissorTop = Math.Max(centerPanelScissor.Y, originalScissor.Y);
            int scissorRight = Math.Min(centerPanelScissor.Right, originalScissor.Right);
            int scissorBottom = Math.Min(centerPanelScissor.Bottom, originalScissor.Bottom);
            
            int scissorWidth = Math.Max(0, scissorRight - scissorLeft);
            int scissorHeight = Math.Max(0, scissorBottom - scissorTop);
            
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
            
            // Draw content based on current view
            if (_isInspectingCharacter)
            {
                DrawCharacterInspectionView(spriteBatch, scrollOffset, scissorRect, uiFont);
            }
            else
            {
                DrawTabView(spriteBatch, scissorRect, uiFont);
            }
            
            // Restore scissor rectangle
            spriteBatch.GraphicsDevice.ScissorRectangle = originalScissor;
            spriteBatch.GraphicsDevice.RasterizerState = new RasterizerState
            {
                ScissorTestEnable = false
            };
        }
        
        private void DrawTabView(SpriteBatch spriteBatch, Rectangle scissorRect, SpriteFont uiFont)
        {
            // Draw content based on selected tab
            if (_currentView == "All")
            {
                DrawAllTab(spriteBatch, scissorRect, uiFont);
            }
            else if (_currentView == "Characters")
            {
                if (_characters.Count > 0)
                    DrawCharactersListTab(spriteBatch, scissorRect, uiFont);
                else
                    DrawEmptyTab(spriteBatch, scissorRect, uiFont, "characters", "Create Character");
            }
            else if (_currentView == "Traits")
            {
                DrawEmptyTab(spriteBatch, scissorRect, uiFont, "traits", "Create Trait");
            }
            else if (_currentView == "Skills")
            {
                DrawEmptyTab(spriteBatch, scissorRect, uiFont, "skills", "Create Skill");
            }
            else if (_currentView == "Effects")
            {
                DrawEmptyTab(spriteBatch, scissorRect, uiFont, "effects", "Create Effect");
            }
            else if (_currentView == "Stats")
            {
                DrawEmptyTab(spriteBatch, scissorRect, uiFont, "stats", "Create Stat");
            }
            else if (_currentView == "Tags")
            {
                DrawEmptyTab(spriteBatch, scissorRect, uiFont, "tags", "Create Tag");
            }
        }
        
        private void DrawAllTab(SpriteBatch spriteBatch, Rectangle scissorRect, SpriteFont uiFont)
        {
            // Check if there are any entities
            bool hasEntities = false; // For now, always show empty state
            
            if (!hasEntities)
            {
                // Draw "Nothing is there" message
                string message = "Nothing is there";
                string subMessage = "Create your first entity to get started";
                
                Vector2 messageSize = uiFont.MeasureString(message);
                Vector2 subMessageSize = uiFont.MeasureString(subMessage);
                
                Vector2 messagePosition = new Vector2(
                    _centerPanelBounds.X + (_centerPanelBounds.Width - messageSize.X) / 2,
                    _centerPanelBounds.Y + PANEL_PADDING * 2
                );
                
                Vector2 subMessagePosition = new Vector2(
                    _centerPanelBounds.X + (_centerPanelBounds.Width - subMessageSize.X) / 2,
                    messagePosition.Y + messageSize.Y + 20
                );
                
                spriteBatch.DrawString(uiFont, message, messagePosition, TEXT_COLOR);
                spriteBatch.DrawString(uiFont, subMessage, subMessagePosition, TEXT_SECONDARY);
                
                // Draw all entity type buttons in a grid
                int startY = (int)subMessagePosition.Y + (int)subMessageSize.Y + 40;
                int buttonWidth = 180;
                int buttonHeight = 35;
                int buttonSpacing = 15;
                int buttonsPerRow = 3;
                
                // Calculate starting X to center the grid
                int totalWidth = (buttonsPerRow * buttonWidth) + ((buttonsPerRow - 1) * buttonSpacing);
                int startX = _centerPanelBounds.X + (_centerPanelBounds.Width - totalWidth) / 2;
                
                // List of all entity types
                string[] entityTypes = { "Character", "Trait", "Skill", "Effect", "Stat", "Tag" };
                
                int buttonIndex = 0;
                foreach (string entityType in entityTypes)
                {
                    int row = buttonIndex / buttonsPerRow;
                    int col = buttonIndex % buttonsPerRow;
                    
                    int buttonX = startX + col * (buttonWidth + buttonSpacing);
                    int buttonY = startY + row * (buttonHeight + buttonSpacing);
                    
                    DrawDisabledButton(spriteBatch, $"Create {entityType}", buttonX, buttonY, buttonWidth, uiFont);
                    
                    buttonIndex++;
                }
            }
        }
        
        private void DrawEmptyTab(SpriteBatch spriteBatch, Rectangle scissorRect, SpriteFont uiFont, string entityType, string buttonText)
        {
            string workspaceName = !string.IsNullOrEmpty(_currentWorkspaceName) && _currentWorkspaceName != "No workspace" 
                ? _currentWorkspaceName 
                : "this project";
            
            string messagePrefix = $"No {entityType} in the project ";
            string messageSuffix = " there yet";
            string subMessage = $"Create first one to get started";
            
            // Measure text parts
            Vector2 prefixSize = uiFont.MeasureString(messagePrefix);
            Vector2 workspaceNameSize = uiFont.MeasureString(workspaceName);
            Vector2 suffixSize = uiFont.MeasureString(messageSuffix);
            Vector2 subMessageSize = uiFont.MeasureString(subMessage);
            
            // Account for bold scale when calculating total width
            float boldScale = 1.1f; // Slightly larger for bold effect
            float totalMessageWidth = prefixSize.X + (workspaceNameSize.X * boldScale) + suffixSize.X;
            
            Vector2 messagePosition = new Vector2(
                _centerPanelBounds.X + (_centerPanelBounds.Width - totalMessageWidth) / 2,
                _centerPanelBounds.Y + (_centerPanelBounds.Height - prefixSize.Y) / 2 - 30
            );
            
            // Draw message parts with project name in purple and bold
            Vector2 currentPos = messagePosition;
            spriteBatch.DrawString(uiFont, messagePrefix, currentPos, TEXT_COLOR);
            currentPos.X += prefixSize.X;
            
            // Draw project name in purple with bold effect (slightly larger scale)
            Vector2 workspaceNamePos = new Vector2(
                currentPos.X,
                currentPos.Y - (workspaceNameSize.Y * (boldScale - 1.0f)) / 2
            );
            spriteBatch.DrawString(uiFont, workspaceName, workspaceNamePos, PROJECT_NAME_COLOR, 0f, Vector2.Zero, boldScale, SpriteEffects.None, 0f);
            currentPos.X += workspaceNameSize.X * boldScale;
            
            spriteBatch.DrawString(uiFont, messageSuffix, currentPos, TEXT_COLOR);
            
            Vector2 subMessagePosition = new Vector2(
                _centerPanelBounds.X + (_centerPanelBounds.Width - subMessageSize.X) / 2,
                messagePosition.Y + prefixSize.Y + 20
            );
            
            spriteBatch.DrawString(uiFont, subMessage, subMessagePosition, TEXT_SECONDARY);
            
            // Draw create button (disabled for now)
            int buttonY = (int)subMessagePosition.Y + (int)subMessageSize.Y + 30;
            int buttonX = _centerPanelBounds.X + (_centerPanelBounds.Width - 200) / 2;
            DrawDisabledButton(spriteBatch, buttonText, buttonX, buttonY, 200, uiFont);
        }

        private void DrawCharactersListTab(SpriteBatch spriteBatch, Rectangle scissorRect, SpriteFont uiFont)
        {
            _characterListBounds.Clear();
            _characterDeleteButtonBounds.Clear();
            int rowHeight = 40;
            int padding = PANEL_PADDING;
            int deleteBtnWidth = 60;
            int deleteBtnMargin = 8;
            int y = _centerPanelBounds.Y + padding;

            foreach (string name in _characters)
            {
                Rectangle rowBounds = new Rectangle(
                    _centerPanelBounds.X + padding,
                    y,
                    _centerPanelBounds.Width - padding * 2,
                    rowHeight
                );
                _characterListBounds.Add(rowBounds);

                Rectangle deleteBounds = new Rectangle(
                    rowBounds.Right - deleteBtnWidth - deleteBtnMargin,
                    rowBounds.Y + (rowHeight - 28) / 2,
                    deleteBtnWidth,
                    28
                );
                _characterDeleteButtonBounds.Add(deleteBounds);

                if (IsVisible(rowBounds, scissorRect))
                {
                    var mousePos = _currentMouseState.Position;
                    bool rowHover = rowBounds.Contains(mousePos) && !deleteBounds.Contains(mousePos);
                    Color bg = rowHover ? BUTTON_HOVER : BUTTON_COLOR;
                    spriteBatch.Draw(_pixel, rowBounds, bg);
                    DrawBorder(spriteBatch, rowBounds, PANEL_BORDER);

                    Vector2 textPos = new Vector2(rowBounds.X + 12, rowBounds.Y + (rowHeight - uiFont.MeasureString(name).Y) / 2);
                    spriteBatch.DrawString(uiFont, name, textPos, TEXT_COLOR);

                    bool deleteHover = deleteBounds.Contains(mousePos);
                    Color deleteBg = deleteHover ? new Color(180, 60, 60) : new Color(120, 40, 40);
                    spriteBatch.Draw(_pixel, deleteBounds, deleteBg);
                    DrawBorder(spriteBatch, deleteBounds, PANEL_BORDER);
                    string deleteLabel = "Delete";
                    Vector2 deleteTextPos = new Vector2(
                        deleteBounds.X + (deleteBounds.Width - uiFont.MeasureString(deleteLabel).X) / 2,
                        deleteBounds.Y + (deleteBounds.Height - uiFont.MeasureString(deleteLabel).Y) / 2
                    );
                    spriteBatch.DrawString(uiFont, deleteLabel, deleteTextPos, TEXT_COLOR);
                }

                y += rowHeight + 4;
            }
        }
        
        private void DrawDisabledButton(SpriteBatch spriteBatch, string text, int x, int y, int width, SpriteFont font)
        {
            Rectangle buttonBounds = new Rectangle(x, y, width, 35);
            Color disabledColor = new Color(BUTTON_COLOR.R / 2, BUTTON_COLOR.G / 2, BUTTON_COLOR.B / 2);
            spriteBatch.Draw(_pixel, buttonBounds, disabledColor);
            DrawBorder(spriteBatch, buttonBounds, new Color(PANEL_BORDER.R / 2, PANEL_BORDER.G / 2, PANEL_BORDER.B / 2));
            
            Vector2 textSize = font.MeasureString(text);
            Vector2 textPos = new Vector2(
                buttonBounds.X + (buttonBounds.Width - textSize.X) / 2,
                buttonBounds.Y + (buttonBounds.Height - textSize.Y) / 2
            );
            spriteBatch.DrawString(font, text, textPos, new Color(TEXT_COLOR.R / 2, TEXT_COLOR.G / 2, TEXT_COLOR.B / 2));
        }
        
        private void DrawCharacterInspectionView(SpriteBatch spriteBatch, int scrollOffset, Rectangle scissorRect, SpriteFont uiFont)
        {
            // Apply scroll offset to all drawing positions
            int scrollY = -scrollOffset;

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

            // Draw SKILLS section (apply scroll offset) - only if visible
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
                spriteBatch.DrawString(uiFont, "SKILLS", abilitiesSectionPos, TEXT_COLOR);
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
        }

        private void DrawRightPanel(SpriteBatch spriteBatch)
        {
            if (_pixel == null || _menuFont == null) return;
            if (_rightPanelBounds.Width <= 0 || _rightPanelBounds.Height <= 0) return;
            
            SpriteFont uiFont = _uiFont ?? _menuFont;
            
            // Draw panel background
            spriteBatch.Draw(_pixel, _rightPanelBounds, PANEL_BACKGROUND);
            DrawBorder(spriteBatch, _rightPanelBounds, PANEL_BORDER);
            
            // Set up scissor rectangle for right panel content (intersect with existing scissor)
            Rectangle originalScissor = spriteBatch.GraphicsDevice.ScissorRectangle;
            int scissorWidth = _rightPanelBounds.Width - (_rightPanelNeedsScrollbar ? SCROLLBAR_WIDTH + SCROLLBAR_PADDING : 0);
            
            // Ensure scissor starts at panel top, not above it
            // Intersect with existing scissor and window bounds, but ensure we don't go above panel top
            int scissorLeft = Math.Max(_rightPanelBounds.X, originalScissor.X);
            int scissorTop = Math.Max(_rightPanelBounds.Y, Math.Max(originalScissor.Y, _rightPanelBounds.Y));
            int scissorRight = Math.Min(_rightPanelBounds.X + scissorWidth, originalScissor.Right);
            int scissorBottom = Math.Min(_rightPanelBounds.Bottom, originalScissor.Bottom);
            
            int finalScissorWidth = Math.Max(0, scissorRight - scissorLeft);
            int finalScissorHeight = Math.Max(0, scissorBottom - scissorTop);
            
            Rectangle rightPanelScissor = new Rectangle(
                scissorLeft,
                scissorTop,
                finalScissorWidth,
                finalScissorHeight
            );
            
            spriteBatch.GraphicsDevice.ScissorRectangle = rightPanelScissor;
            spriteBatch.GraphicsDevice.RasterizerState = new RasterizerState
            {
                ScissorTestEnable = true,
                CullMode = CullMode.None
            };
            
            // Draw entity type descriptions
            int scrollY = -_rightPanelScrollY;
            // Start drawing from panel top, applying scroll offset
            // Note: currentY can go above panel top when scrolling - visibility checks will prevent drawing outside bounds
            int currentY = _rightPanelBounds.Y + PANEL_PADDING + scrollY;
            float headerBoldScale = 1.3f; // Larger scale for entity names
            float subtitleScale = 1.1f; // Slightly larger for subtitles
            float lineSpacing = uiFont.LineSpacing * 1.2f;
            float descriptionLineSpacing = uiFont.LineSpacing * 1.1f;
            
            if (_currentView == "All")
            {
                // Draw all entity type descriptions
                string[] entityTypes = { "Character", "Trait", "Skill", "Effect", "Stat", "Tag" };
                
                foreach (string entityType in entityTypes)
                {
                    if (_entityDescriptionsShort != null && _entityDescriptionsShort.ContainsKey(entityType))
                    {
                        // Draw header in purple and bold
                        Vector2 headerSize = uiFont.MeasureString(entityType) * headerBoldScale;
                        Vector2 headerPos = new Vector2(_rightPanelBounds.X + PANEL_PADDING, currentY);
                        
                        // Only draw if header is visible within scissor bounds and panel bounds
                        if (currentY >= _rightPanelBounds.Y && currentY + headerSize.Y <= _rightPanelBounds.Bottom && 
                            currentY + headerSize.Y >= rightPanelScissor.Y && currentY <= rightPanelScissor.Bottom)
                        {
                            spriteBatch.DrawString(uiFont, entityType, headerPos, PROJECT_NAME_COLOR, 0f, Vector2.Zero, headerBoldScale, SpriteEffects.None, 0f);
                        }
                        currentY += (int)(headerSize.Y + 10);
                        
                        // Draw short description (word wrap) for "All" tab
                        string description = _entityDescriptionsShort[entityType];
                        float maxWidth = _rightPanelBounds.Width - PANEL_PADDING * 2 - (_rightPanelNeedsScrollbar ? SCROLLBAR_WIDTH : 0);
                        currentY = DrawWrappedText(spriteBatch, uiFont, description, 
                            _rightPanelBounds.X + PANEL_PADDING, currentY, maxWidth, TEXT_COLOR, descriptionLineSpacing, rightPanelScissor);
                        
                        currentY += 30; // Spacing between sections
                    }
                }
            }
            else
            {
                // Draw only the relevant entity type description
                // Map tab name to entity type name
                string entityType = _currentView;
                if (_tabToEntityType.ContainsKey(_currentView))
                {
                    entityType = _tabToEntityType[_currentView];
                }
                
                // Use long description for individual tabs
                if (_entityDescriptionsLong.ContainsKey(entityType))
                {
                    // Draw entity name header in purple and bold (larger)
                    Vector2 headerSize = uiFont.MeasureString(entityType) * headerBoldScale;
                    Vector2 headerPos = new Vector2(_rightPanelBounds.X + PANEL_PADDING, currentY);
                    
                    // Only draw if visible within scissor bounds and panel bounds
                    if (currentY >= _rightPanelBounds.Y && currentY + headerSize.Y <= _rightPanelBounds.Bottom && 
                        currentY + headerSize.Y >= rightPanelScissor.Y && currentY <= rightPanelScissor.Bottom)
                    {
                        spriteBatch.DrawString(uiFont, entityType, headerPos, PROJECT_NAME_COLOR, 0f, Vector2.Zero, headerBoldScale, SpriteEffects.None, 0f);
                    }
                    currentY += (int)(headerSize.Y + 15);
                    
                    // Draw long description with paragraph subtitles
                    string description = _entityDescriptionsLong[entityType];
                    float maxWidth = _rightPanelBounds.Width - PANEL_PADDING * 2 - (_rightPanelNeedsScrollbar ? SCROLLBAR_WIDTH : 0);
                    DrawLongDescriptionWithSubtitles(spriteBatch, uiFont, description, 
                        _rightPanelBounds.X + PANEL_PADDING, currentY, maxWidth, TEXT_COLOR, descriptionLineSpacing, rightPanelScissor, subtitleScale);
                }
                else if (_entityDescriptionsShort.ContainsKey(entityType))
                {
                    // Fallback to short description if long is not available
                    Vector2 headerSize = uiFont.MeasureString(entityType) * headerBoldScale;
                    Vector2 headerPos = new Vector2(_rightPanelBounds.X + PANEL_PADDING, currentY);
                    
                    if (currentY >= _rightPanelBounds.Y && currentY + headerSize.Y <= _rightPanelBounds.Bottom && 
                        currentY + headerSize.Y >= rightPanelScissor.Y && currentY <= rightPanelScissor.Bottom)
                    {
                        spriteBatch.DrawString(uiFont, entityType, headerPos, PROJECT_NAME_COLOR, 0f, Vector2.Zero, headerBoldScale, SpriteEffects.None, 0f);
                    }
                    currentY += (int)(headerSize.Y + 10);
                    
                    string description = _entityDescriptionsShort[entityType];
                    float maxWidth = _rightPanelBounds.Width - PANEL_PADDING * 2 - (_rightPanelNeedsScrollbar ? SCROLLBAR_WIDTH : 0);
                    DrawWrappedText(spriteBatch, uiFont, description, 
                        _rightPanelBounds.X + PANEL_PADDING, currentY, maxWidth, TEXT_COLOR, descriptionLineSpacing, rightPanelScissor);
                }
            }
            
            // Restore scissor rectangle
            spriteBatch.GraphicsDevice.ScissorRectangle = originalScissor;
            spriteBatch.GraphicsDevice.RasterizerState = new RasterizerState
            {
                ScissorTestEnable = false
            };
            
            // Draw scrollbar if needed
            if (_rightPanelNeedsScrollbar)
            {
                DrawRightPanelScrollbar(spriteBatch);
            }
        }
        
        private int DrawWrappedText(SpriteBatch spriteBatch, SpriteFont font, string text, int x, int y, float maxWidth, Color color, float lineSpacing, Rectangle scissorRect)
        {
            // Split by newlines first to handle paragraph breaks
            string[] paragraphs = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            int currentY = y;
            
            foreach (string paragraph in paragraphs)
            {
                if (string.IsNullOrWhiteSpace(paragraph))
                {
                    currentY += (int)lineSpacing; // Extra spacing for empty paragraphs
                    continue;
                }
                
                string[] words = paragraph.Split(' ');
                string currentLine = "";
                
                foreach (string word in words)
                {
                    string testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                    Vector2 size = font.MeasureString(testLine);
                    
                    if (size.X > maxWidth && !string.IsNullOrEmpty(currentLine))
                    {
                        // Only draw if line is visible within scissor bounds
                        if (currentY >= scissorRect.Y && currentY <= scissorRect.Bottom)
                        {
                            spriteBatch.DrawString(font, currentLine, new Vector2(x, currentY), color);
                        }
                        currentY += (int)lineSpacing;
                        currentLine = word;
                    }
                    else
                    {
                        currentLine = testLine;
                    }
                }
                
                // Draw last line of paragraph if visible
                if (!string.IsNullOrEmpty(currentLine))
                {
                    if (currentY >= scissorRect.Y && currentY <= scissorRect.Bottom)
                    {
                        spriteBatch.DrawString(font, currentLine, new Vector2(x, currentY), color);
                    }
                    currentY += (int)lineSpacing;
                }
                
                // Add spacing between paragraphs
                currentY += (int)(lineSpacing * 0.5f);
            }
            
            return currentY;
        }
        
        private int DrawLongDescriptionWithSubtitles(SpriteBatch spriteBatch, SpriteFont font, string text, int x, int y, float maxWidth, Color color, float lineSpacing, Rectangle scissorRect, float subtitleScale)
        {
            // Split by newlines to get paragraphs
            string[] paragraphs = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            int currentY = y;
            string[] subtitles = { "Defines", "Exists", "Links" };
            int subtitleIndex = 0;
            
            foreach (string paragraph in paragraphs)
            {
                if (string.IsNullOrWhiteSpace(paragraph))
                {
                    currentY += (int)lineSpacing;
                    continue;
                }
                
                // Draw subtitle if available
                if (subtitleIndex < subtitles.Length)
                {
                    string subtitle = subtitles[subtitleIndex];
                    Vector2 subtitleSize = font.MeasureString(subtitle) * subtitleScale;
                    Vector2 subtitlePos = new Vector2(x, currentY);
                    
                    // Only draw if visible
                    if (currentY >= scissorRect.Y && currentY + subtitleSize.Y <= scissorRect.Bottom)
                    {
                        spriteBatch.DrawString(font, subtitle, subtitlePos, PROJECT_NAME_COLOR, 0f, Vector2.Zero, subtitleScale, SpriteEffects.None, 0f);
                    }
                    currentY += (int)(subtitleSize.Y + 5);
                    subtitleIndex++;
                }
                
                // Draw paragraph text (word wrap)
                string[] words = paragraph.Split(' ');
                string currentLine = "";
                
                foreach (string word in words)
                {
                    string testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                    Vector2 size = font.MeasureString(testLine);
                    
                    if (size.X > maxWidth && !string.IsNullOrEmpty(currentLine))
                    {
                        // Only draw if line is visible within scissor bounds
                        if (currentY >= scissorRect.Y && currentY <= scissorRect.Bottom)
                        {
                            spriteBatch.DrawString(font, currentLine, new Vector2(x, currentY), color);
                        }
                        currentY += (int)lineSpacing;
                        currentLine = word;
                    }
                    else
                    {
                        currentLine = testLine;
                    }
                }
                
                // Draw last line of paragraph if visible
                if (!string.IsNullOrEmpty(currentLine))
                {
                    if (currentY >= scissorRect.Y && currentY <= scissorRect.Bottom)
                    {
                        spriteBatch.DrawString(font, currentLine, new Vector2(x, currentY), color);
                    }
                    currentY += (int)lineSpacing;
                }
                
                // Add spacing between paragraphs
                currentY += (int)(lineSpacing * 0.8f);
            }
            
            return currentY;
        }
        
        private void UpdateRightPanelScrolling()
        {
            if (_windowManagement == null || !_windowManagement.IsVisible())
                return;
            
            // Calculate content height
            _rightPanelContentHeight = CalculateRightPanelContentHeight();
            
            // Check if scrollbar is needed
            _rightPanelNeedsScrollbar = _rightPanelContentHeight > _rightPanelBounds.Height;
            
            if (_rightPanelNeedsScrollbar)
            {
                // Update scrollbar bounds
                _rightPanelScrollbarBounds = new Rectangle(
                    _rightPanelBounds.Right - SCROLLBAR_WIDTH - 2,
                    _rightPanelBounds.Y,
                    SCROLLBAR_WIDTH,
                    _rightPanelBounds.Height
                );
                
                // Clamp scroll position
                int maxScroll = Math.Max(0, _rightPanelContentHeight - _rightPanelBounds.Height);
                _rightPanelScrollY = MathHelper.Clamp(_rightPanelScrollY, 0, maxScroll);
            }
            else
            {
                _rightPanelScrollY = 0;
                _rightPanelScrollbarBounds = Rectangle.Empty;
            }
        }
        
        private int CalculateRightPanelContentHeight()
        {
            if (_uiFont == null) return 0;
            
            float headerBoldScale = 1.3f; // Larger scale for entity names
            float subtitleScale = 1.1f; // Slightly larger for subtitles
            float lineSpacing = _uiFont.LineSpacing * 1.2f;
            float descriptionLineSpacing = _uiFont.LineSpacing * 1.1f;
            float maxWidth = _rightPanelBounds.Width - PANEL_PADDING * 2 - SCROLLBAR_WIDTH;
            int height = PANEL_PADDING;
            
            if (_currentView == "All")
            {
                string[] entityTypes = { "Character", "Trait", "Skill", "Effect", "Stat", "Tag" };
                
                foreach (string entityType in entityTypes)
                {
                    if (_entityDescriptionsShort.ContainsKey(entityType))
                    {
                        // Header height
                        Vector2 headerSize = _uiFont.MeasureString(entityType) * headerBoldScale;
                        height += (int)headerSize.Y + 10;
                        
                        // Description height (word wrap calculation) - use short for "All" tab
                        string description = _entityDescriptionsShort[entityType];
                        string[] words = description.Split(' ');
                        string currentLine = "";
                        int lines = 0;
                        
                        foreach (string word in words)
                        {
                            string testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                            Vector2 size = _uiFont.MeasureString(testLine);
                            
                            if (size.X > maxWidth && !string.IsNullOrEmpty(currentLine))
                            {
                                lines++;
                                currentLine = word;
                            }
                            else
                            {
                                currentLine = testLine;
                            }
                        }
                        if (!string.IsNullOrEmpty(currentLine)) lines++;
                        
                        height += (int)(lines * descriptionLineSpacing);
                        height += 30; // Spacing between sections
                    }
                }
            }
            else
            {
                // Map tab name to entity type name
                string entityType = _currentView;
                if (_tabToEntityType.ContainsKey(_currentView))
                {
                    entityType = _tabToEntityType[_currentView];
                }
                
                // Use long description for individual tabs
                if (_entityDescriptionsLong != null && _entityDescriptionsLong.ContainsKey(entityType))
                {
                    // Header height (larger entity name)
                    Vector2 headerSize = _uiFont.MeasureString(entityType) * headerBoldScale;
                    height += (int)headerSize.Y + 15;
                    
                    // Description height (word wrap calculation) - use long for individual tabs with subtitles
                    string description = _entityDescriptionsLong[entityType];
                    // Split by newlines first to handle paragraph breaks
                    string[] paragraphs = description.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    int lines = 0;
                    string[] subtitles = { "Defines", "Exists", "Links" };
                    int subtitleIndex = 0;
                    
                    foreach (string paragraph in paragraphs)
                    {
                        if (string.IsNullOrWhiteSpace(paragraph))
                        {
                            lines++;
                            continue;
                        }
                        
                        // Add subtitle height
                        if (subtitleIndex < subtitles.Length)
                        {
                            Vector2 subtitleSize = _uiFont.MeasureString(subtitles[subtitleIndex]) * subtitleScale;
                            height += (int)(subtitleSize.Y + 5);
                            subtitleIndex++;
                        }
                        
                        string[] words = paragraph.Split(' ');
                        string currentLine = "";
                        
                        foreach (string word in words)
                        {
                            string testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                            Vector2 size = _uiFont.MeasureString(testLine);
                            
                            if (size.X > maxWidth && !string.IsNullOrEmpty(currentLine))
                            {
                                lines++;
                                currentLine = word;
                            }
                            else
                            {
                                currentLine = testLine;
                            }
                        }
                        if (!string.IsNullOrEmpty(currentLine)) lines++;
                        lines++; // Spacing between paragraphs
                    }
                    
                    height += (int)(lines * descriptionLineSpacing);
                }
                else if (_entityDescriptionsShort != null && _entityDescriptionsShort.ContainsKey(entityType))
                {
                    // Fallback to short description if long is not available
                    Vector2 headerSize = _uiFont.MeasureString(entityType) * headerBoldScale;
                    height += (int)headerSize.Y + 10;
                    
                    string description = _entityDescriptionsShort[entityType];
                    string[] words = description.Split(' ');
                    string currentLine = "";
                    int lines = 0;
                    
                    foreach (string word in words)
                    {
                        string testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                        Vector2 size = _uiFont.MeasureString(testLine);
                        
                        if (size.X > maxWidth && !string.IsNullOrEmpty(currentLine))
                        {
                            lines++;
                            currentLine = word;
                        }
                        else
                        {
                            currentLine = testLine;
                        }
                    }
                    if (!string.IsNullOrEmpty(currentLine)) lines++;
                    
                    height += (int)(lines * descriptionLineSpacing);
                }
            }
            
            height += PANEL_PADDING;
            return height;
        }
        
        private void HandleRightPanelScrollbarInteraction()
        {
            if (!_rightPanelNeedsScrollbar) return;
            
            var mousePosition = _currentMouseState.Position;
            
            // Handle scrollbar dragging
            if (_currentMouseState.LeftButton == ButtonState.Pressed && 
                _previousMouseState.LeftButton == ButtonState.Released)
            {
                if (_rightPanelScrollbarBounds.Contains(mousePosition))
                {
                    _isDraggingRightPanelScrollbar = true;
                    _rightPanelScrollbarDragStart = new Vector2(mousePosition.X, mousePosition.Y);
                }
            }
            
            if (_isDraggingRightPanelScrollbar)
            {
                if (_currentMouseState.LeftButton == ButtonState.Pressed)
                {
                    float deltaY = mousePosition.Y - _rightPanelScrollbarDragStart.Y;
                    float scrollbarHeight = _rightPanelScrollbarBounds.Height;
                    
                    float scrollRatio = deltaY / scrollbarHeight;
                    _rightPanelScrollY = (int)(scrollRatio * (_rightPanelContentHeight - _rightPanelBounds.Height));
                    
                    int maxScroll = Math.Max(0, _rightPanelContentHeight - _rightPanelBounds.Height);
                    _rightPanelScrollY = MathHelper.Clamp(_rightPanelScrollY, 0, maxScroll);
                }
                else
                {
                    _isDraggingRightPanelScrollbar = false;
                }
            }
            
            // Handle mouse wheel scrolling
            if (_rightPanelBounds.Contains(mousePosition) && 
                _currentMouseState.ScrollWheelValue != _previousMouseState.ScrollWheelValue)
            {
                int delta = _currentMouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
                _rightPanelScrollY -= delta / 3;
                
                int maxScroll = Math.Max(0, _rightPanelContentHeight - _rightPanelBounds.Height);
                _rightPanelScrollY = MathHelper.Clamp(_rightPanelScrollY, 0, maxScroll);
            }
        }
        
        private void DrawRightPanelScrollbar(SpriteBatch spriteBatch)
        {
            if (!_rightPanelNeedsScrollbar || _rightPanelScrollbarBounds.IsEmpty) return;
            if (_pixel == null) return;
            
            // Draw scrollbar background
            spriteBatch.Draw(_pixel, _rightPanelScrollbarBounds, new Color(55, 65, 81));
            DrawBorder(spriteBatch, _rightPanelScrollbarBounds, PANEL_BORDER);
            
            // Calculate thumb size and position
            float contentRatio = (float)_rightPanelBounds.Height / Math.Max(1, _rightPanelContentHeight);
            int thumbHeight = Math.Max(20, (int)(_rightPanelScrollbarBounds.Height * contentRatio));
            
            int maxScroll = Math.Max(1, _rightPanelContentHeight - _rightPanelBounds.Height);
            int thumbY = _rightPanelScrollbarBounds.Y + (int)((_rightPanelScrollbarBounds.Height - thumbHeight) * (_rightPanelScrollY / (float)maxScroll));
            
            var thumbBounds = new Rectangle(_rightPanelScrollbarBounds.X + 2, thumbY + 2, _rightPanelScrollbarBounds.Width - 4, thumbHeight - 4);
            bool isThumbHovered = thumbBounds.Contains(_currentMouseState.Position) || _isDraggingRightPanelScrollbar;
            Color thumbColor = isThumbHovered ? BUTTON_HOVER : BUTTON_COLOR;
            
            spriteBatch.Draw(_pixel, thumbBounds, thumbColor);
            DrawBorder(spriteBatch, thumbBounds, PANEL_BORDER);
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
                
                // Load entity descriptions from JSON (will override defaults if JSON exists)
                LoadEntityDescriptions();
            }
            catch (Exception ex)
            {
                _engine?.Log($"CharacterCreation: Error in LoadContent: {ex.Message}");
                _uiFont = _menuFont; // Fallback to menu font
                _pixelFont = _menuFont;
            }
        }
        
        private void LoadEntityDescriptions()
        {
            try
            {
                string descriptionsPath = Path.Combine(Directory.GetCurrentDirectory(), "Content", "Modules", "CharacterCreation", "EntityDescriptions.json");
                
                if (File.Exists(descriptionsPath))
                {
                    string jsonContent = File.ReadAllText(descriptionsPath);
                    
                    using (JsonDocument doc = JsonDocument.Parse(jsonContent))
                    {
                        var root = doc.RootElement;
                        
                        // Load short descriptions (override defaults)
                        if (root.TryGetProperty("short", out JsonElement shortElement))
                        {
                            int count = 0;
                            foreach (var prop in shortElement.EnumerateObject())
                            {
                                _entityDescriptionsShort[prop.Name] = prop.Value.GetString() ?? "";
                                count++;
                            }
                            _engine?.Log($"CharacterCreation: Loaded {count} short descriptions from JSON (total: {_entityDescriptionsShort.Count})");
                        }
                        
                        // Load long descriptions (override defaults)
                        if (root.TryGetProperty("long", out JsonElement longElement))
                        {
                            int count = 0;
                            foreach (var prop in longElement.EnumerateObject())
                            {
                                _entityDescriptionsLong[prop.Name] = prop.Value.GetString() ?? "";
                                count++;
                            }
                            _engine?.Log($"CharacterCreation: Loaded {count} long descriptions from JSON (total: {_entityDescriptionsLong.Count})");
                        }
                    }
                }
                else
                {
                    _engine?.Log($"CharacterCreation: EntityDescriptions.json not found at {descriptionsPath}, using default descriptions");
                }
            }
            catch (Exception ex)
            {
                _engine?.Log($"CharacterCreation: Error loading entity descriptions: {ex.Message}");
                _engine?.Log($"CharacterCreation: Stack trace: {ex.StackTrace}");
            }
        }

        public void Dispose()
        {
            _pixel?.Dispose();
        }
    }
}

