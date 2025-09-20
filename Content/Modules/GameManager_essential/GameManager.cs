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

    public class GameManager : IModule
    {
        private WindowManagement _windowManagement;
        private GraphicsDevice _graphicsDevice;
        private SpriteFont _menuFont;
        private SpriteFont _uiFont;
        private SpriteFont _sidebarFont; // Separate font for sidebar
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
        private bool _isCreatingProject = false;
        private List<GameProject> _projects;
        private GameProject _selectedProject;
        private int _selectedProjectIndex = -1;

        // Layout properties
        private Rectangle _leftSidebarBounds;
        private Rectangle _rightContentBounds;
        private Rectangle _createProjectFormBounds;
        private int _sidebarWidth = 200; // Increased back to 200 for better visibility
        private int _contentPadding = 15; // Increased padding
        private int _sectionButtonHeight = 35; // Increased button height
        private int _projectItemHeight = 50; // Increased item height
        private int _projectItemPadding = 8; // Increased padding

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

        // Create Project Form
        private UIElements _createProjectUI;
        private Dictionary<string, object> _formValues;

        // Font scaling
        private const float FONT_SCALE = 1.0f; // Normal font size

        // Colors
        private readonly Color SIDEBAR_BACKGROUND = new Color(50, 50, 50);
        private readonly Color CONTENT_BACKGROUND = new Color(60, 60, 60);
        private readonly Color SECTION_HOVER_COLOR = new Color(147, 112, 219, 100);
        private readonly Color SECTION_ACTIVE_COLOR = new Color(147, 112, 219);
        private readonly Color PROJECT_HOVER_COLOR = new Color(80, 80, 80);
        private readonly Color PROJECT_SELECTED_COLOR = new Color(147, 112, 219, 150);
        private readonly Color BORDER_COLOR = new Color(100, 100, 100);
        private readonly Color TEXT_COLOR = new Color(220, 220, 220);
        private readonly Color SUBTEXT_COLOR = new Color(150, 150, 150);
        private readonly Color BUTTON_COLOR = new Color(147, 112, 219);
        private readonly Color BUTTON_HOVER_COLOR = new Color(180, 145, 250);

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
                _formValues = new Dictionary<string, object>();

                // Create pixel texture
                _pixel = new Texture2D(graphicsDevice, 1, 1);
                _pixel.SetData(new[] { Color.White });

                // Initialize window management
                var windowProperties = new WindowProperties
                {
                    IsVisible = false,
                    IsMovable = true,
                    IsResizable = false
                };

                _windowManagement = new WindowManagement(graphicsDevice, menuFont, windowWidth, windowProperties);
                _windowManagement.SetWindowTitle("Game Manager");
                
                // Set custom dimensions (1/4 of default height - 400/4 = 100, but make it a bit larger for usability)
                _windowManagement.SetDefaultSize(800, 200);
                _windowManagement.SetCustomMinimumSize(600, 150); // Set minimum size
                _windowManagement.SetPosition(new Vector2(100, 50)); // Set initial position

                System.Diagnostics.Debug.WriteLine("GameManager: Window management created");

                // Initialize UI bounds
                UpdateBounds();

                // Load existing projects
                LoadProjects();

                // Initialize create project form
                InitializeCreateProjectForm();
                
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

                _createProjectFormBounds = new Rectangle(
                    _rightContentBounds.X + _contentPadding,
                    _rightContentBounds.Y + _contentPadding,
                    _rightContentBounds.Width - (_contentPadding * 2),
                    _rightContentBounds.Height - (_contentPadding * 2)
                );

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
                    _rightContentBounds.Right - SCROLLBAR_WIDTH - SCROLLBAR_PADDING,
                    _rightContentBounds.Y + SCROLLBAR_PADDING,
                    SCROLLBAR_WIDTH,
                    _rightContentBounds.Height - (SCROLLBAR_PADDING * 2)
                );
            }
        }

        private void InitializeCreateProjectForm()
        {
            string markdown = @"
# Create New Project

## Project Details

**Project Name** (required)
- Text Input: projectName

**Description**
- Text Input: description

**Version**
- Text Input: version

**Tags** (comma-separated)
- Text Input: tags

**Engine Version**
- Text Input: engineVersion

## Project Settings

**Create in Directory**
- Text Input: projectPath

**Make Favorite**
- Checkbox: isFavorite

## Actions

- Button: Save Project
- Button: Cancel
";

            _createProjectUI = new UIElements(_graphicsDevice, _uiFont, _createProjectFormBounds, markdown);
            _createProjectUI.SetSaveSettingsCallback(OnSaveProject);
            _createProjectUI.SetResetToDefaultsCallback(OnCancelProject);
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
                }
                else
                {
                    _projects = new List<GameProject>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading projects: {ex.Message}");
                _projects = new List<GameProject>();
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
        }

        public void Update()
        {
            try
            {
                _previousMouseState = _currentMouseState;
                _currentMouseState = Mouse.GetState();
                _previousKeyboardState = _currentKeyboardState;
                _currentKeyboardState = Keyboard.GetState();

                _windowManagement.Update();

                // Always update bounds to ensure they're calculated
                UpdateBounds();

                if (!_windowManagement.IsVisible())
                    return;
                HandleInput();
                UpdateScrolling();

                if (_isCreatingProject && _createProjectUI != null)
                {
                    _createProjectUI.Update(_windowManagement.IsVisible());
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

            // Handle section button clicks
            if (_currentMouseState.LeftButton == ButtonState.Pressed && 
                _previousMouseState.LeftButton == ButtonState.Released)
            {
                for (int i = 0; i < _sectionButtonBounds.Count; i++)
                {
                    if (_sectionButtonBounds[i].Contains(mousePosition))
                    {
                        _currentSection = _sectionNames[i];
                        _isCreatingProject = false;
                        break;
                    }
                }

                // Handle project item clicks
                if (_currentSection == "Projects" && !_isCreatingProject)
                {
                    HandleProjectClicks(mousePosition);
                }

                // Handle create project button
                if (_currentSection == "Projects" && !_isCreatingProject && _projects.Count == 0)
                {
                    var createButtonBounds = GetCreateProjectButtonBounds();
                    if (createButtonBounds.Contains(mousePosition))
                    {
                        _isCreatingProject = true;
                    }
                }

                // Handle back button in create project form
                if (_isCreatingProject)
                {
                    var backButtonBounds = GetBackButtonBounds();
                    if (backButtonBounds.Contains(mousePosition))
                    {
                        _isCreatingProject = false;
                    }
                }
            }

            // Handle scrollbar interaction
            HandleScrollbarInteraction();
        }

        private void HandleProjectClicks(Point mousePosition)
        {
            int startY = _rightContentBounds.Y + _contentPadding - _scrollY;
            int itemY = startY;

            for (int i = 0; i < _projects.Count; i++)
            {
                var itemBounds = new Rectangle(
                    _rightContentBounds.X + _contentPadding,
                    itemY,
                    _rightContentBounds.Width - (_contentPadding * 2) - (_needsScrollbar ? SCROLLBAR_WIDTH + SCROLLBAR_PADDING : 0),
                    _projectItemHeight
                );

                if (itemBounds.Contains(mousePosition))
                {
                    _selectedProjectIndex = i;
                    _selectedProject = _projects[i];
                    break;
                }

                itemY += _projectItemHeight + _projectItemPadding;
            }
        }

        private Rectangle GetCreateProjectButtonBounds()
        {
            int centerX = _rightContentBounds.X + (_rightContentBounds.Width / 2);
            int centerY = _rightContentBounds.Y + (_rightContentBounds.Height / 2);
            int buttonWidth = 200;
            int buttonHeight = 50;

            return new Rectangle(
                centerX - (buttonWidth / 2),
                centerY - (buttonHeight / 2),
                buttonWidth,
                buttonHeight
            );
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

        private void UpdateScrolling()
        {
            if (!_scrollingEnabled) return;

            _contentHeight = CalculateContentHeight();
            _needsScrollbar = _contentHeight > _rightContentBounds.Height;

            if (_needsScrollbar)
            {
                _scrollY = MathHelper.Clamp(_scrollY, 0, Math.Max(0, _contentHeight - _rightContentBounds.Height));
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
                    return _createProjectFormBounds.Height;
                }
                else if (_projects.Count == 0)
                {
                    return _rightContentBounds.Height; // Center the "no projects" message
                }
                else
                {
                    return (_projects.Count * _projectItemHeight) + ((_projects.Count - 1) * _projectItemPadding) + (_contentPadding * 2);
                }
            }
            else
            {
                return _rightContentBounds.Height; // For Learn and Community sections
            }
        }

        private void HandleScrollbarInteraction()
        {
            if (!_needsScrollbar) return;

            var mousePosition = _currentMouseState.Position;

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
                    float contentHeight = (float)_contentHeight;
                    float visibleHeight = _rightContentBounds.Height;

                    float scrollRatio = deltaY / (scrollbarHeight - (scrollbarHeight * visibleHeight / contentHeight));
                    _scrollY = (int)(scrollRatio * (contentHeight - visibleHeight));
                    _scrollY = MathHelper.Clamp(_scrollY, 0, Math.Max(0, (int)(contentHeight - visibleHeight)));
                }
                else
                {
                    _isDraggingScrollbar = false;
                }
            }

            // Handle mouse wheel scrolling
            if (_currentMouseState.ScrollWheelValue != _previousMouseState.ScrollWheelValue)
            {
                int delta = _currentMouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
                _scrollY -= delta / 3; // Adjust scroll speed
                _scrollY = MathHelper.Clamp(_scrollY, 0, Math.Max(0, _contentHeight - _rightContentBounds.Height));
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

                System.Diagnostics.Debug.WriteLine($"Created project: {project.Name}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving project: {ex.Message}");
            }
        }

        private void OnCancelProject()
        {
            _isCreatingProject = false;
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

                System.Diagnostics.Debug.WriteLine("GameManager: Drawing window");
                _windowManagement.Draw(spriteBatch, "Game Manager");
                DrawContent(spriteBatch);
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
                
                // Draw sidebar
                DrawSidebar(spriteBatch);

                // Draw main content
                DrawMainContent(spriteBatch);

                // Draw scrollbar if needed
                if (_needsScrollbar)
                {
                    DrawScrollbar(spriteBatch);
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
                
                // Draw sidebar background
                spriteBatch.Draw(_pixel, _leftSidebarBounds, SIDEBAR_BACKGROUND);

                // Draw sidebar border
                DrawBorder(spriteBatch, _leftSidebarBounds, BORDER_COLOR);

                // Draw section buttons
                for (int i = 0; i < _sectionNames.Count; i++)
                {
                    if (i >= _sectionButtonBounds.Count)
                    {
                        System.Diagnostics.Debug.WriteLine($"GameManager: Warning - Section {i} has no bounds, skipping");
                        continue;
                    }
                    
                    var buttonBounds = _sectionButtonBounds[i];
                    bool isActive = _sectionNames[i] == _currentSection;
                    bool isHovered = buttonBounds.Contains(_currentMouseState.Position);

                    Color buttonColor = isActive ? SECTION_ACTIVE_COLOR : 
                                      isHovered ? SECTION_HOVER_COLOR : Color.Transparent;

                    if (buttonColor != Color.Transparent)
                    {
                        spriteBatch.Draw(_pixel, buttonBounds, buttonColor);
                    }

                    // Draw button text with scaling (use sidebar font)
                    Vector2 textSize = _sidebarFont.MeasureString(_sectionNames[i]) * FONT_SCALE;
                    Vector2 textPosition = new Vector2(
                        buttonBounds.X + (buttonBounds.Width - textSize.X) / 2,
                        buttonBounds.Y + (buttonBounds.Height - textSize.Y) / 2
                    );

                    // Debug: Log text drawing
                    if (i == 0) // Only log for first button to avoid spam
                    {
                        System.Diagnostics.Debug.WriteLine($"GameManager: Drawing text '{_sectionNames[i]}' at {textPosition} with size {textSize} in bounds {buttonBounds}");
                    }

                    spriteBatch.DrawString(_sidebarFont, _sectionNames[i], textPosition, TEXT_COLOR, 0f, Vector2.Zero, FONT_SCALE, SpriteEffects.None, 0f);
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
            // Draw content background
            spriteBatch.Draw(_pixel, _rightContentBounds, CONTENT_BACKGROUND);

            // Draw content border
            DrawBorder(spriteBatch, _rightContentBounds, BORDER_COLOR);

            if (_currentSection == "Projects")
            {
                if (_isCreatingProject)
                {
                    DrawCreateProjectForm(spriteBatch);
                }
                else if (_projects.Count == 0)
                {
                    DrawNoProjectsMessage(spriteBatch);
                }
                else
                {
                    DrawProjectsList(spriteBatch);
                }
            }
            else if (_currentSection == "Learn" || _currentSection == "Community")
            {
                DrawNotImplementedMessage(spriteBatch);
            }
        }

        private void DrawCreateProjectForm(SpriteBatch spriteBatch)
        {
            // Draw back button
            var backButtonBounds = GetBackButtonBounds();
            bool isBackHovered = backButtonBounds.Contains(_currentMouseState.Position);
            Color backButtonColor = isBackHovered ? BUTTON_HOVER_COLOR : BUTTON_COLOR;
            
            spriteBatch.Draw(_pixel, backButtonBounds, backButtonColor);
            spriteBatch.DrawString(_uiFont, "Back", new Vector2(backButtonBounds.X + 5, backButtonBounds.Y + 5), Color.White, 0f, Vector2.Zero, FONT_SCALE, SpriteEffects.None, 0f);

            // Draw the form UI
            if (_createProjectUI != null)
            {
                System.Diagnostics.Debug.WriteLine($"GameManager: Drawing create project form with bounds: {_createProjectFormBounds}");
                _createProjectUI.Draw(spriteBatch);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("GameManager: Create project UI is null");
            }
        }

        private void DrawNoProjectsMessage(SpriteBatch spriteBatch)
        {
            string message = "No projects yet - create new one";
            Vector2 messageSize = _uiFont.MeasureString(message) * FONT_SCALE;
            Vector2 messagePosition = new Vector2(
                _rightContentBounds.X + (_rightContentBounds.Width - messageSize.X) / 2,
                _rightContentBounds.Y + (_rightContentBounds.Height - messageSize.Y) / 2 - 50 // Increased spacing
            );

            spriteBatch.DrawString(_uiFont, message, messagePosition, TEXT_COLOR, 0f, Vector2.Zero, FONT_SCALE, SpriteEffects.None, 0f);

            // Draw create project button
            var createButtonBounds = GetCreateProjectButtonBounds();
            bool isHovered = createButtonBounds.Contains(_currentMouseState.Position);
            Color buttonColor = isHovered ? BUTTON_HOVER_COLOR : BUTTON_COLOR;

            spriteBatch.Draw(_pixel, createButtonBounds, buttonColor);
            DrawBorder(spriteBatch, createButtonBounds, BORDER_COLOR);

            // Draw plus icon and text
            string buttonText = "+ Create Project";
            Vector2 buttonTextSize = _uiFont.MeasureString(buttonText) * FONT_SCALE;
            Vector2 buttonTextPosition = new Vector2(
                createButtonBounds.X + (createButtonBounds.Width - buttonTextSize.X) / 2,
                createButtonBounds.Y + (createButtonBounds.Height - buttonTextSize.Y) / 2
            );

            spriteBatch.DrawString(_uiFont, buttonText, buttonTextPosition, Color.White, 0f, Vector2.Zero, FONT_SCALE, SpriteEffects.None, 0f);
        }

        private void DrawProjectsList(SpriteBatch spriteBatch)
        {
            int startY = _rightContentBounds.Y + _contentPadding - _scrollY;
            int itemY = startY;

            // Draw header
            DrawProjectHeader(spriteBatch, _rightContentBounds.Y + _contentPadding);

            itemY += 30; // Space for header

            for (int i = 0; i < _projects.Count; i++)
            {
                var project = _projects[i];
                var itemBounds = new Rectangle(
                    _rightContentBounds.X + _contentPadding,
                    itemY,
                    _rightContentBounds.Width - (_contentPadding * 2) - (_needsScrollbar ? SCROLLBAR_WIDTH + SCROLLBAR_PADDING : 0),
                    _projectItemHeight
                );

                // Only draw if visible
                if (itemBounds.Bottom > _rightContentBounds.Y && itemBounds.Top < _rightContentBounds.Bottom)
                {
                    bool isSelected = i == _selectedProjectIndex;
                    bool isHovered = itemBounds.Contains(_currentMouseState.Position);

                    Color itemColor = isSelected ? PROJECT_SELECTED_COLOR :
                                    isHovered ? PROJECT_HOVER_COLOR : Color.Transparent;

                    if (itemColor != Color.Transparent)
                    {
                        spriteBatch.Draw(_pixel, itemBounds, itemColor);
                    }

                    // Draw project info
                    DrawProjectItem(spriteBatch, project, itemBounds);
                }

                itemY += _projectItemHeight + _projectItemPadding;
            }
        }

        private void DrawProjectHeader(SpriteBatch spriteBatch, int y)
        {
            int x = _rightContentBounds.X + _contentPadding;
            int width = _rightContentBounds.Width - (_contentPadding * 2) - (_needsScrollbar ? SCROLLBAR_WIDTH + SCROLLBAR_PADDING : 0);

            // Draw header background
            var headerBounds = new Rectangle(x, y, width, 25);
            spriteBatch.Draw(_pixel, headerBounds, new Color(70, 70, 70));

            // Draw header text
            string[] headers = { "Name", "Version", "Created", "Modified" };
            int[] columnWidths = { width / 3, width / 6, width / 4, width / 4 };

            int currentX = x + 10;
            for (int i = 0; i < headers.Length; i++)
            {
                spriteBatch.DrawString(_uiFont, headers[i], new Vector2(currentX, y + 5), SUBTEXT_COLOR, 0f, Vector2.Zero, FONT_SCALE, SpriteEffects.None, 0f);
                currentX += columnWidths[i];
            }
        }

        private void DrawProjectItem(SpriteBatch spriteBatch, GameProject project, Rectangle bounds)
        {
            int x = bounds.X + 10;
            int y = bounds.Y + 5;
            int width = bounds.Width - 20;
            int[] columnWidths = { width / 3, width / 6, width / 4, width / 4 };

            // Draw project name
            spriteBatch.DrawString(_uiFont, project.Name, new Vector2(x, y), TEXT_COLOR, 0f, Vector2.Zero, FONT_SCALE, SpriteEffects.None, 0f);

            // Draw version
            x += columnWidths[0];
            spriteBatch.DrawString(_uiFont, project.Version, new Vector2(x, y), SUBTEXT_COLOR, 0f, Vector2.Zero, FONT_SCALE, SpriteEffects.None, 0f);

            // Draw created date
            x += columnWidths[1];
            spriteBatch.DrawString(_uiFont, project.CreatedDate.ToString("MM/dd/yyyy"), new Vector2(x, y), SUBTEXT_COLOR, 0f, Vector2.Zero, FONT_SCALE, SpriteEffects.None, 0f);

            // Draw modified date
            x += columnWidths[2];
            spriteBatch.DrawString(_uiFont, project.LastModified.ToString("MM/dd/yyyy"), new Vector2(x, y), SUBTEXT_COLOR, 0f, Vector2.Zero, FONT_SCALE, SpriteEffects.None, 0f);

            // Draw description on second line
            if (!string.IsNullOrEmpty(project.Description))
            {
                x = bounds.X + 10;
                y += 20;
                string description = project.Description.Length > 50 ? project.Description.Substring(0, 47) + "..." : project.Description;
                spriteBatch.DrawString(_uiFont, description, new Vector2(x, y), SUBTEXT_COLOR, 0f, Vector2.Zero, FONT_SCALE, SpriteEffects.None, 0f);
            }
        }

        private void DrawNotImplementedMessage(SpriteBatch spriteBatch)
        {
            string message = "Not implemented";
            Vector2 messageSize = _menuFont.MeasureString(message) * FONT_SCALE;
            Vector2 messagePosition = new Vector2(
                _rightContentBounds.X + (_rightContentBounds.Width - messageSize.X) / 2,
                _rightContentBounds.Y + (_rightContentBounds.Height - messageSize.Y) / 2
            );

            spriteBatch.DrawString(_uiFont, message, messagePosition, SUBTEXT_COLOR, 0f, Vector2.Zero, FONT_SCALE, SpriteEffects.None, 0f);
        }

        private void DrawScrollbar(SpriteBatch spriteBatch)
        {
            if (!_needsScrollbar) return;

            // Draw scrollbar background
            spriteBatch.Draw(_pixel, _scrollbarBounds, new Color(80, 80, 80));

            // Calculate thumb size and position
            float contentRatio = (float)_rightContentBounds.Height / _contentHeight;
            int thumbHeight = Math.Max(20, (int)(_scrollbarBounds.Height * contentRatio));
            int thumbY = _scrollbarBounds.Y + (int)((_scrollbarBounds.Height - thumbHeight) * (_scrollY / (float)Math.Max(1, _contentHeight - _rightContentBounds.Height)));

            var thumbBounds = new Rectangle(_scrollbarBounds.X, thumbY, _scrollbarBounds.Width, thumbHeight);
            bool isThumbHovered = thumbBounds.Contains(_currentMouseState.Position) || _isDraggingScrollbar;
            Color thumbColor = isThumbHovered ? BUTTON_HOVER_COLOR : BUTTON_COLOR;

            spriteBatch.Draw(_pixel, thumbBounds, thumbColor);
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
                
                // Load a better font for UI elements
                _uiFont = content.Load<SpriteFont>("Fonts/SpriteFonts/open_sans/regular");
                System.Diagnostics.Debug.WriteLine("GameManager: Loaded Open Sans regular font");
                
                // Load a smaller font for sidebar
                _sidebarFont = content.Load<SpriteFont>("Fonts/SpriteFonts/open_sans/light");
                System.Diagnostics.Debug.WriteLine("GameManager: Loaded Open Sans light font for sidebar");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GameManager LoadContent Error: {ex.Message}");
                // Fall back to menu font if loading fails
                _uiFont = _menuFont;
                _sidebarFont = _menuFont;
            }
        }

        public void Dispose()
        {
            _pixel?.Dispose();
            _createProjectUI?.Dispose();
            _windowManagement?.Dispose();
        }

        public void OnWindowVisibilityChanged(bool isVisible)
        {
            // Handle visibility changes if needed
        }

        public void ClearFocus()
        {
            // Clear focus if needed
        }
    }
}
