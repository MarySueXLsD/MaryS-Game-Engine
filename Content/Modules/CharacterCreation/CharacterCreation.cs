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
    public partial class CharacterCreation : IModule
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
        private Point _hoverMousePosition; // When a popup is active, set off-screen so no hover on list/buttons
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
        private List<string> _characters = new List<string>();
        private List<Rectangle> _characterListBounds = new List<Rectangle>();
        private List<Rectangle> _characterDeleteButtonBounds = new List<Rectangle>();

        // All-tab entity lists (traits, skills, effects, stats, tags) - create/delete only, no inspection
        private List<string> _traits = new List<string>();
        private List<string> _skills = new List<string>();
        private List<string> _effects = new List<string>();
        private List<string> _stats = new List<string>();
        private List<string> _tags = new List<string>();
        private List<Rectangle> _traitListBounds = new List<Rectangle>();
        private List<Rectangle> _traitDeleteBounds = new List<Rectangle>();
        private List<Rectangle> _skillListBounds = new List<Rectangle>();
        private List<Rectangle> _skillDeleteBounds = new List<Rectangle>();
        private List<Rectangle> _effectListBounds = new List<Rectangle>();
        private List<Rectangle> _effectDeleteBounds = new List<Rectangle>();
        private List<Rectangle> _statListBounds = new List<Rectangle>();
        private List<Rectangle> _statDeleteBounds = new List<Rectangle>();
        private List<Rectangle> _tagListBounds = new List<Rectangle>();
        private List<Rectangle> _tagDeleteBounds = new List<Rectangle>();
        private Rectangle[] _allTabCreateButtonBounds = new Rectangle[6]; // Character, Trait, Skill, Effect, Stat, Tag
        private bool _allTabViewIsGrid = false; // false = List, true = Grid (applies to all sections in All tab)
        private Rectangle _allTabViewListButtonBounds = Rectangle.Empty;
        private Rectangle _allTabViewGridButtonBounds = Rectangle.Empty;

        // List/grid "alive" animation: gentle pulse and border glow
        private DateTime _listViewAnimStart = DateTime.UtcNow;
        // Left sidebar light glints (блики света): occasional sweep on category buttons
        private DateTime _sidebarGlintStart = DateTime.UtcNow;

        // Center Panel - Character Details
        private string _characterId = "Knight_Warrior";
        private string _characterName = "Knight_Warrior";
        private Dictionary<string, string> _characterNames = new Dictionary<string, string>();
        private Dictionary<string, string> _characterTags = new Dictionary<string, string>();

        private bool _isEditingId = false;
        private bool _isEditingName = false;
        private Rectangle _nameInputBounds = Rectangle.Empty;

        // ID editing state
        private string _idEditBuffer = "";
        private int _idCursorPosition = 0;
        private int _idAnchorPosition = 0;
        private float _idCursorBlinkTimer = 0f;
        private bool _idShowCursor = true;
        private Rectangle _idEditButtonBounds = Rectangle.Empty;
        private Rectangle _idConfirmButtonBounds = Rectangle.Empty;
        private Rectangle _idTextBounds = Rectangle.Empty;
        private Rectangle _backButtonBounds = Rectangle.Empty;

        // Name editing state
        private string _nameEditBuffer = "";
        private int _nameCursorPosition = 0;
        private int _nameAnchorPosition = 0;
        private KeyboardState _currentKeyboardState;
        private KeyboardState _previousKeyboardState;
        private float _nameCursorBlinkTimer = 0f;
        private bool _nameShowCursor = true;
        private Rectangle _nameEditButtonBounds = Rectangle.Empty;
        private Rectangle _nameConfirmButtonBounds = Rectangle.Empty;
        private Rectangle _nameTextBounds = Rectangle.Empty;
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

        // Toolbar (top bar with action buttons in center panel)
        private Rectangle _toolbarBounds = Rectangle.Empty;
        private Rectangle _createCharacterButtonBounds = Rectangle.Empty;
        private Rectangle _emptyTabCreateButtonBounds = Rectangle.Empty;
        private const int TOOLBAR_HEIGHT = 40;

        // Panel widths
        private const int LEFT_PANEL_WIDTH = 250;
        private const int RIGHT_PANEL_WIDTH = 300;
        private const int PANEL_PADDING = 10;
        private const int SECTION_SPACING = 20;

        // ID / Name field limits
        private const int MAX_ID_LENGTH = 12;
        private const int MAX_NAME_LENGTH = 24;
        private const int MIN_INPUT_WIDTH_ID = 85;
        private const int MIN_INPUT_WIDTH_NAME = 260;

        // Key repeat (backspace/delete when held)
        private const float KEY_REPEAT_DELAY = 0.4f;
        private const float KEY_REPEAT_INTERVAL = 0.05f;
        private Keys _editLastRepeatedKey = Keys.None;
        private float _editKeyRepeatTimer = 0f;

        // Skip input when window just opened so the click that opened the window is not treated as a click inside (e.g. Create Character).
        // Use last frame's visibility because TaskBar runs before us and sets window visible in the same frame.
        private bool _wasWindowVisibleLastFrame = false;
        private bool _ignoreInputUntilMouseReleased = false;

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
            if (_ignoreInputUntilMouseReleased)
            {
                if (_currentMouseState.LeftButton == ButtonState.Released)
                    _ignoreInputUntilMouseReleased = false;
                return;
            }
            // Do not process clicks when a popup (e.g. confirm dialog) is open - prevents click-through
            if (MarySGameEngine.Modules.PopUp_essential.PopUp.Instance != null &&
                MarySGameEngine.Modules.PopUp_essential.PopUp.Instance.HasActivePopUp)
                return;
            // PopUp may have run before us and consumed the click (Confirm/Cancel); don't process it again
            if (MarySGameEngine.Modules.PopUp_essential.PopUp.ConsumedClickThisFrame)
                return;

            if (_currentMouseState.LeftButton == ButtonState.Pressed &&
                _previousMouseState.LeftButton == ButtonState.Released)
            {
                // Only process clicks when this window is the topmost under the mouse (prevents click-through to windows underneath)
                if (_windowManagement == null || !_windowManagement.IsThisWindowTopmostUnderMouse(_currentMouseState.Position))
                    return;

                // Mark that this window handled the click so Desktop/TaskBar/other modules don't process it (prevents click-through)
                Point p = _currentMouseState.Position;
                if (_leftPanelBounds.Contains(p) || _centerPanelBounds.Contains(p) || _rightPanelBounds.Contains(p))
                    _engine?.SetAnyWindowHandledClick(true);

                // Only process inspection-view clicks (Back, Edit, etc.) if we were already in inspection when the click happened.
                // Otherwise a list click that opens a character would also hit Back in the same frame (same screen position).
                bool wasInspectingAtClickStart = _isInspectingCharacter;

                // Handle category button clicks
                for (int i = 0; i < _categoryButtonBounds.Count && i < _assetCategories.Count; i++)
                {
                    if (_categoryButtonBounds[i].Contains(_currentMouseState.Position))
                    {
                        _selectedCategoryIndex = i;
                        _currentView = _assetCategories[i];
                        _isInspectingCharacter = false;
                        _isEditingId = false;
                        _isEditingName = false;
                        
                        // Reset scroll position when switching tabs
                        if (_previousView != _currentView)
                        {
                            _rightPanelScrollY = 0;
                            _previousView = _currentView;
                        }
                        break;
                    }
                }

                // Handle create character button click (toolbar when list exists, or empty-tab button on Characters tab, or grid in All tab)
                if (!_isInspectingCharacter && (_currentView == "Characters" || _currentView == "All"))
                {
                    bool clickedCreate = (_characters.Count > 0 && _createCharacterButtonBounds.Contains(_currentMouseState.Position))
                        || (_characters.Count == 0 && (_currentView == "Characters" || _currentView == "All") && _emptyTabCreateButtonBounds != Rectangle.Empty && _emptyTabCreateButtonBounds.Contains(_currentMouseState.Position))
                        || (_currentView == "All" && _allTabCreateButtonBounds != null && _allTabCreateButtonBounds.Length > 0 && _allTabCreateButtonBounds[0] != Rectangle.Empty && _allTabCreateButtonBounds[0].Contains(_currentMouseState.Position));

                    if (clickedCreate)
                    {
                        string newId = GetNextUniqueCharacterId();
                        _characters.Add(newId);
                        _characterId = newId;
                        _characterName = $"Character_{newId}";
                        _characterNames[_characterId] = _characterName;
                        _characterTags[_characterId] = "";
                        _isInspectingCharacter = true;
                        _scrollY = 0;
                        SaveCharacters();
                    }
                }
                // Handle create Trait/Skill/Effect/Stat/Tag (toolbar when list has items, or empty-tab button when list is empty)
                else if (!_isInspectingCharacter)
                {
                    bool clickedCreate = _createCharacterButtonBounds.Contains(_currentMouseState.Position) || (_emptyTabCreateButtonBounds != Rectangle.Empty && _emptyTabCreateButtonBounds.Contains(_currentMouseState.Position));
                    if (clickedCreate)
                    {
                        if (_currentView == "Traits" && (_traits.Count > 0 || _emptyTabCreateButtonBounds.Contains(_currentMouseState.Position))) { AddTrait(); }
                        else if (_currentView == "Skills" && (_skills.Count > 0 || _emptyTabCreateButtonBounds.Contains(_currentMouseState.Position))) { AddSkill(); }
                        else if (_currentView == "Effects" && (_effects.Count > 0 || _emptyTabCreateButtonBounds.Contains(_currentMouseState.Position))) { AddEffect(); }
                        else if (_currentView == "Stats" && (_stats.Count > 0 || _emptyTabCreateButtonBounds.Contains(_currentMouseState.Position))) { AddStat(); }
                        else if (_currentView == "Tags" && (_tags.Count > 0 || _emptyTabCreateButtonBounds.Contains(_currentMouseState.Position))) { AddTag(); }
                    }
                }

                // Handle character list click (Characters tab or All tab when list is shown)
                if ((_currentView == "Characters" || _currentView == "All") && !_isInspectingCharacter && _centerPanelBounds.Contains(_currentMouseState.Position))
                {
                    bool hitDelete = false;
                    for (int i = 0; i < _characterDeleteButtonBounds.Count && i < _characters.Count; i++)
                    {
                        if (_characterDeleteButtonBounds[i].Contains(_currentMouseState.Position))
                        {
                            hitDelete = true;
                            string idToDelete = _characters[i];
                            string nameToDelete = _characterNames.TryGetValue(idToDelete, out string displayName) ? displayName : idToDelete;
                            MarySGameEngine.Modules.PopUp_essential.PopUp.ShowConfirm(
                                "Delete Character",
                                $"Delete character?\nID: {idToDelete}\nName: {nameToDelete}",
                                onConfirm: () =>
                                {
                                    _characters.Remove(idToDelete);
                                    _characterNames.Remove(idToDelete);
                                    _characterTags.Remove(idToDelete);
                                    if (_isInspectingCharacter && _characterId == idToDelete)
                                    {
                                        _isInspectingCharacter = false;
                                    }
                                    SaveCharacters();
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
                                _characterId = _characters[i];
                                _characterName = _characterNames.TryGetValue(_characterId, out string n) ? n : _characterId;
                                _isInspectingCharacter = true;
                                _scrollY = 0;
                                break;
                            }
                        }
                    }
                }

                // View style bar (List / Grid) - works on any tab that shows the bar (All, Characters, Traits, etc.)
                if (_allTabViewListButtonBounds != Rectangle.Empty && _allTabViewListButtonBounds.Contains(_currentMouseState.Position))
                {
                    _allTabViewIsGrid = false;
                }
                else if (_allTabViewGridButtonBounds != Rectangle.Empty && _allTabViewGridButtonBounds.Contains(_currentMouseState.Position))
                {
                    _allTabViewIsGrid = true;
                }
                // All tab: Create Trait / Skill / Effect / Stat / Tag (grid buttons 1–5) and delete for entity lists
                else if (_currentView == "All" && !_isInspectingCharacter && _centerPanelBounds.Contains(_currentMouseState.Position))
                {
                    if (_allTabCreateButtonBounds != null && _allTabCreateButtonBounds.Length >= 6)
                    {
                        if (_allTabCreateButtonBounds[1].Contains(_currentMouseState.Position)) { AddTrait(); goto SkipAllTabRest; }
                        if (_allTabCreateButtonBounds[2].Contains(_currentMouseState.Position)) { AddSkill(); goto SkipAllTabRest; }
                        if (_allTabCreateButtonBounds[3].Contains(_currentMouseState.Position)) { AddEffect(); goto SkipAllTabRest; }
                        if (_allTabCreateButtonBounds[4].Contains(_currentMouseState.Position)) { AddStat(); goto SkipAllTabRest; }
                        if (_allTabCreateButtonBounds[5].Contains(_currentMouseState.Position)) { AddTag(); goto SkipAllTabRest; }
                    }
                    for (int i = 0; i < _traitDeleteBounds.Count && i < _traits.Count; i++)
                        if (_traitDeleteBounds[i].Contains(_currentMouseState.Position))
                        {
                            string name = _traits[i];
                            MarySGameEngine.Modules.PopUp_essential.PopUp.ShowConfirm("Delete Trait", $"Delete \"{name}\"?", onConfirm: () => { _traits.Remove(name); SaveEntityList("traits.json", _traits); }, onCancel: () => { }, confirmText: "Delete", cancelText: "Cancel");
                            goto SkipAllTabRest;
                        }
                    for (int i = 0; i < _skillDeleteBounds.Count && i < _skills.Count; i++)
                        if (_skillDeleteBounds[i].Contains(_currentMouseState.Position))
                        {
                            string name = _skills[i];
                            MarySGameEngine.Modules.PopUp_essential.PopUp.ShowConfirm("Delete Skill", $"Delete \"{name}\"?", onConfirm: () => { _skills.Remove(name); SaveEntityList("skills.json", _skills); }, onCancel: () => { }, confirmText: "Delete", cancelText: "Cancel");
                            goto SkipAllTabRest;
                        }
                    for (int i = 0; i < _effectDeleteBounds.Count && i < _effects.Count; i++)
                        if (_effectDeleteBounds[i].Contains(_currentMouseState.Position))
                        {
                            string name = _effects[i];
                            MarySGameEngine.Modules.PopUp_essential.PopUp.ShowConfirm("Delete Effect", $"Delete \"{name}\"?", onConfirm: () => { _effects.Remove(name); SaveEntityList("effects.json", _effects); }, onCancel: () => { }, confirmText: "Delete", cancelText: "Cancel");
                            goto SkipAllTabRest;
                        }
                    for (int i = 0; i < _statDeleteBounds.Count && i < _stats.Count; i++)
                        if (_statDeleteBounds[i].Contains(_currentMouseState.Position))
                        {
                            string name = _stats[i];
                            MarySGameEngine.Modules.PopUp_essential.PopUp.ShowConfirm("Delete Stat", $"Delete \"{name}\"?", onConfirm: () => { _stats.Remove(name); SaveEntityList("stats.json", _stats); }, onCancel: () => { }, confirmText: "Delete", cancelText: "Cancel");
                            goto SkipAllTabRest;
                        }
                    for (int i = 0; i < _tagDeleteBounds.Count && i < _tags.Count; i++)
                        if (_tagDeleteBounds[i].Contains(_currentMouseState.Position))
                        {
                            string name = _tags[i];
                            MarySGameEngine.Modules.PopUp_essential.PopUp.ShowConfirm("Delete Tag", $"Delete \"{name}\"?", onConfirm: () => { _tags.Remove(name); SaveEntityList("tags.json", _tags); }, onCancel: () => { }, confirmText: "Delete", cancelText: "Cancel");
                            goto SkipAllTabRest;
                        }
                    SkipAllTabRest: ;
                }
                // Traits / Skills / Effects / Stats / Tags tab: delete when viewing that tab
                else if (!_isInspectingCharacter && _centerPanelBounds.Contains(_currentMouseState.Position))
                {
                    if (_currentView == "Traits")
                    {
                        for (int i = 0; i < _traitDeleteBounds.Count && i < _traits.Count; i++)
                            if (_traitDeleteBounds[i].Contains(_currentMouseState.Position))
                            {
                                string name = _traits[i];
                                MarySGameEngine.Modules.PopUp_essential.PopUp.ShowConfirm("Delete Trait", $"Delete \"{name}\"?", onConfirm: () => { _traits.Remove(name); SaveEntityList("traits.json", _traits); }, onCancel: () => { }, confirmText: "Delete", cancelText: "Cancel");
                                break;
                            }
                    }
                    else if (_currentView == "Skills")
                    {
                        for (int i = 0; i < _skillDeleteBounds.Count && i < _skills.Count; i++)
                            if (_skillDeleteBounds[i].Contains(_currentMouseState.Position))
                            {
                                string name = _skills[i];
                                MarySGameEngine.Modules.PopUp_essential.PopUp.ShowConfirm("Delete Skill", $"Delete \"{name}\"?", onConfirm: () => { _skills.Remove(name); SaveEntityList("skills.json", _skills); }, onCancel: () => { }, confirmText: "Delete", cancelText: "Cancel");
                                break;
                            }
                    }
                    else if (_currentView == "Effects")
                    {
                        for (int i = 0; i < _effectDeleteBounds.Count && i < _effects.Count; i++)
                            if (_effectDeleteBounds[i].Contains(_currentMouseState.Position))
                            {
                                string name = _effects[i];
                                MarySGameEngine.Modules.PopUp_essential.PopUp.ShowConfirm("Delete Effect", $"Delete \"{name}\"?", onConfirm: () => { _effects.Remove(name); SaveEntityList("effects.json", _effects); }, onCancel: () => { }, confirmText: "Delete", cancelText: "Cancel");
                                break;
                            }
                    }
                    else if (_currentView == "Stats")
                    {
                        for (int i = 0; i < _statDeleteBounds.Count && i < _stats.Count; i++)
                            if (_statDeleteBounds[i].Contains(_currentMouseState.Position))
                            {
                                string name = _stats[i];
                                MarySGameEngine.Modules.PopUp_essential.PopUp.ShowConfirm("Delete Stat", $"Delete \"{name}\"?", onConfirm: () => { _stats.Remove(name); SaveEntityList("stats.json", _stats); }, onCancel: () => { }, confirmText: "Delete", cancelText: "Cancel");
                                break;
                            }
                    }
                    else if (_currentView == "Tags")
                    {
                        for (int i = 0; i < _tagDeleteBounds.Count && i < _tags.Count; i++)
                            if (_tagDeleteBounds[i].Contains(_currentMouseState.Position))
                            {
                                string name = _tags[i];
                                MarySGameEngine.Modules.PopUp_essential.PopUp.ShowConfirm("Delete Tag", $"Delete \"{name}\"?", onConfirm: () => { _tags.Remove(name); SaveEntityList("tags.json", _tags); }, onCancel: () => { }, confirmText: "Delete", cancelText: "Cancel");
                                break;
                            }
                    }
                }

                // Handle ID / Name Edit and Confirm button clicks (character inspection view)
                if (wasInspectingAtClickStart)
                {
                    if (_backButtonBounds.Contains(_currentMouseState.Position))
                    {
                        _isInspectingCharacter = false;
                    }
                    else if (!_isEditingId && !_isEditingName && _idEditButtonBounds.Contains(_currentMouseState.Position))
                    {
                        _isEditingId = true;
                        _idEditBuffer = _characterId.Length <= MAX_ID_LENGTH ? _characterId : _characterId.Substring(0, MAX_ID_LENGTH);
                        _idCursorPosition = _idEditBuffer.Length;
                        _idAnchorPosition = _idCursorPosition;
                        ResetIdCursorBlink();
                    }
                    else if (_isEditingId && _idTextBounds.Contains(_currentMouseState.Position))
                    {
                        SpriteFont font = _uiFont ?? _menuFont;
                        if (font != null)
                        {
                            float localX = _currentMouseState.Position.X - _idTextBounds.X - 6;
                            _idCursorPosition = GetCharacterIndexAtPosition(_idEditBuffer, localX, font);
                            if (!(_currentKeyboardState.IsKeyDown(Keys.LeftShift) || _currentKeyboardState.IsKeyDown(Keys.RightShift)))
                                _idAnchorPosition = _idCursorPosition;
                            ResetIdCursorBlink();
                        }
                    }
                    else if (_isEditingId && _idConfirmButtonBounds.Contains(_currentMouseState.Position))
                    {
                        ConfirmIdEdit();
                    }
                    else if (_isEditingId && !_idTextBounds.Contains(_currentMouseState.Position)
                             && !_idConfirmButtonBounds.Contains(_currentMouseState.Position))
                    {
                        if (IsIdEditConfirmDisabled())
                        {
                            _isEditingId = false;
                            _editLastRepeatedKey = Keys.None;
                            _editKeyRepeatTimer = 0f;
                        }
                        else
                            ConfirmIdEdit();
                    }
                    else if (!_isEditingName && !_isEditingId && _nameEditButtonBounds.Contains(_currentMouseState.Position))
                    {
                        _isEditingName = true;
                        _nameEditBuffer = _characterName;
                        _nameCursorPosition = _nameEditBuffer.Length;
                        _nameAnchorPosition = _nameCursorPosition;
                        ResetNameCursorBlink();
                    }
                    else if (_isEditingName && _nameTextBounds.Contains(_currentMouseState.Position))
                    {
                        SpriteFont font = _uiFont ?? _menuFont;
                        if (font != null)
                        {
                            float localX = _currentMouseState.Position.X - _nameTextBounds.X - 6;
                            _nameCursorPosition = GetCharacterIndexAtPosition(_nameEditBuffer, localX, font);
                            if (!(_currentKeyboardState.IsKeyDown(Keys.LeftShift) || _currentKeyboardState.IsKeyDown(Keys.RightShift)))
                                _nameAnchorPosition = _nameCursorPosition;
                            ResetNameCursorBlink();
                        }
                    }
                    else if (_isEditingName && _nameConfirmButtonBounds.Contains(_currentMouseState.Position))
                    {
                        ConfirmNameEdit();
                    }
                    else if (_isEditingName && !_nameTextBounds.Contains(_currentMouseState.Position)
                             && !_nameConfirmButtonBounds.Contains(_currentMouseState.Position))
                    {
                        ConfirmNameEdit();
                    }
                }
            }
        }

        private string GetNextUniqueCharacterId()
        {
            for (int n = 1; n <= _characters.Count + 1; n++)
            {
                string id = n.ToString();
                if (!_characters.Contains(id))
                    return id;
            }
            return (_characters.Count + 1).ToString();
        }

        private static string GetNextUniqueEntityId(List<string> items, string prefix)
        {
            for (int n = 1; n <= items.Count + 1; n++)
            {
                string id = $"{prefix}_{n}";
                if (!items.Contains(id))
                    return id;
            }
            return $"{prefix}_{items.Count + 1}";
        }

        private void AddTrait() { string id = GetNextUniqueEntityId(_traits, "Trait"); _traits.Add(id); SaveEntityList("traits.json", _traits); }
        private void AddSkill() { string id = GetNextUniqueEntityId(_skills, "Skill"); _skills.Add(id); SaveEntityList("skills.json", _skills); }
        private void AddEffect() { string id = GetNextUniqueEntityId(_effects, "Effect"); _effects.Add(id); SaveEntityList("effects.json", _effects); }
        private void AddStat() { string id = GetNextUniqueEntityId(_stats, "Stat"); _stats.Add(id); SaveEntityList("stats.json", _stats); }
        private void AddTag() { string id = GetNextUniqueEntityId(_tags, "Tag"); _tags.Add(id); SaveEntityList("tags.json", _tags); }

        private string GetEntityListFilePath(string fileName)
        {
            string workspacePath = GetActiveWorkspacePath();
            if (string.IsNullOrEmpty(workspacePath)) return "";
            return Path.Combine(workspacePath, fileName);
        }

        private void SaveEntityList(string fileName, List<string> items)
        {
            try
            {
                string filePath = GetEntityListFilePath(fileName);
                if (string.IsNullOrEmpty(filePath)) return;
                string dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(items, options);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex) { _engine?.Log($"CharacterCreation: Error saving {fileName}: {ex.Message}"); }
        }

        private void LoadEntityList(string fileName, List<string> items)
        {
            try
            {
                string filePath = GetEntityListFilePath(fileName);
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) { items.Clear(); return; }
                string json = File.ReadAllText(filePath);
                var loaded = JsonSerializer.Deserialize<List<string>>(json);
                if (loaded != null) { items.Clear(); items.AddRange(loaded); }
            }
            catch (Exception ex) { _engine?.Log($"CharacterCreation: Error loading {fileName}: {ex.Message}"); }
        }

        private bool IsIdEditConfirmDisabled()
        {
            string trimmed = _idEditBuffer.Trim();
            if (string.IsNullOrEmpty(trimmed)) return true;
            if (trimmed == _characterId) return false;
            return _characters.Contains(trimmed);
        }

        private void ConfirmIdEdit()
        {
            if (IsIdEditConfirmDisabled()) return;
            string newId = _idEditBuffer.Trim();
            if (newId != _characterId)
            {
                int idx = _characters.IndexOf(_characterId);
                if (idx >= 0)
                {
                    string oldName = _characterNames.TryGetValue(_characterId, out string n) ? n : _characterId;
                    string oldTags = _characterTags.TryGetValue(_characterId, out string t) ? t : "";
                    _characterNames.Remove(_characterId);
                    _characterTags.Remove(_characterId);
                    _characters[idx] = newId;
                    _characterNames[newId] = oldName;
                    _characterTags[newId] = oldTags;
                    SaveCharacters();
                }
                _characterId = newId;
            }
            _isEditingId = false;
            _editLastRepeatedKey = Keys.None;
            _editKeyRepeatTimer = 0f;
        }

        private void ConfirmNameEdit()
        {
            string newName = _nameEditBuffer.Trim();
            if (!string.IsNullOrEmpty(newName) && newName != _characterName)
            {
                _characterNames[_characterId] = newName;
                _characterName = newName;
                SaveCharacters();
            }
            _isEditingName = false;
            _editLastRepeatedKey = Keys.None;
            _editKeyRepeatTimer = 0f;
        }

        private void HandleNameEditInput()
        {
            float dt = (float)GameEngine.Instance.TargetElapsedTime.TotalSeconds;

            if (_isEditingId)
            {
                _idCursorBlinkTimer += dt;
                if (_idCursorBlinkTimer >= 0.5f)
                {
                    _idShowCursor = !_idShowCursor;
                    _idCursorBlinkTimer = 0f;
                }
                var pressedKeys = _currentKeyboardState.GetPressedKeys();
                foreach (var key in pressedKeys)
                {
                    if (_previousKeyboardState.IsKeyUp(key))
                    {
                        HandleIdKey(key);
                        if (key == Keys.Back || key == Keys.Delete)
                            _editLastRepeatedKey = key;
                    }
                }
                if (!_currentKeyboardState.IsKeyDown(_editLastRepeatedKey))
                {
                    _editLastRepeatedKey = Keys.None;
                    _editKeyRepeatTimer = 0f;
                }
                else if (_editLastRepeatedKey == Keys.Back || _editLastRepeatedKey == Keys.Delete)
                {
                    _editKeyRepeatTimer += dt;
                    if (_editKeyRepeatTimer >= KEY_REPEAT_DELAY)
                    {
                        float since = _editKeyRepeatTimer - KEY_REPEAT_DELAY;
                        if (since >= KEY_REPEAT_INTERVAL)
                        {
                            _editKeyRepeatTimer = KEY_REPEAT_DELAY;
                            if (_editLastRepeatedKey == Keys.Back)
                                ApplyIdBackspace();
                            else
                                ApplyIdDelete();
                        }
                    }
                }
                return;
            }
            if (!_isEditingName) return;

            _nameCursorBlinkTimer += dt;
            if (_nameCursorBlinkTimer >= 0.5f)
            {
                _nameShowCursor = !_nameShowCursor;
                _nameCursorBlinkTimer = 0f;
            }

            var keys = _currentKeyboardState.GetPressedKeys();
            foreach (var key in keys)
            {
                if (_previousKeyboardState.IsKeyUp(key))
                {
                    HandleNameKey(key);
                    if (key == Keys.Back || key == Keys.Delete)
                        _editLastRepeatedKey = key;
                }
            }
            if (!_currentKeyboardState.IsKeyDown(_editLastRepeatedKey))
            {
                _editLastRepeatedKey = Keys.None;
                _editKeyRepeatTimer = 0f;
            }
            else if (_editLastRepeatedKey == Keys.Back || _editLastRepeatedKey == Keys.Delete)
            {
                _editKeyRepeatTimer += dt;
                if (_editKeyRepeatTimer >= KEY_REPEAT_DELAY)
                {
                    float since = _editKeyRepeatTimer - KEY_REPEAT_DELAY;
                    if (since >= KEY_REPEAT_INTERVAL)
                    {
                        _editKeyRepeatTimer = KEY_REPEAT_DELAY;
                        if (_editLastRepeatedKey == Keys.Back)
                            ApplyNameBackspace();
                        else
                            ApplyNameDelete();
                    }
                }
            }
        }

        private void ApplyIdBackspace()
        {
            int min = Math.Min(_idCursorPosition, _idAnchorPosition);
            int max = Math.Max(_idCursorPosition, _idAnchorPosition);
            if (min < max)
            {
                _idEditBuffer = _idEditBuffer.Remove(min, max - min);
                _idCursorPosition = min;
                _idAnchorPosition = min;
                ResetIdCursorBlink();
                return;
            }
            if (_idCursorPosition > 0)
            {
                _idEditBuffer = _idEditBuffer.Remove(_idCursorPosition - 1, 1);
                _idCursorPosition--;
                _idAnchorPosition = _idCursorPosition;
                ResetIdCursorBlink();
            }
        }

        private void ApplyIdDelete()
        {
            int min = Math.Min(_idCursorPosition, _idAnchorPosition);
            int max = Math.Max(_idCursorPosition, _idAnchorPosition);
            if (min < max)
            {
                _idEditBuffer = _idEditBuffer.Remove(min, max - min);
                _idCursorPosition = min;
                _idAnchorPosition = min;
                ResetIdCursorBlink();
                return;
            }
            if (_idCursorPosition < _idEditBuffer.Length)
            {
                _idEditBuffer = _idEditBuffer.Remove(_idCursorPosition, 1);
                _idAnchorPosition = _idCursorPosition;
                ResetIdCursorBlink();
            }
        }

        private void ApplyNameBackspace()
        {
            int min = Math.Min(_nameCursorPosition, _nameAnchorPosition);
            int max = Math.Max(_nameCursorPosition, _nameAnchorPosition);
            if (min < max)
            {
                _nameEditBuffer = _nameEditBuffer.Remove(min, max - min);
                _nameCursorPosition = min;
                _nameAnchorPosition = min;
                ResetNameCursorBlink();
                return;
            }
            if (_nameCursorPosition > 0)
            {
                _nameEditBuffer = _nameEditBuffer.Remove(_nameCursorPosition - 1, 1);
                _nameCursorPosition--;
                _nameAnchorPosition = _nameCursorPosition;
                ResetNameCursorBlink();
            }
        }

        private void ApplyNameDelete()
        {
            int min = Math.Min(_nameCursorPosition, _nameAnchorPosition);
            int max = Math.Max(_nameCursorPosition, _nameAnchorPosition);
            if (min < max)
            {
                _nameEditBuffer = _nameEditBuffer.Remove(min, max - min);
                _nameCursorPosition = min;
                _nameAnchorPosition = min;
                ResetNameCursorBlink();
                return;
            }
            if (_nameCursorPosition < _nameEditBuffer.Length)
            {
                _nameEditBuffer = _nameEditBuffer.Remove(_nameCursorPosition, 1);
                _nameAnchorPosition = _nameCursorPosition;
                ResetNameCursorBlink();
            }
        }

        private void ResetIdCursorBlink()
        {
            _idShowCursor = true;
            _idCursorBlinkTimer = 0f;
        }

        private static int GetCharacterIndexAtPosition(string text, float localX, SpriteFont font)
        {
            if (localX <= 0) return 0;
            if (string.IsNullOrEmpty(text)) return 0;
            for (int i = 1; i <= text.Length; i++)
            {
                float x = font.MeasureString(text.Substring(0, i)).X;
                if (x >= localX)
                    return i - 1;
            }
            return text.Length;
        }

        private void HandleIdKey(Keys key)
        {
            switch (key)
            {
                case Keys.Enter:
                    if (!IsIdEditConfirmDisabled())
                        ConfirmIdEdit();
                    return;
                case Keys.Escape:
                    _isEditingId = false;
                    _editLastRepeatedKey = Keys.None;
                    _editKeyRepeatTimer = 0f;
                    return;
                case Keys.Back:
                    ApplyIdBackspace();
                    return;
                case Keys.Delete:
                    ApplyIdDelete();
                    return;
                case Keys.Left:
                    if (!(_currentKeyboardState.IsKeyDown(Keys.LeftShift) || _currentKeyboardState.IsKeyDown(Keys.RightShift)))
                        _idAnchorPosition = _idCursorPosition;
                    if (_idCursorPosition > 0) { _idCursorPosition--; ResetIdCursorBlink(); }
                    return;
                case Keys.Right:
                    if (!(_currentKeyboardState.IsKeyDown(Keys.LeftShift) || _currentKeyboardState.IsKeyDown(Keys.RightShift)))
                        _idAnchorPosition = _idCursorPosition;
                    if (_idCursorPosition < _idEditBuffer.Length) { _idCursorPosition++; ResetIdCursorBlink(); }
                    return;
                case Keys.Home:
                    if (!(_currentKeyboardState.IsKeyDown(Keys.LeftShift) || _currentKeyboardState.IsKeyDown(Keys.RightShift)))
                        _idAnchorPosition = _idCursorPosition;
                    _idCursorPosition = 0;
                    ResetIdCursorBlink();
                    return;
                case Keys.End:
                    if (!(_currentKeyboardState.IsKeyDown(Keys.LeftShift) || _currentKeyboardState.IsKeyDown(Keys.RightShift)))
                        _idAnchorPosition = _idCursorPosition;
                    _idCursorPosition = _idEditBuffer.Length;
                    ResetIdCursorBlink();
                    return;
            }
            char c = GetNameCharFromKey(key);
            if (c != '\0')
            {
                int min = Math.Min(_idCursorPosition, _idAnchorPosition);
                int max = Math.Max(_idCursorPosition, _idAnchorPosition);
                int newLen = (min < max) ? (_idEditBuffer.Length - (max - min) + 1) : (_idEditBuffer.Length + 1);
                if (newLen > MAX_ID_LENGTH)
                {
                    ResetIdCursorBlink();
                    return;
                }
                if (min < max)
                {
                    _idEditBuffer = _idEditBuffer.Remove(min, max - min);
                    _idEditBuffer = _idEditBuffer.Insert(min, c.ToString());
                    _idCursorPosition = min + 1;
                    _idAnchorPosition = min + 1;
                }
                else
                {
                    _idEditBuffer = _idEditBuffer.Insert(_idCursorPosition, c.ToString());
                    _idCursorPosition++;
                    _idAnchorPosition = _idCursorPosition;
                }
                ResetIdCursorBlink();
            }
        }

        private void HandleNameKey(Keys key)
        {
            switch (key)
            {
                case Keys.Enter:
                    ConfirmNameEdit();
                    return;
                case Keys.Escape:
                    _isEditingName = false;
                    _editLastRepeatedKey = Keys.None;
                    _editKeyRepeatTimer = 0f;
                    return;
                case Keys.Back:
                    ApplyNameBackspace();
                    return;
                case Keys.Delete:
                    ApplyNameDelete();
                    return;
                case Keys.Left:
                    if (!(_currentKeyboardState.IsKeyDown(Keys.LeftShift) || _currentKeyboardState.IsKeyDown(Keys.RightShift)))
                        _nameAnchorPosition = _nameCursorPosition;
                    if (_nameCursorPosition > 0)
                    {
                        _nameCursorPosition--;
                        ResetNameCursorBlink();
                    }
                    return;
                case Keys.Right:
                    if (!(_currentKeyboardState.IsKeyDown(Keys.LeftShift) || _currentKeyboardState.IsKeyDown(Keys.RightShift)))
                        _nameAnchorPosition = _nameCursorPosition;
                    if (_nameCursorPosition < _nameEditBuffer.Length)
                    {
                        _nameCursorPosition++;
                        ResetNameCursorBlink();
                    }
                    return;
                case Keys.Home:
                    if (!(_currentKeyboardState.IsKeyDown(Keys.LeftShift) || _currentKeyboardState.IsKeyDown(Keys.RightShift)))
                        _nameAnchorPosition = _nameCursorPosition;
                    _nameCursorPosition = 0;
                    ResetNameCursorBlink();
                    return;
                case Keys.End:
                    if (!(_currentKeyboardState.IsKeyDown(Keys.LeftShift) || _currentKeyboardState.IsKeyDown(Keys.RightShift)))
                        _nameAnchorPosition = _nameCursorPosition;
                    _nameCursorPosition = _nameEditBuffer.Length;
                    ResetNameCursorBlink();
                    return;
            }

            char c = GetNameCharFromKey(key);
            if (c != '\0')
            {
                int min = Math.Min(_nameCursorPosition, _nameAnchorPosition);
                int max = Math.Max(_nameCursorPosition, _nameAnchorPosition);
                if (min < max)
                {
                    _nameEditBuffer = _nameEditBuffer.Remove(min, max - min);
                    _nameEditBuffer = _nameEditBuffer.Insert(min, c.ToString());
                    _nameCursorPosition = min + 1;
                    _nameAnchorPosition = min + 1;
                }
                else if (_nameEditBuffer.Length < MAX_NAME_LENGTH)
                {
                    _nameEditBuffer = _nameEditBuffer.Insert(_nameCursorPosition, c.ToString());
                    _nameCursorPosition++;
                    _nameAnchorPosition = _nameCursorPosition;
                }
                ResetNameCursorBlink();
            }
        }

        private void ResetNameCursorBlink()
        {
            _nameShowCursor = true;
            _nameCursorBlinkTimer = 0f;
        }

        private char GetNameCharFromKey(Keys key)
        {
            bool shift = _currentKeyboardState.IsKeyDown(Keys.LeftShift) || _currentKeyboardState.IsKeyDown(Keys.RightShift);

            if (key >= Keys.A && key <= Keys.Z)
                return shift ? (char)('A' + (key - Keys.A)) : (char)('a' + (key - Keys.A));
            if (key >= Keys.D0 && key <= Keys.D9 && !shift)
                return (char)('0' + (key - Keys.D0));
            if (key == Keys.Space) return ' ';
            if (key == Keys.OemMinus) return shift ? '_' : '-';
            if (key == Keys.OemPeriod) return shift ? '>' : '.';
            if (key == Keys.OemComma) return shift ? '<' : ',';

            switch (key)
            {
                case Keys.D1 when shift: return '!';
                case Keys.D2 when shift: return '@';
                case Keys.D3 when shift: return '#';
                case Keys.D4 when shift: return '$';
                case Keys.D5 when shift: return '%';
                case Keys.D6 when shift: return '^';
                case Keys.D7 when shift: return '&';
                case Keys.D8 when shift: return '*';
                case Keys.D9 when shift: return '(';
                case Keys.D0 when shift: return ')';
            }

            if (key == Keys.OemPlus) return shift ? '+' : '=';
            if (key == Keys.OemQuestion) return shift ? '?' : '/';
            if (key == Keys.OemOpenBrackets) return shift ? '{' : '[';
            if (key == Keys.OemCloseBrackets) return shift ? '}' : ']';
            if (key == Keys.OemSemicolon) return shift ? ':' : ';';
            if (key == Keys.OemQuotes) return shift ? '"' : '\'';
            if (key == Keys.OemTilde) return shift ? '~' : '`';
            if (key == Keys.OemBackslash || key == Keys.OemPipe) return shift ? '|' : '\\';

            return '\0';
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
                    LoadCharacters();
                    _isInspectingCharacter = false;
                    _scrollY = 0;
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

        private string GetActiveWorkspacePath()
        {
            try
            {
                var modules = GameEngine.Instance.GetActiveModules();
                foreach (var module in modules)
                {
                    if (module is MarySGameEngine.Modules.GameManager_essential.GameManager gameManager)
                    {
                        var field = gameManager.GetType().GetField("_activeWorkspacePath",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (field != null)
                        {
                            string path = field.GetValue(gameManager) as string ?? "";
                            if (!string.IsNullOrEmpty(path))
                                return path;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _engine?.Log($"CharacterCreation: Error getting workspace path: {ex.Message}");
            }
            return "";
        }

        private string GetCharactersFilePath()
        {
            string workspacePath = GetActiveWorkspacePath();
            if (string.IsNullOrEmpty(workspacePath))
                return "";
            return Path.Combine(workspacePath, "characters.json");
        }

        private void SaveCharacters()
        {
            try
            {
                string filePath = GetCharactersFilePath();
                if (string.IsNullOrEmpty(filePath))
                    return;

                string dir = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                var data = new { ids = _characters, names = _characterNames, tags = _characterTags };
                string json = JsonSerializer.Serialize(data, options);
                File.WriteAllText(filePath, json);
                _engine?.Log($"CharacterCreation: Saved {_characters.Count} characters to {filePath}");
            }
            catch (Exception ex)
            {
                _engine?.Log($"CharacterCreation: Error saving characters: {ex.Message}");
            }
        }

        private void LoadCharacters()
        {
            try
            {
                string filePath = GetCharactersFilePath();
                if (string.IsNullOrEmpty(filePath))
                {
                    _characters.Clear();
                    _characterNames.Clear();
                    _characterTags.Clear();
                    return;
                }

                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    if (root.ValueKind == JsonValueKind.Array)
                    {
                        _characters = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                        _characterNames.Clear();
                        _characterTags.Clear();
                        foreach (string id in _characters)
                        {
                            _characterNames[id] = id;
                            _characterTags[id] = "";
                        }
                    }
                    else
                    {
                        _characters = new List<string>();
                        if (root.TryGetProperty("ids", out var idsEl))
                            _characters = JsonSerializer.Deserialize<List<string>>(idsEl.GetRawText()) ?? new List<string>();
                        _characterNames = new Dictionary<string, string>();
                        if (root.TryGetProperty("names", out var namesEl))
                            _characterNames = JsonSerializer.Deserialize<Dictionary<string, string>>(namesEl.GetRawText()) ?? new Dictionary<string, string>();
                        _characterTags = new Dictionary<string, string>();
                        if (root.TryGetProperty("tags", out var tagsEl))
                            _characterTags = JsonSerializer.Deserialize<Dictionary<string, string>>(tagsEl.GetRawText()) ?? new Dictionary<string, string>();
                        foreach (string id in _characters)
                            if (!_characterTags.ContainsKey(id))
                                _characterTags[id] = "";
                    }
                    _engine?.Log($"CharacterCreation: Loaded {_characters.Count} characters from {filePath}");
                }
                else
                {
                    _characters.Clear();
                    _characterNames.Clear();
                    _characterTags.Clear();
                }
                LoadEntityList("traits.json", _traits);
                LoadEntityList("skills.json", _skills);
                LoadEntityList("effects.json", _effects);
                LoadEntityList("stats.json", _stats);
                LoadEntityList("tags.json", _tags);
            }
            catch (Exception ex)
            {
                _engine?.Log($"CharacterCreation: Error loading characters: {ex.Message}");
                _characters.Clear();
                _characterNames.Clear();
                _characterTags.Clear();
            }
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
                _previousKeyboardState = _currentKeyboardState;
                _currentKeyboardState = Keyboard.GetState();

                bool wasVisible = _wasWindowVisibleLastFrame;
                _windowManagement?.Update();
                bool isVisible = _windowManagement?.IsVisible() ?? false;
                _wasWindowVisibleLastFrame = isVisible;

                // Use off-screen position when popup is open or when another window is on top, so only the front window shows hover
                bool popupActive = MarySGameEngine.Modules.PopUp_essential.PopUp.Instance != null &&
                    MarySGameEngine.Modules.PopUp_essential.PopUp.Instance.HasActivePopUp;
                bool otherWindowOnTop = _windowManagement != null && isVisible && !_windowManagement.IsThisWindowTopmostUnderMouse(_currentMouseState.Position);
                _hoverMousePosition = (popupActive || otherWindowOnTop) ? new Point(-10000, -10000) : _currentMouseState.Position;

                // If window just became visible, ignore input until mouse is released so the open-click is not treated as in-window click
                if (!wasVisible && isVisible)
                {
                    _ignoreInputUntilMouseReleased = true;
                    // Reset to "All" tab when opening (keep last position/size so window stays where user left it)
                    _selectedCategoryIndex = 0;
                    _currentView = "All";
                    _previousView = "All";
                    _isInspectingCharacter = false;
                    _rightPanelScrollY = 0;
                    LoadCharacters();
                    UpdateBounds();
                }
                // If taskbar (or similar) just focused this window, ignore the focusing click so it is not treated as in-window (e.g. Create Character)
                if (_windowManagement != null && _windowManagement.ConsumeShouldIgnoreNextClick())
                    _ignoreInputUntilMouseReleased = true;

                if (_windowManagement == null || !isVisible)
                    return;

                UpdateWorkspaceDisplay();
                UpdateBounds();
                UpdateScrolling();
                UpdateInspectionHitTestBounds();
                HandleInput();
                HandleNameEditInput();
                
                // Reset scroll position if view changed
                if (_previousView != _currentView)
                {
                    _rightPanelScrollY = 0;
                    _previousView = _currentView;
                }

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

            // Toolbar at the top of the center panel
            _toolbarBounds = new Rectangle(
                _centerPanelBounds.X,
                _centerPanelBounds.Y,
                _centerPanelBounds.Width,
                TOOLBAR_HEIGHT
            );

            int createBtnWidth = 200;
            int createBtnHeight = 30;
            _createCharacterButtonBounds = new Rectangle(
                _toolbarBounds.X + PANEL_PADDING,
                _toolbarBounds.Y + (TOOLBAR_HEIGHT - createBtnHeight) / 2,
                createBtnWidth,
                createBtnHeight
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
            int nameRowWidth = Math.Min(1000, _centerPanelBounds.Width - PANEL_PADDING * 2);
            _nameInputBounds = new Rectangle(
                _centerPanelBounds.X + PANEL_PADDING,
                centerTop,
                nameRowWidth,
                30
            );

            // Update character image bounds (below ID+Name row)
            _characterImageBounds = new Rectangle(
                _centerPanelBounds.X + PANEL_PADDING,
                _nameInputBounds.Bottom + PANEL_PADDING * 2,
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

            // Enable scrolling for both character inspection view and list/grid view
            // Calculate content height (approximate based on center panel content)
            _contentHeight = CalculateContentHeight();

            if (!_isInspectingCharacter)
            {
                // List/grid view: allow scroll when content exceeds panel
                // _scrollY is already clamped below when _needsScrollbar is set
            }
            
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
                height += 30 + PANEL_PADDING; // ID and Name row
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
                // List/grid tab view - compute total content height
                return CalculateListGridContentHeight();
            }
        }

        private void HandleScrollbarInteraction()
        {
            if (!_needsScrollbar || !_scrollingEnabled) return;

            var mousePosition = _currentMouseState.Position;
            bool isTopmost = _windowManagement != null && _windowManagement.IsThisWindowTopmostUnderMouse(mousePosition);

            // Handle scrollbar dragging - only start drag when this window is topmost
            if (_currentMouseState.LeftButton == ButtonState.Pressed && 
                _previousMouseState.LeftButton == ButtonState.Released)
            {
                if (isTopmost && _scrollbarBounds.Contains(mousePosition))
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

            // Handle mouse wheel scrolling - only when this window is topmost
            if (isTopmost && _centerPanelBounds.Contains(mousePosition) && 
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

                // Draw left panel (will flush with content scissor)
                DrawLeftPanel(spriteBatch);

                // Flush batch and start a new one so center panel draws use center scissor and clip at top (no draw-through title bar)
                spriteBatch.End();
                int resizeHandleSizeForScissor = 24;
                int centerPanelScissorW = _centerPanelBounds.Width - (_needsScrollbar ? SCROLLBAR_WIDTH + SCROLLBAR_PADDING : 0);
                int maxVisibleBottom = windowBounds.Y + windowBounds.Height - resizeHandleSizeForScissor;
                int centerPanelScissorBottom = Math.Min(_centerPanelBounds.Y + _centerPanelBounds.Height, maxVisibleBottom);
                int centerPanelScissorH = Math.Max(0, centerPanelScissorBottom - _centerPanelBounds.Y);
                Rectangle centerPanelScissor = new Rectangle(_centerPanelBounds.X, _centerPanelBounds.Y, centerPanelScissorW, centerPanelScissorH);
                int csLeft = Math.Max(centerPanelScissor.X, contentScissor.X);
                int csTop = Math.Max(centerPanelScissor.Y, contentScissor.Y);
                int csRight = Math.Min(centerPanelScissor.Right, contentScissor.Right);
                int csBottom = Math.Min(centerPanelScissor.Bottom, contentScissor.Bottom);
                Rectangle centerScissorFinal = new Rectangle(csLeft, csTop, Math.Max(0, csRight - csLeft), Math.Max(0, csBottom - csTop));
                spriteBatch.GraphicsDevice.ScissorRectangle = centerScissorFinal;
                spriteBatch.GraphicsDevice.RasterizerState = new RasterizerState { ScissorTestEnable = true, CullMode = CullMode.None };
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, spriteBatch.GraphicsDevice.RasterizerState);

                DrawCenterPanel(spriteBatch, _scrollY, windowBounds, restoreScissorAtEnd: false);

                spriteBatch.End();
                spriteBatch.GraphicsDevice.ScissorRectangle = contentScissor;
                spriteBatch.GraphicsDevice.RasterizerState = new RasterizerState { ScissorTestEnable = true };
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, spriteBatch.GraphicsDevice.RasterizerState);

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
                
                // Draw resize handle and border on top (ensure they're always visible)
                _windowManagement.DrawOverlay(spriteBatch);
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

            // Draw category buttons (no tab active when in character inspection - we're not in list/grid view)
            for (int i = 0; i < _assetCategories.Count && i < _categoryButtonBounds.Count; i++)
            {
                Rectangle bounds = _categoryButtonBounds[i];
                bool isSelected = !_isInspectingCharacter && i == _selectedCategoryIndex;
                bool isHovered = bounds.Contains(_hoverMousePosition);
                Color baseColor = isSelected ? BUTTON_ACTIVE : (isHovered ? BUTTON_HOVER : BUTTON_COLOR);

                // Gradient: top slightly darker, bottom slightly lighter (vertical gradient feel)
                int split = bounds.Y + bounds.Height * 55 / 100;
                Color topColor = new Color(
                    Math.Max(0, baseColor.R - 12),
                    Math.Max(0, baseColor.G - 10),
                    Math.Max(0, baseColor.B - 8));
                Color bottomColor = new Color(
                    Math.Min(255, baseColor.R + 8),
                    Math.Min(255, baseColor.G + 6),
                    Math.Min(255, baseColor.B + 6));
                spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, split - bounds.Y), topColor);
                spriteBatch.Draw(_pixel, new Rectangle(bounds.X, split, bounds.Width, bounds.Bottom - split), bottomColor);

                // Border: always visible, stronger when selected or hovered
                Color borderColor = isSelected ? new Color(180, 150, 255) : (isHovered ? PANEL_BORDER : new Color(55, 55, 60));
                DrawBorder(spriteBatch, bounds, borderColor);

                // Occasional light glint (блик света): diagonal line, horizontal sweep, on one random sidebar button every 10–15 sec
                double elapsed = (DateTime.UtcNow - _sidebarGlintStart).TotalSeconds;
                const double glintIntervalSeconds = 12.5;
                const double sweepDuration = 1.0;
                int categoryCount = Math.Min(_assetCategories.Count, _categoryButtonBounds.Count);
                int intervalIndex = (int)(elapsed / glintIntervalSeconds);
                int whichButton = categoryCount > 0 ? (intervalIndex * 31 + 17) % categoryCount : -1;
                double phaseInInterval = (elapsed % glintIntervalSeconds) / glintIntervalSeconds;
                bool inSweep = whichButton == i && phaseInInterval < (sweepDuration / glintIntervalSeconds);
                if (inSweep && categoryCount > 0)
                {
                    Rectangle prevScissor = spriteBatch.GraphicsDevice.ScissorRectangle;
                    RasterizerState prevRasterizer = spriteBatch.GraphicsDevice.RasterizerState;
                    Rectangle clip = Rectangle.Intersect(bounds, prevScissor);
                    if (clip.Width > 0 && clip.Height > 0)
                    {
                        spriteBatch.End();
                        spriteBatch.GraphicsDevice.ScissorRectangle = clip;
                        var clipRasterizer = new RasterizerState { ScissorTestEnable = true, CullMode = CullMode.None };
                        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, clipRasterizer);
                        float progress = (float)(phaseInInterval / (sweepDuration / glintIntervalSeconds));
                        float ease = (float)Math.Sin(progress * Math.PI);
                        float centerX = bounds.X + progress * (bounds.Width + 24f) - 12f;
                        float centerY = bounds.Y + bounds.Height * 0.5f;
                        const float length = 28f;
                        const float thickness = 2.8f;
                        float rotation = (float)(115.0 * Math.PI / 180.0);
                        Vector2 origin = new Vector2(0.5f, 0.5f);
                        Rectangle src = new Rectangle(0, 0, 1, 1);
                        int outerA = (int)(75 * ease);
                        spriteBatch.Draw(_pixel, new Vector2(centerX, centerY), src, new Color(255, 255, 255, outerA), rotation, origin, new Vector2(length + 4f, thickness + 0.8f), SpriteEffects.None, 0f);
                        int coreA = (int)(160 * ease);
                        spriteBatch.Draw(_pixel, new Vector2(centerX, centerY), src, new Color(255, 255, 255, coreA), rotation, origin, new Vector2(length, thickness), SpriteEffects.None, 0f);
                        spriteBatch.End();
                        spriteBatch.GraphicsDevice.ScissorRectangle = prevScissor;
                        spriteBatch.GraphicsDevice.RasterizerState = prevRasterizer;
                        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, prevRasterizer);
                    }
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

        private void DrawCenterPanel(SpriteBatch spriteBatch, int scrollOffset, Rectangle windowBounds, bool restoreScissorAtEnd = true)
        {
            if (_pixel == null || _menuFont == null) return;
            if (_centerPanelBounds.Width <= 0 || _centerPanelBounds.Height <= 0) return;

            SpriteFont uiFont = _uiFont ?? _menuFont; // Use UI font for center panel

            Rectangle scissorRect;
            Rectangle savedScissor = Rectangle.Empty;
            if (restoreScissorAtEnd)
            {
                // Set up scissor rectangle specifically for the center panel using CURRENT window bounds
                Rectangle originalScissor = spriteBatch.GraphicsDevice.ScissorRectangle;
                savedScissor = originalScissor;
                int resizeHandleSize = 24;
                int centerPanelX = _centerPanelBounds.X;
                int centerPanelY = _centerPanelBounds.Y;
                int centerPanelWidth = _centerPanelBounds.Width - (_needsScrollbar ? SCROLLBAR_WIDTH + SCROLLBAR_PADDING : 0);
                int maxVisibleBottom = windowBounds.Y + windowBounds.Height - resizeHandleSize;
                int centerPanelScissorBottom = Math.Min(centerPanelY + _centerPanelBounds.Height, maxVisibleBottom);
                int centerPanelScissorHeight = Math.Max(0, centerPanelScissorBottom - centerPanelY);
                Rectangle centerPanelScissor = new Rectangle(centerPanelX, centerPanelY, centerPanelWidth, centerPanelScissorHeight);
                int scissorLeft = Math.Max(centerPanelScissor.X, originalScissor.X);
                int scissorTop = Math.Max(centerPanelScissor.Y, originalScissor.Y);
                int scissorRight = Math.Min(centerPanelScissor.Right, originalScissor.Right);
                int scissorBottom = Math.Min(centerPanelScissor.Bottom, originalScissor.Bottom);
                scissorRect = new Rectangle(scissorLeft, scissorTop, Math.Max(0, scissorRight - scissorLeft), Math.Max(0, scissorBottom - scissorTop));
                spriteBatch.GraphicsDevice.ScissorRectangle = scissorRect;
                spriteBatch.GraphicsDevice.RasterizerState = new RasterizerState { ScissorTestEnable = true, CullMode = CullMode.None };
            }
            else
            {
                scissorRect = spriteBatch.GraphicsDevice.ScissorRectangle;
            }

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
                DrawTabView(spriteBatch, scissorRect, uiFont, scrollOffset);
            }

            if (restoreScissorAtEnd && !savedScissor.IsEmpty)
            {
                spriteBatch.GraphicsDevice.ScissorRectangle = savedScissor;
                spriteBatch.GraphicsDevice.RasterizerState = new RasterizerState { ScissorTestEnable = false };
            }
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
            bool isThumbHovered = thumbBounds.Contains(_hoverMousePosition) || _isDraggingScrollbar;
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
                    // Load and set module logo for title bar and taskbar
                    try
                    {
                        var logo = content.Load<Texture2D>("Modules/CharacterCreation/logo");
                        _windowManagement.SetWindowLogo(logo);
                    }
                    catch (Exception exLogo)
                    {
                        _engine?.Log($"CharacterCreation: Could not load logo: {exLogo.Message}");
                    }
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

