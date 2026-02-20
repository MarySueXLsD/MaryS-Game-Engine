using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Content;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System;
using System.Linq;
using MarySGameEngine;
using MarySGameEngine.Modules.WindowManagement_essential;
using MarySGameEngine.Modules.TaskBar_essential;
using MarySGameEngine.Modules.UIElements_essential;
using MarySGameEngine.Modules.TopBar_essential;
using MarySGameEngine.Modules.FlashMessage_essential;

namespace MarySGameEngine.Modules.GameManager_essential
{
    public class GameProject
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime LastModified { get; set; }
        public string Description { get; set; }
        public string Version { get; set; }
        public string EngineVersion { get; set; }
        public string[] Tags { get; set; }
        public bool IsFavorite { get; set; }
        public string Genre { get; set; }

        public GameProject()
        {
            Name = "";
            Path = "";
            CreatedDate = DateTime.Now;
            LastModified = DateTime.Now;
            Description = "";
            Version = "1.0.0";
            EngineVersion = "1.0.0";
            Tags = new string[0];
            IsFavorite = false;
        }
    }

    public class ActiveWorkspaceData
    {
        public string Path { get; set; } = "";
    }

    public class ContextMenuItem
    {
        public string Text { get; set; }
        public bool IsHovered { get; set; }

        public ContextMenuItem(string text)
        {
            Text = text;
            IsHovered = false;
        }
    }

    public class GameManager : IModule
    {
        private WindowManagement _windowManagement;
        private GraphicsDevice _graphicsDevice;
        private SpriteFont _menuFont;
        private SpriteFont _uiFont;
        private SpriteFont _sidebarFont; // Separate font for sidebar
        private SpriteFont _pixelFont; // Pixel font for sidebar and titles
        private int _windowWidth;
        private TaskBar _taskBar;
        private Texture2D _pixel;
        private ContentManager _content;
        private MouseState _currentMouseState;
        private MouseState _previousMouseState;
        private KeyboardState _currentKeyboardState;
        private KeyboardState _previousKeyboardState;

        // UI State
        private string _currentSection = "Projects"; // "Projects", "Learn", "Community"
        private List<GameProject> _projects;
        private GameProject _selectedProject;
        private int _selectedProjectIndex = -1;
        private string _activeWorkspacePath = ""; // Path of the active workspace

        // Layout properties
        private Rectangle _leftSidebarBounds;
        private Rectangle _rightContentBounds;
        private Rectangle _scrollableContentBounds; // Area that can be scrolled (excludes create button)
        // Wizard step data
        private string[] _genreOptions = { "Isometric", "JRPG", "Top Down", "Card Based" };
        private string[] _genreDescriptions = {
            "Classic isometric view with 2.5D perspective, perfect for strategy and RPG games",
            "Traditional JRPG style with turn-based combat and character progression",
            "Bird's eye view perfect for action games, puzzles, and real-time strategy",
            "Card-based gameplay with roguelike elements and deck building mechanics"
        };
        private int _sidebarWidth = 240; // Wider sidebar for better content
        private int _contentPadding = 24; // Modern spacing
        private int _sectionButtonHeight = 44; // Larger touch targets
        private int _projectItemHeight = 64; // More spacious items
        private int _projectItemPadding = 12; // Better spacing
        private int _borderRadius = 8; // Rounded corners
        private int _cardPadding = 16; // Card internal padding

        // Scrolling properties
        private int _scrollY = 0;
        private int _contentHeight = 0;
        private bool _needsScrollbar = false;
        private Rectangle _scrollbarBounds;
        private bool _isDraggingScrollbar = false;
        private Vector2 _scrollbarDragStart;
        private const int SCROLLBAR_WIDTH = 16;
        private const int SCROLLBAR_PADDING = 2;
        private bool _scrollingEnabled = true;

        // Project creation wizard
        private bool _isCreatingProject = false;
        private int _currentWizardStep = 0; // 0 = name input, 1 = genre selection
        private string _projectName = "";
        private string _projectDescription = "";
        private string _selectedGenre = "";
        private int _hoveredGenreIndex = -1;
        private Rectangle _wizardContentBounds;
        private bool _isInputFocused = false;

        // Font scaling
        private const float FONT_SCALE = 1.0f; // Normal font size
        private const float HEADER_FONT_SCALE = 1.1f; // Slightly larger for headers
        private const float SMALL_FONT_SCALE = 0.9f; // Smaller for secondary text

        // Context menu related fields
        private bool _isContextMenuVisible;
        private Vector2 _contextMenuPosition;
        private Rectangle _contextMenuBounds;
        private List<ContextMenuItem> _contextMenuItems;
        private const int CONTEXT_MENU_ITEM_HEIGHT = 40;
        private const int CONTEXT_MENU_PADDING = 10;
        private const int MENU_BORDER_THICKNESS = 2;
        private const int MENU_SHADOW_OFFSET = 2;
        private readonly Color MENU_BACKGROUND_COLOR = new Color(40, 40, 40);
        private readonly Color MENU_HOVER_COLOR = new Color(147, 112, 219, 180); // Semi-transparent purple
        private readonly Color MENU_BORDER_COLOR = new Color(147, 112, 219); // Solid purple
        private readonly Color MENU_SHADOW_COLOR = new Color(0, 0, 0, 100);
        private readonly Color MENU_TEXT_COLOR = new Color(220, 220, 220);
        private GameProject _contextMenuTargetProject; // The project that was right-clicked

        // Rename functionality
        private bool _isRenaming = false;
        private string _renameText = "";
        private Rectangle _renameInputBounds;
        private const int MAX_PROJECT_NAME_LENGTH = 10; // Constant for name limit
        private float _cursorBlinkTime = 0f;
        private const float CURSOR_BLINK_RATE = 0.5f; // Blink every 0.5 seconds
        private int _cursorPosition = 0; // Current cursor position in the text

        // Animation variables
        private float _nextButtonAnimationTime = 0f;
        private const float ANIMATION_DURATION = 0.3f; // 300ms animation

        // Double-click detection
        private float _lastProjectClickTime = 0f;
        private Point _lastProjectClickPosition = Point.Zero;
        private int _lastClickedProjectIndex = -1;
        private const float DOUBLE_CLICK_TIME = 0.5f; // 500ms
        private const int DOUBLE_CLICK_DISTANCE = 5; // 5 pixels
        private float _currentTime = 0f;

        // Modern Color Palette
        private readonly Color SIDEBAR_BACKGROUND = new Color(30, 30, 35); // Dark charcoal
        private readonly Color CONTENT_BACKGROUND = new Color(40, 40, 45); // Slightly lighter charcoal
        private readonly Color CARD_BACKGROUND = new Color(50, 50, 55); // Card background
        private readonly Color SECTION_HOVER_COLOR = new Color(99, 102, 241, 80); // Indigo with transparency
        private readonly Color SECTION_ACTIVE_COLOR = new Color(99, 102, 241); // Indigo
        private readonly Color PROJECT_HOVER_COLOR = new Color(60, 60, 65); // Subtle hover
        private readonly Color PROJECT_SELECTED_COLOR = new Color(99, 102, 241, 120); // Indigo with transparency
        private readonly Color PROJECT_ACTIVE_WORKSPACE_COLOR = new Color(147, 112, 219, 200); // Purple with more opacity for active workspace
        private readonly Color BORDER_COLOR = new Color(60, 60, 65); // Subtle border
        private readonly Color TEXT_PRIMARY = new Color(248, 250, 252); // Near white
        private readonly Color TEXT_SECONDARY = new Color(156, 163, 175); // Gray-400
        private readonly Color TEXT_TERTIARY = new Color(107, 114, 128); // Gray-500
        private readonly Color BUTTON_PRIMARY = new Color(99, 102, 241); // Indigo
        private readonly Color BUTTON_PRIMARY_HOVER = new Color(79, 70, 229); // Indigo-600
        private readonly Color BUTTON_SECONDARY = new Color(55, 65, 81); // Gray-700
        private readonly Color BUTTON_SECONDARY_HOVER = new Color(75, 85, 99); // Gray-600
        private readonly Color SUCCESS_COLOR = new Color(34, 197, 94); // Green-500
        private readonly Color WARNING_COLOR = new Color(245, 158, 11); // Amber-500
        private readonly Color ERROR_COLOR = new Color(239, 68, 68); // Red-500
        private readonly Color ACCENT_COLOR = new Color(139, 92, 246); // Purple-500
        private readonly Color ACCENT_HOVER = new Color(124, 58, 237); // Purple-600

        // Section buttons
        private List<Rectangle> _sectionButtonBounds = new List<Rectangle>();
        private List<string> _sectionNames = new List<string> { "Projects", "Learn", "Community" };

        public GameManager(GraphicsDevice graphicsDevice, SpriteFont menuFont, int windowWidth)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("GameManager: Constructor called");
                
                _graphicsDevice = graphicsDevice;
                _menuFont = menuFont;
                _uiFont = menuFont; // Use same font for now
                _windowWidth = windowWidth;
                _projects = new List<GameProject>();
                // Initialize wizard state
            _projectName = "";
            _projectDescription = "";
            _selectedGenre = "";
            _hoveredGenreIndex = -1;
            
            // Initialize context menu
            _contextMenuItems = new List<ContextMenuItem>();
            _isContextMenuVisible = false;
            _isRenaming = false;
            
            // Debug: Log initial font state
            System.Diagnostics.Debug.WriteLine($"[GameManager] Constructor - Menu font: {_menuFont != null}");
            System.Diagnostics.Debug.WriteLine($"[GameManager] Constructor - UI font: {_uiFont != null}");
            System.Diagnostics.Debug.WriteLine($"[GameManager] Constructor - Sidebar font: {_sidebarFont != null}");
            System.Diagnostics.Debug.WriteLine($"[GameManager] Constructor - Pixel font: {_pixelFont != null}");

                // Create pixel texture
                _pixel = new Texture2D(graphicsDevice, 1, 1);
                _pixel.SetData(new[] { Color.White });

                // Initialize window management
                var windowProperties = new WindowProperties
                {
                    IsVisible = false,
                    IsMovable = true,
                    IsResizable = true  // Allow resize so user can adjust if default is too small
                };

                _windowManagement = new WindowManagement(graphicsDevice, menuFont, windowWidth, windowProperties);
                _windowManagement.SetWindowTitle("Game Manager");
                _windowManagement.SetVisible(false);  // Do not open by default
                
                // Use larger default height - 200 was too small on typical screens
                _windowManagement.SetDefaultSize(1000, 550);
                _windowManagement.SetCustomMinimumSize(800, 250);
                _windowManagement.SetPosition(new Vector2(100, 50)); // Set initial position

                System.Diagnostics.Debug.WriteLine("GameManager: Window management created");

                // Initialize UI bounds
                UpdateBounds();

                // Load existing projects
                LoadProjects();

                // Don't start wizard by default - show projects list instead
                // StartProjectWizard();
                
                // Debug: Log initial state
                System.Diagnostics.Debug.WriteLine($"[GameManager] Constructor - _isCreatingProject: {_isCreatingProject}, _currentSection: {_currentSection}");
                
                System.Diagnostics.Debug.WriteLine("GameManager: Constructor completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GameManager Constructor Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"GameManager Constructor Stack Trace: {ex.StackTrace}");
            }
        }

        private void UpdateBounds()
        {
            try
            {
                var windowBounds = _windowManagement.GetWindowBounds();
                
                // Debug: Log window bounds
                System.Diagnostics.Debug.WriteLine($"GameManager: Window bounds - X: {windowBounds.X}, Y: {windowBounds.Y}, Width: {windowBounds.Width}, Height: {windowBounds.Height}");
                
                // Ensure minimum window size for proper bounds calculation
                if (windowBounds.Width < 200 || windowBounds.Height < 100)
                {
                    System.Diagnostics.Debug.WriteLine($"GameManager: Window too small for proper bounds calculation, using defaults");
                    windowBounds = new Rectangle(windowBounds.X, windowBounds.Y, Math.Max(200, windowBounds.Width), Math.Max(100, windowBounds.Height));
                }
                
                _leftSidebarBounds = new Rectangle(
                    windowBounds.X + 2, // Account for window border
                    windowBounds.Y + 42, // Account for title bar
                    _sidebarWidth,
                    windowBounds.Height - 44 // Account for borders
                );

                _rightContentBounds = new Rectangle(
                    _leftSidebarBounds.Right,
                    _leftSidebarBounds.Y,
                    windowBounds.Width - _sidebarWidth - 4, // Account for borders
                    _leftSidebarBounds.Height
                );

                _wizardContentBounds = new Rectangle(
                    _rightContentBounds.X + _contentPadding,
                    _rightContentBounds.Y + _contentPadding,
                    _rightContentBounds.Width - (_contentPadding * 2),
                    _rightContentBounds.Height - (_contentPadding * 2)
                );
                
                // Calculate scrollable content bounds (area below create button)
                // Create button is at Y + _contentPadding with height 44, plus some spacing (60 total from top)
                int scrollableStartY = _rightContentBounds.Y + _contentPadding + 60; // Start after create button
                int scrollableHeight = _rightContentBounds.Height - (_contentPadding + 60); // Remaining height
                _scrollableContentBounds = new Rectangle(
                    _rightContentBounds.X,
                    scrollableStartY,
                    _rightContentBounds.Width,
                    scrollableHeight
                );
                
                System.Diagnostics.Debug.WriteLine($"GameManager: Calculated wizard bounds: {_wizardContentBounds}, Right content bounds: {_rightContentBounds}, Scrollable bounds: {_scrollableContentBounds}");

                // Update section button bounds
                _sectionButtonBounds = new List<Rectangle>();
                System.Diagnostics.Debug.WriteLine($"GameManager: Creating {_sectionNames.Count} section button bounds");
                for (int i = 0; i < _sectionNames.Count; i++)
                {
                    var buttonBounds = new Rectangle(
                        _leftSidebarBounds.X + 10,
                        _leftSidebarBounds.Y + 10 + (i * (_sectionButtonHeight + 5)),
                        _leftSidebarBounds.Width - 20,
                        _sectionButtonHeight
                    );
                    _sectionButtonBounds.Add(buttonBounds);
                    System.Diagnostics.Debug.WriteLine($"GameManager: Section {i} '{_sectionNames[i]}' bounds: {buttonBounds}");
                }

                // Update scrollbar bounds
                UpdateScrollbarBounds();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GameManager UpdateBounds Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"GameManager UpdateBounds Stack Trace: {ex.StackTrace}");
            }
        }

        private void UpdateScrollbarBounds()
        {
            if (_needsScrollbar)
            {
                _scrollbarBounds = new Rectangle(
                    _scrollableContentBounds.Right - SCROLLBAR_WIDTH - SCROLLBAR_PADDING,
                    _scrollableContentBounds.Y + SCROLLBAR_PADDING,
                    SCROLLBAR_WIDTH,
                    _scrollableContentBounds.Height - (SCROLLBAR_PADDING * 2)
                );
            }
        }

        private void SetupProjectContextMenu(GameProject project, Vector2 position)
        {
            System.Diagnostics.Debug.WriteLine($"GameManager: Setting up context menu for project: {project.Name} at position: {position}");
            
            _contextMenuItems.Clear();
            
            // Check if this project is the active workspace
            bool isActiveWorkspace = false;
            if (!string.IsNullOrEmpty(project.Path) && !string.IsNullOrEmpty(_activeWorkspacePath))
            {
                string normalizedProjectPath = Path.GetFullPath(project.Path);
                string normalizedActivePath = Path.GetFullPath(_activeWorkspacePath);
                isActiveWorkspace = normalizedProjectPath == normalizedActivePath;
            }
            
            // Show "Activate" or "Deactivate" instead of "Open" based on workspace state
            if (isActiveWorkspace)
            {
                _contextMenuItems.Add(new ContextMenuItem("Deactivate"));
            }
            else
            {
                _contextMenuItems.Add(new ContextMenuItem("Activate"));
            }
            
            _contextMenuItems.Add(new ContextMenuItem("Rename"));
            _contextMenuItems.Add(new ContextMenuItem("Delete"));
            
            _contextMenuTargetProject = project;
            _contextMenuPosition = position;
            _isContextMenuVisible = true;
            
            // Calculate context menu bounds
            int maxWidth = 0;
            foreach (var item in _contextMenuItems)
            {
                Vector2 textSize = _uiFont.MeasureString(item.Text);
                maxWidth = Math.Max(maxWidth, (int)textSize.X);
            }
            
            int menuWidth = maxWidth + (CONTEXT_MENU_PADDING * 2);
            int menuHeight = _contextMenuItems.Count * CONTEXT_MENU_ITEM_HEIGHT;
            
            _contextMenuBounds = new Rectangle(
                (int)position.X,
                (int)position.Y,
                menuWidth,
                menuHeight
            );
            
            // Adjust position if menu would go off screen
            if (_contextMenuBounds.Right > _windowWidth)
            {
                _contextMenuBounds.X = _windowWidth - _contextMenuBounds.Width - 10;
            }
            if (_contextMenuBounds.Bottom > _graphicsDevice.Viewport.Height)
            {
                _contextMenuBounds.Y = _graphicsDevice.Viewport.Height - _contextMenuBounds.Height - 10;
            }
            
            System.Diagnostics.Debug.WriteLine($"GameManager: Context menu bounds: {_contextMenuBounds}, items count: {_contextMenuItems.Count}");
        }

        private void CloseContextMenu()
        {
            _isContextMenuVisible = false;
            _contextMenuTargetProject = null;
            _contextMenuItems.Clear();
        }

        private void HandleContextMenuAction(int itemIndex)
        {
            if (_contextMenuTargetProject == null || itemIndex < 0 || itemIndex >= _contextMenuItems.Count)
            {
                System.Diagnostics.Debug.WriteLine("GameManager: Invalid context menu action");
                return;
            }

            string action = _contextMenuItems[itemIndex].Text;
            System.Diagnostics.Debug.WriteLine($"GameManager: Processing context menu action: {action} for project: {_contextMenuTargetProject.Name}");

            // Store the target project before closing the menu
            var targetProject = _contextMenuTargetProject;
            CloseContextMenu();

            switch (action)
            {
                case "Activate":
                    System.Diagnostics.Debug.WriteLine("GameManager: Activating workspace for project: " + targetProject.Name);
                    ActivateWorkspace(targetProject);
                    break;
                case "Deactivate":
                    System.Diagnostics.Debug.WriteLine("GameManager: Deactivating workspace for project: " + targetProject.Name);
                    DeactivateWorkspace();
                    break;
                case "Rename":
                    System.Diagnostics.Debug.WriteLine("GameManager: Starting rename for project: " + targetProject.Name);
                    StartRenameProject(targetProject);
                    break;
                case "Delete":
                    System.Diagnostics.Debug.WriteLine("GameManager: Deleting project: " + targetProject.Name);
                    DeleteProject(targetProject);
                    break;
                default:
                    System.Diagnostics.Debug.WriteLine($"GameManager: Unknown context menu action: {action}");
                    break;
            }
        }

        private void StartRenameProject(GameProject project)
        {
            System.Diagnostics.Debug.WriteLine($"GameManager: Starting rename for project: {project.Name}");
            _isRenaming = true;
            _renameText = project.Name;
            _cursorPosition = _renameText.Length; // Set cursor to end of text
            _contextMenuTargetProject = project; // Keep reference to the project being renamed
            
            // Calculate input field bounds based on the project's position in the list
            int projectIndex = _projects.IndexOf(project);
            if (projectIndex >= 0)
            {
                int startY = _rightContentBounds.Y + _contentPadding + 60 - _scrollY; // Below create button
                int itemY = startY + 40 + (projectIndex * (_projectItemHeight + _projectItemPadding)); // Below header + project offset
                
                // Calculate width for just the name column (1/3 of the total width)
                int totalWidth = _rightContentBounds.Width - (_contentPadding * 2) - (_needsScrollbar ? SCROLLBAR_WIDTH + SCROLLBAR_PADDING : 0);
                int nameColumnWidth = totalWidth / 3;
                
                _renameInputBounds = new Rectangle(
                    _rightContentBounds.X + _contentPadding + _cardPadding,
                    itemY + _cardPadding,
                    nameColumnWidth,
                    30
                );
                
                System.Diagnostics.Debug.WriteLine($"GameManager: Rename input bounds: {_renameInputBounds}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("GameManager: Project not found in list for rename");
            }
        }

        private void FinishRenameProject()
        {
            System.Diagnostics.Debug.WriteLine($"GameManager: Finishing rename. Target: {_contextMenuTargetProject?.Name}, Text: '{_renameText}', Valid: {IsValidProjectName(_renameText)}");
            
            if (_contextMenuTargetProject != null && !string.IsNullOrWhiteSpace(_renameText) && IsValidProjectName(_renameText))
            {
                _contextMenuTargetProject.Name = _renameText.Trim();
                _contextMenuTargetProject.LastModified = DateTime.Now;
                SaveProjects();
                System.Diagnostics.Debug.WriteLine($"GameManager: Successfully renamed project to: {_contextMenuTargetProject.Name}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("GameManager: Rename cancelled or invalid");
            }
            
            // Clear rename state
            _isRenaming = false;
            _renameText = "";
            _cursorPosition = 0;
            _contextMenuTargetProject = null;
        }

        private void DeleteProject(GameProject project)
        {
            if (project != null)
            {
                // Check if this was the active workspace
                bool wasActiveWorkspace = false;
                if (!string.IsNullOrEmpty(project.Path))
                {
                    string normalizedProjectPath = Path.GetFullPath(project.Path);
                    string normalizedActivePath = !string.IsNullOrEmpty(_activeWorkspacePath) ? Path.GetFullPath(_activeWorkspacePath) : "";
                    wasActiveWorkspace = normalizedProjectPath == normalizedActivePath;
                }
                else
                {
                    // If project has no path, construct it and compare
                    string projectPath = Path.Combine(Directory.GetCurrentDirectory(), "Projects", project.Name);
                    string normalizedProjectPath = Path.GetFullPath(projectPath);
                    string normalizedActivePath = !string.IsNullOrEmpty(_activeWorkspacePath) ? Path.GetFullPath(_activeWorkspacePath) : "";
                    wasActiveWorkspace = normalizedProjectPath == normalizedActivePath;
                }
                
                // Store project name for flash message
                string projectName = project.Name;
                string gameType = !string.IsNullOrEmpty(project.Genre) ? project.Genre : "Unknown";
                
                _projects.Remove(project);
                SaveProjects();
                
                // Clear active workspace if this was the active workspace
                if (wasActiveWorkspace)
                {
                    _activeWorkspacePath = "";
                    SaveActiveWorkspace();
                }
                
                // Clear selection if the deleted project was selected
                if (_selectedProject == project)
                {
                    _selectedProject = null;
                    _selectedProjectIndex = -1;
                }
                
                // Show flash message
                FlashMessage.Show(
                    $"Deleted workspace {projectName} ({gameType})",
                    FlashMessageType.Success,
                    3.0f
                );
                
                System.Diagnostics.Debug.WriteLine($"Deleted project: {project.Name}");
            }
        }

        private void StartProjectWizard()
        {
            _isCreatingProject = true;
            _currentWizardStep = 0;
            _projectName = "";
            _projectDescription = "";
            _selectedGenre = "";
            _hoveredGenreIndex = -1;
            System.Diagnostics.Debug.WriteLine("GameManager: Started project creation wizard");
        }

        private void LoadProjects()
        {
            try
            {
                string projectsPath = Path.Combine(Directory.GetCurrentDirectory(), "Projects", "projects.json");
                if (File.Exists(projectsPath))
                {
                    string jsonContent = File.ReadAllText(projectsPath);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip
                    };
                    _projects = JsonSerializer.Deserialize<List<GameProject>>(jsonContent, options) ?? new List<GameProject>();
                
                // Ensure all projects have paths set
                foreach (var project in _projects)
                {
                    if (string.IsNullOrEmpty(project.Path))
                    {
                        project.Path = Path.Combine(Directory.GetCurrentDirectory(), "Projects", project.Name);
                    }
                }
                }
                else
                {
                    _projects = new List<GameProject>();
                }
                
                // Load active workspace after loading projects
                LoadActiveWorkspace();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading projects: {ex.Message}");
                _projects = new List<GameProject>();
            }
        }
        
        private void LoadActiveWorkspace()
        {
            try
            {
                string workspacePath = Path.Combine(Directory.GetCurrentDirectory(), "Projects", "active_workspace.json");
                if (File.Exists(workspacePath))
                {
                    string jsonContent = File.ReadAllText(workspacePath);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip
                    };
                    var workspaceData = JsonSerializer.Deserialize<ActiveWorkspaceData>(jsonContent, options);
                    if (workspaceData != null && !string.IsNullOrEmpty(workspaceData.Path))
                    {
                        _activeWorkspacePath = workspaceData.Path;
                        System.Diagnostics.Debug.WriteLine($"GameManager: Loaded active workspace: {_activeWorkspacePath}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading active workspace: {ex.Message}");
                _activeWorkspacePath = "";
            }
        }
        
        private void SaveActiveWorkspace()
        {
            try
            {
                string projectsDir = Path.Combine(Directory.GetCurrentDirectory(), "Projects");
                if (!Directory.Exists(projectsDir))
                {
                    Directory.CreateDirectory(projectsDir);
                }

                string workspacePath = Path.Combine(projectsDir, "active_workspace.json");
                var workspaceData = new ActiveWorkspaceData
                {
                    Path = _activeWorkspacePath
                };
                
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                string jsonContent = JsonSerializer.Serialize(workspaceData, options);
                File.WriteAllText(workspacePath, jsonContent);
                System.Diagnostics.Debug.WriteLine($"GameManager: Saved active workspace: {_activeWorkspacePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving active workspace: {ex.Message}");
            }
        }

        private void ActivateWorkspace(GameProject project)
        {
            try
            {
                // Ensure project has a path
                if (string.IsNullOrEmpty(project.Path))
                {
                    project.Path = Path.Combine(Directory.GetCurrentDirectory(), "Projects", project.Name);
                }
                
                // Normalize paths for comparison
                string normalizedProjectPath = Path.GetFullPath(project.Path);
                string normalizedActivePath = !string.IsNullOrEmpty(_activeWorkspacePath) ? Path.GetFullPath(_activeWorkspacePath) : "";
                
                // Only set and show message if it's a different workspace
                if (normalizedProjectPath != normalizedActivePath)
                {
                    // Set as active workspace (only one can be active at a time)
                    _activeWorkspacePath = project.Path;
                    SaveActiveWorkspace();
                    
                    // Show flash message
                    string gameType = !string.IsNullOrEmpty(project.Genre) ? project.Genre : "Unknown";
                    FlashMessage.Show(
                        $"Activated workspace: {project.Name} ({gameType})",
                        FlashMessageType.Info,
                        3.0f
                    );
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error activating workspace: {ex.Message}");
            }
        }

        private void DeactivateWorkspace()
        {
            try
            {
                if (!string.IsNullOrEmpty(_activeWorkspacePath))
                {
                    // Clear active workspace
                    _activeWorkspacePath = "";
                    SaveActiveWorkspace();
                    
                    // Show flash message
                    FlashMessage.Show(
                        "Workspace deactivated",
                        FlashMessageType.Info,
                        3.0f
                    );
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deactivating workspace: {ex.Message}");
            }
        }

        private void SaveProjects()
        {
            try
            {
                string projectsDir = Path.Combine(Directory.GetCurrentDirectory(), "Projects");
                if (!Directory.Exists(projectsDir))
                {
                    Directory.CreateDirectory(projectsDir);
                }

                string projectsPath = Path.Combine(projectsDir, "projects.json");
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                string jsonContent = JsonSerializer.Serialize(_projects, options);
                File.WriteAllText(projectsPath, jsonContent);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving projects: {ex.Message}");
            }
        }

        public void SetTaskBar(TaskBar taskBar)
        {
            _taskBar = taskBar;
            _windowManagement.SetTaskBar(taskBar);
            
            // If window is already visible, ensure icon is added to taskbar
            if (_windowManagement != null && _windowManagement.IsVisible())
            {
                // Icon will be added in LoadContent when content is available
                // But we can also trigger it here if content is already loaded
                if (_content != null)
                {
                    _taskBar.EnsureModuleIconExists("Game Manager", _content);
                }
            }
        }

        public void Update()
        {
            try
            {
                _previousMouseState = _currentMouseState;
                _currentMouseState = Mouse.GetState();
                _previousKeyboardState = _currentKeyboardState;
                _currentKeyboardState = Keyboard.GetState();

                // Update time for double-click detection
                _currentTime += (float)GameEngine.Instance.TargetElapsedTime.TotalSeconds;

                _windowManagement.Update();

                // Always update bounds to ensure they're calculated
                UpdateBounds();

                if (!_windowManagement.IsVisible())
                    return;
                HandleInput();
                UpdateScrolling();

                if (_isCreatingProject)
                {
                    HandleWizardInput();
                }

                // Update animation
                UpdateAnimations();
                
                // Update cursor blink for rename input
                if (_isRenaming)
                {
                    _cursorBlinkTime += 0.016f; // Assuming 60fps
                    if (_cursorBlinkTime >= CURSOR_BLINK_RATE * 2)
                    {
                        _cursorBlinkTime = 0f;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GameManager Update Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"GameManager Stack Trace: {ex.StackTrace}");
            }
        }

        private void HandleInput()
        {
            var mousePosition = _currentMouseState.Position;

            // Handle rename input first (highest priority)
            if (_isRenaming)
            {
                HandleRenameInput();
                return; // Don't handle other input when renaming
            }

            // Handle context menu clicks
            if (_isContextMenuVisible)
            {
                if (_currentMouseState.LeftButton == ButtonState.Pressed && 
                    _previousMouseState.LeftButton == ButtonState.Released)
                {
                    // Only handle clicks if window is topmost
                    if (!IsTopmostWindowUnderMouse(_windowManagement, mousePosition))
                    {
                        CloseContextMenu();
                        return;
                    }

                    // Check if clicked on context menu item
                    bool clickedOnMenuItem = false;
                    for (int i = 0; i < _contextMenuItems.Count; i++)
                    {
                        Rectangle itemBounds = new Rectangle(
                            _contextMenuBounds.X,
                            _contextMenuBounds.Y + (i * CONTEXT_MENU_ITEM_HEIGHT),
                            _contextMenuBounds.Width,
                            CONTEXT_MENU_ITEM_HEIGHT
                        );
                        
                        if (itemBounds.Contains(mousePosition))
                        {
                            System.Diagnostics.Debug.WriteLine($"GameManager: Clicked on context menu item: {_contextMenuItems[i].Text}");
                            HandleContextMenuAction(i);
                            clickedOnMenuItem = true;
                            break;
                        }
                    }
                    
                    // Only close context menu if clicked outside of it
                    if (!clickedOnMenuItem)
                    {
                        System.Diagnostics.Debug.WriteLine("GameManager: Clicked outside context menu, closing it");
                        CloseContextMenu();
                    }
                }
                return; // Don't handle other input when context menu is visible
            }

            // Handle section button clicks
            if (_currentMouseState.LeftButton == ButtonState.Pressed && 
                _previousMouseState.LeftButton == ButtonState.Released)
            {
                // Only handle clicks if window is topmost
                if (!IsTopmostWindowUnderMouse(_windowManagement, mousePosition))
                    return;

                // Debug: Log mouse position for troubleshooting
                System.Diagnostics.Debug.WriteLine($"GameManager: Mouse click at {mousePosition}");
                
                for (int i = 0; i < _sectionButtonBounds.Count; i++)
                {
                    if (_sectionButtonBounds[i].Contains(mousePosition))
                    {
                        _currentSection = _sectionNames[i];
                        _isCreatingProject = false;
                        System.Diagnostics.Debug.WriteLine($"GameManager: Section changed to {_sectionNames[i]}");
                        break;
                    }
                }

                // Handle project item clicks
                if (_currentSection == "Projects" && !_isCreatingProject)
                {
                    HandleProjectClicks(mousePosition);
                }

                // Handle create project button
                if (_currentSection == "Projects" && !_isCreatingProject)
                {
                    var createButtonBounds = GetCreateProjectButtonBounds();
                    System.Diagnostics.Debug.WriteLine($"GameManager: Create button bounds: {createButtonBounds}");
                    if (createButtonBounds.Contains(mousePosition))
                    {
                        StartProjectWizard();
                        System.Diagnostics.Debug.WriteLine("GameManager: Started project creation wizard");
                    }
                }

                // Wizard input is handled in HandleWizardInput method
            }

            // Handle right-click on project items
            if (_currentMouseState.RightButton == ButtonState.Pressed && 
                _previousMouseState.RightButton == ButtonState.Released)
            {
                // Only handle clicks if window is topmost
                if (!IsTopmostWindowUnderMouse(_windowManagement, mousePosition))
                    return;

                // Close TaskBar context menu if it's visible
                if (_taskBar != null)
                {
                    _taskBar.CloseContextMenuIfVisible();
                }
                
                if (_currentSection == "Projects" && !_isCreatingProject)
                {
                    HandleProjectRightClick(mousePosition);
                }
            }

            // Handle scrollbar interaction
            HandleScrollbarInteraction();
        }

        private bool IsMouseOverTopBarDropdown(Point mousePosition)
        {
            var modules = GameEngine.Instance.GetActiveModules();
            foreach (var module in modules)
            {
                if (module is TopBar topBar)
                {
                    // Check if mouse is over any TopBar dropdown
                    foreach (var menuItem in topBar.GetMenuItems())
                    {
                        if (menuItem.IsDropdownVisible)
                        {
                            foreach (var dropdownBound in menuItem.DropdownBounds)
                            {
                                if (dropdownBound.Contains(mousePosition))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
            return false;
        }

        private void HandleProjectClicks(Point mousePosition)
        {
            // Only handle clicks if window is topmost
            if (!IsTopmostWindowUnderMouse(_windowManagement, mousePosition))
                return;

            // Only handle clicks within the scrollable content bounds
            if (!_scrollableContentBounds.Contains(mousePosition))
                return;

            // Account for scrolling - match drawing positions exactly
            int startY = _scrollableContentBounds.Y - _scrollY; // Start of scrollable content
            int itemY = startY + 40; // Below header

            System.Diagnostics.Debug.WriteLine($"HandleProjectClicks: Mouse at {mousePosition}, startY: {startY}, itemY: {itemY}, projects count: {_projects.Count}");

            bool clickedOnProject = false;
            bool isDoubleClick = false;
            int clickedProjectIndex = -1;

            for (int i = 0; i < _projects.Count; i++)
            {
                var itemBounds = new Rectangle(
                    _scrollableContentBounds.X + _contentPadding,
                    itemY,
                    _scrollableContentBounds.Width - (_contentPadding * 2) - (_needsScrollbar ? SCROLLBAR_WIDTH + SCROLLBAR_PADDING : 0),
                    _projectItemHeight
                );

                // Only check clicks on items that are actually visible
                if (itemBounds.Contains(mousePosition) && 
                    itemBounds.Bottom > _scrollableContentBounds.Y && 
                    itemBounds.Top < _scrollableContentBounds.Bottom)
                {
                    clickedProjectIndex = i;
                    
                    // Check for double-click
                    if (_currentTime - _lastProjectClickTime < DOUBLE_CLICK_TIME &&
                        _lastClickedProjectIndex == i &&
                        Vector2.Distance(new Vector2(mousePosition.X, mousePosition.Y), new Vector2(_lastProjectClickPosition.X, _lastProjectClickPosition.Y)) < DOUBLE_CLICK_DISTANCE)
                    {
                        isDoubleClick = true;
                        System.Diagnostics.Debug.WriteLine($"Double-click detected on project: {_projects[i].Name}");
                    }

                    // Only set selected for context menu/rename purposes, not for visual selection
                    _selectedProjectIndex = i;
                    _selectedProject = _projects[i];
                    clickedOnProject = true;
                    System.Diagnostics.Debug.WriteLine($"Clicked on project: {_projects[i].Name} (bounds: {itemBounds}, mouse: {mousePosition})");
                    break;
                }

                itemY += _projectItemHeight + _projectItemPadding;
            }

            // Handle double-click
            if (isDoubleClick && clickedProjectIndex >= 0 && clickedProjectIndex < _projects.Count)
            {
                var project = _projects[clickedProjectIndex];
                
                // Ensure project has a path
                if (string.IsNullOrEmpty(project.Path))
                {
                    project.Path = Path.Combine(Directory.GetCurrentDirectory(), "Projects", project.Name);
                }
                
                // Normalize paths for comparison
                string normalizedProjectPath = Path.GetFullPath(project.Path);
                string normalizedActivePath = !string.IsNullOrEmpty(_activeWorkspacePath) ? Path.GetFullPath(_activeWorkspacePath) : "";
                
                // Only set and show message if it's a different workspace
                if (normalizedProjectPath != normalizedActivePath)
                {
                    // Set as active workspace (only one can be active at a time)
                    _activeWorkspacePath = project.Path;
                    SaveActiveWorkspace();
                    
                    // Show flash message
                    string gameType = !string.IsNullOrEmpty(project.Genre) ? project.Genre : "Unknown";
                    FlashMessage.Show(
                        $"Changed workspace to {project.Name} ({gameType})",
                        FlashMessageType.Info,
                        3.0f
                    );
                }
            }

            // Update double-click tracking
            if (clickedOnProject)
            {
                _lastProjectClickTime = _currentTime;
                _lastProjectClickPosition = mousePosition;
                _lastClickedProjectIndex = clickedProjectIndex;
            }

            // If clicked outside of any project item, clear selection (for context menu/rename)
            if (!clickedOnProject)
            {
                _selectedProjectIndex = -1;
                _selectedProject = null;
                System.Diagnostics.Debug.WriteLine("Clicked outside project items");
            }
        }

        private void HandleProjectRightClick(Point mousePosition)
        {
            // Only handle clicks if window is topmost
            if (!IsTopmostWindowUnderMouse(_windowManagement, mousePosition))
                return;

            // Only handle clicks within the scrollable content bounds
            if (!_scrollableContentBounds.Contains(mousePosition))
                return;

            // Account for scrolling - match drawing positions exactly
            int startY = _scrollableContentBounds.Y - _scrollY; // Start of scrollable content
            int itemY = startY + 40; // Below header

            for (int i = 0; i < _projects.Count; i++)
            {
                var itemBounds = new Rectangle(
                    _scrollableContentBounds.X + _contentPadding,
                    itemY,
                    _scrollableContentBounds.Width - (_contentPadding * 2) - (_needsScrollbar ? SCROLLBAR_WIDTH + SCROLLBAR_PADDING : 0),
                    _projectItemHeight
                );

                // Only check clicks on items that are actually visible
                if (itemBounds.Bottom > _scrollableContentBounds.Y && 
                    itemBounds.Top < _scrollableContentBounds.Bottom &&
                    itemBounds.Contains(mousePosition))

                if (itemBounds.Contains(mousePosition))
                {
                    SetupProjectContextMenu(_projects[i], new Vector2(mousePosition.X, mousePosition.Y));
                    System.Diagnostics.Debug.WriteLine($"Right-clicked on project: {_projects[i].Name}");
                    break;
                }

                itemY += _projectItemHeight + _projectItemPadding;
            }
        }

        private void HandleRenameInput()
        {
            // Handle mouse clicks in rename input field
            if (_currentMouseState.LeftButton == ButtonState.Pressed && 
                _previousMouseState.LeftButton == ButtonState.Released)
            {
                if (_renameInputBounds.Contains(_currentMouseState.Position))
                {
                    // Calculate cursor position based on mouse click
                    var clickX = _currentMouseState.Position.X - _renameInputBounds.X - 8; // Account for padding
                    
                    // Find the closest character position
                    var bestPosition = 0;
                    var bestDistance = float.MaxValue;
                    
                    for (int i = 0; i <= _renameText.Length; i++)
                    {
                        var testText = _renameText.Substring(0, i);
                        var textWidth = _uiFont.MeasureString(testText).X;
                        var distance = Math.Abs(clickX - textWidth);
                        
                        if (distance < bestDistance)
                        {
                            bestDistance = distance;
                            bestPosition = i;
                        }
                    }
                    
                    _cursorPosition = bestPosition;
                    System.Diagnostics.Debug.WriteLine($"GameManager: Clicked in rename input, cursor position: {_cursorPosition}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("GameManager: Clicked outside rename input, finishing rename");
                    FinishRenameProject();
                }
            }

            // Handle keyboard input for rename
            var keyboardState = Keyboard.GetState();
            var pressedKeys = keyboardState.GetPressedKeys();

            foreach (var key in pressedKeys)
            {
                if (_previousKeyboardState.IsKeyUp(key))
                {
                    if (key == Keys.Enter)
                    {
                        System.Diagnostics.Debug.WriteLine("GameManager: Enter pressed, finishing rename");
                        FinishRenameProject();
                        return;
                    }
                    else if (key == Keys.Escape)
                    {
                        System.Diagnostics.Debug.WriteLine("GameManager: Escape pressed, cancelling rename");
                        _isRenaming = false;
                        _renameText = "";
                        _cursorPosition = 0;
                        _contextMenuTargetProject = null;
                        return;
                    }
                    else if (key == Keys.Back && _cursorPosition > 0)
                    {
                        _renameText = _renameText.Remove(_cursorPosition - 1, 1);
                        _cursorPosition--;
                        System.Diagnostics.Debug.WriteLine($"GameManager: Backspace pressed, rename text: '{_renameText}', cursor: {_cursorPosition}");
                    }
                    else if (key == Keys.Delete && _cursorPosition < _renameText.Length)
                    {
                        _renameText = _renameText.Remove(_cursorPosition, 1);
                        System.Diagnostics.Debug.WriteLine($"GameManager: Delete pressed, rename text: '{_renameText}', cursor: {_cursorPosition}");
                    }
                    else if (key == Keys.Left && _cursorPosition > 0)
                    {
                        _cursorPosition--;
                        System.Diagnostics.Debug.WriteLine($"GameManager: Left arrow pressed, cursor: {_cursorPosition}");
                    }
                    else if (key == Keys.Right && _cursorPosition < _renameText.Length)
                    {
                        _cursorPosition++;
                        System.Diagnostics.Debug.WriteLine($"GameManager: Right arrow pressed, cursor: {_cursorPosition}");
                    }
                    else if (key == Keys.Home)
                    {
                        _cursorPosition = 0;
                        System.Diagnostics.Debug.WriteLine("GameManager: Home pressed, cursor at start");
                    }
                    else if (key == Keys.End)
                    {
                        _cursorPosition = _renameText.Length;
                        System.Diagnostics.Debug.WriteLine($"GameManager: End pressed, cursor at end: {_cursorPosition}");
                    }
                    else if (IsPrintableKey(key) && _renameText.Length < MAX_PROJECT_NAME_LENGTH)
                    {
                        char character = GetCharacterFromKey(key);
                        if (character != '\0' && IsValidProjectName(_renameText.Insert(_cursorPosition, character.ToString())))
                        {
                            _renameText = _renameText.Insert(_cursorPosition, character.ToString());
                            _cursorPosition++;
                            System.Diagnostics.Debug.WriteLine($"GameManager: Character added, rename text: '{_renameText}', cursor: {_cursorPosition}");
                        }
                    }
                }
            }
        }

        private Rectangle GetCreateProjectButtonBounds()
        {
            string buttonText = "Create New Project";
            Vector2 textSize = _pixelFont.MeasureString(buttonText) * FONT_SCALE; // Use pixel font for measurement
            int buttonWidth = (int)textSize.X + 40; // Increased padding from 32 to 40 for better frame
            int buttonHeight = 44; // Match section button height
            int x = _rightContentBounds.X + _contentPadding;
            int y = _rightContentBounds.Y + _contentPadding;

            return new Rectangle(x, y, buttonWidth, buttonHeight);
        }

        private Rectangle GetBackButtonBounds()
        {
            return new Rectangle(
                _rightContentBounds.X + _contentPadding,
                _rightContentBounds.Y + _contentPadding,
                100,
                30
            );
        }

        private void UpdateAnimations()
        {
            // Update next button animation
            bool canProceed = !string.IsNullOrWhiteSpace(_projectName) && _projectName.Length <= 10 && IsValidProjectName(_projectName);
            if (canProceed)
            {
                _nextButtonAnimationTime = Math.Min(_nextButtonAnimationTime + 0.016f, ANIMATION_DURATION); // 60fps
            }
            else
            {
                _nextButtonAnimationTime = Math.Max(_nextButtonAnimationTime - 0.016f, 0f);
            }
        }

        private void UpdateScrolling()
        {
            if (!_scrollingEnabled) return;

            // Disable scrolling when in wizard mode (creating project)
            if (_isCreatingProject)
            {
                _needsScrollbar = false;
                _scrollY = 0;
                return;
            }

            _contentHeight = CalculateContentHeight();
            // Use scrollable content bounds height for scrollbar calculation
            _needsScrollbar = _contentHeight > _scrollableContentBounds.Height;

            if (_needsScrollbar)
            {
                _scrollY = MathHelper.Clamp(_scrollY, 0, Math.Max(0, _contentHeight - _scrollableContentBounds.Height));
                UpdateScrollbarBounds();
            }
            else
            {
                _scrollY = 0;
            }
        }

        private int CalculateContentHeight()
        {
            if (_currentSection == "Projects")
            {
                if (_isCreatingProject)
                {
                    return _wizardContentBounds.Height;
                }
                else if (_projects.Count == 0)
                {
                    return _scrollableContentBounds.Height; // Center the "no projects" message
                }
                else
                {
                    // Calculate total height: header (40) + all project items + padding
                    int headerHeight = 40; // Header height
                    int projectsHeight = (_projects.Count * _projectItemHeight) + ((_projects.Count - 1) * _projectItemPadding);
                    return headerHeight + projectsHeight + (_contentPadding * 2);
                }
            }
            else
            {
                return _scrollableContentBounds.Height; // For Learn and Community sections
            }
        }

        private void HandleWizardInput()
        {
            var mouseState = Mouse.GetState();
            var mousePosition = mouseState.Position;

            // Update hover states for genre buttons
            if (_currentWizardStep == 1)
            {
                _hoveredGenreIndex = -1;
                if (!IsMouseOverTopBarDropdown(mousePosition) && IsTopmostWindowUnderMouse(_windowManagement, mousePosition))
                {
                    for (int i = 0; i < _genreOptions.Length; i++)
                    {
                        var genreButtonBounds = GetGenreButtonBounds(i);
                        if (genreButtonBounds.Contains(mousePosition))
                        {
                            _hoveredGenreIndex = i;
                            break;
                        }
                    }
                }
            }

            if (mouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released)
            {
                // Only handle clicks if window is topmost
                if (!IsTopmostWindowUnderMouse(_windowManagement, mousePosition))
                    return;

                System.Diagnostics.Debug.WriteLine($"GameManager: Wizard click at {mousePosition}, step {_currentWizardStep}");
                
                if (_currentWizardStep == 0)
                {
                    HandleNameStepInput(mousePosition);
                }
                else if (_currentWizardStep == 1)
                {
                    HandleGenreStepInput(mousePosition);
                }
            }

            // Handle keyboard input for project name
            if (_currentWizardStep == 0)
            {
                HandleProjectNameInput();
            }
        }

        private void HandleNameStepInput(Point mousePosition)
        {
            // Handle Cancel button
            var cancelButtonBounds = GetWizardCancelButtonBounds();
            System.Diagnostics.Debug.WriteLine($"GameManager: Cancel button bounds: {cancelButtonBounds}");
            if (cancelButtonBounds.Contains(mousePosition))
            {
                CancelProjectWizard();
                return;
            }

            // Handle Next button
            var nextButtonBounds = GetWizardNextButtonBounds();
            System.Diagnostics.Debug.WriteLine($"GameManager: Next button bounds: {nextButtonBounds}");
            bool canProceed = !string.IsNullOrWhiteSpace(_projectName) && _projectName.Length <= 10 && IsValidProjectName(_projectName);
            if (nextButtonBounds.Contains(mousePosition) && canProceed)
            {
                _currentWizardStep = 1;
                System.Diagnostics.Debug.WriteLine($"GameManager: Moved to genre selection step. Project name: {_projectName}");
                return;
            }

            // Handle input field click
            Rectangle inputBounds = GetInputFieldBounds();
            System.Diagnostics.Debug.WriteLine($"GameManager: Input field bounds: {inputBounds}");
            if (inputBounds.Contains(mousePosition))
            {
                _isInputFocused = true;
                System.Diagnostics.Debug.WriteLine("GameManager: Input field focused");
            }
            else
            {
                _isInputFocused = false;
            }
        }

        private void HandleGenreStepInput(Point mousePosition)
        {
            // Handle Back button
            var backButtonBounds = GetWizardBackButtonBounds();
            System.Diagnostics.Debug.WriteLine($"GameManager: Back button bounds: {backButtonBounds}");
            if (backButtonBounds.Contains(mousePosition))
            {
                _currentWizardStep = 0;
                return;
            }

            // Handle Create Project button
            var nextButtonBounds = GetWizardNextButtonBounds();
            var createButtonBounds = new Rectangle(
                nextButtonBounds.X - 70,
                nextButtonBounds.Y,
                nextButtonBounds.Width + 70,
                nextButtonBounds.Height
            );
            System.Diagnostics.Debug.WriteLine($"GameManager: Create Project button bounds: {createButtonBounds}");
            if (createButtonBounds.Contains(mousePosition) && !string.IsNullOrWhiteSpace(_selectedGenre))
            {
                CreateProject();
                return;
            }

            // Handle genre selection
            for (int i = 0; i < _genreOptions.Length; i++)
            {
                var genreButtonBounds = GetGenreButtonBounds(i);
                System.Diagnostics.Debug.WriteLine($"GameManager: Genre button {i} bounds: {genreButtonBounds}");
                if (genreButtonBounds.Contains(mousePosition))
                {
                    _selectedGenre = _genreOptions[i];
                    System.Diagnostics.Debug.WriteLine($"GameManager: Selected genre: {_selectedGenre}");
                    break;
                }
            }
        }

        private void HandleProjectNameInput()
        {
            if (!_isInputFocused) return;

            var keyboardState = Keyboard.GetState();
            var pressedKeys = keyboardState.GetPressedKeys();

            foreach (var key in pressedKeys)
            {
                if (_previousKeyboardState.IsKeyUp(key))
                {
                    if (key == Keys.Back && _projectName.Length > 0)
                    {
                        _projectName = _projectName.Substring(0, _projectName.Length - 1);
                        System.Diagnostics.Debug.WriteLine($"Project name: {_projectName}");
                    }
                    else if (IsPrintableKey(key) && _projectName.Length < 20)
                    {
                        char character = GetCharacterFromKey(key);
                        if (character != '\0' && IsValidProjectName(_projectName + character))
                        {
                            _projectName += character;
                            System.Diagnostics.Debug.WriteLine($"Project name: {_projectName}");
                        }
                    }
                }
            }
        }

        private Rectangle GetInputFieldBounds()
        {
            // This should match the input field bounds in DrawNameStep
            int descY = _wizardContentBounds.Y + 100; // Approximate position
            return new Rectangle(
                _wizardContentBounds.X + 50,
                descY + 40,
                _wizardContentBounds.Width - 100,
                40 // Match the height in DrawNameStep
            );
        }

        private void HandleScrollbarInteraction()
        {
            if (!_needsScrollbar) return;

            var mousePosition = _currentMouseState.Position;

            // Only handle scrollbar interaction if window is topmost
            bool isTopmost = IsTopmostWindowUnderMouse(_windowManagement, mousePosition);

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
                if (_currentMouseState.LeftButton == ButtonState.Pressed && isTopmost)
                {
                    float deltaY = mousePosition.Y - _scrollbarDragStart.Y;
                    float scrollbarHeight = _scrollbarBounds.Height;
                    float contentHeight = (float)_contentHeight;
                    float visibleHeight = _scrollableContentBounds.Height;

                    float scrollRatio = deltaY / (scrollbarHeight - (scrollbarHeight * visibleHeight / contentHeight));
                    _scrollY = (int)(scrollRatio * (contentHeight - visibleHeight));
                    _scrollY = MathHelper.Clamp(_scrollY, 0, Math.Max(0, (int)(contentHeight - visibleHeight)));
                }
                else
                {
                    _isDraggingScrollbar = false;
                }
            }

            // Handle mouse wheel scrolling - only if mouse is over scrollable area and window is topmost
            if (isTopmost && _scrollableContentBounds.Contains(_currentMouseState.Position) && 
                _currentMouseState.ScrollWheelValue != _previousMouseState.ScrollWheelValue)
            {
                int delta = _currentMouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
                _scrollY -= delta / 3; // Adjust scroll speed
                _scrollY = MathHelper.Clamp(_scrollY, 0, Math.Max(0, _contentHeight - _scrollableContentBounds.Height));
            }
        }

        private void OnSaveProject(Dictionary<string, object> values)
        {
            try
            {
                var project = new GameProject
                {
                    Name = values.ContainsKey("projectName") ? values["projectName"].ToString() : "",
                    Description = values.ContainsKey("description") ? values["description"].ToString() : "",
                    Version = values.ContainsKey("version") ? values["version"].ToString() : "1.0.0",
                    EngineVersion = values.ContainsKey("engineVersion") ? values["engineVersion"].ToString() : "1.0.0",
                    Path = values.ContainsKey("projectPath") ? values["projectPath"].ToString() : "",
                    IsFavorite = values.ContainsKey("isFavorite") && (bool)values["isFavorite"],
                    CreatedDate = DateTime.Now,
                    LastModified = DateTime.Now
                };

                // Parse tags
                if (values.ContainsKey("tags") && !string.IsNullOrEmpty(values["tags"].ToString()))
                {
                    var tagStrings = values["tags"].ToString().Split(',', StringSplitOptions.RemoveEmptyEntries);
                    project.Tags = new string[tagStrings.Length];
                    for (int i = 0; i < tagStrings.Length; i++)
                    {
                        project.Tags[i] = tagStrings[i].Trim();
                    }
                }

                // Validate required fields
                if (string.IsNullOrEmpty(project.Name))
                {
                    System.Diagnostics.Debug.WriteLine("Project name is required");
                    return;
                }

                // Set default path if not provided
                if (string.IsNullOrEmpty(project.Path))
                {
                    project.Path = Path.Combine(Directory.GetCurrentDirectory(), "Projects", project.Name);
                }

                _projects.Add(project);
                SaveProjects();
                _isCreatingProject = false;
                _selectedProjectIndex = _projects.Count - 1;
                _selectedProject = project;

                // Wizard state is reset in CancelProjectWizard

                System.Diagnostics.Debug.WriteLine($"Created project: {project.Name}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving project: {ex.Message}");
            }
        }

        private void CancelProjectWizard()
        {
            _isCreatingProject = false;
            _currentWizardStep = 0;
            _projectName = "";
            _projectDescription = "";
            _selectedGenre = "";
            _hoveredGenreIndex = -1;
            System.Diagnostics.Debug.WriteLine("GameManager: Cancelled project creation wizard");
        }

        private Rectangle GetWizardCancelButtonBounds()
        {
            return new Rectangle(
                _wizardContentBounds.X + 10,
                _wizardContentBounds.Bottom - 50,
                100,
                40
            );
        }

        private Rectangle GetWizardBackButtonBounds()
        {
            return new Rectangle(
                _wizardContentBounds.X + 10,
                _wizardContentBounds.Bottom - 50,
                100,
                40
            );
        }

        private Rectangle GetWizardNextButtonBounds()
        {
            return new Rectangle(
                _wizardContentBounds.Right - 110,
                _wizardContentBounds.Bottom - 50,
                100,
                40
            );
        }

        private Rectangle GetGenreButtonBounds(int index)
        {
            int buttonWidth = (_wizardContentBounds.Width - 40) / 2; // 2 buttons per row with more spacing
            int buttonHeight = 50; // Taller buttons for better click detection
            int x = _wizardContentBounds.X + 15 + (index % 2) * (buttonWidth + 10);
            int y = _wizardContentBounds.Y + 150 + (index / 2) * (buttonHeight + 15);
            return new Rectangle(x, y, buttonWidth, buttonHeight);
        }

        private bool IsValidProjectName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            
            // Check for valid characters only (letters, numbers, spaces, hyphens, underscores)
            foreach (char c in name)
            {
                if (!char.IsLetterOrDigit(c) && c != ' ' && c != '-' && c != '_')
                {
                    return false;
                }
            }
            return true;
        }

        private bool IsPrintableKey(Keys key)
        {
            return (key >= Keys.A && key <= Keys.Z) ||
                   (key >= Keys.D0 && key <= Keys.D9) ||
                   key == Keys.Space || key == Keys.OemMinus || key == Keys.OemPlus ||
                   key == Keys.OemOpenBrackets || key == Keys.OemCloseBrackets ||
                   key == Keys.OemSemicolon || key == Keys.OemQuotes || key == Keys.OemComma ||
                   key == Keys.OemPeriod || key == Keys.OemQuestion || key == Keys.OemTilde;
        }

        private char GetCharacterFromKey(Keys key)
        {
            var keyboardState = Keyboard.GetState();
            bool isShiftPressed = keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift);

            switch (key)
            {
                case Keys.A: return isShiftPressed ? 'A' : 'a';
                case Keys.B: return isShiftPressed ? 'B' : 'b';
                case Keys.C: return isShiftPressed ? 'C' : 'c';
                case Keys.D: return isShiftPressed ? 'D' : 'd';
                case Keys.E: return isShiftPressed ? 'E' : 'e';
                case Keys.F: return isShiftPressed ? 'F' : 'f';
                case Keys.G: return isShiftPressed ? 'G' : 'g';
                case Keys.H: return isShiftPressed ? 'H' : 'h';
                case Keys.I: return isShiftPressed ? 'I' : 'i';
                case Keys.J: return isShiftPressed ? 'J' : 'j';
                case Keys.K: return isShiftPressed ? 'K' : 'k';
                case Keys.L: return isShiftPressed ? 'L' : 'l';
                case Keys.M: return isShiftPressed ? 'M' : 'm';
                case Keys.N: return isShiftPressed ? 'N' : 'n';
                case Keys.O: return isShiftPressed ? 'O' : 'o';
                case Keys.P: return isShiftPressed ? 'P' : 'p';
                case Keys.Q: return isShiftPressed ? 'Q' : 'q';
                case Keys.R: return isShiftPressed ? 'R' : 'r';
                case Keys.S: return isShiftPressed ? 'S' : 's';
                case Keys.T: return isShiftPressed ? 'T' : 't';
                case Keys.U: return isShiftPressed ? 'U' : 'u';
                case Keys.V: return isShiftPressed ? 'V' : 'v';
                case Keys.W: return isShiftPressed ? 'W' : 'w';
                case Keys.X: return isShiftPressed ? 'X' : 'x';
                case Keys.Y: return isShiftPressed ? 'Y' : 'y';
                case Keys.Z: return isShiftPressed ? 'Z' : 'z';
                case Keys.D0: return isShiftPressed ? ')' : '0';
                case Keys.D1: return isShiftPressed ? '!' : '1';
                case Keys.D2: return isShiftPressed ? '@' : '2';
                case Keys.D3: return isShiftPressed ? '#' : '3';
                case Keys.D4: return isShiftPressed ? '$' : '4';
                case Keys.D5: return isShiftPressed ? '%' : '5';
                case Keys.D6: return isShiftPressed ? '^' : '6';
                case Keys.D7: return isShiftPressed ? '&' : '7';
                case Keys.D8: return isShiftPressed ? '*' : '8';
                case Keys.D9: return isShiftPressed ? '(' : '9';
                case Keys.Space: return ' ';
                case Keys.OemMinus: return isShiftPressed ? '_' : '-';
                case Keys.OemPlus: return isShiftPressed ? '+' : '=';
                case Keys.OemOpenBrackets: return isShiftPressed ? '{' : '[';
                case Keys.OemCloseBrackets: return isShiftPressed ? '}' : ']';
                case Keys.OemSemicolon: return isShiftPressed ? ':' : ';';
                case Keys.OemQuotes: return isShiftPressed ? '"' : '\'';
                case Keys.OemComma: return isShiftPressed ? '<' : ',';
                case Keys.OemPeriod: return isShiftPressed ? '>' : '.';
                case Keys.OemQuestion: return isShiftPressed ? '?' : '/';
                case Keys.OemTilde: return isShiftPressed ? '~' : '`';
                default: return '\0';
            }
        }

        private List<string> WrapText(string text, int maxWidth)
        {
            List<string> lines = new List<string>();
            string[] words = text.Split(' ');
            string currentLine = "";

            foreach (string word in words)
            {
                string testLine = currentLine + (currentLine.Length > 0 ? " " : "") + word;
                Vector2 testSize = _uiFont.MeasureString(testLine) * FONT_SCALE;
                
                if (testSize.X <= maxWidth)
                {
                    currentLine = testLine;
                }
                else
                {
                    if (currentLine.Length > 0)
                    {
                        lines.Add(currentLine);
                        currentLine = word;
                    }
                    else
                    {
                        lines.Add(word); // Single word is too long, add it anyway
                    }
                }
            }
            
            if (currentLine.Length > 0)
            {
                lines.Add(currentLine);
            }
            
            return lines;
        }

        private void CreateProject()
        {
            try
            {
                var project = new GameProject
                {
                    Name = _projectName,
                    Description = _projectDescription,
                    Genre = _selectedGenre,
                    CreatedDate = DateTime.Now,
                    LastModified = DateTime.Now
                };

                // Set default path if not provided
                if (string.IsNullOrEmpty(project.Path))
                {
                    project.Path = Path.Combine(Directory.GetCurrentDirectory(), "Projects", project.Name);
                }

                _projects.Add(project);
                SaveProjects();
                CancelProjectWizard();
                _selectedProjectIndex = _projects.Count - 1;
                _selectedProject = project;

                System.Diagnostics.Debug.WriteLine($"Created project: {project.Name} with genre: {project.Genre}, path: {project.Path}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating project: {ex.Message}");
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            try
            {
                if (!_windowManagement.IsVisible())
                {
                    System.Diagnostics.Debug.WriteLine("GameManager: Window not visible, skipping draw");
                    return;
                }

                // Don't draw content if window is minimized
                if (_windowManagement.IsMinimized())
                {
                    System.Diagnostics.Debug.WriteLine("GameManager: Window is minimized, skipping content draw");
                    _windowManagement.Draw(spriteBatch, "Game Manager");
                    return;
                }

                System.Diagnostics.Debug.WriteLine("GameManager: Drawing window");
                _windowManagement.Draw(spriteBatch, "Game Manager");
                DrawContent(spriteBatch);
                _windowManagement.DrawOverlay(spriteBatch);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GameManager Draw Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"GameManager Draw Stack Trace: {ex.StackTrace}");
            }
        }

        private void DrawContent(SpriteBatch spriteBatch)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"GameManager: Drawing content - Sidebar: {_leftSidebarBounds}, Content: {_rightContentBounds}");
                
                // Set up scissor rectangle for the entire window content area to prevent clipping through borders
                Rectangle originalScissor = spriteBatch.GraphicsDevice.ScissorRectangle;
                var windowBounds = _windowManagement.GetWindowBounds();
                Rectangle windowScissorRect = new Rectangle(
                    windowBounds.X + 2, // Account for window border
                    windowBounds.Y + 42, // Account for title bar
                    windowBounds.Width - 4, // Account for borders (2px on each side)
                    windowBounds.Height - 44 // Account for title bar and borders
                );
                
                // Enable scissor test for entire window content
                spriteBatch.GraphicsDevice.ScissorRectangle = windowScissorRect;
                spriteBatch.GraphicsDevice.RasterizerState = new RasterizerState
                {
                    ScissorTestEnable = true
                };
                
                // Draw sidebar
                DrawSidebar(spriteBatch);

                // Draw main content
                DrawMainContent(spriteBatch);

                // Restore scissor test before drawing UI elements that should be able to extend beyond (scrollbar, context menu)
                spriteBatch.GraphicsDevice.ScissorRectangle = originalScissor;
                spriteBatch.GraphicsDevice.RasterizerState = new RasterizerState
                {
                    ScissorTestEnable = false
                };

                // Draw scrollbar if needed (can extend slightly beyond but should still respect window bounds)
                if (_needsScrollbar)
                {
                    DrawScrollbar(spriteBatch);
                }

                // Draw context menu if visible (can extend beyond content area but should respect window bounds)
                if (_isContextMenuVisible)
                {
                    DrawContextMenu(spriteBatch);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GameManager DrawContent Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"GameManager DrawContent Stack Trace: {ex.StackTrace}");
            }
        }

        private void DrawSidebar(SpriteBatch spriteBatch)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"GameManager: Drawing sidebar with {_sectionNames.Count} sections, {_sectionButtonBounds.Count} button bounds");
                
                // Draw sidebar background with subtle gradient effect
                spriteBatch.Draw(_pixel, _leftSidebarBounds, SIDEBAR_BACKGROUND);

                // Draw sidebar border
                DrawBorder(spriteBatch, _leftSidebarBounds, BORDER_COLOR);

                // Draw section buttons with modern styling
                for (int i = 0; i < _sectionNames.Count; i++)
                {
                    if (i >= _sectionButtonBounds.Count)
                    {
                        System.Diagnostics.Debug.WriteLine($"GameManager: Warning - Section {i} has no bounds, skipping");
                        continue;
                    }
                    
                    var buttonBounds = _sectionButtonBounds[i];
                    bool isActive = _sectionNames[i] == _currentSection;
                    bool isHovered = !IsMouseOverTopBarDropdown(_currentMouseState.Position) && 
                                     IsTopmostWindowUnderMouse(_windowManagement, _currentMouseState.Position) &&
                                     buttonBounds.Contains(_currentMouseState.Position);

                    // Draw modern button
                    if (isActive || isHovered)
                    {
                        Color buttonColor = isActive ? SECTION_ACTIVE_COLOR : SECTION_HOVER_COLOR;
                        DrawRoundedRectangle(spriteBatch, buttonBounds, buttonColor, _borderRadius);
                    }

                    // Draw button text with pixel font for sidebar
                    Vector2 textSize = _pixelFont.MeasureString(_sectionNames[i]) * FONT_SCALE;
                    Vector2 textPosition = new Vector2(
                        buttonBounds.X + (buttonBounds.Width - textSize.X) / 2,
                        buttonBounds.Y + (buttonBounds.Height - textSize.Y) / 2
                    );

                    // Use appropriate text color based on state
                    Color textColor = isActive ? Color.White : TEXT_PRIMARY;

                    // Debug: Log text drawing
                    if (i == 0) // Only log for first button to avoid spam
                    {
                        Console.WriteLine($"[GameManager] Drawing sidebar text '{_sectionNames[i]}' with pixel font at {textPosition}");
                        Console.WriteLine($"[GameManager] Pixel font loaded: {_pixelFont != null}");
                    }

                    spriteBatch.DrawString(_pixelFont, _sectionNames[i], textPosition, textColor, 0f, Vector2.Zero, FONT_SCALE, SpriteEffects.None, 0f);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GameManager DrawSidebar Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"GameManager DrawSidebar Stack Trace: {ex.StackTrace}");
            }
        }

        private void DrawMainContent(SpriteBatch spriteBatch)
        {
            // Draw content background with subtle gradient effect
            spriteBatch.Draw(_pixel, _rightContentBounds, CONTENT_BACKGROUND);

            // Draw subtle inner border for depth
            DrawBorder(spriteBatch, _rightContentBounds, BORDER_COLOR);

            if (_currentSection == "Projects")
            {
                // Debug: Log the state of _isCreatingProject
                System.Diagnostics.Debug.WriteLine($"[GameManager] DrawMainContent - _isCreatingProject: {_isCreatingProject}, _currentSection: {_currentSection}");
                
                if (_isCreatingProject)
                {
                    DrawProjectWizard(spriteBatch);
                }
                else
                {
                    // Always draw create project button
                    DrawCreateProjectButton(spriteBatch);
                    
                    if (_projects.Count == 0)
                    {
                        DrawNoProjectsMessage(spriteBatch);
                    }
                    else
                    {
                        DrawProjectsList(spriteBatch);
                    }
                }
            }
            else if (_currentSection == "Learn" || _currentSection == "Community")
            {
                DrawNotImplementedMessage(spriteBatch);
            }
        }

        private void DrawProjectWizard(SpriteBatch spriteBatch)
        {
            if (_currentWizardStep == 0)
            {
                DrawNameStep(spriteBatch);
            }
            else if (_currentWizardStep == 1)
            {
                DrawGenreStep(spriteBatch);
            }
        }

        private void DrawNameStep(SpriteBatch spriteBatch)
        {
            // Cancel button is now at the bottom, drawn after the Next button

            // Draw title with pixel font
            string title = "Create New Project";
            Vector2 titleSize = _pixelFont.MeasureString(title) * HEADER_FONT_SCALE;
            Vector2 titlePosition = new Vector2(
                _wizardContentBounds.X + (_wizardContentBounds.Width - titleSize.X) / 2,
                _wizardContentBounds.Y + 60
            );
            
            // Debug: Log which font is being used for title
            Console.WriteLine($"[GameManager] Drawing title '{title}' with pixel font");
            Console.WriteLine($"[GameManager] Pixel font loaded: {_pixelFont != null}");
            
            spriteBatch.DrawString(_pixelFont, title, titlePosition, TEXT_PRIMARY, 0f, Vector2.Zero, HEADER_FONT_SCALE, SpriteEffects.None, 0f);

            // Draw description
            string description = "Enter a name for your new project";
            Vector2 descSize = _pixelFont.MeasureString(description) * FONT_SCALE;
            Vector2 descPosition = new Vector2(
                _wizardContentBounds.X + (_wizardContentBounds.Width - descSize.X) / 2,
                titlePosition.Y + titleSize.Y + 20
            );
            spriteBatch.DrawString(_pixelFont, description, descPosition, TEXT_SECONDARY, 0f, Vector2.Zero, FONT_SCALE, SpriteEffects.None, 0f);

            // Draw modern text input field
            Rectangle inputBounds = new Rectangle(
                _wizardContentBounds.X + 50,
                (int)descPosition.Y + 40,
                _wizardContentBounds.Width - 100,
                40
            );
            
            // Draw input field with modern styling
            Color inputBgColor = _isInputFocused ? CARD_BACKGROUND : new Color(55, 65, 81);
            Color inputBorderColor = _isInputFocused ? BUTTON_PRIMARY : BORDER_COLOR;
            
            DrawRoundedRectangle(spriteBatch, inputBounds, inputBgColor, _borderRadius);
            DrawBorder(spriteBatch, inputBounds, inputBorderColor);
            
            // Draw project name text
            if (!string.IsNullOrEmpty(_projectName))
            {
                spriteBatch.DrawString(_pixelFont, _projectName, new Vector2(inputBounds.X + 12, inputBounds.Y + 10), TEXT_PRIMARY, 0f, Vector2.Zero, FONT_SCALE, SpriteEffects.None, 0f);
            }
            else
            {
                // Draw placeholder text
                spriteBatch.DrawString(_pixelFont, "Enter the project name", new Vector2(inputBounds.X + 12, inputBounds.Y + 10), TEXT_TERTIARY, 0f, Vector2.Zero, FONT_SCALE, SpriteEffects.None, 0f);
            }

            // Draw Next button with modern styling and pixel font
            var nextButtonBounds = GetWizardNextButtonBounds();
            bool isNextHovered = !IsMouseOverTopBarDropdown(_currentMouseState.Position) && 
                                 IsTopmostWindowUnderMouse(_windowManagement, _currentMouseState.Position) &&
                                 nextButtonBounds.Contains(_currentMouseState.Position);
            bool canProceed = !string.IsNullOrWhiteSpace(_projectName) && _projectName.Length <= 20 && IsValidProjectName(_projectName);
            
            // Calculate animated color
            float animationProgress = _nextButtonAnimationTime / ANIMATION_DURATION;
            Color animatedButtonColor = Color.Lerp(new Color(40, 40, 45), BUTTON_PRIMARY, animationProgress);
            Color animatedTextColor = Color.Lerp(TEXT_TERTIARY, Color.White, animationProgress);
            
            DrawAnimatedButton(spriteBatch, nextButtonBounds, "Next", isNextHovered && canProceed, false, false, true, animatedButtonColor, animatedTextColor);
            
            // Draw Cancel button at the bottom
            var cancelButtonBounds = GetWizardCancelButtonBounds();
            bool isCancelHovered = !IsMouseOverTopBarDropdown(_currentMouseState.Position) && 
                                   IsTopmostWindowUnderMouse(_windowManagement, _currentMouseState.Position) &&
                                   cancelButtonBounds.Contains(_currentMouseState.Position);
            DrawModernButton(spriteBatch, cancelButtonBounds, "Cancel", isCancelHovered, false, true, true);
        }

        private void DrawGenreStep(SpriteBatch spriteBatch)
        {
            // Draw Back button with modern styling and pixel font
            var backButtonBounds = GetWizardBackButtonBounds();
            bool isBackHovered = !IsMouseOverTopBarDropdown(_currentMouseState.Position) && 
                                 IsTopmostWindowUnderMouse(_windowManagement, _currentMouseState.Position) &&
                                 backButtonBounds.Contains(_currentMouseState.Position);
            DrawModernButton(spriteBatch, backButtonBounds, "Back", isBackHovered, false, true, true);

            // Draw title with pixel font
            string title = "Choose Project Genre";
            Vector2 titleSize = _pixelFont.MeasureString(title) * HEADER_FONT_SCALE;
            Vector2 titlePosition = new Vector2(
                _wizardContentBounds.X + (_wizardContentBounds.Width - titleSize.X) / 2,
                _wizardContentBounds.Y + 60
            );
            spriteBatch.DrawString(_pixelFont, title, titlePosition, TEXT_PRIMARY, 0f, Vector2.Zero, HEADER_FONT_SCALE, SpriteEffects.None, 0f);

            // Draw description
            string description = "Select the genre that best fits your project";
            Vector2 descSize = _pixelFont.MeasureString(description) * FONT_SCALE;
            Vector2 descPosition = new Vector2(
                _wizardContentBounds.X + (_wizardContentBounds.Width - descSize.X) / 2,
                titlePosition.Y + titleSize.Y + 20
            );
            spriteBatch.DrawString(_pixelFont, description, descPosition, TEXT_SECONDARY, 0f, Vector2.Zero, FONT_SCALE, SpriteEffects.None, 0f);

            // Draw genre buttons with modern styling
            for (int i = 0; i < _genreOptions.Length; i++)
            {
                var buttonBounds = GetGenreButtonBounds(i);
                bool isHovered = _hoveredGenreIndex == i;
                bool isSelected = _selectedGenre == _genreOptions[i];
                
                // Use modern button styling with pixel font
                if (isSelected)
                {
                    DrawModernButton(spriteBatch, buttonBounds, _genreOptions[i], isHovered, true, false, true);
                }
                else
                {
                    DrawModernButton(spriteBatch, buttonBounds, _genreOptions[i], isHovered, false, true, true);
                }
            }

            // Draw selected genre description with modern styling
            if (!string.IsNullOrEmpty(_selectedGenre))
            {
                int selectedIndex = Array.IndexOf(_genreOptions, _selectedGenre);
                if (selectedIndex >= 0 && selectedIndex < _genreDescriptions.Length)
                {
                    string selectedDesc = _genreDescriptions[selectedIndex];
                    
                    // Wrap text to fit within bounds with more padding
                    int maxWidth = _wizardContentBounds.Width - 80; // More padding for better spacing
                    List<string> wrappedLines = WrapText(selectedDesc, maxWidth);
                    
                    // Ensure we have at least 4 lines of text by splitting if needed
                    if (wrappedLines.Count < 4 && wrappedLines.Count > 0)
                    {
                        // Simple approach: split the longest line
                        int longestLineIndex = 0;
                        for (int i = 1; i < wrappedLines.Count; i++)
                        {
                            if (wrappedLines[i].Length > wrappedLines[longestLineIndex].Length)
                                longestLineIndex = i;
                        }
                        
                        if (wrappedLines[longestLineIndex].Length > 20) // Only split if line is long enough
                        {
                            string longestLine = wrappedLines[longestLineIndex];
                            int splitPoint = longestLine.Length / 2;
                            while (splitPoint < longestLine.Length && longestLine[splitPoint] != ' ')
                                splitPoint++;
                            
                            if (splitPoint < longestLine.Length)
                            {
                                wrappedLines[longestLineIndex] = longestLine.Substring(0, splitPoint).Trim();
                                wrappedLines.Insert(longestLineIndex + 1, longestLine.Substring(splitPoint).Trim());
                            }
                        }
                    }
                    
                    int lineHeight = (int)(_uiFont.MeasureString("A").Y * FONT_SCALE) + 8; // Increased line spacing
                    int startY = _wizardContentBounds.Bottom - 180; // Moved up more to accommodate 4 lines
                    
                    // Draw description background with more padding
                    var descBounds = new Rectangle(
                        _wizardContentBounds.X + 30, // More left padding
                        startY - 15, // More top padding
                        _wizardContentBounds.Width - 60, // More horizontal padding
                        Math.Min(4 * lineHeight + 30, 120) // Fixed height for 4 lines with padding
                    );
                    DrawCard(spriteBatch, descBounds, new Color(55, 65, 81), true);
                    
                    // Draw up to 4 lines with better spacing
                    int maxLines = Math.Min(4, wrappedLines.Count);
                    for (int i = 0; i < maxLines; i++)
                    {
                        Vector2 lineSize = _uiFont.MeasureString(wrappedLines[i]) * FONT_SCALE;
                        Vector2 linePosition = new Vector2(
                            _wizardContentBounds.X + (_wizardContentBounds.Width - lineSize.X) / 2,
                            startY + (i * lineHeight)
                        );
                        spriteBatch.DrawString(_uiFont, wrappedLines[i], linePosition, TEXT_SECONDARY, 0f, Vector2.Zero, FONT_SCALE, SpriteEffects.None, 0f);
                    }
                }
            }

            // Draw Create Project button with modern styling and pixel font
            var nextButtonBounds = GetWizardNextButtonBounds();
            bool isNextHovered = !IsMouseOverTopBarDropdown(_currentMouseState.Position) && 
                                 IsTopmostWindowUnderMouse(_windowManagement, _currentMouseState.Position) &&
                                 nextButtonBounds.Contains(_currentMouseState.Position);
            bool canProceed = !string.IsNullOrWhiteSpace(_selectedGenre);
            
            // Make the button wider for "Create Project" text but keep it within bounds
            var createButtonBounds = new Rectangle(
                nextButtonBounds.X - 70, // Extend left a bit more
                nextButtonBounds.Y,
                nextButtonBounds.Width + 70, // Make a bit wider
                nextButtonBounds.Height
            );
            
            DrawModernButton(spriteBatch, createButtonBounds, "Create Project", isNextHovered && canProceed, false, false, true);
        }

        private void DrawCreateProjectButton(SpriteBatch spriteBatch)
        {
            var createButtonBounds = GetCreateProjectButtonBounds();
            bool isHovered = !IsMouseOverTopBarDropdown(_currentMouseState.Position) && 
                             IsTopmostWindowUnderMouse(_windowManagement, _currentMouseState.Position) &&
                             createButtonBounds.Contains(_currentMouseState.Position);
            
            // Use modern button styling with pixel font
            DrawModernButton(spriteBatch, createButtonBounds, "Create New Project", isHovered, false, false, true);
        }

        private void DrawNoProjectsMessage(SpriteBatch spriteBatch)
        {
            string message = "No projects yet";
            string subMessage = "Create your first project to get started";
            
            Vector2 messageSize = _uiFont.MeasureString(message) * FONT_SCALE;
            Vector2 subMessageSize = _uiFont.MeasureString(subMessage) * FONT_SCALE;
            
            Vector2 messagePosition = new Vector2(
                _rightContentBounds.X + (_rightContentBounds.Width - messageSize.X) / 2,
                _rightContentBounds.Y + (_rightContentBounds.Height - messageSize.Y) / 2 - 30
            );
            
            Vector2 subMessagePosition = new Vector2(
                _rightContentBounds.X + (_rightContentBounds.Width - subMessageSize.X) / 2,
                messagePosition.Y + messageSize.Y + 10
            );

            spriteBatch.DrawString(_uiFont, message, messagePosition, TEXT_PRIMARY, 0f, Vector2.Zero, FONT_SCALE, SpriteEffects.None, 0f);
            spriteBatch.DrawString(_uiFont, subMessage, subMessagePosition, TEXT_SECONDARY, 0f, Vector2.Zero, FONT_SCALE, SpriteEffects.None, 0f);
        }

        private void DrawProjectsList(SpriteBatch spriteBatch)
        {
            // Set up scissor rectangle specifically for the scrollable content area
            // This will be intersected with any existing scissor rectangle (window bounds)
            Rectangle currentScissor = spriteBatch.GraphicsDevice.ScissorRectangle;
            Rectangle scrollableScissorRect = new Rectangle(
                _scrollableContentBounds.X,
                _scrollableContentBounds.Y,
                _scrollableContentBounds.Width - (_needsScrollbar ? SCROLLBAR_WIDTH + SCROLLBAR_PADDING : 0),
                _scrollableContentBounds.Height
            );
            
            // Intersect with current scissor (window bounds) to ensure proper clipping
            int scissorLeft = Math.Max(scrollableScissorRect.X, currentScissor.X);
            int scissorTop = Math.Max(scrollableScissorRect.Y, currentScissor.Y);
            int scissorRight = Math.Min(scrollableScissorRect.Right, currentScissor.Right);
            int scissorBottom = Math.Min(scrollableScissorRect.Bottom, currentScissor.Bottom);
            
            // Ensure we have valid dimensions
            int scissorWidth = Math.Max(0, scissorRight - scissorLeft);
            int scissorHeight = Math.Max(0, scissorBottom - scissorTop);
            
            Rectangle scissorRect = new Rectangle(
                scissorLeft,
                scissorTop,
                scissorWidth,
                scissorHeight
            );
            
            // Only set scissor if we have valid dimensions
            if (scissorWidth > 0 && scissorHeight > 0)
            {
                // Enable scissor test for scrollable area
                spriteBatch.GraphicsDevice.ScissorRectangle = scissorRect;
                spriteBatch.GraphicsDevice.RasterizerState = new RasterizerState
                {
                    ScissorTestEnable = true
                };
                
                // Header is always fixed at the top of scrollable area - only draw if visible
                if (_scrollableContentBounds.Y + 40 >= scissorRect.Y && _scrollableContentBounds.Y < scissorRect.Bottom)
                {
                    DrawProjectHeader(spriteBatch, _scrollableContentBounds.Y, scissorRect);
                }
            }
            else
            {
                // If scissor not set, use current scissor for header clipping
                DrawProjectHeader(spriteBatch, _scrollableContentBounds.Y, currentScissor);
            }

            // Items scroll, starting below the header
            int startY = _scrollableContentBounds.Y + 40 - _scrollY; // Start below header, account for scroll
            int itemY = startY;

            for (int i = 0; i < _projects.Count; i++)
            {
                var project = _projects[i];
                var itemBounds = new Rectangle(
                    _scrollableContentBounds.X + _contentPadding,
                    itemY,
                    _scrollableContentBounds.Width - (_contentPadding * 2) - (_needsScrollbar ? SCROLLBAR_WIDTH + SCROLLBAR_PADDING : 0),
                    _projectItemHeight
                );

                // Only draw if visible within scrollable bounds and not overlapping the header
                // Header is 40px tall, so items should start at Y + 40
                // Use the scissor rectangle bottom to ensure proper clipping to window bounds
                int visibleTop = _scrollableContentBounds.Y + 40; // Start below header
                // Use the actual scissor bottom if scissor was set, otherwise use current scissor (window bounds)
                int visibleBottom = (scissorWidth > 0 && scissorHeight > 0) ? scissorRect.Bottom : currentScissor.Bottom;
                
                // Only draw if item is at least partially visible and doesn't start below the visible area
                if (itemBounds.Top < visibleBottom && itemBounds.Bottom > visibleTop)
                {
                    // Clip item bounds to visible area to prevent drawing outside window
                    // This ensures that even if scissor doesn't work, we won't draw outside bounds
                    int clippedTop = Math.Max(itemBounds.Y, visibleTop);
                    int clippedBottom = Math.Min(itemBounds.Bottom, visibleBottom);
                    int clippedHeight = clippedBottom - clippedTop;
                    
                    // Only draw if there's something visible
                    if (clippedHeight > 0)
                    {
                        // Calculate the source rectangle for partial drawing
                        int sourceY = clippedTop - itemBounds.Y;
                        Rectangle clippedBounds = new Rectangle(
                            itemBounds.X,
                            clippedTop,
                            itemBounds.Width,
                            clippedHeight
                        );
                        
                        // Only allow hover if mouse is within scrollable bounds and window is topmost
                        bool isHovered = !IsMouseOverTopBarDropdown(_currentMouseState.Position) && 
                                         IsTopmostWindowUnderMouse(_windowManagement, _currentMouseState.Position) &&
                                         _scrollableContentBounds.Contains(_currentMouseState.Position) &&
                                         itemBounds.Contains(_currentMouseState.Position);
                        
                        // Ensure project has a path for comparison
                        string projectPath = !string.IsNullOrEmpty(project.Path) ? project.Path : Path.Combine(Directory.GetCurrentDirectory(), "Projects", project.Name);
                        string normalizedProjectPath = Path.GetFullPath(projectPath);
                        string normalizedActivePath = !string.IsNullOrEmpty(_activeWorkspacePath) ? Path.GetFullPath(_activeWorkspacePath) : "";
                        bool isActiveWorkspace = !string.IsNullOrEmpty(_activeWorkspacePath) && 
                                               normalizedProjectPath == normalizedActivePath;

                        // Draw modern card - active workspace has highest priority, hover only if not active
                        Color cardColor;
                        if (isActiveWorkspace)
                        {
                            cardColor = PROJECT_ACTIVE_WORKSPACE_COLOR;
                        }
                        else if (isHovered)
                        {
                            cardColor = PROJECT_HOVER_COLOR;
                        }
                        else
                        {
                            cardColor = CARD_BACKGROUND;
                        }
                        
                        // Draw card using clipped bounds - this ensures we only draw the visible portion
                        // The scissor rectangle will also clip, but this is a backup
                        DrawCard(spriteBatch, clippedBounds, cardColor, true);
                        
                        // Draw subtle hover border if hovering and not active (only if fully visible)
                        if (isHovered && !isActiveWorkspace && itemBounds.Top >= visibleTop && itemBounds.Bottom <= visibleBottom)
                        {
                            DrawBorder(spriteBatch, clippedBounds, new Color(147, 112, 219, 100)); // Subtle purple border
                        }

                        // Draw project info - pass clipped bounds to ensure text is also clipped
                        DrawProjectItem(spriteBatch, project, itemBounds, clippedBounds, isActiveWorkspace);
                    }
                }

                itemY += _projectItemHeight + _projectItemPadding;
            }

            // Restore scissor test to the window-level scissor (not completely disabled)
            spriteBatch.GraphicsDevice.ScissorRectangle = currentScissor;
            spriteBatch.GraphicsDevice.RasterizerState = new RasterizerState
            {
                ScissorTestEnable = true // Keep enabled to maintain window clipping
            };
        }

        private void DrawProjectHeader(SpriteBatch spriteBatch, int y, Rectangle clipBounds)
        {
            int x = _scrollableContentBounds.X + _contentPadding;
            int width = _scrollableContentBounds.Width - (_contentPadding * 2) - (_needsScrollbar ? SCROLLBAR_WIDTH + SCROLLBAR_PADDING : 0);

            // Draw header background with modern styling - clip to visible area
            var headerBounds = new Rectangle(x, y, width, 32);
            Rectangle clippedHeaderBounds = new Rectangle(
                headerBounds.X,
                Math.Max(headerBounds.Y, clipBounds.Y),
                headerBounds.Width,
                Math.Min(headerBounds.Bottom, clipBounds.Bottom) - Math.Max(headerBounds.Y, clipBounds.Y)
            );
            
            if (clippedHeaderBounds.Height > 0)
            {
                DrawCard(spriteBatch, clippedHeaderBounds, new Color(55, 65, 81), false); // Gray-700
            }

            // Draw header text with normal font - only if fully within visible bounds
            float textHeight = _uiFont.MeasureString("A").Y * FONT_SCALE;
            int textY = y + (headerBounds.Height - (int)textHeight) / 2; // Center vertically
            
            // Only draw if text is fully within clipped bounds
            if (textY >= clipBounds.Y && textY + textHeight <= clipBounds.Bottom)
            {
                string[] headers = { "Name", "Genre", "Created", "Modified" };
                int[] columnWidths = { width / 4, width / 4, width / 4, width / 4 };

                int currentX = x + _cardPadding;
                for (int i = 0; i < headers.Length; i++)
                {
                    Vector2 textSize = _uiFont.MeasureString(headers[i]) * FONT_SCALE;
                    // Only draw if text doesn't extend beyond right edge
                    if (currentX + textSize.X <= clipBounds.Right)
                    {
                        spriteBatch.DrawString(_uiFont, headers[i], new Vector2(currentX, textY), TEXT_SECONDARY, 0f, Vector2.Zero, FONT_SCALE, SpriteEffects.None, 0f);
                    }
                    currentX += columnWidths[i];
                }
            }
        }

        private void DrawProjectItem(SpriteBatch spriteBatch, GameProject project, Rectangle bounds, Rectangle clippedBounds, bool isActiveWorkspace)
        {
            int x = bounds.X + _cardPadding;
            int y = bounds.Y + _cardPadding;
            int width = bounds.Width - (_cardPadding * 2);
            int[] columnWidths = { width / 4, width / 4, width / 4, width / 4 };

            // Text colors - black if active workspace, otherwise use normal colors
            Color textPrimaryColor = isActiveWorkspace ? Color.Black : TEXT_PRIMARY;
            Color textSecondaryColor = isActiveWorkspace ? Color.Black : TEXT_SECONDARY;
            Color textTertiaryColor = isActiveWorkspace ? new Color(30, 30, 30) : TEXT_TERTIARY; // Dark gray instead of pure black for tertiary
            Color genreColor = isActiveWorkspace ? Color.Black : (string.IsNullOrEmpty(project.Genre) ? TEXT_TERTIARY : ACCENT_COLOR);

            // Get text height for visibility checking
            float textHeight = _uiFont.MeasureString("A").Y * FONT_SCALE;

            // Check if this project is being renamed
            bool isBeingRenamed = _isRenaming && _contextMenuTargetProject == project;

            if (isBeingRenamed)
            {
                System.Diagnostics.Debug.WriteLine($"GameManager: Drawing rename input field for project: {project.Name}");
                // Draw rename input field only in the name column - check if visible
                if (y + 30 >= clippedBounds.Y && y < clippedBounds.Bottom)
                {
                    DrawRenameInputField(spriteBatch, bounds, x, y, columnWidths[0]);
                }
                
                // Draw other columns normally - only if fully within visible bounds
                if (y >= clippedBounds.Y && y + textHeight <= clippedBounds.Bottom)
                {
                    x += columnWidths[0];
                    Vector2 textSize = _uiFont.MeasureString(project.Genre ?? "Unknown") * FONT_SCALE;
                    if (x + textSize.X <= clippedBounds.Right)
                    {
                        spriteBatch.DrawString(_uiFont, project.Genre ?? "Unknown", new Vector2(x, y), genreColor, 0f, Vector2.Zero, FONT_SCALE, SpriteEffects.None, 0f);
                    }

                    // Draw created date
                    x += columnWidths[1];
                    textSize = _uiFont.MeasureString(project.CreatedDate.ToString("MM/dd/yyyy")) * FONT_SCALE;
                    if (x + textSize.X <= clippedBounds.Right)
                    {
                        spriteBatch.DrawString(_uiFont, project.CreatedDate.ToString("MM/dd/yyyy"), new Vector2(x, y), textTertiaryColor, 0f, Vector2.Zero, FONT_SCALE, SpriteEffects.None, 0f);
                    }

                    // Draw modified date
                    x += columnWidths[2];
                    textSize = _uiFont.MeasureString(project.LastModified.ToString("MM/dd/yyyy")) * FONT_SCALE;
                    if (x + textSize.X <= clippedBounds.Right)
                    {
                        spriteBatch.DrawString(_uiFont, project.LastModified.ToString("MM/dd/yyyy"), new Vector2(x, y), textTertiaryColor, 0f, Vector2.Zero, FONT_SCALE, SpriteEffects.None, 0f);
                    }
                }
            }
            else
            {
                // Draw project name - only if fully within visible bounds
                if (y >= clippedBounds.Y && y + textHeight <= clippedBounds.Bottom)
                {
                    Vector2 textSize = _uiFont.MeasureString(project.Name) * FONT_SCALE;
                    if (x + textSize.X <= clippedBounds.Right)
                    {
                        spriteBatch.DrawString(_uiFont, project.Name, new Vector2(x, y), textPrimaryColor, 0f, Vector2.Zero, FONT_SCALE, SpriteEffects.None, 0f);
                    }

                    // Draw genre
                    x += columnWidths[0];
                    textSize = _uiFont.MeasureString(project.Genre ?? "Unknown") * FONT_SCALE;
                    if (x + textSize.X <= clippedBounds.Right)
                    {
                        spriteBatch.DrawString(_uiFont, project.Genre ?? "Unknown", new Vector2(x, y), genreColor, 0f, Vector2.Zero, FONT_SCALE, SpriteEffects.None, 0f);
                    }

                    // Draw created date
                    x += columnWidths[1];
                    textSize = _uiFont.MeasureString(project.CreatedDate.ToString("MM/dd/yyyy")) * FONT_SCALE;
                    if (x + textSize.X <= clippedBounds.Right)
                    {
                        spriteBatch.DrawString(_uiFont, project.CreatedDate.ToString("MM/dd/yyyy"), new Vector2(x, y), textTertiaryColor, 0f, Vector2.Zero, FONT_SCALE, SpriteEffects.None, 0f);
                    }

                    // Draw modified date
                    x += columnWidths[2];
                    textSize = _uiFont.MeasureString(project.LastModified.ToString("MM/dd/yyyy")) * FONT_SCALE;
                    if (x + textSize.X <= clippedBounds.Right)
                    {
                        spriteBatch.DrawString(_uiFont, project.LastModified.ToString("MM/dd/yyyy"), new Vector2(x, y), textTertiaryColor, 0f, Vector2.Zero, FONT_SCALE, SpriteEffects.None, 0f);
                    }
                }

                // Draw description on second line with better styling - only if fully visible
                if (!string.IsNullOrEmpty(project.Description))
                {
                    x = bounds.X + _cardPadding;
                    int descY = bounds.Y + _cardPadding + 24; // Better line spacing
                    
                    // Only draw if description line is fully within visible bounds
                    if (descY >= clippedBounds.Y && descY + textHeight <= clippedBounds.Bottom)
                    {
                        string description = project.Description.Length > 60 ? project.Description.Substring(0, 57) + "..." : project.Description;
                        Vector2 textSize = _uiFont.MeasureString(description) * FONT_SCALE;
                        if (x + textSize.X <= clippedBounds.Right)
                        {
                            spriteBatch.DrawString(_uiFont, description, new Vector2(x, descY), textSecondaryColor, 0f, Vector2.Zero, FONT_SCALE, SpriteEffects.None, 0f);
                        }
                    }
                }
            }
        }

        private void DrawRenameInputField(SpriteBatch spriteBatch, Rectangle projectBounds, int x, int y, int width)
        {
            // Calculate input field bounds for just the name column
            Rectangle inputBounds = new Rectangle(x, y, width, 30);
            
            System.Diagnostics.Debug.WriteLine($"GameManager: Drawing rename input field at bounds: {inputBounds}, text: '{_renameText}'");
            
            // Draw input field background
            Color inputBgColor = CARD_BACKGROUND;
            Color inputBorderColor = BUTTON_PRIMARY;
            
            DrawRoundedRectangle(spriteBatch, inputBounds, inputBgColor, _borderRadius);
            
            // Draw thinner border (1 pixel instead of the default border thickness)
            DrawThinBorder(spriteBatch, inputBounds, inputBorderColor);
            
            // Draw rename text - center vertically
            float textHeight = _uiFont.MeasureString("A").Y * FONT_SCALE;
            float textY = inputBounds.Y + (inputBounds.Height - textHeight) / 2;
            
            if (!string.IsNullOrEmpty(_renameText))
            {
                spriteBatch.DrawString(_uiFont, _renameText, new Vector2(inputBounds.X + 8, textY), TEXT_PRIMARY, 0f, Vector2.Zero, FONT_SCALE, SpriteEffects.None, 0f);
                
                // Draw blinking cursor at the correct position
                bool showCursor = (_cursorBlinkTime % (CURSOR_BLINK_RATE * 2)) < CURSOR_BLINK_RATE;
                if (showCursor)
                {
                    // Calculate cursor position based on text before cursor
                    string textBeforeCursor = _renameText.Substring(0, _cursorPosition);
                    Vector2 textBeforeSize = _uiFont.MeasureString(textBeforeCursor) * FONT_SCALE;
                    float cursorX = inputBounds.X + 8 + textBeforeSize.X;
                    float cursorY = textY;
                    float cursorHeight = textHeight;
                    
                    // Draw cursor as a vertical line
                    spriteBatch.Draw(_pixel, new Rectangle((int)cursorX, (int)cursorY, 1, (int)cursorHeight), TEXT_PRIMARY);
                }
            }
            else
            {
                // Draw placeholder text
                spriteBatch.DrawString(_uiFont, "Enter new name", new Vector2(inputBounds.X + 8, textY), TEXT_TERTIARY, 0f, Vector2.Zero, FONT_SCALE, SpriteEffects.None, 0f);
                
                // Draw blinking cursor at the beginning when no text
                bool showCursor = (_cursorBlinkTime % (CURSOR_BLINK_RATE * 2)) < CURSOR_BLINK_RATE;
                if (showCursor)
                {
                    float cursorX = inputBounds.X + 8;
                    float cursorY = textY;
                    float cursorHeight = textHeight;
                    
                    // Draw cursor as a vertical line
                    spriteBatch.Draw(_pixel, new Rectangle((int)cursorX, (int)cursorY, 1, (int)cursorHeight), TEXT_TERTIARY);
                }
            }
        }

        private void DrawThinBorder(SpriteBatch spriteBatch, Rectangle bounds, Color color)
        {
            // Draw 1-pixel border instead of the default thick border
            // Top
            spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), color);
            // Bottom
            spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), color);
            // Left
            spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), color);
            // Right
            spriteBatch.Draw(_pixel, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), color);
        }

        private void DrawNotImplementedMessage(SpriteBatch spriteBatch)
        {
            string message = "Coming Soon";
            string subMessage = "This section is under development";
            
            Vector2 messageSize = _uiFont.MeasureString(message) * FONT_SCALE;
            Vector2 subMessageSize = _uiFont.MeasureString(subMessage) * FONT_SCALE;
            
            Vector2 messagePosition = new Vector2(
                _rightContentBounds.X + (_rightContentBounds.Width - messageSize.X) / 2,
                _rightContentBounds.Y + (_rightContentBounds.Height - messageSize.Y) / 2 - 20
            );
            
            Vector2 subMessagePosition = new Vector2(
                _rightContentBounds.X + (_rightContentBounds.Width - subMessageSize.X) / 2,
                messagePosition.Y + messageSize.Y + 10
            );

            spriteBatch.DrawString(_uiFont, message, messagePosition, TEXT_PRIMARY, 0f, Vector2.Zero, FONT_SCALE, SpriteEffects.None, 0f);
            spriteBatch.DrawString(_uiFont, subMessage, subMessagePosition, TEXT_SECONDARY, 0f, Vector2.Zero, FONT_SCALE, SpriteEffects.None, 0f);
        }

        private void DrawScrollbar(SpriteBatch spriteBatch)
        {
            if (!_needsScrollbar) return;

            // Draw scrollbar background with modern styling
            DrawRoundedRectangle(spriteBatch, _scrollbarBounds, new Color(55, 65, 81), 4);

            // Calculate thumb size and position
            float contentRatio = (float)_scrollableContentBounds.Height / _contentHeight;
            int thumbHeight = Math.Max(20, (int)(_scrollbarBounds.Height * contentRatio));
            int thumbY = _scrollbarBounds.Y + (int)((_scrollbarBounds.Height - thumbHeight) * (_scrollY / (float)Math.Max(1, _contentHeight - _scrollableContentBounds.Height)));

            var thumbBounds = new Rectangle(_scrollbarBounds.X + 2, thumbY + 2, _scrollbarBounds.Width - 4, thumbHeight - 4);
            bool isThumbHovered = (_isDraggingScrollbar) || 
                                  (!IsMouseOverTopBarDropdown(_currentMouseState.Position) && 
                                   IsTopmostWindowUnderMouse(_windowManagement, _currentMouseState.Position) &&
                                   thumbBounds.Contains(_currentMouseState.Position));
            Color thumbColor = isThumbHovered ? BUTTON_PRIMARY_HOVER : BUTTON_PRIMARY;

            DrawRoundedRectangle(spriteBatch, thumbBounds, thumbColor, 4);
        }

        private void DrawContextMenu(SpriteBatch spriteBatch)
        {
            if (!_isContextMenuVisible) 
            {
                System.Diagnostics.Debug.WriteLine("GameManager: Context menu not visible, skipping draw");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"GameManager: Drawing context menu at bounds: {_contextMenuBounds} with {_contextMenuItems.Count} items");

            // Draw menu shadow
            Rectangle shadowBounds = new Rectangle(
                _contextMenuBounds.X + MENU_SHADOW_OFFSET,
                _contextMenuBounds.Y + MENU_SHADOW_OFFSET,
                _contextMenuBounds.Width,
                _contextMenuBounds.Height
            );
            spriteBatch.Draw(_pixel, shadowBounds, MENU_SHADOW_COLOR);

            // Draw main menu background
            spriteBatch.Draw(_pixel, _contextMenuBounds, MENU_BACKGROUND_COLOR);

            // Draw main menu border
            DrawBorder(spriteBatch, _contextMenuBounds, MENU_BORDER_COLOR);

            // Draw menu items
            for (int i = 0; i < _contextMenuItems.Count; i++)
            {
                var item = _contextMenuItems[i];
                Rectangle itemBounds = new Rectangle(
                    _contextMenuBounds.X,
                    _contextMenuBounds.Y + (i * CONTEXT_MENU_ITEM_HEIGHT),
                    _contextMenuBounds.Width,
                    CONTEXT_MENU_ITEM_HEIGHT
                );

                // Check if item is hovered
                bool isHovered = !IsMouseOverTopBarDropdown(_currentMouseState.Position) && 
                                 IsTopmostWindowUnderMouse(_windowManagement, _currentMouseState.Position) &&
                                 itemBounds.Contains(_currentMouseState.Position);
                item.IsHovered = isHovered;

                // Draw item background if hovered
                if (isHovered)
                {
                    spriteBatch.Draw(_pixel, itemBounds, MENU_HOVER_COLOR);
                }

                // Draw item text
                Vector2 textPos = new Vector2(
                    itemBounds.X + CONTEXT_MENU_PADDING,
                    itemBounds.Y + (CONTEXT_MENU_ITEM_HEIGHT - _uiFont.LineSpacing) / 2
                );
                spriteBatch.DrawString(_uiFont, item.Text, textPos, MENU_TEXT_COLOR);
            }
        }

        private void DrawBorder(SpriteBatch spriteBatch, Rectangle bounds, Color color)
        {
            // Top
            spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), color);
            // Bottom
            spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), color);
            // Left
            spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), color);
            // Right
            spriteBatch.Draw(_pixel, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), color);
        }

        private void DrawRoundedRectangle(SpriteBatch spriteBatch, Rectangle bounds, Color color, int radius = 8)
        {
            // For simplicity, we'll draw a regular rectangle with a subtle border
            // In a full implementation, you'd want to use a proper rounded rectangle shader
            spriteBatch.Draw(_pixel, bounds, color);
            
            // Draw subtle border
            DrawBorder(spriteBatch, bounds, new Color(color.R + 20, color.G + 20, color.B + 20));
        }

        private void DrawCard(SpriteBatch spriteBatch, Rectangle bounds, Color backgroundColor, bool hasShadow = true)
        {
            // Draw shadow if enabled
            if (hasShadow)
            {
                var shadowBounds = new Rectangle(bounds.X + 2, bounds.Y + 2, bounds.Width, bounds.Height);
                spriteBatch.Draw(_pixel, shadowBounds, new Color(0, 0, 0, 50));
            }
            
            // Draw card background
            DrawRoundedRectangle(spriteBatch, bounds, backgroundColor, _borderRadius);
        }

        private void DrawAnimatedButton(SpriteBatch spriteBatch, Rectangle bounds, string text, bool isHovered, bool isActive, bool isSecondary, bool usePixelFont, Color buttonColor, Color textColor)
        {
            // Apply hover effect if applicable
            if (isHovered && isActive)
            {
                buttonColor = Color.Lerp(buttonColor, BUTTON_PRIMARY_HOVER, 0.3f);
            }
            
            DrawRoundedRectangle(spriteBatch, bounds, buttonColor, _borderRadius);
            
            // Choose font based on parameter
            SpriteFont fontToUse = usePixelFont ? _pixelFont : _uiFont;
            
            // Draw button text
            Vector2 textSize = fontToUse.MeasureString(text) * FONT_SCALE;
            Vector2 textPosition = new Vector2(
                bounds.X + (bounds.Width - textSize.X) / 2,
                bounds.Y + (bounds.Height - textSize.Y) / 2
            );
            
            spriteBatch.DrawString(fontToUse, text, textPosition, textColor, 0f, Vector2.Zero, FONT_SCALE, SpriteEffects.None, 0f);
        }

        private void DrawModernButton(SpriteBatch spriteBatch, Rectangle bounds, string text, bool isHovered, bool isActive = false, bool isSecondary = false, bool usePixelFont = false)
        {
            Color buttonColor = isActive ? BUTTON_PRIMARY : 
                               isHovered ? (isSecondary ? BUTTON_SECONDARY_HOVER : BUTTON_PRIMARY_HOVER) : 
                               (isSecondary ? BUTTON_SECONDARY : BUTTON_PRIMARY);
            
            DrawRoundedRectangle(spriteBatch, bounds, buttonColor, _borderRadius);
            
            // Choose font based on parameter
            SpriteFont fontToUse = usePixelFont ? _pixelFont : _uiFont;
            
            // Debug: Log which font is being used
            if (text == "Cancel" || text == "Next" || text == "Back" || text == "Create Project" || text == "Create New Project")
            {
                Console.WriteLine($"[GameManager] Drawing button '{text}' with {(usePixelFont ? "pixel" : "UI")} font");
            }
            
            // Draw button text
            Vector2 textSize = fontToUse.MeasureString(text) * FONT_SCALE;
            Vector2 textPosition = new Vector2(
                bounds.X + (bounds.Width - textSize.X) / 2,
                bounds.Y + (bounds.Height - textSize.Y) / 2
            );
            
            Color textColor = isSecondary ? TEXT_PRIMARY : Color.White;
            spriteBatch.DrawString(fontToUse, text, textPosition, textColor, 0f, Vector2.Zero, FONT_SCALE, SpriteEffects.None, 0f);
        }

        public void UpdateWindowWidth(int newWidth)
        {
            _windowWidth = newWidth;
            _windowManagement.UpdateWindowWidth(newWidth);
            UpdateBounds();
        }

        public void LoadContent(ContentManager content)
        {
            try
            {
                _content = content;
                _windowManagement.LoadContent(content);
                
                // Try to load fonts with detailed error handling - using Roboto
                try
                {
                    _uiFont = content.Load<SpriteFont>("Fonts/SpriteFonts/roboto/regular");
                    System.Diagnostics.Debug.WriteLine("[GameManager] Successfully loaded Roboto regular font");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[GameManager] Failed to load Roboto regular: {ex.Message}");
                    try
                    {
                        // Fallback to Open Sans
                        _uiFont = content.Load<SpriteFont>("Fonts/SpriteFonts/open_sans/regular");
                        System.Diagnostics.Debug.WriteLine("[GameManager] Using Open Sans as fallback");
                    }
                    catch (Exception ex2)
                    {
                        System.Diagnostics.Debug.WriteLine($"[GameManager] Failed to load Open Sans fallback: {ex2.Message}");
                        _uiFont = _menuFont;
                    }
                }
                
                try
                {
                    _sidebarFont = content.Load<SpriteFont>("Fonts/SpriteFonts/roboto/light");
                    System.Diagnostics.Debug.WriteLine("[GameManager] Successfully loaded Roboto light font for sidebar");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[GameManager] Failed to load Roboto light: {ex.Message}");
                    try
                    {
                        // Fallback to Open Sans light
                        _sidebarFont = content.Load<SpriteFont>("Fonts/SpriteFonts/open_sans/light");
                        System.Diagnostics.Debug.WriteLine("[GameManager] Using Open Sans light as fallback");
                    }
                    catch (Exception ex2)
                    {
                        System.Diagnostics.Debug.WriteLine($"[GameManager] Failed to load Open Sans light fallback: {ex2.Message}");
                        _sidebarFont = _menuFont;
                    }
                }
                
                try
                {
                    _pixelFont = content.Load<SpriteFont>("Fonts/SpriteFonts/roboto/regular");
                    System.Diagnostics.Debug.WriteLine("[GameManager] Successfully loaded Roboto for titles");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[GameManager] Failed to load Roboto: {ex.Message}");
                    try
                    {
                        // Fallback to pixel font
                        _pixelFont = content.Load<SpriteFont>("Fonts/SpriteFonts/pixel/regular");
                        System.Diagnostics.Debug.WriteLine("[GameManager] Using pixel font as fallback");
                    }
                    catch (Exception ex2)
                    {
                        System.Diagnostics.Debug.WriteLine($"[GameManager] Failed to load pixel font fallback: {ex2.Message}");
                        _pixelFont = _menuFont;
                    }
                }
                
                // Verify fonts are loaded correctly
                System.Diagnostics.Debug.WriteLine($"[GameManager] UI Font loaded: {_uiFont != null}");
                System.Diagnostics.Debug.WriteLine($"[GameManager] Sidebar Font loaded: {_sidebarFont != null}");
                System.Diagnostics.Debug.WriteLine($"[GameManager] Pixel Font loaded: {_pixelFont != null}");
                
                // Ensure TaskBar has an icon for this module with logo loading
                if (_taskBar != null)
                {
                    _taskBar.EnsureModuleIconExists("Game Manager", _content);
                    System.Diagnostics.Debug.WriteLine("[GameManager] Ensured module icon exists in TaskBar");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GameManager] LoadContent Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[GameManager] LoadContent Stack Trace: {ex.StackTrace}");
                
                // Final fallback to menu font
                _uiFont = _menuFont;
                _sidebarFont = _menuFont;
                _pixelFont = _menuFont;
                System.Diagnostics.Debug.WriteLine("[GameManager] Using menu font as final fallback");
            }
        }

        public void Dispose()
        {
            _pixel?.Dispose();
            _windowManagement?.Dispose();
        }

        public void OnWindowVisibilityChanged(bool isVisible)
        {
            if (isVisible && _taskBar != null)
            {
                // Ensure the module icon exists and is properly registered
                _taskBar.EnsureModuleIconExists("Game Manager", _content);
                // Ensure the module is not marked as minimized in TaskBar
                _taskBar.SetModuleMinimized("Game Manager", false);
                System.Diagnostics.Debug.WriteLine("GameManager: Ensured module icon exists and is not minimized in TaskBar");
            }
        }

        public void ClearFocus()
        {
            // Clear focus if needed
        }

        private bool IsTopmostWindowUnderMouse(WindowManagement window, Point mousePosition)
        {
            if (!window.IsVisible())
                return false;

            // Get the window bounds
            Rectangle windowBounds = window.GetWindowBounds();
            
            // Check if mouse is over this window
            if (!windowBounds.Contains(mousePosition))
                return false;

            // Get all active windows from WindowManagement using reflection
            var activeWindowsField = typeof(WindowManagement).GetField("_activeWindows", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            
            if (activeWindowsField == null)
                return true; // If we can't access the list, assume this window is topmost

            var activeWindows = activeWindowsField.GetValue(null) as List<WindowManagement>;
            if (activeWindows == null)
                return true;

            // Check if this window has the highest z-order among all windows under the mouse
            int highestZOrder = -1;
            WindowManagement topmostWindow = null;

            foreach (var activeWindow in activeWindows)
            {
                if (activeWindow.IsVisible() && activeWindow.GetWindowBounds().Contains(mousePosition))
                {
                    int zOrder = activeWindow.GetZOrder();
                    if (zOrder > highestZOrder)
                    {
                        highestZOrder = zOrder;
                        topmostWindow = activeWindow;
                    }
                }
            }

            return topmostWindow == window;
        }
    }
}
