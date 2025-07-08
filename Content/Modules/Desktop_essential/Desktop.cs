using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Content;
using System.Collections.Generic;
using System;
using System.Linq;
using MarySGameEngine.Modules.WindowManagement_essential;
using MarySGameEngine.Modules.TaskBar_essential;
using MarySGameEngine.Modules.TopBar_essential;
using System.IO;

namespace MarySGameEngine.Modules.Desktop_essential
{
    public class Desktop : IModule
    {
        private GraphicsDevice _graphicsDevice;
        private SpriteFont _menuFont;
        private SpriteFont _desktopFont;
        private SpriteFont _arialFont;
        private int _windowWidth;
        private int _windowHeight;
        private Texture2D _pixel;
        private Texture2D _arrowTexture;
        private MouseState _currentMouseState;
        private MouseState _previousMouseState;
        private bool _isDragging;
        private Vector2 _dragStart;
        private Vector2 _dragCurrent;
        private Rectangle _selectionRectangle;
        private Color _backgroundColor;
        private const int TOP_BAR_HEIGHT = 35;
        private const int TASK_BAR_SIZE = 60;
        private TaskBar _taskBar;
        private GameEngine _engine;
        private bool _showGrid = false;

        // Grid system related fields
        private const float IDEAL_CELL_ASPECT_RATIO = 1.0f; // Square cells
        private const int MIN_CELL_SIZE = 80; // Increased minimum cell size for larger icons
        private int _gridColumns;
        private int _gridRows;
        private int _gridCellSize;
        private readonly Color GRID_LINE_COLOR = new Color(147, 112, 219, 50); // Semi-transparent purple
        private readonly Color GRID_LINE_COLOR_DARK = new Color(147, 112, 219, 30); // More transparent purple for secondary lines
        private int _gridStartX;
        private int _gridStartY;
        private int _gridEndX;
        private int _gridEndY;

        // Context menu related fields
        private bool _isContextMenuVisible;
        private Vector2 _contextMenuPosition;
        private Rectangle _contextMenuBounds;
        private List<ContextMenuItem> _contextMenuItems;
        private const int CONTEXT_MENU_ITEM_HEIGHT = 30;
        private const int CONTEXT_MENU_PADDING = 5;
        private const int DROPDOWN_EXTRA_PADDING = 25;
        private const int DROPDOWN_LEFT_PADDING = 25;
        private const int MENU_BORDER_THICKNESS = 2;
        private const int MENU_SHADOW_OFFSET = 2;
        private readonly Color MENU_BACKGROUND_COLOR = new Color(40, 40, 40);
        private readonly Color MENU_HOVER_COLOR = new Color(147, 112, 219, 180); // Semi-transparent purple
        private readonly Color MENU_BORDER_COLOR = new Color(147, 112, 219); // Solid purple
        private readonly Color MENU_SHADOW_COLOR = new Color(0, 0, 0, 100);
        private readonly Color MENU_TEXT_COLOR = new Color(220, 220, 220);
        private readonly Color MENU_DISABLED_TEXT_COLOR = new Color(128, 128, 128);

        private bool _isFirstDropdownVisible;
        private int _firstDropdownX;
        
        // New fields for context menu types
        private bool _isFileContextMenu; // true if right-clicked on file, false if on empty cell
        private DesktopFile _contextMenuTargetFile; // The file that was right-clicked (if any)

        private TaskBarPosition _lastTaskBarPosition;

        // File system related fields
        private Dictionary<string, Texture2D> _fileIcons;
        private List<DesktopFile> _desktopFiles;
        private const float ICON_SIZE_RATIO = 0.8f; // Icon will be 80% of cell size
        private const int FILE_NAME_PADDING = 8;
        private const int FILE_NAME_MAX_WIDTH = 120;
        private int _currentIconSize;
        private const int ICON_TOP_OFFSET = -2; // Negative offset to move icon to the top of cell
        private const int TEXT_TOP_OFFSET = 2; // Keep text close to icon
        private DesktopFile _lastSelectedFile; // Track the last selected file for Shift+Click
        private bool _isCtrlPressed; // Track if Ctrl is pressed
        private bool _isShiftPressed; // Track if Shift is pressed
        private FileSystemWatcher _fileWatcher; // Watch for file system changes
        private DateTime _lastFileChange = DateTime.MinValue; // Prevent rapid reloading
        private bool _filesLoaded = false; // Track if files have been loaded
        private int _loadRetryCount = 0; // Track retry attempts for loading
        private const int MAX_LOAD_RETRIES = 10; // Maximum retry attempts

        // Drag and drop related fields
        private bool _isDraggingFiles;
        private Vector2 _dragOffset;
        private List<DesktopFile> _draggedFiles;
        private Vector2 _dragPreviewPosition;
        private const float DRAG_PREVIEW_ALPHA = 0.7f;
        private const int DRAG_PREVIEW_OFFSET = 20;
        private bool _isValidDropTarget;
        private Rectangle _dropTargetCell;
        private const int DROP_TARGET_HIGHLIGHT_THICKNESS = 2;
        private readonly Color DROP_TARGET_VALID_COLOR = new Color(147, 112, 219, 180); // Semi-transparent purple
        private readonly Color DROP_TARGET_INVALID_COLOR = new Color(232, 17, 35, 180); // Semi-transparent red
        private const int DRAG_PREVIEW_ICON_SIZE = 48; // Larger preview icon
        private const int DRAG_PREVIEW_TEXT_OFFSET = 4; // Space between preview icon and text
        private const float DRAG_PREVIEW_ALPHA_VISIBLE = 0.9f; // More visible alpha for debugging
        private const float CURSOR_ICON_ALPHA = 0.8f; // More visible alpha for cursor icon
        private const int CURSOR_ICON_SIZE = 48; // Larger size for the cursor-following icon
        private int _updateCounter = 0; // Debug counter
        // Highlight effect
        private bool _isHighlighted = false;
        private float _highlightTimer = 0f;
        private const float HIGHLIGHT_DURATION = 1.5f; // Match WindowManagement
        private const float HIGHLIGHT_BLINK_SPEED = 2.0f; // Match WindowManagement
        private const float HIGHLIGHT_MIN_ALPHA = 0.3f; // Match WindowManagement
        private const float HIGHLIGHT_MAX_ALPHA = 0.7f; // Match WindowManagement

        public class DesktopFile
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public Vector2 Position { get; set; }
            public Rectangle IconBounds { get; set; }
            public Rectangle TextBounds { get; set; }
            public bool IsSelected { get; set; }
            public Texture2D Icon { get; set; }
        }

        public Desktop(GraphicsDevice graphicsDevice, SpriteFont menuFont, int windowWidth)
        {
            _graphicsDevice = graphicsDevice;
            _menuFont = menuFont;
            _windowWidth = windowWidth;
            _windowHeight = graphicsDevice.Viewport.Height;
            _backgroundColor = new Color(0x3e, 0x37, 0x63); // Default purple color
            _engine = (GameEngine)GameEngine.Instance;
            _isDragging = false;
            _selectionRectangle = Rectangle.Empty;
            _isFirstDropdownVisible = false;
            _firstDropdownX = 0;
            _fileIcons = new Dictionary<string, Texture2D>();
            _desktopFiles = new List<DesktopFile>();

            // Create a 1x1 white texture for drawing rectangles
            _pixel = new Texture2D(graphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });

            // Initialize context menu
            _isContextMenuVisible = false;
            _contextMenuItems = new List<ContextMenuItem>();
            _isFileContextMenu = false;
            _contextMenuTargetFile = null;

            // Initialize file watcher
            InitializeFileWatcher();
        }

        public void SetTaskBar(TaskBar taskBar)
        {
            _taskBar = taskBar;
            if (_taskBar != null)
            {
                _lastTaskBarPosition = _taskBar.GetCurrentPosition();
            }
            UpdateGridDimensions();
        }

        private int CalculateMenuWidth(ContextMenuItem item)
        {
            float textWidth = _menuFont.MeasureString(item.Text).X;
            if (item.DropdownItems != null)
            {
                textWidth += _menuFont.MeasureString(">").X + CONTEXT_MENU_PADDING;
            }
            return (int)textWidth + CONTEXT_MENU_PADDING + DROPDOWN_EXTRA_PADDING;
        }

        private int CalculateDropdownWidth(ContextMenuItem item)
        {
            if (item.DropdownItems == null) return 0;

            float maxWidth = 0;
            foreach (var dropdownItem in item.DropdownItems)
            {
                float textWidth = _menuFont.MeasureString(dropdownItem).X;
                maxWidth = Math.Max(maxWidth, textWidth);
            }
            return (int)maxWidth + CONTEXT_MENU_PADDING + DROPDOWN_EXTRA_PADDING;
        }

        private bool ShouldShowDropdownsOnLeft(Vector2 position, int dropdownWidth)
        {
            // Calculate the maximum width needed for the main menu
            int maxMenuWidth = 0;
            foreach (var item in _contextMenuItems)
            {
                maxMenuWidth = Math.Max(maxMenuWidth, CalculateMenuWidth(item));
            }
            return (position.X + maxMenuWidth + dropdownWidth) > _windowWidth;
        }

        private void SetupEmptyCellContextMenu()
        {
            _contextMenuItems.Clear();
            _contextMenuItems.Add(new ContextMenuItem("New", new List<string> { "Project", "Folder", "Text File" }));
            _contextMenuItems.Add(new ContextMenuItem("View", new List<string> { "Large Icons", "Medium Icons", "Small Icons"}));
            _contextMenuItems.Add(new ContextMenuItem("Settings", null));
            _isFileContextMenu = false;
            _contextMenuTargetFile = null;
        }

        private void SetupFileContextMenu(DesktopFile file)
        {
            _contextMenuItems.Clear();
            _contextMenuItems.Add(new ContextMenuItem("Open", null));
            _contextMenuItems.Add(new ContextMenuItem("Rename", null));
            _contextMenuItems.Add(new ContextMenuItem("Delete", null));
            _contextMenuItems.Add(new ContextMenuItem("Properties", null));
            _isFileContextMenu = true;
            _contextMenuTargetFile = file;
        }

        private void HandleContextMenuAction(int mainMenuIndex, int dropdownIndex = -1)
        {
            if (mainMenuIndex < 0 || mainMenuIndex >= _contextMenuItems.Count)
                return;

            var item = _contextMenuItems[mainMenuIndex];
            string action = item.Text;
            
            if (dropdownIndex >= 0 && item.DropdownItems != null && dropdownIndex < item.DropdownItems.Count)
            {
                action = item.DropdownItems[dropdownIndex];
            }

            _engine.Log($"Desktop: Context menu action: {action} (File context: {_isFileContextMenu})");

            if (_isFileContextMenu && _contextMenuTargetFile != null)
            {
                // Handle file context menu actions
                switch (action)
                {
                    case "Open":
                        _engine.Log($"Desktop: Opening file: {_contextMenuTargetFile.Name}");
                        // TODO: Implement file opening logic
                        break;
                    case "Rename":
                        _engine.Log($"Desktop: Renaming file: {_contextMenuTargetFile.Name}");
                        // TODO: Implement file renaming logic
                        break;
                    case "Delete":
                        _engine.Log($"Desktop: Deleting file: {_contextMenuTargetFile.Name}");
                        // TODO: Implement file deletion logic
                        break;
                    case "Properties":
                        _engine.Log($"Desktop: Showing properties for file: {_contextMenuTargetFile.Name}");
                        // TODO: Implement file properties logic
                        break;
                }
            }
            else
            {
                // Handle empty cell context menu actions
                switch (action)
                {
                    case "Project":
                        _engine.Log("Desktop: Creating new project");
                        // TODO: Implement new project creation
                        break;
                    case "Folder":
                        _engine.Log("Desktop: Creating new folder");
                        // TODO: Implement new folder creation
                        break;
                    case "Text File":
                        _engine.Log("Desktop: Creating new text file");
                        // TODO: Implement new text file creation
                        break;
                    case "Large Icons":
                        _engine.Log("Desktop: Switching to large icons");
                        // TODO: Implement icon size change
                        break;
                    case "Medium Icons":
                        _engine.Log("Desktop: Switching to medium icons");
                        // TODO: Implement icon size change
                        break;
                    case "Small Icons":
                        _engine.Log("Desktop: Switching to small icons");
                        // TODO: Implement icon size change
                        break;
                    case "Settings":
                        _engine.Log("Desktop: Opening settings");
                        // TODO: Implement settings opening
                        break;
                }
            }

            // Close the context menu after action
            _isContextMenuVisible = false;
            _isFirstDropdownVisible = false;
        }

        public void Update()
        {
            _updateCounter++;
            if (_updateCounter % 60 == 0) // Log every 60 frames (about once per second)
            {
                _engine.Log($"Desktop: Update called - Frame {_updateCounter}, Mouse: {_currentMouseState.Position}, LeftButton: {_currentMouseState.LeftButton}, Dragging: {_isDraggingFiles}");
            }
            
            _previousMouseState = _currentMouseState;
            _currentMouseState = Mouse.GetState();

            // Update modifier key states
            var keyboardState = Keyboard.GetState();
            _isCtrlPressed = keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl);
            _isShiftPressed = keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift);

            // Check if TaskBar position has changed
            if (_taskBar != null)
            {
                var taskBarPosition = _taskBar.GetCurrentPosition();
                if (taskBarPosition != _lastTaskBarPosition)
                {
                    _lastTaskBarPosition = taskBarPosition;
                    _filesLoaded = false; // Reset flag to reload files with new grid dimensions
                    _loadRetryCount = 0; // Reset retry count
                    UpdateGridDimensions();
                    _engine.Log($"Desktop: TaskBar position changed to {taskBarPosition}, updating grid");
                }
            }

            // Try to load files if not loaded yet and grid is ready
            if (!_filesLoaded && _gridCellSize > 0 && _gridStartX > 0 && _gridStartY > 0)
            {
                LoadDesktopFiles();
                _filesLoaded = true;
                _engine.Log("Desktop: Files loaded successfully");
            }
            else if (!_filesLoaded && _loadRetryCount < MAX_LOAD_RETRIES)
            {
                _loadRetryCount++;
                _engine.Log($"Desktop: Grid not ready, retry {_loadRetryCount}/{MAX_LOAD_RETRIES} - CellSize: {_gridCellSize}, StartX: {_gridStartX}, StartY: {_gridStartY}");
            }

            // Don't handle any desktop interactions if any window is being dragged or resizing
            if (IsAnyWindowBeingDragged())
            {
                return;
            }

            // Don't handle new desktop interactions if mouse is over any window (but allow ongoing drags to continue)
            if (!IsMouseOverDesktop() && !_isDraggingFiles && !_isDragging)
            {
                return;
            }

            // Don't handle clicks if TopBar has already handled them (prevents click-through)
            if (GameEngine.Instance.HasTopBarHandledClick())
            {
                _engine.Log("Desktop: TopBar handled click, skipping desktop click processing");
                return;
            }

            // Don't handle clicks if any window has already handled them (prevents window-to-desktop click-through)
            if (GameEngine.Instance.HasAnyWindowHandledClick())
            {
                _engine.Log("Desktop: Window handled click, skipping desktop click processing");
                return;
            }

            // Handle file selection and drag start
            if (_currentMouseState.LeftButton == ButtonState.Pressed && 
                _previousMouseState.LeftButton == ButtonState.Released)
            {
                _engine.Log($"Desktop: Mouse left button pressed at {_currentMouseState.Position}");
                
                // Don't process clicks if any window is being dragged
                if (IsAnyWindowBeingDragged())
                {
                    _engine.Log("Desktop: Window being dragged, ignoring click");
                    return;
                }

                // Don't process clicks if mouse is over any window
                if (!IsMouseOverDesktop())
                {
                    _engine.Log("Desktop: Mouse not over desktop, ignoring click");
                    return;
                }

                bool clickedOnFile = false;
                DesktopFile clickedFile = null;

                // First pass: find the clicked file and its cell
                foreach (var file in _desktopFiles)
                {
                    if (file.IconBounds.Contains(_currentMouseState.Position) || 
                        file.TextBounds.Contains(_currentMouseState.Position))
                    {
                        clickedFile = file;
                        clickedOnFile = true;
                        _engine.Log($"Desktop: Clicked on file: {file.Name}");
                        break;
                    }
                }

                if (clickedOnFile && clickedFile != null)
                {
                    _engine.Log($"Desktop: Processing click on file: {clickedFile.Name}");
                    
                    if (_isCtrlPressed)
                    {
                        // Ctrl+Click: Toggle selection of clicked file
                        clickedFile.IsSelected = !clickedFile.IsSelected;
                        if (clickedFile.IsSelected)
                        {
                            _lastSelectedFile = clickedFile;
                        }
                        _engine.Log($"Desktop: Ctrl+Click - File {clickedFile.Name} selected: {clickedFile.IsSelected}");
                    }
                    else if (_isShiftPressed && _lastSelectedFile != null)
                    {
                        // Shift+Click: Select all files between last selected and clicked
                        int lastIndex = _desktopFiles.IndexOf(_lastSelectedFile);
                        int currentIndex = _desktopFiles.IndexOf(clickedFile);
                        int start = Math.Min(lastIndex, currentIndex);
                        int end = Math.Max(lastIndex, currentIndex);

                        for (int i = 0; i < _desktopFiles.Count; i++)
                        {
                            _desktopFiles[i].IsSelected = i >= start && i <= end;
                        }
                        _engine.Log($"Desktop: Shift+Click - Selected files from index {start} to {end}");
                    }
                    else
                    {
                        // Normal click: Select only the clicked file
                        foreach (var file in _desktopFiles)
                        {
                            file.IsSelected = file == clickedFile;
                        }
                        _lastSelectedFile = clickedFile;
                        _engine.Log($"Desktop: Normal click - Selected only file: {clickedFile.Name}");
                    }

                    // Start dragging if the clicked file is selected
                    if (clickedFile.IsSelected)
                    {
                        _isDraggingFiles = true;
                        _dragStart = new Vector2(_currentMouseState.X, _currentMouseState.Y);
                        _dragCurrent = _dragStart;
                        _dragOffset = new Vector2(
                            _currentMouseState.X - clickedFile.Position.X,
                            _currentMouseState.Y - clickedFile.Position.Y
                        );
                        _draggedFiles = _desktopFiles.Where(f => f.IsSelected).ToList();
                        _dragPreviewPosition = clickedFile.Position;
                        _engine.Log($"Desktop: Started dragging {_draggedFiles.Count} files from position {clickedFile.Position}");
                        _engine.Log($"Desktop: Drag offset: {_dragOffset}, Dragged files: {string.Join(", ", _draggedFiles.Select(f => f.Name))}");
                    }
                    else
                    {
                        _engine.Log($"Desktop: File {clickedFile.Name} not selected, not starting drag");
                    }
                }
                else if (!clickedOnFile && IsMouseOverDesktop())
                {
                    _engine.Log("Desktop: Clicked on empty space");
                    
                    // Clear selection if clicking empty space
                    if (!_isCtrlPressed && !_isShiftPressed)
                    {
                        foreach (var file in _desktopFiles)
                        {
                            file.IsSelected = false;
                        }
                        _lastSelectedFile = null;
                        _engine.Log("Desktop: Cleared file selection");
                    }

                    // Start selection rectangle
                    _isDragging = true;
                    _dragStart = new Vector2(_currentMouseState.X, _currentMouseState.Y);
                    _dragCurrent = _dragStart;
                    _engine.Log($"Desktop: Started selection rectangle at {_dragStart}");
                }
            }
            // Update drag while mouse is down
            else if (_currentMouseState.LeftButton == ButtonState.Pressed)
            {
                if (_isDraggingFiles)
                {
                    _dragCurrent = new Vector2(_currentMouseState.X, _currentMouseState.Y);
                    _dragPreviewPosition = _dragCurrent - _dragOffset;

                    // Only check for valid drop target if mouse is over desktop
                    if (IsMouseOverDesktop())
                    {
                        // Check if drop target is valid using cursor position instead of drag preview position
                        Vector2 cursorPosition = new Vector2(_currentMouseState.X, _currentMouseState.Y);
                        _isValidDropTarget = IsValidDropTarget(cursorPosition);
                        if (_isValidDropTarget)
                        {
                            _dropTargetCell = GetCellAtPosition(cursorPosition);
                        }
                    }
                    else
                    {
                        // Mouse is over a window, so no valid drop target
                        _isValidDropTarget = false;
                    }

                    _engine.Log($"Desktop: Dragging files to {_dragPreviewPosition}, valid target: {_isValidDropTarget}, mouse at {_currentMouseState.Position}");
                    _engine.Log($"Desktop: Dragging files, cursor at {_currentMouseState.Position}, valid target: {_isValidDropTarget}");
                }
                else if (_isDragging)
                {
                    _dragCurrent = new Vector2(_currentMouseState.X, _currentMouseState.Y);
                    UpdateSelectionRectangle();

                    // Update file selection based on selection rectangle (only if mouse is over desktop)
                    if (IsMouseOverDesktop())
                    {
                        foreach (var file in _desktopFiles)
                        {
                            file.IsSelected = _selectionRectangle.Intersects(file.IconBounds) || 
                                            _selectionRectangle.Intersects(file.TextBounds);
                        }
                    }

                    _engine.Log($"Desktop: Updated selection rectangle to {_selectionRectangle}");
                }
            }
            // End dragging on mouse up
            else if (_currentMouseState.LeftButton == ButtonState.Released && 
                     _previousMouseState.LeftButton == ButtonState.Pressed)
            {
                if (_isDraggingFiles)
                {
                    // Only complete the drop if mouse is over desktop and target is valid
                    if (_isValidDropTarget && IsMouseOverDesktop())
                    {
                        // Use the actual mouse position to determine the cell, not the drag preview position
                        Vector2 mousePosition = new Vector2(_currentMouseState.X, _currentMouseState.Y);
                        Rectangle actualTargetCell = GetCellAtPosition(mousePosition);
                        
                        _engine.Log($"Desktop: Mouse position: {mousePosition}, Target cell: {actualTargetCell}");
                        
                        // Move files to new position
                        MoveFilesToCell(_draggedFiles, actualTargetCell);
                        _engine.Log($"Desktop: Dropped {_draggedFiles.Count} files at cell {actualTargetCell}");
                    }
                    else
                    {
                        _engine.Log($"Desktop: Drag cancelled - not over desktop or invalid target");
                    }
                    _isDraggingFiles = false;
                    _draggedFiles = null;
                    _isValidDropTarget = false;
                }
                else if (_isDragging)
                {
                    _isDragging = false;
                    _selectionRectangle = Rectangle.Empty;
                    _engine.Log("Desktop: Ended selection rectangle");
                }
            }

            // Handle right-click for context menu
            if (_currentMouseState.RightButton == ButtonState.Pressed && 
                _previousMouseState.RightButton == ButtonState.Released)
            {
                if (IsMouseOverDesktop())
                {
                    // Check if right-click is on a file
                    DesktopFile clickedFile = null;
                    bool clickedOnFile = false;
                    
                    foreach (var file in _desktopFiles)
                    {
                        if (file.IconBounds.Contains(_currentMouseState.Position) || 
                            file.TextBounds.Contains(_currentMouseState.Position))
                        {
                            clickedFile = file;
                            clickedOnFile = true;
                            _engine.Log($"Desktop: Right-clicked on file: {file.Name}");
                            break;
                        }
                    }

                    if (clickedOnFile && clickedFile != null)
                    {
                        // Right-clicked on a file - select it and show file context menu
                        foreach (var file in _desktopFiles)
                        {
                            file.IsSelected = file == clickedFile;
                        }
                        _lastSelectedFile = clickedFile;
                        _engine.Log($"Desktop: Selected file {clickedFile.Name} via right-click");
                        
                        SetupFileContextMenu(clickedFile);
                    }
                    else
                    {
                        // Right-clicked on empty space - clear selection and show empty cell context menu
                        if (!_isCtrlPressed && !_isShiftPressed)
                        {
                            foreach (var file in _desktopFiles)
                            {
                                file.IsSelected = false;
                            }
                            _lastSelectedFile = null;
                            _engine.Log("Desktop: Cleared file selection via right-click on empty space");
                        }
                        
                        SetupEmptyCellContextMenu();
                    }

                    _isContextMenuVisible = true;
                    
                    // Calculate the maximum width needed for the main menu
                    int maxMenuWidth = 0;
                    foreach (var item in _contextMenuItems)
                    {
                        maxMenuWidth = Math.Max(maxMenuWidth, CalculateMenuWidth(item));
                    }

                    // Calculate the maximum dropdown width needed
                    int maxDropdownWidth = 0;
                    foreach (var item in _contextMenuItems)
                    {
                        if (item.DropdownItems != null)
                        {
                            maxDropdownWidth = Math.Max(maxDropdownWidth, CalculateDropdownWidth(item));
                        }
                    }

                    // Calculate total height of the menu
                    int totalHeight = _contextMenuItems.Count * CONTEXT_MENU_ITEM_HEIGHT;

                    // Check if menu would go off the bottom edge
                    int menuY = _currentMouseState.Y;
                    if (menuY + totalHeight > _windowHeight)
                    {
                        // Position menu above the cursor
                        menuY = menuY - totalHeight;
                    }

                    // Check if dropdowns should be on the left
                    bool showDropdownsOnLeft = ShouldShowDropdownsOnLeft(new Vector2(_currentMouseState.X, menuY), maxDropdownWidth);
                    
                    // Set the context menu position
                    _contextMenuPosition = new Vector2(
                        showDropdownsOnLeft ? _currentMouseState.X - maxMenuWidth : _currentMouseState.X,
                        menuY
                    );

                    _isFirstDropdownVisible = false;
                    UpdateContextMenuBounds();
                    _engine.Log($"Desktop: Showing {(clickedOnFile ? "file" : "empty cell")} context menu at {_contextMenuPosition}");
                }
            }

            // Handle clicks on menu items (context menu closing is now handled globally in Main.cs)
            if (_currentMouseState.LeftButton == ButtonState.Pressed && 
                _previousMouseState.LeftButton == ButtonState.Released &&
                _isContextMenuVisible && _contextMenuBounds.Contains(_currentMouseState.Position))
            {
                // Don't handle clicks if TopBar has already handled them (prevents click-through)
                if (GameEngine.Instance.HasTopBarHandledClick())
                {
                    _engine.Log("Desktop: TopBar handled click, skipping context menu click processing");
                    return;
                }

                // Don't handle clicks if any window has already handled them (prevents window-to-desktop click-through)
                if (GameEngine.Instance.HasAnyWindowHandledClick())
                {
                    _engine.Log("Desktop: Window handled click, skipping context menu click processing");
                    return;
                }

                // Handle clicks on menu items
                
                // Calculate the maximum width needed for the main menu
                int maxMenuWidth = 0;
                foreach (var item in _contextMenuItems)
                {
                    maxMenuWidth = Math.Max(maxMenuWidth, CalculateMenuWidth(item));
                }

                // Calculate the maximum dropdown width needed
                int maxDropdownWidth = 0;
                foreach (var item in _contextMenuItems)
                {
                    if (item.DropdownItems != null)
                    {
                        maxDropdownWidth = Math.Max(maxDropdownWidth, CalculateDropdownWidth(item));
                    }
                }

                // Check if dropdowns should be on the left
                bool showDropdownsOnLeft = ShouldShowDropdownsOnLeft(_contextMenuPosition, maxDropdownWidth);
                
                for (int i = 0; i < _contextMenuItems.Count; i++)
                {
                    var item = _contextMenuItems[i];
                    Rectangle itemBounds = new Rectangle(
                        (int)_contextMenuPosition.X,
                        (int)_contextMenuPosition.Y + (i * CONTEXT_MENU_ITEM_HEIGHT),
                        maxMenuWidth,
                        CONTEXT_MENU_ITEM_HEIGHT
                    );
                    
                    if (itemBounds.Contains(_currentMouseState.Position))
                    {
                        // Clicked on main menu item
                        if (item.DropdownItems == null)
                        {
                            // No dropdown - execute action directly
                            HandleContextMenuAction(i);
                        }
                        // If it has dropdown, it will be handled by hover logic
                        break;
                    }

                    // Check if clicked on dropdown items
                    if (item.IsDropdownVisible && item.DropdownItems != null)
                    {
                        int dropdownWidth = CalculateDropdownWidth(item);
                        int dropdownX;
                        if (showDropdownsOnLeft)
                        {
                            dropdownX = (int)_contextMenuPosition.X - dropdownWidth;
                        }
                        else
                        {
                            dropdownX = (int)_contextMenuPosition.X + maxMenuWidth;
                        }

                        // Calculate dropdown Y position based on whether main menu is above or below cursor
                        int dropdownY;
                        if (_contextMenuBounds.Y < _contextMenuPosition.Y)
                        {
                            // If main menu is above cursor, align dropdown with its item
                            dropdownY = _contextMenuBounds.Y + (i * CONTEXT_MENU_ITEM_HEIGHT);
                        }
                        else
                        {
                            // If main menu is below cursor, use original positioning
                            dropdownY = (int)_contextMenuPosition.Y + (i * CONTEXT_MENU_ITEM_HEIGHT);
                        }

                        for (int j = 0; j < item.DropdownItems.Count; j++)
                        {
                            Rectangle dropdownBounds = new Rectangle(
                                dropdownX,
                                dropdownY + (j * CONTEXT_MENU_ITEM_HEIGHT),
                                dropdownWidth,
                                CONTEXT_MENU_ITEM_HEIGHT
                            );
                            
                            if (dropdownBounds.Contains(_currentMouseState.Position))
                            {
                                // Clicked on dropdown item - execute action
                                HandleContextMenuAction(i, j);
                                break;
                            }
                        }
                    }
                }
            }

            // Handle context menu interactions (hover effects and dropdown visibility)
            if (_isContextMenuVisible)
            {
                bool isOverAnyDropdown = false;
                bool isOverMainMenu = false;
                int hoveredMainIndex = -1;

                // Calculate the maximum width needed for the main menu
                int maxMenuWidth = 0;
                foreach (var item in _contextMenuItems)
                {
                    maxMenuWidth = Math.Max(maxMenuWidth, CalculateMenuWidth(item));
                }

                // Calculate the maximum dropdown width needed
                int maxDropdownWidth = 0;
                foreach (var item in _contextMenuItems)
                {
                    if (item.DropdownItems != null)
                    {
                        maxDropdownWidth = Math.Max(maxDropdownWidth, CalculateDropdownWidth(item));
                    }
                }

                // Check if dropdowns should be on the left
                bool showDropdownsOnLeft = ShouldShowDropdownsOnLeft(_contextMenuPosition, maxDropdownWidth);

                // First pass: check if mouse is over main menu or any dropdown
                for (int i = 0; i < _contextMenuItems.Count; i++)
                {
                    var item = _contextMenuItems[i];
                    Rectangle itemBounds = new Rectangle(
                        (int)_contextMenuPosition.X,
                        (int)_contextMenuPosition.Y + (i * CONTEXT_MENU_ITEM_HEIGHT),
                        maxMenuWidth,
                        CONTEXT_MENU_ITEM_HEIGHT
                    );
                    
                    if (itemBounds.Contains(_currentMouseState.Position))
                    {
                        isOverMainMenu = true;
                        hoveredMainIndex = i;
                        break;
                    }

                    // Check if mouse is over this item's dropdown
                    if (item.IsDropdownVisible && item.DropdownItems != null)
                    {
                        int dropdownWidth = CalculateDropdownWidth(item);
                        int dropdownX;
                        if (showDropdownsOnLeft)
                        {
                            dropdownX = (int)_contextMenuPosition.X - dropdownWidth;
                        }
                        else
                        {
                            dropdownX = (int)_contextMenuPosition.X + maxMenuWidth;
                        }

                        for (int j = 0; j < item.DropdownItems.Count; j++)
                        {
                            Rectangle dropdownBounds = new Rectangle(
                                dropdownX,
                                (int)_contextMenuPosition.Y + (i * CONTEXT_MENU_ITEM_HEIGHT) + (j * CONTEXT_MENU_ITEM_HEIGHT),
                                dropdownWidth,
                                CONTEXT_MENU_ITEM_HEIGHT
                            );
                            
                            if (dropdownBounds.Contains(_currentMouseState.Position))
                            {
                                isOverAnyDropdown = true;
                                hoveredMainIndex = i;
                                break;
                            }
                        }
                    }
                }

                // Second pass: update menu items
                for (int i = 0; i < _contextMenuItems.Count; i++)
                {
                    var item = _contextMenuItems[i];
                    Rectangle itemBounds = new Rectangle(
                        (int)_contextMenuPosition.X,
                        (int)_contextMenuPosition.Y + (i * CONTEXT_MENU_ITEM_HEIGHT),
                        maxMenuWidth,
                        CONTEXT_MENU_ITEM_HEIGHT
                    );
                    
                    // Show dropdown for hovered main menu item or if mouse is over its dropdown
                    item.IsHovered = itemBounds.Contains(_currentMouseState.Position);
                    item.IsDropdownVisible = (i == hoveredMainIndex) || (item.IsDropdownVisible && isOverAnyDropdown);

                    // Handle dropdown items if this is the hovered main menu item
                    if (item.IsDropdownVisible && item.DropdownItems != null)
                    {
                        item.DropdownBounds.Clear();
                        int dropdownWidth = CalculateDropdownWidth(item);
                        int dropdownX;
                        if (showDropdownsOnLeft)
                        {
                            dropdownX = (int)_contextMenuPosition.X - dropdownWidth;
                        }
                        else
                        {
                            dropdownX = (int)_contextMenuPosition.X + maxMenuWidth;
                        }

                        // Calculate dropdown Y position based on whether main menu is above or below cursor
                        int dropdownY;
                        if (_contextMenuBounds.Y < _contextMenuPosition.Y)
                        {
                            // If main menu is above cursor, align dropdown with its item
                            dropdownY = _contextMenuBounds.Y + (i * CONTEXT_MENU_ITEM_HEIGHT);
                        }
                        else
                        {
                            // If main menu is below cursor, use original positioning
                            dropdownY = (int)_contextMenuPosition.Y + (i * CONTEXT_MENU_ITEM_HEIGHT);
                        }

                        // Create and store dropdown bounds, and check for hover in a single pass
                        for (int j = 0; j < item.DropdownItems.Count; j++)
                        {
                            Rectangle dropdownBounds = new Rectangle(
                                dropdownX,
                                dropdownY + (j * CONTEXT_MENU_ITEM_HEIGHT),
                                dropdownWidth,
                                CONTEXT_MENU_ITEM_HEIGHT
                            );
                            item.DropdownBounds.Add(dropdownBounds);
                            
                            // Check for hover while we're creating the bounds
                            if (dropdownBounds.Contains(_currentMouseState.Position))
                            {
                                item.IsHovered = true;
                            }
                        }
                    }
                    else
                    {
                        item.DropdownBounds.Clear();
                    }
                }
            }

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
        }

        private bool IsMouseOverDesktop()
        {
            Point mousePos = _currentMouseState.Position;

            // Check if mouse is over taskbar
            if (_taskBar != null && _taskBar.GetTaskBarBounds().Contains(mousePos))
            {
                return false;
            }

            // Check if mouse is over TopBar dropdowns
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
                                if (dropdownBound.Contains(mousePos))
                                {
                                    return false;
                                }
                            }
                        }
                    }
                }
            }

            // Check if mouse is over any window
            foreach (var module in modules)
            {
                var windowManagementField = module.GetType().GetField("_windowManagement", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (windowManagementField != null)
                {
                    var windowManagement = windowManagementField.GetValue(module) as WindowManagement;
                    if (windowManagement != null && windowManagement.IsVisible() && 
                        windowManagement.GetWindowBounds().Contains(mousePos))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private bool IsAnyWindowBeingDragged()
        {
            var modules = GameEngine.Instance.GetActiveModules();
            foreach (var module in modules)
            {
                var windowManagementField = module.GetType().GetField("_windowManagement", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (windowManagementField != null)
                {
                    var windowManagement = windowManagementField.GetValue(module) as WindowManagement;
                    if (windowManagement != null && windowManagement.IsVisible() && 
                        (windowManagement.IsDragging() || windowManagement.IsResizing()))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private void UpdateSelectionRectangle()
        {
            int x = (int)Math.Min(_dragStart.X, _dragCurrent.X);
            int y = (int)Math.Min(_dragStart.Y, _dragCurrent.Y);
            int width = (int)Math.Abs(_dragCurrent.X - _dragStart.X);
            int height = (int)Math.Abs(_dragCurrent.Y - _dragStart.Y);

            _selectionRectangle = new Rectangle(x, y, width, height);
            _engine.Log($"Desktop: Updated selection rectangle to {_selectionRectangle}");
        }

        public void DrawBackground(SpriteBatch spriteBatch)
        {
            // Draw background
            spriteBatch.Draw(_pixel, new Rectangle(0, 0, _windowWidth, _windowHeight), _backgroundColor);

            // Draw files
            DrawFiles(spriteBatch);

            // Draw selection rectangle if dragging
            if (_isDragging && !_selectionRectangle.IsEmpty)
            {
                _engine.Log($"Desktop: Drawing selection rectangle {_selectionRectangle}");
                
                // Draw semi-transparent fill
                Color selectionFillColor = new Color(147, 112, 219, 50); // Semi-transparent purple
                spriteBatch.Draw(_pixel, _selectionRectangle, selectionFillColor);

                // Draw border
                Color selectionBorderColor = new Color(147, 112, 219); // Solid purple
                int borderThickness = 1;

                // Draw top border
                spriteBatch.Draw(_pixel, new Rectangle(_selectionRectangle.X, _selectionRectangle.Y, 
                    _selectionRectangle.Width, borderThickness), selectionBorderColor);
                
                // Draw bottom border
                spriteBatch.Draw(_pixel, new Rectangle(_selectionRectangle.X, _selectionRectangle.Bottom - borderThickness, 
                    _selectionRectangle.Width, borderThickness), selectionBorderColor);
                
                // Draw left border
                spriteBatch.Draw(_pixel, new Rectangle(_selectionRectangle.X, _selectionRectangle.Y, 
                    borderThickness, _selectionRectangle.Height), selectionBorderColor);
                
                // Draw right border
                spriteBatch.Draw(_pixel, new Rectangle(_selectionRectangle.Right - borderThickness, _selectionRectangle.Y, 
                    borderThickness, _selectionRectangle.Height), selectionBorderColor);
            }
        }

        public void DrawGrid(SpriteBatch spriteBatch)
        {
            if (_taskBar == null) return;
            if (!_showGrid) return;

            // Draw vertical lines
            for (int x = _gridStartX; x <= _gridEndX; x += _gridCellSize)
            {
                // Draw main grid lines (every cell)
                spriteBatch.Draw(_pixel, new Rectangle(x, _gridStartY, 1, _gridEndY - _gridStartY), GRID_LINE_COLOR);
            }

            // Draw horizontal lines
            for (int y = _gridStartY; y <= _gridEndY; y += _gridCellSize)
            {
                // Draw main grid lines (every cell)
                spriteBatch.Draw(_pixel, new Rectangle(_gridStartX, y, _gridEndX - _gridStartX, 1), GRID_LINE_COLOR);
            }

            // Draw secondary grid lines (every 4 cells) with darker color
            for (int x = _gridStartX; x <= _gridEndX; x += _gridCellSize * 4)
            {
                spriteBatch.Draw(_pixel, new Rectangle(x, _gridStartY, 1, _gridEndY - _gridStartY), GRID_LINE_COLOR_DARK);
            }

            for (int y = _gridStartY; y <= _gridEndY; y += _gridCellSize * 4)
            {
                spriteBatch.Draw(_pixel, new Rectangle(_gridStartX, y, _gridEndX - _gridStartX, 1), GRID_LINE_COLOR_DARK);
            }
        }

        public void DrawContextMenu(SpriteBatch spriteBatch)
        {
            // Draw context menu if visible
            if (_isContextMenuVisible)
            {
                // Calculate the maximum width needed for the main menu
                int maxMenuWidth = 0;
                foreach (var item in _contextMenuItems)
                {
                    maxMenuWidth = Math.Max(maxMenuWidth, CalculateMenuWidth(item));
                }

                // Calculate the maximum dropdown width needed
                int maxDropdownWidth = 0;
                foreach (var item in _contextMenuItems)
                {
                    if (item.DropdownItems != null)
                    {
                        maxDropdownWidth = Math.Max(maxDropdownWidth, CalculateDropdownWidth(item));
                    }
                }

                // Check if dropdowns should be on the left
                bool showDropdownsOnLeft = ShouldShowDropdownsOnLeft(_contextMenuPosition, maxDropdownWidth);

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
                // Top border
                spriteBatch.Draw(_pixel, new Rectangle(_contextMenuBounds.X, _contextMenuBounds.Y, 
                    _contextMenuBounds.Width, MENU_BORDER_THICKNESS), MENU_BORDER_COLOR);
                // Bottom border
                spriteBatch.Draw(_pixel, new Rectangle(_contextMenuBounds.X, _contextMenuBounds.Bottom - MENU_BORDER_THICKNESS, 
                    _contextMenuBounds.Width, MENU_BORDER_THICKNESS), MENU_BORDER_COLOR);
                // Left border
                spriteBatch.Draw(_pixel, new Rectangle(_contextMenuBounds.X, _contextMenuBounds.Y, 
                    MENU_BORDER_THICKNESS, _contextMenuBounds.Height), MENU_BORDER_COLOR);
                // Right border
                spriteBatch.Draw(_pixel, new Rectangle(_contextMenuBounds.Right - MENU_BORDER_THICKNESS, _contextMenuBounds.Y, 
                    MENU_BORDER_THICKNESS, _contextMenuBounds.Height), MENU_BORDER_COLOR);

                // Draw menu items
                for (int i = 0; i < _contextMenuItems.Count; i++)
                {
                    var item = _contextMenuItems[i];
                    Rectangle itemBounds = new Rectangle(
                        (int)_contextMenuPosition.X,
                        (int)_contextMenuPosition.Y + (i * CONTEXT_MENU_ITEM_HEIGHT),
                        maxMenuWidth,
                        CONTEXT_MENU_ITEM_HEIGHT
                    );

                    // Draw item background if hovered
                    if (item.IsHovered)
                    {
                        spriteBatch.Draw(_pixel, itemBounds, MENU_HOVER_COLOR);
                    }

                    // Draw item text
                    Vector2 textPos = new Vector2(
                        itemBounds.X + (showDropdownsOnLeft ? DROPDOWN_LEFT_PADDING : CONTEXT_MENU_PADDING),
                        itemBounds.Y + (CONTEXT_MENU_ITEM_HEIGHT - _menuFont.LineSpacing) / 2
                    );
                    spriteBatch.DrawString(_menuFont, item.Text, textPos, MENU_TEXT_COLOR);

                    // Draw dropdown arrow if item has dropdown
                    if (item.DropdownItems != null)
                    {
                        float arrowScale = 0.03f; // Make the arrow 4 times smaller
                        float rotation = showDropdownsOnLeft ? MathHelper.ToRadians(90) : MathHelper.ToRadians(270);
                        Vector2 arrowOrigin = new Vector2(_arrowTexture.Width / 2f, _arrowTexture.Height / 2f);
                        Vector2 arrowPos = new Vector2(
                            showDropdownsOnLeft ? 
                                itemBounds.X + 12 : // Move slightly into the dropdown
                                itemBounds.Right - 12, // Move slightly into the dropdown
                            itemBounds.Y + CONTEXT_MENU_ITEM_HEIGHT / 2f
                        );
                        spriteBatch.Draw(_arrowTexture, arrowPos, null, MENU_TEXT_COLOR, rotation, arrowOrigin, arrowScale, SpriteEffects.None, 0f);
                    }
                }

                // Draw all dropdowns last to ensure they're on top
                for (int i = 0; i < _contextMenuItems.Count; i++)
                {
                    var item = _contextMenuItems[i];
                    if (item.IsDropdownVisible && item.DropdownItems != null)
                    {
                        int dropdownWidth = CalculateDropdownWidth(item);
                        int dropdownX;
                        if (showDropdownsOnLeft)
                        {
                            dropdownX = (int)_contextMenuPosition.X - dropdownWidth;
                        }
                        else
                        {
                            dropdownX = (int)_contextMenuPosition.X + maxMenuWidth;
                        }

                        // Calculate dropdown Y position based on whether main menu is above or below cursor
                        int dropdownY;
                        if (_contextMenuBounds.Y < _contextMenuPosition.Y)
                        {
                            // If main menu is above cursor, align dropdown with its item
                            dropdownY = _contextMenuBounds.Y + (i * CONTEXT_MENU_ITEM_HEIGHT);
                        }
                        else
                        {
                            // If main menu is below cursor, use original positioning
                            dropdownY = (int)_contextMenuPosition.Y + (i * CONTEXT_MENU_ITEM_HEIGHT);
                        }

                        Rectangle dropdownBounds = new Rectangle(
                            dropdownX,
                            dropdownY,
                            dropdownWidth,
                            item.DropdownItems.Count * CONTEXT_MENU_ITEM_HEIGHT
                        );

                        // Draw dropdown shadow
                        Rectangle dropdownShadowBounds = new Rectangle(
                            dropdownBounds.X + MENU_SHADOW_OFFSET,
                            dropdownBounds.Y + MENU_SHADOW_OFFSET,
                            dropdownBounds.Width,
                            dropdownBounds.Height
                        );
                        spriteBatch.Draw(_pixel, dropdownShadowBounds, MENU_SHADOW_COLOR);

                        // Draw dropdown background
                        spriteBatch.Draw(_pixel, dropdownBounds, MENU_BACKGROUND_COLOR);

                        // Draw dropdown border
                        // Top border
                        spriteBatch.Draw(_pixel, new Rectangle(dropdownBounds.X, dropdownBounds.Y, 
                            dropdownBounds.Width, MENU_BORDER_THICKNESS), MENU_BORDER_COLOR);
                        // Bottom border
                        spriteBatch.Draw(_pixel, new Rectangle(dropdownBounds.X, dropdownBounds.Bottom - MENU_BORDER_THICKNESS, 
                            dropdownBounds.Width, MENU_BORDER_THICKNESS), MENU_BORDER_COLOR);
                        // Left border
                        spriteBatch.Draw(_pixel, new Rectangle(dropdownBounds.X, dropdownBounds.Y, 
                            MENU_BORDER_THICKNESS, dropdownBounds.Height), MENU_BORDER_COLOR);
                        // Right border
                        spriteBatch.Draw(_pixel, new Rectangle(dropdownBounds.Right - MENU_BORDER_THICKNESS, dropdownBounds.Y, 
                            MENU_BORDER_THICKNESS, dropdownBounds.Height), MENU_BORDER_COLOR);

                        // Draw dropdown items
                        for (int j = 0; j < item.DropdownItems.Count; j++)
                        {
                            Rectangle dropdownItemBounds = new Rectangle(
                                dropdownBounds.X,
                                dropdownBounds.Y + (j * CONTEXT_MENU_ITEM_HEIGHT),
                                dropdownWidth,
                                CONTEXT_MENU_ITEM_HEIGHT
                            );

                            // Draw item background if hovered
                            if (dropdownItemBounds.Contains(_currentMouseState.Position))
                            {
                                spriteBatch.Draw(_pixel, dropdownItemBounds, MENU_HOVER_COLOR);
                            }

                            // Draw item text
                            Vector2 dropdownTextPos = new Vector2(
                                dropdownItemBounds.X + CONTEXT_MENU_PADDING,
                                dropdownItemBounds.Y + (CONTEXT_MENU_ITEM_HEIGHT - _menuFont.LineSpacing) / 2
                            );
                            spriteBatch.DrawString(_menuFont, item.DropdownItems[j], dropdownTextPos, MENU_TEXT_COLOR);
                        }
                    }
                }
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            // Draw background
            DrawBackground(spriteBatch);

            // Draw files
            DrawFiles(spriteBatch);

            // Draw drag preview if dragging files
            if (_isDraggingFiles && _draggedFiles != null && _draggedFiles.Count > 0)
            {
                _engine.Log($"Desktop: Drawing drag preview for {_draggedFiles.Count} files");
                
                // Draw drop target indicator first (behind preview)
                if (_isValidDropTarget)
                {
                    Color targetColor = DROP_TARGET_VALID_COLOR;
                    spriteBatch.Draw(_pixel, _dropTargetCell, targetColor);

                    // Draw border
                    Color borderColor = Color.White;
                    int thickness = DROP_TARGET_HIGHLIGHT_THICKNESS;

                    // Draw top border
                    spriteBatch.Draw(_pixel, new Rectangle(_dropTargetCell.X, _dropTargetCell.Y, 
                        _dropTargetCell.Width, thickness), borderColor);
                    
                    // Draw bottom border
                    spriteBatch.Draw(_pixel, new Rectangle(_dropTargetCell.X, _dropTargetCell.Bottom - thickness, 
                        _dropTargetCell.Width, thickness), borderColor);
                    
                    // Draw left border
                    spriteBatch.Draw(_pixel, new Rectangle(_dropTargetCell.X, _dropTargetCell.Y, 
                        thickness, _dropTargetCell.Height), borderColor);
                    
                    // Draw right border
                    spriteBatch.Draw(_pixel, new Rectangle(_dropTargetCell.Right - thickness, _dropTargetCell.Y, 
                        thickness, _dropTargetCell.Height), borderColor);
                }

                // Draw cursor-following half-transparent icon
                Vector2 cursorIconPosition = new Vector2(_currentMouseState.X - CURSOR_ICON_SIZE / 2, _currentMouseState.Y - CURSOR_ICON_SIZE / 2);
                
                _engine.Log($"Desktop: Drawing cursor icon at {cursorIconPosition} for file {_draggedFiles[0].Name}");
                
                // Draw shadow/outline for the cursor icon
                Color cursorShadowColor = new Color(0, 0, 0, 150);
                Rectangle cursorShadowBounds = new Rectangle(
                    (int)cursorIconPosition.X - 2,
                    (int)cursorIconPosition.Y - 2,
                    CURSOR_ICON_SIZE + 4,
                    CURSOR_ICON_SIZE + 4
                );
                spriteBatch.Draw(_pixel, cursorShadowBounds, cursorShadowColor);

                // Draw the main dragged file icon at cursor position with half transparency
                Color cursorIconColor = Color.White * CURSOR_ICON_ALPHA;
                Rectangle cursorIconBounds = new Rectangle(
                    (int)cursorIconPosition.X,
                    (int)cursorIconPosition.Y,
                    CURSOR_ICON_SIZE,
                    CURSOR_ICON_SIZE
                );
                
                // Use the first dragged file's icon for the cursor icon
                if (_draggedFiles.Count > 0 && _draggedFiles[0].Icon != null)
                {
                    spriteBatch.Draw(_draggedFiles[0].Icon, cursorIconBounds, cursorIconColor);
                    _engine.Log($"Desktop: Successfully drew cursor icon");
                }
                else
                {
                    // Fallback: draw a colored rectangle if icon is null
                    Color fallbackColor = new Color(147, 112, 219, 200); // Purple with high alpha
                    spriteBatch.Draw(_pixel, cursorIconBounds, fallbackColor);
                    
                    // Draw text indicating number of files being dragged
                    string dragText = _draggedFiles.Count > 1 ? $"{_draggedFiles.Count} files" : "1 file";
                    Vector2 textPos = new Vector2(
                        cursorIconPosition.X + CURSOR_ICON_SIZE / 2 - _desktopFont.MeasureString(dragText).X / 2,
                        cursorIconPosition.Y + CURSOR_ICON_SIZE / 2 - _desktopFont.LineSpacing / 2
                    );
                    spriteBatch.DrawString(_desktopFont, dragText, textPos, Color.White);
                    
                    _engine.Log($"Desktop: Drew fallback cursor indicator for {_draggedFiles.Count} files");
                }

                // Draw additional preview for multiple files (smaller icons following the cursor)
                if (_draggedFiles.Count > 1)
                {
                    for (int i = 1; i < _draggedFiles.Count && i < 4; i++) // Show up to 4 files total
                    {
                        var file = _draggedFiles[i];
                        Vector2 offsetPos = cursorIconPosition + new Vector2(i * 15, i * 15);
                        
                        // Draw smaller icon
                        Rectangle smallIconBounds = new Rectangle(
                            (int)offsetPos.X,
                            (int)offsetPos.Y,
                            CURSOR_ICON_SIZE / 2,
                            CURSOR_ICON_SIZE / 2
                        );
                        
                        if (file.Icon != null)
                        {
                            spriteBatch.Draw(file.Icon, smallIconBounds, cursorIconColor);
                        }
                    }
                }
            }

            // Draw context menu last to ensure it's on top
            DrawContextMenu(spriteBatch);
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
                
                // Only highlight the desktop grid area (excluding TopBar and TaskBar)
                // Top border (at grid start Y)
                spriteBatch.Draw(_pixel, new Rectangle(_gridStartX, _gridStartY, _gridEndX - _gridStartX, borderThickness), highlightColor);
                // Bottom border (at grid end Y)
                spriteBatch.Draw(_pixel, new Rectangle(_gridStartX, _gridEndY - borderThickness, _gridEndX - _gridStartX, borderThickness), highlightColor);
                // Left border (at grid start X)
                spriteBatch.Draw(_pixel, new Rectangle(_gridStartX, _gridStartY, borderThickness, _gridEndY - _gridStartY), highlightColor);
                // Right border (at grid end X)
                spriteBatch.Draw(_pixel, new Rectangle(_gridEndX - borderThickness, _gridStartY, borderThickness, _gridEndY - _gridStartY), highlightColor);
            }
        }

        public void UpdateWindowWidth(int newWidth)
        {
            _windowWidth = newWidth;
            _windowHeight = _graphicsDevice.Viewport.Height;
            UpdateGridDimensions();
        }

        public void LoadContent(ContentManager content)
        {
            _arrowTexture = content.Load<Texture2D>("Modules/Desktop_essential/arrow_down");
            _desktopFont = content.Load<SpriteFont>("Fonts/SpriteFonts/desktop_font");
            _arialFont = content.Load<SpriteFont>("Fonts/SpriteFonts/roboto_font");

            // Load all file icons
            _fileIcons["txt"] = content.Load<Texture2D>("Logos/text_file_icon");
            _fileIcons["cs"] = content.Load<Texture2D>("Logos/c-sharp_file_icon");
            _fileIcons["py"] = content.Load<Texture2D>("Logos/python_file_icon");
            _fileIcons["js"] = content.Load<Texture2D>("Logos/javascript_file_icon");
            _fileIcons["json"] = content.Load<Texture2D>("Logos/json_file_icon");
            _fileIcons["md"] = content.Load<Texture2D>("Logos/markdown_file_icon");
            _fileIcons["sh"] = content.Load<Texture2D>("Logos/shell_script_file_icon");
            _fileIcons["bat"] = content.Load<Texture2D>("Logos/shell_script_file_icon");
            _fileIcons["cmd"] = content.Load<Texture2D>("Logos/shell_script_file_icon");
            _fileIcons["ps1"] = content.Load<Texture2D>("Logos/shell_script_file_icon");
            _fileIcons["config"] = content.Load<Texture2D>("Logos/config_file_icon");
            _fileIcons["ini"] = content.Load<Texture2D>("Logos/config_file_icon");
            _fileIcons["xml"] = content.Load<Texture2D>("Logos/config_file_icon");
            _fileIcons["yaml"] = content.Load<Texture2D>("Logos/config_file_icon");
            _fileIcons["yml"] = content.Load<Texture2D>("Logos/config_file_icon");
            _fileIcons["ttf"] = content.Load<Texture2D>("Logos/font_file_icon");
            _fileIcons["otf"] = content.Load<Texture2D>("Logos/font_file_icon");
            _fileIcons["woff"] = content.Load<Texture2D>("Logos/font_file_icon");
            _fileIcons["woff2"] = content.Load<Texture2D>("Logos/font_file_icon");
            _fileIcons["png"] = content.Load<Texture2D>("Logos/image_file_icon");
            _fileIcons["jpg"] = content.Load<Texture2D>("Logos/image_file_icon");
            _fileIcons["jpeg"] = content.Load<Texture2D>("Logos/image_file_icon");
            _fileIcons["gif"] = content.Load<Texture2D>("Logos/image_file_icon");
            _fileIcons["bmp"] = content.Load<Texture2D>("Logos/image_file_icon");
            _fileIcons["ico"] = content.Load<Texture2D>("Logos/ico_file_icon");
            _fileIcons["mp4"] = content.Load<Texture2D>("Logos/video_file_icon");
            _fileIcons["avi"] = content.Load<Texture2D>("Logos/video_file_icon");
            _fileIcons["mkv"] = content.Load<Texture2D>("Logos/video_file_icon");
            _fileIcons["mov"] = content.Load<Texture2D>("Logos/video_file_icon");
            _fileIcons["wmv"] = content.Load<Texture2D>("Logos/video_file_icon");
            _fileIcons["mp3"] = content.Load<Texture2D>("Logos/audio_file_icon");
            _fileIcons["wav"] = content.Load<Texture2D>("Logos/audio_file_icon");
            _fileIcons["ogg"] = content.Load<Texture2D>("Logos/audio_file_icon");
            _fileIcons["flac"] = content.Load<Texture2D>("Logos/audio_file_icon");
            _fileIcons["pdf"] = content.Load<Texture2D>("Logos/pdf_file_icon");
            _fileIcons["xlsx"] = content.Load<Texture2D>("Logos/excel_file_icon");
            _fileIcons["xls"] = content.Load<Texture2D>("Logos/excel_file_icon");
            _fileIcons["csv"] = content.Load<Texture2D>("Logos/excel_file_icon");
            _fileIcons["pptx"] = content.Load<Texture2D>("Logos/presentation_file_icon");
            _fileIcons["ppt"] = content.Load<Texture2D>("Logos/presentation_file_icon");
            _fileIcons["zip"] = content.Load<Texture2D>("Logos/zip_archive_file_icon");
            _fileIcons["rar"] = content.Load<Texture2D>("Logos/rar_archive_file_icon");
            _fileIcons["7z"] = content.Load<Texture2D>("Logos/zip_archive_file_icon");
            _fileIcons["tar"] = content.Load<Texture2D>("Logos/zip_archive_file_icon");
            _fileIcons["gz"] = content.Load<Texture2D>("Logos/zip_archive_file_icon");

            LoadDesktopFiles();
        }

        public void Dispose()
        {
            _pixel?.Dispose();
            _arrowTexture?.Dispose();
            _fileWatcher?.Dispose();
        }

        // Public method to get/set background color
        public Color GetBackgroundColor()
        {
            return _backgroundColor;
        }

        public void SetBackgroundColor(Color color)
        {
            _backgroundColor = color;
        }

        public void CloseContextMenuIfOutside(Point mousePosition)
        {
            if (_isContextMenuVisible && !_contextMenuBounds.Contains(mousePosition))
            {
                _isContextMenuVisible = false;
                _isFirstDropdownVisible = false;
                _engine.Log("Desktop: Closing context menu from global click");
            }
        }

        private void UpdateContextMenuBounds()
        {
            // Calculate the maximum width needed for the main menu
            int maxMenuWidth = 0;
            foreach (var item in _contextMenuItems)
            {
                maxMenuWidth = Math.Max(maxMenuWidth, CalculateMenuWidth(item));
            }

            int totalHeight = _contextMenuItems.Count * CONTEXT_MENU_ITEM_HEIGHT;
            
            // Calculate initial position
            int menuX = (int)_contextMenuPosition.X;
            int menuY = (int)_contextMenuPosition.Y;

            // Check if menu would go off the right edge
            if (menuX + maxMenuWidth > _windowWidth)
            {
                menuX = _windowWidth - maxMenuWidth;
            }

            // Check if menu would go off the bottom edge
            if (menuY + totalHeight > _windowHeight)
            {
                // Position menu above the cursor
                menuY = menuY - totalHeight;
            }

            _contextMenuBounds = new Rectangle(
                menuX,
                menuY,
                maxMenuWidth,
                totalHeight
            );
        }

        private void UpdateGridDimensions()
        {
            if (_taskBar == null) return;

            // Get taskbar position and bounds
            var taskBarPosition = _taskBar.GetCurrentPosition();
            var taskBarBounds = _taskBar.GetTaskBarBounds();

            // Calculate available area based on taskbar position
            switch (taskBarPosition)
            {
                case TaskBarPosition.Left:
                    _gridStartX = TASK_BAR_SIZE;
                    _gridStartY = TOP_BAR_HEIGHT;
                    _gridEndX = _windowWidth;
                    _gridEndY = _windowHeight;
                    break;
                case TaskBarPosition.Right:
                    _gridStartX = 0;
                    _gridStartY = TOP_BAR_HEIGHT;
                    _gridEndX = _windowWidth - TASK_BAR_SIZE;
                    _gridEndY = _windowHeight;
                    break;
                case TaskBarPosition.Top:
                    _gridStartX = 0;
                    _gridStartY = TOP_BAR_HEIGHT + TASK_BAR_SIZE;
                    _gridEndX = _windowWidth;
                    _gridEndY = _windowHeight;
                    break;
                case TaskBarPosition.Bottom:
                    _gridStartX = 0;
                    _gridStartY = TOP_BAR_HEIGHT;
                    _gridEndX = _windowWidth;
                    _gridEndY = _windowHeight - TASK_BAR_SIZE;
                    break;
            }

            // Calculate available space
            int availableWidth = _gridEndX - _gridStartX;
            int availableHeight = _gridEndY - _gridStartY;

            // Calculate optimal cell size based on both dimensions
            // We want to maximize the number of cells while maintaining minimum cell size
            int maxCellsByWidth = availableWidth / MIN_CELL_SIZE;
            int maxCellsByHeight = availableHeight / MIN_CELL_SIZE;
            
            // Calculate cell size that would fit the available space optimally
            float cellSizeByWidth = (float)availableWidth / maxCellsByWidth;
            float cellSizeByHeight = (float)availableHeight / maxCellsByHeight;
            
            // Use the smaller cell size to ensure we fit in both dimensions
            _gridCellSize = (int)Math.Min(cellSizeByWidth, cellSizeByHeight);
            
            // Ensure minimum cell size
            _gridCellSize = Math.Max(_gridCellSize, MIN_CELL_SIZE);
            
            // Calculate number of columns and rows based on the determined cell size
                _gridColumns = Math.Max(1, availableWidth / _gridCellSize);
                _gridRows = Math.Max(1, availableHeight / _gridCellSize);

            // Recalculate grid end positions to ensure perfect fit
            _gridEndX = _gridStartX + (_gridColumns * _gridCellSize);
            _gridEndY = _gridStartY + (_gridRows * _gridCellSize);

            // Update icon size based on cell size
            _currentIconSize = (int)(_gridCellSize * ICON_SIZE_RATIO);

            _engine.Log($"Desktop: Updated grid dimensions - Position: {taskBarPosition}, Cell Size: {_gridCellSize}, Columns: {_gridColumns}, Rows: {_gridRows}, Start: ({_gridStartX}, {_gridStartY}), End: ({_gridEndX}, {_gridEndY})");
            _engine.Log($"Desktop: Available space - Width: {availableWidth}, Height: {availableHeight}");

            // Reposition files if they exist and grid has changed
            if (_desktopFiles.Count > 0)
            {
                RepositionFilesForNewGrid();
            }
        }

        private void RepositionFilesForNewGrid()
        {
            _engine.Log($"Desktop: Repositioning {_desktopFiles.Count} files for new grid ({_gridColumns}x{_gridRows})");
            
            // Load the original saved cell positions
            var savedCellPositions = LoadDesktopFileCellPositions();
            var usedCells = new HashSet<(int, int)>();
            var repositionedFiles = new List<DesktopFile>();

            _engine.Log($"Desktop: Loaded {savedCellPositions.Count} saved cell positions");

            // First pass: try to place files in their original saved cell positions if they fit
            foreach (var file in _desktopFiles)
            {
                string normalizedFilePath = Path.GetFullPath(file.Path);
                _engine.Log($"Desktop: Processing file {file.Name} with path {normalizedFilePath}");
                
                if (savedCellPositions.ContainsKey(normalizedFilePath))
                {
                    // Get the original saved cell position
                    var (originalRow, originalColumn) = savedCellPositions[normalizedFilePath];
                    _engine.Log($"Desktop: File {file.Name} has saved position Row {originalRow}, Column {originalColumn}");
                    
                    // Ensure cell coordinates are within bounds
                    int column = Math.Max(0, Math.Min(originalColumn, _gridColumns - 1));
                    int row = Math.Max(0, Math.Min(originalRow, _gridRows - 1));
                    
                    _engine.Log($"Desktop: File {file.Name} adjusted to Row {row}, Column {column} (within bounds)");
                    
                    // Check if this cell is available
                    if (!usedCells.Contains((column, row)))
                    {
                        // File can stay in its original cell position
                        usedCells.Add((column, row));
                        
                        // Calculate the new pixel position for this cell
                        Vector2 newPosition = new Vector2(
                            _gridStartX + (column * _gridCellSize) + (_gridCellSize - _currentIconSize) / 2,
                            _gridStartY + (row * _gridCellSize) + (_gridCellSize - _currentIconSize) / 2 + ICON_TOP_OFFSET
                        );
                        
                        // Update file position and bounds
                        file.Position = newPosition;
                        file.IconBounds = new Rectangle(
                            (int)newPosition.X,
                            (int)newPosition.Y,
                            _currentIconSize,
                            _currentIconSize
                        );
                        file.TextBounds = new Rectangle(
                            (int)newPosition.X - FILE_NAME_PADDING,
                            (int)newPosition.Y + _currentIconSize + TEXT_TOP_OFFSET,
                            _currentIconSize + (FILE_NAME_PADDING * 2),
                            (int)_desktopFont.MeasureString(file.Name).Y
                        );
                        
                        _engine.Log($"Desktop: File {file.Name} stays at Row {row}, Column {column}");
                    }
                    else
                    {
                        // Cell is occupied, need to find new position
                        _engine.Log($"Desktop: File {file.Name} needs repositioning - cell Row {row}, Column {column} is occupied");
                        repositionedFiles.Add(file);
                    }
                }
                else
                {
                    // File doesn't have a saved position, needs repositioning
                    _engine.Log($"Desktop: File {file.Name} needs repositioning - no saved position");
                    repositionedFiles.Add(file);
                }
            }

            _engine.Log($"Desktop: Files needing repositioning: {repositionedFiles.Count}");

            // Second pass: reposition files that couldn't stay in their original positions
            foreach (var file in repositionedFiles)
            {
                // Get the original target cell position for this file
                string normalizedFilePath = Path.GetFullPath(file.Path);
                (int targetRow, int targetColumn) targetCell = (0, 0);
                
                if (savedCellPositions.ContainsKey(normalizedFilePath))
                {
                    var (originalRow, originalColumn) = savedCellPositions[normalizedFilePath];
                    targetCell = (originalRow, originalColumn);
                    _engine.Log($"Desktop: Looking for closest cell to Row {targetCell.targetRow}, Column {targetCell.targetColumn} for {file.Name}");
                }
                else
                {
                    _engine.Log($"Desktop: No saved position for {file.Name}, using default target (0,0)");
                }
                
                var newPosition = FindClosestAvailableCell(usedCells, targetCell);
                int newColumn = (int)((newPosition.X - _gridStartX) / _gridCellSize);
                int newRow = (int)((newPosition.Y - _gridStartY) / _gridCellSize);
                
                // Update file position and bounds
                file.Position = newPosition;
                file.IconBounds = new Rectangle(
                    (int)newPosition.X,
                    (int)newPosition.Y,
                    _currentIconSize,
                    _currentIconSize
                );
                file.TextBounds = new Rectangle(
                    (int)newPosition.X - FILE_NAME_PADDING,
                    (int)newPosition.Y + _currentIconSize + TEXT_TOP_OFFSET,
                    _currentIconSize + (FILE_NAME_PADDING * 2),
                    (int)_desktopFont.MeasureString(file.Name).Y
                );
                
                usedCells.Add((newColumn, newRow));
                _engine.Log($"Desktop: Repositioned {file.Name} to Row {newRow}, Column {newColumn} (target was Row {targetCell.targetRow}, Column {targetCell.targetColumn})");
            }
            
            _engine.Log($"Desktop: Repositioned {repositionedFiles.Count} files for new grid");
        }

        private void LoadDesktopFiles(bool savePositions = true)
        {
            _desktopFiles.Clear();
            string desktopPath = Path.Combine(Directory.GetCurrentDirectory(), "Content", "Desktop");
            
            if (!Directory.Exists(desktopPath))
            {
                Directory.CreateDirectory(desktopPath);
                return;
            }

            // Ensure grid dimensions are set before loading positions
            if (_gridCellSize == 0 || _gridStartX == 0 || _gridStartY == 0)
            {
                _engine.Log($"Desktop: Grid dimensions not set - CellSize: {_gridCellSize}, StartX: {_gridStartX}, StartY: {_gridStartY}");
                return;
            }

            _engine.Log($"Desktop: Grid dimensions OK - CellSize: {_gridCellSize}, StartX: {_gridStartX}, StartY: {_gridStartY}");

            // Load saved positions first
            var savedPositions = LoadDesktopFilePositions();
            var usedCells = new HashSet<(int, int)>(); // Track used cells to prevent conflicts

            // Get all files in the directory
            string[] files = Directory.GetFiles(desktopPath);
            _engine.Log($"Desktop: Found {files.Length} files in directory");
            
            // Process each file in the directory
            for (int i = 0; i < files.Length; i++)
            {
                string filePath = files[i];
                string fileName = Path.GetFileName(filePath);
                
                // Skip the desktop_positions.json file - it should not appear on desktop
                if (fileName == "desktop_positions.json")
                {
                    _engine.Log($"Desktop: Skipping {fileName} (positions file)");
                    continue;
                }
                
                string extension = Path.GetExtension(filePath).TrimStart('.').ToLower();
                
                // Check if this file has a saved position
                Vector2 position;
                string normalizedFilePath = Path.GetFullPath(filePath);
                _engine.Log($"Desktop: Checking file {fileName} with normalized path: {normalizedFilePath}");
                
                if (savedPositions.ContainsKey(normalizedFilePath))
                {
                    position = savedPositions[normalizedFilePath];
                    
                    // Check if this position conflicts with already loaded files
                    int column = (int)((position.X - _gridStartX) / _gridCellSize);
                    int row = (int)((position.Y - _gridStartY) / _gridCellSize);
                    column = Math.Max(0, Math.Min(column, _gridColumns - 1));
                    row = Math.Max(0, Math.Min(row, _gridRows - 1));
                    
                    if (usedCells.Contains((column, row)))
                    {
                        _engine.Log($"Desktop: Warning - Cell Row {row}, Column {column} already used, finding new position for {fileName}");
                        // Find a new available cell
                        var newPosition = FindFirstAvailableCell(usedCells);
                        column = (int)((newPosition.X - _gridStartX) / _gridCellSize);
                        row = (int)((newPosition.Y - _gridStartY) / _gridCellSize);
                        position = newPosition;
                    }
                    else
                    {
                        usedCells.Add((column, row));
                    }
                    
                    _engine.Log($"Desktop: ✓ Found saved position for {fileName}: {position}");
                }
                else
                {
                    // This is a new file, find the first available cell
                    position = FindFirstAvailableCell(usedCells);
                    
                    // Mark this cell as used
                    int column = (int)((position.X - _gridStartX) / _gridCellSize);
                    int row = (int)((position.Y - _gridStartY) / _gridCellSize);
                    column = Math.Max(0, Math.Min(column, _gridColumns - 1));
                    row = Math.Max(0, Math.Min(row, _gridRows - 1));
                    usedCells.Add((column, row));
                    
                    _engine.Log($"Desktop: ✗ File {fileName} not found in saved positions, using new position: {position}");
                    _engine.Log($"Desktop: Looking for path: {normalizedFilePath}");
                    _engine.Log($"Desktop: Available saved paths:");
                    foreach (var savedPath in savedPositions.Keys)
                    {
                        _engine.Log($"Desktop:   - {savedPath}");
                    }
                }
                
                // Get the appropriate icon for the file extension
                Texture2D icon = _fileIcons.ContainsKey(extension) ? _fileIcons[extension] : _fileIcons["txt"];

                var newFile = new DesktopFile
                {
                    Name = fileName,
                    Path = filePath,
                    Position = position,
                    IconBounds = new Rectangle(
                        (int)position.X,
                        (int)position.Y,
                        _currentIconSize,
                        _currentIconSize
                    ),
                    TextBounds = new Rectangle(
                        (int)position.X - FILE_NAME_PADDING,
                        (int)position.Y + _currentIconSize + TEXT_TOP_OFFSET,
                        _currentIconSize + (FILE_NAME_PADDING * 2),
                        (int)_desktopFont.MeasureString(fileName).Y
                    ),
                    IsSelected = false,
                    Icon = icon
                };

                _desktopFiles.Add(newFile);
                _engine.Log($"Desktop: Added file {fileName} at position {position}");
            }
            
            // Save the updated positions only if requested (not when repositioning due to window resize)
            if (savePositions)
            {
            SaveDesktopFilePositions();
            }
            _engine.Log($"Desktop: Loaded {_desktopFiles.Count} files");
        }

        private void DrawFiles(SpriteBatch spriteBatch)
        {
            foreach (var file in _desktopFiles)
            {
                // Draw file icon
                spriteBatch.Draw(file.Icon, file.IconBounds, Color.White);

                // Draw file name with word wrapping if needed
                string fileName = file.Name;
                string extension = Path.GetExtension(file.Name); // includes the dot
                string nameWithoutExt = Path.GetFileNameWithoutExtension(file.Name);
                
                // Calculate text layout
                float lineHeight = _desktopFont.LineSpacing;
                float totalHeight = lineHeight * 2; // Space for two lines
                float iconBottom = file.IconBounds.Bottom;
                float textStartY = iconBottom - 8; // Reduced gap between icon and text (was -16)
                float textCenterY = textStartY + (totalHeight / 2); // Center point between two potential lines

                // Split name into main part and extension
                extension = extension.TrimStart('.'); // Remove the dot
                bool needsTruncation = nameWithoutExt.Length > 9;

                // Calculate widths for layout decisions
                float nameWidth = _desktopFont.MeasureString(nameWithoutExt).X;
                float dotsWidth = needsTruncation ? _arialFont.MeasureString("...").X : 0;
                float dotWidth = _arialFont.MeasureString(".").X;
                float extWidth = _desktopFont.MeasureString(extension).X;
                float totalWidth = nameWidth + dotsWidth + dotWidth + extWidth;

                // Decide if we need two lines - check if filename part alone fits on one line
                bool useTwoLines = needsTruncation || nameWidth > file.IconBounds.Width * 0.9f;

                if (useTwoLines)
                {
                    // First line: name (truncated if needed)
                    string displayName = nameWithoutExt;
                    if (needsTruncation)
                    {
                        displayName = nameWithoutExt.Substring(0, 9);
                    }

                    // Calculate total width for first line (including dots if needed)
                    float displayNameWidth = _desktopFont.MeasureString(displayName).X;
                    float totalFirstLineWidth = displayNameWidth + dotsWidth;

                    // Draw main part on first line
                    Vector2 mainTextPos = new Vector2(
                        file.IconBounds.X + (file.IconBounds.Width - totalFirstLineWidth) / 2,
                        textStartY
                    );
                    spriteBatch.DrawString(_desktopFont, displayName, mainTextPos, Color.White);

                    // Draw dots if needed
                    if (needsTruncation)
                    {
                        Vector2 dotsPos = new Vector2(
                            mainTextPos.X + displayNameWidth,
                            mainTextPos.Y - (_arialFont.LineSpacing - _desktopFont.LineSpacing)
                        );
                        spriteBatch.DrawString(_arialFont, "...", dotsPos, Color.White);
                    }

                    // Draw extension on second line with dot
                    float totalSecondLineWidth = dotWidth + extWidth;
                    float secondLineY = textStartY + lineHeight;
                    
                    // Draw the dot in Arial, aligned with the extension
                    Vector2 dotPos = new Vector2(
                        file.IconBounds.X + (file.IconBounds.Width - totalSecondLineWidth) / 2,
                        secondLineY + (_desktopFont.LineSpacing - _arialFont.LineSpacing) // Increased offset to move dot higher
                    );
                    spriteBatch.DrawString(_arialFont, ".", dotPos, Color.White);

                    // Then draw the extension in desktop font
                    Vector2 extTextPos = new Vector2(
                        dotPos.X + dotWidth,
                        secondLineY
                    );
                    spriteBatch.DrawString(_desktopFont, extension, extTextPos, Color.White);
                }
                else
                {
                    // Single line - center vertically between the two potential lines
                    float singleLineY = textCenterY - (lineHeight / 2);
                    
                    // Draw the filename
                    Vector2 namePos = new Vector2(
                        file.IconBounds.X + (file.IconBounds.Width - totalWidth) / 2,
                        singleLineY
                    );
                    spriteBatch.DrawString(_desktopFont, nameWithoutExt, namePos, Color.White);

                    // Draw the dot in Arial, aligned with the extension
                    Vector2 dotPos = new Vector2(
                        namePos.X + nameWidth,
                        singleLineY + (_desktopFont.LineSpacing - _arialFont.LineSpacing) // Increased offset to move dot higher
                    );
                    spriteBatch.DrawString(_arialFont, ".", dotPos, Color.White);

                    // Then draw the extension in desktop font
                    Vector2 extTextPos = new Vector2(
                        dotPos.X + dotWidth,
                        singleLineY
                    );
                    spriteBatch.DrawString(_desktopFont, extension, extTextPos, Color.White);
                }

                // Draw selection highlight if selected
                if (file.IsSelected)
                {
                    Color highlightColor = new Color(147, 112, 219, 40); // Transparent purple overlay
                    spriteBatch.Draw(_pixel, file.IconBounds, highlightColor);
                }
            }
        }

        private bool IsValidDropTarget(Vector2 position)
        {
            // Check if position is within grid bounds
            if (position.X < _gridStartX || position.X > _gridEndX ||
                position.Y < _gridStartY || position.Y > _gridEndY)
            {
                return false;
            }

            // Get the cell at the position
            Rectangle targetCell = GetCellAtPosition(position);

            // Check if the cell is already occupied
            foreach (var file in _desktopFiles)
            {
                if (!_draggedFiles.Contains(file) && // Don't check against dragged files
                    file.IconBounds.Intersects(targetCell))
                {
                    return false;
                }
            }

            return true;
        }

        private Rectangle GetCellAtPosition(Vector2 position)
        {
            // Calculate cell coordinates based on grid
            int column = (int)((position.X - _gridStartX) / _gridCellSize);
            int row = (int)((position.Y - _gridStartY) / _gridCellSize);

            // Ensure cell coordinates are within bounds
            column = Math.Max(0, Math.Min(column, _gridColumns - 1));
            row = Math.Max(0, Math.Min(row, _gridRows - 1));

            // Calculate cell bounds
            Rectangle cell = new Rectangle(
                _gridStartX + (column * _gridCellSize),
                _gridStartY + (row * _gridCellSize),
                _gridCellSize,
                _gridCellSize
            );

            _engine.Log($"Desktop: Position {position} maps to cell Row {row}, Column {column} = {cell}");
            return cell;
        }

        private void MoveFilesToCell(List<DesktopFile> files, Rectangle targetCell)
        {
            try
            {
                // Center the files within the entire cell
                Vector2 targetPosition = new Vector2(
                    targetCell.X + (_gridCellSize - _currentIconSize) / 2,
                    targetCell.Y + (_gridCellSize - _currentIconSize) / 2 + ICON_TOP_OFFSET
                );

                _engine.Log($"Desktop: Target cell: {targetCell}, Calculated position: {targetPosition}");

                // Update file positions
                foreach (var file in files)
                {
                    file.Position = targetPosition;
                    file.IconBounds = new Rectangle(
                        (int)targetPosition.X,
                        (int)targetPosition.Y,
                        _currentIconSize,
                        _currentIconSize
                    );
                    file.TextBounds = new Rectangle(
                        (int)targetPosition.X - FILE_NAME_PADDING,
                        (int)targetPosition.Y + _currentIconSize + TEXT_TOP_OFFSET,
                        _currentIconSize + (FILE_NAME_PADDING * 2),
                        (int)_desktopFont.MeasureString(file.Name).Y
                    );
                    
                    _engine.Log($"Desktop: Moved {file.Name} to position {targetPosition}");
                }

                // Save the new positions to disk immediately
                SaveDesktopFilePositions();
                _engine.Log($"Desktop: Moved {files.Count} files to cell center at {targetPosition}");
            }
            catch (Exception ex)
            {
                _engine.Log($"Desktop: Error moving files: {ex.Message}");
            }
        }

        private void SaveDesktopFilePositions()
        {
            try
            {
                string desktopEssentialPath = Path.Combine(Directory.GetCurrentDirectory(), "Content", "Modules", "Desktop_essential");
                string positionsFile = Path.Combine(desktopEssentialPath, "desktop_positions.json");

                // Create a list of positions with cell coordinates
                var positions = new List<object>();
                var usedCells = new HashSet<(int, int)>(); // Track used cells to prevent duplicates

                foreach (var file in _desktopFiles)
                {
                    // Calculate cell coordinates from pixel position
                    int column = (int)((file.Position.X - _gridStartX) / _gridCellSize);
                    int row = (int)((file.Position.Y - _gridStartY) / _gridCellSize);
                    
                    // Ensure cell coordinates are within bounds
                    column = Math.Max(0, Math.Min(column, _gridColumns - 1));
                    row = Math.Max(0, Math.Min(row, _gridRows - 1));
                    
                    // Check if this cell is already used
                    if (usedCells.Contains((column, row)))
                    {
                        _engine.Log($"Desktop: Warning - Cell Row {row}, Column {column} already used, finding new position for {file.Name}");
                        // Find a new available cell
                        var newPosition = FindFirstAvailableCell(usedCells);
                        column = (int)((newPosition.X - _gridStartX) / _gridCellSize);
                        row = (int)((newPosition.Y - _gridStartY) / _gridCellSize);
                        file.Position = newPosition;
                    }
                    
                    usedCells.Add((column, row));
                    
                    positions.Add(new
                    {
                        Path = file.Path,
                        Cell = new { Row = row, Column = column }
                    });
                    
                    _engine.Log($"Desktop: Saving {file.Name} at cell Row {row}, Column {column}");
                }

                string json = System.Text.Json.JsonSerializer.Serialize(positions, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(positionsFile, json);
                _engine.Log($"Desktop: Saved {positions.Count} file positions to {positionsFile}");
            }
            catch (Exception ex)
            {
                _engine.Log($"Desktop: Error saving file positions: {ex.Message}");
            }
        }

        private Dictionary<string, Vector2> LoadDesktopFilePositions()
        {
            try
            {
                string desktopEssentialPath = Path.Combine(Directory.GetCurrentDirectory(), "Content", "Modules", "Desktop_essential");
                string positionsFile = Path.Combine(desktopEssentialPath, "desktop_positions.json");

                _engine.Log($"Desktop: Attempting to load positions from {positionsFile}");
                _engine.Log($"Desktop: Current grid dimensions - StartX: {_gridStartX}, StartY: {_gridStartY}, CellSize: {_gridCellSize}");

                if (File.Exists(positionsFile))
                {
                    string json = File.ReadAllText(positionsFile);
                    var positions = System.Text.Json.JsonSerializer.Deserialize<List<System.Text.Json.JsonElement>>(json);

                    var savedPositions = new Dictionary<string, Vector2>();
                    foreach (var pos in positions)
                    {
                        string filePath = pos.GetProperty("Path").GetString();
                        int column = pos.GetProperty("Cell").GetProperty("Column").GetInt32();
                        int row = pos.GetProperty("Cell").GetProperty("Row").GetInt32();
                        
                        // Convert row/column coordinates back to pixel coordinates
                        // Use the same calculation as in MoveFilesToCell and FindFirstAvailableCell
                        Vector2 pixelPosition = new Vector2(
                            _gridStartX + (column * _gridCellSize) + (_gridCellSize - _currentIconSize) / 2,
                            _gridStartY + (row * _gridCellSize) + (_gridCellSize - _currentIconSize) / 2 + ICON_TOP_OFFSET
                        );
                        
                        // Normalize the path to ensure consistent comparison
                        string normalizedPath = Path.GetFullPath(filePath);
                        savedPositions.Add(normalizedPath, pixelPosition);
                        _engine.Log($"Desktop: Loaded position for {Path.GetFileName(filePath)} - Cell: Row {row}, Column {column} -> Pixel: {pixelPosition}");
                    }
                    _engine.Log($"Desktop: Loaded {savedPositions.Count} file positions from {positionsFile}");
                    return savedPositions;
                }
                else
                {
                    _engine.Log($"Desktop: Positions file not found at {positionsFile}");
                }
                return new Dictionary<string, Vector2>();
            }
            catch (Exception ex)
            {
                _engine.Log($"Desktop: Error loading file positions: {ex.Message}");
                return new Dictionary<string, Vector2>();
            }
        }

        private Vector2 FindFirstAvailableCell()
        {
            return FindFirstAvailableCell(new HashSet<(int, int)>());
        }

        private Vector2 FindFirstAvailableCell(HashSet<(int, int)> usedCells)
        {
            return FindClosestAvailableCell(usedCells, (0, 0));
        }

        private Vector2 FindClosestAvailableCell(HashSet<(int, int)> usedCells, (int targetRow, int targetColumn) targetCell)
        {
            _engine.Log($"Desktop: FindClosestAvailableCell called with target Row {targetCell.targetRow}, Column {targetCell.targetColumn}");
            _engine.Log($"Desktop: Grid bounds: {_gridRows} rows x {_gridColumns} columns");
            _engine.Log($"Desktop: Used cells count: {usedCells.Count}");
            
            // Create a set of occupied positions for quick lookup
            var occupiedPositions = new HashSet<(int, int)>(usedCells);
            foreach (var file in _desktopFiles)
            {
                // Convert file position to cell coordinates
                int column = (int)((file.Position.X - _gridStartX) / _gridCellSize);
                int row = (int)((file.Position.Y - _gridStartY) / _gridCellSize);
                
                // Ensure cell coordinates are within bounds
                column = Math.Max(0, Math.Min(column, _gridColumns - 1));
                row = Math.Max(0, Math.Min(row, _gridRows - 1));
                
                occupiedPositions.Add((column, row));
                _engine.Log($"Desktop: File {file.Name} occupies cell Row {row}, Column {column}");
            }

            // If target cell is within bounds and available, use it
            if (targetCell.targetRow >= 0 && targetCell.targetRow < _gridRows && 
                targetCell.targetColumn >= 0 && targetCell.targetColumn < _gridColumns &&
                !occupiedPositions.Contains(targetCell))
            {
                Vector2 position = new Vector2(
                    _gridStartX + (targetCell.targetColumn * _gridCellSize) + (_gridCellSize - _currentIconSize) / 2,
                    _gridStartY + (targetCell.targetRow * _gridCellSize) + (_gridCellSize - _currentIconSize) / 2 + ICON_TOP_OFFSET
                );
                _engine.Log($"Desktop: Found target cell at Row {targetCell.targetRow}, Column {targetCell.targetColumn} -> position {position}");
                return position;
            }

            // If target cell is out of bounds, clamp it to the nearest valid cell
            int clampedTargetRow = Math.Max(0, Math.Min(targetCell.targetRow, _gridRows - 1));
            int clampedTargetColumn = Math.Max(0, Math.Min(targetCell.targetColumn, _gridColumns - 1));
            _engine.Log($"Desktop: Target cell out of bounds, clamped to Row {clampedTargetRow}, Column {clampedTargetColumn}");

            // Find the closest available cell using Euclidean distance
            double closestDistance = double.MaxValue;
            (int closestRow, int closestColumn) closestCell = (-1, -1);
            
            for (int row = 0; row < _gridRows; row++)
            {
                for (int column = 0; column < _gridColumns; column++)
                {
                    if (!occupiedPositions.Contains((column, row)))
                    {
                        // Calculate Euclidean distance to target
                        double distance = Math.Sqrt(
                            Math.Pow(row - clampedTargetRow, 2) + 
                            Math.Pow(column - clampedTargetColumn, 2)
                        );
                        
                        if (distance < closestDistance)
                        {
                            closestDistance = distance;
                            closestCell = (row, column);
                            _engine.Log($"Desktop: Found closer cell at Row {row}, Column {column} with distance {distance}");
                        }
                    }
                }
            }
            
            if (closestCell.closestRow >= 0 && closestCell.closestColumn >= 0)
            {
                Vector2 position = new Vector2(
                    _gridStartX + (closestCell.closestColumn * _gridCellSize) + (_gridCellSize - _currentIconSize) / 2,
                    _gridStartY + (closestCell.closestRow * _gridCellSize) + (_gridCellSize - _currentIconSize) / 2 + ICON_TOP_OFFSET
                );
                _engine.Log($"Desktop: Found closest available cell at Row {closestCell.closestRow}, Column {closestCell.closestColumn} (distance {closestDistance}) -> position {position}");
                return position;
            }

            // If no cell is available, return the last cell position
            Vector2 lastPosition = new Vector2(
                _gridStartX + ((_gridColumns - 1) * _gridCellSize) + (_gridCellSize - _currentIconSize) / 2,
                _gridStartY + ((_gridRows - 1) * _gridCellSize) + (_gridCellSize - _currentIconSize) / 2 + ICON_TOP_OFFSET
            );
            _engine.Log($"Desktop: No available cells found, using last position {lastPosition}");
            return lastPosition;
        }

        private void InitializeFileWatcher()
        {
            try
            {
                string desktopPath = Path.Combine(Directory.GetCurrentDirectory(), "Content", "Desktop");
                if (!Directory.Exists(desktopPath))
                {
                    Directory.CreateDirectory(desktopPath);
                }

                _fileWatcher = new FileSystemWatcher(desktopPath)
                {
                    Filter = "*.*",
                    EnableRaisingEvents = true,
                    IncludeSubdirectories = false
                };

                _fileWatcher.Changed += OnFileChanged;
                _fileWatcher.Created += OnFileChanged;
                _fileWatcher.Deleted += OnFileChanged;
                _fileWatcher.Renamed += OnFileRenamed;
                
                _engine.Log($"Desktop: File watcher initialized for {desktopPath}");
            }
            catch (Exception ex)
            {
                _engine.Log($"Desktop: Error initializing file watcher: {ex.Message}");
            }
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            // Prevent rapid reloading by checking time between changes
            if ((DateTime.Now - _lastFileChange).TotalMilliseconds < 500)
            {
                return;
            }
            _lastFileChange = DateTime.Now;
            
            _engine.Log($"Desktop: File {e.Name} changed, reloading desktop files");
            LoadDesktopFiles();
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            // Prevent rapid reloading by checking time between changes
            if ((DateTime.Now - _lastFileChange).TotalMilliseconds < 500)
            {
                return;
            }
            _lastFileChange = DateTime.Now;
            
            _engine.Log($"Desktop: File {e.OldName} renamed to {e.Name}, reloading desktop files");
            LoadDesktopFiles();
        }

        private Dictionary<string, (int Row, int Column)> LoadDesktopFileCellPositions()
        {
            try
            {
                string desktopEssentialPath = Path.Combine(Directory.GetCurrentDirectory(), "Content", "Modules", "Desktop_essential");
                string positionsFile = Path.Combine(desktopEssentialPath, "desktop_positions.json");

                _engine.Log($"Desktop: Attempting to load cell positions from {positionsFile}");

                if (File.Exists(positionsFile))
                {
                    string json = File.ReadAllText(positionsFile);
                    var positions = System.Text.Json.JsonSerializer.Deserialize<List<System.Text.Json.JsonElement>>(json);

                    var savedCellPositions = new Dictionary<string, (int Row, int Column)>();
                    foreach (var pos in positions)
                    {
                        string filePath = pos.GetProperty("Path").GetString();
                        int column = pos.GetProperty("Cell").GetProperty("Column").GetInt32();
                        int row = pos.GetProperty("Cell").GetProperty("Row").GetInt32();
                        
                        // Normalize the path to ensure consistent comparison
                        string normalizedPath = Path.GetFullPath(filePath);
                        savedCellPositions.Add(normalizedPath, (row, column));
                        _engine.Log($"Desktop: Loaded cell position for {Path.GetFileName(filePath)} - Row {row}, Column {column}");
                    }
                    _engine.Log($"Desktop: Loaded {savedCellPositions.Count} cell positions from {positionsFile}");
                    return savedCellPositions;
                }
                else
                {
                    _engine.Log($"Desktop: Positions file not found at {positionsFile}");
                }
                return new Dictionary<string, (int Row, int Column)>();
            }
            catch (Exception ex)
            {
                _engine.Log($"Desktop: Error loading cell positions: {ex.Message}");
                return new Dictionary<string, (int Row, int Column)>();
            }
        }

        public void Highlight()
        {
            _isHighlighted = true;
            _highlightTimer = 0f;
        }
    }

    public class ContextMenuItem
    {
        public string Text { get; set; }
        public List<string> DropdownItems { get; set; }
        public bool IsHovered { get; set; }
        public bool IsDropdownVisible { get; set; }
        public List<Rectangle> DropdownBounds { get; set; }

        public ContextMenuItem(string text, List<string> dropdownItems)
        {
            Text = text;
            DropdownItems = dropdownItems;
            IsHovered = false;
            IsDropdownVisible = false;
            DropdownBounds = new List<Rectangle>();
        }
    }
} 