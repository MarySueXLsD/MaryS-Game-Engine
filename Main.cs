using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Reflection;
using MarySGameEngine.Modules.TaskBar_essential;
using MarySGameEngine.Modules.WindowManagement_essential;
using MarySGameEngine.Modules.TopBar_essential;
using MarySGameEngine.Modules.Desktop_essential;

namespace MarySGameEngine;

public class ModuleInfo
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Shortcut { get; set; } = string.Empty;
    public string[] Dependencies { get; set; } = Array.Empty<string>();
    public string MinEngineVersion { get; set; } = string.Empty;
    [JsonPropertyName("is_visible")]
    public bool IsVisible { get; set; }
    public string LastUpdated { get; set; } = string.Empty;
}

public class MenuItem
{
    public string Text { get; set; }
    public List<string> DropdownItems { get; set; }
    public Vector2 Position { get; set; }
    public bool IsHovered { get; set; }
    public bool IsDropdownVisible { get; set; }
    public Rectangle Bounds { get; set; }
    public List<Rectangle> DropdownBounds { get; set; }

    public MenuItem(string text, List<string> dropdownItems, Vector2 position)
    {
        Text = text;
        DropdownItems = dropdownItems;
        Position = position;
        IsHovered = false;
        IsDropdownVisible = false;
        DropdownBounds = new List<Rectangle>();
    }
}

public class GameEngine : Game
{
    // Window and graphics settings
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private Color _backgroundColor;
    private SpriteFont _menuFont;
    private List<IModule> _activeModules;
    private StreamWriter _logFile;
    private string _mainDirectory;
    private TaskBar _taskBar;
    private Desktop _desktop;
    public static GameEngine Instance { get; private set; }

    // Window dimensions
    private const int InitialWindowWidth = 800;
    private const int InitialWindowHeight = 600;

    public GameEngine()
    {
        Instance = this;
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        _activeModules = new List<IModule>();
        
        // Set window title
        Window.Title = "MaryS Game Engine";
        
        // Set default background color (#3e3763)
        _backgroundColor = new Color(0x3e, 0x37, 0x63);

        // Get main directory (root project folder)
        _mainDirectory = Directory.GetCurrentDirectory();

        // Initialize log file
        InitializeLogging();
    }

    private void InitializeLogging()
    {
        try
        {
            // Create logs directory in the root project folder
            string logsDir = Path.Combine(_mainDirectory, "logs");
            if (!Directory.Exists(logsDir))
            {
                Directory.CreateDirectory(logsDir);
            }

            // Create log file with today's date
            string dateStr = DateTime.Now.ToString("dd-MM-yyyy");
            string logPath = Path.Combine(logsDir, $"{dateStr}_MaryS_logs.txt");
            
            _logFile = new StreamWriter(logPath, true);
            _logFile.WriteLine($"\n=== Engine Started at {DateTime.Now} ===");
            _logFile.Flush();
            
            // Log the path for debugging
            System.Diagnostics.Debug.WriteLine($"Log file created at: {logPath}");
        }
        catch (Exception ex)
        {
            // If logging fails, we'll just continue without it
            System.Diagnostics.Debug.WriteLine($"Failed to initialize logging: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    public void Log(string message)
    {
        try
        {
            string logMessage = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
            _logFile.WriteLine(logMessage);
            _logFile.Flush();
        }
        catch
        {
            // If logging fails, we'll just continue without it
        }
    }

    private void LoadModules()
    {
        try
        {
            // Use the Content directory from the root project folder
            string modulesPath = Path.Combine(_mainDirectory, "Content", "Modules");
            Log($"Looking for modules in: {modulesPath}");
            
            if (!Directory.Exists(modulesPath))
            {
                Log("Modules directory not found");
                return;
            }

            // Get all module directories
            string[] moduleDirectories = Directory.GetDirectories(modulesPath);
            Log($"Found {moduleDirectories.Length} module directories");

            foreach (string moduleDir in moduleDirectories)
            {
                string moduleName = Path.GetFileName(moduleDir);
                string bridgeJsonPath = Path.Combine(moduleDir, "bridge.json");
                Log($"Checking module: {moduleName} at {bridgeJsonPath}");

                if (!File.Exists(bridgeJsonPath))
                {
                    Log($"bridge.json not found in {moduleName}");
                    continue;
                }

                try
                {
                    string jsonContent = File.ReadAllText(bridgeJsonPath);
                    Log($"Read bridge.json content: {jsonContent}");
                    
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip
                    };
                    
                    var moduleInfo = JsonSerializer.Deserialize<ModuleInfo>(jsonContent, options);

                    if (moduleInfo == null)
                    {
                        Log($"Failed to deserialize bridge.json for {moduleName}");
                        continue;
                    }

                    Log($"Module info - Name: {moduleInfo.Name}, IsVisible: {moduleInfo.IsVisible}");

                    // Special case for Desktop module - always load it regardless of visibility
                    bool shouldLoad = moduleInfo.IsVisible || moduleName == "Desktop_essential";

                    if (!shouldLoad)
                    {
                        Log($"Module {moduleName} is not visible, skipping");
                        continue;
                    }

                    // Get the module type from the namespace
                    string fullTypeName = $"MarySGameEngine.Modules.{moduleName}.{moduleInfo.Name.Replace(" ", "")}";
                    Log($"Looking for type: {fullTypeName}");

                    // Get the assembly containing the type
                    Assembly assembly = Assembly.GetExecutingAssembly();
                    Type moduleType = assembly.GetType(fullTypeName);

                    if (moduleType == null)
                    {
                        Log($"Could not find type: {fullTypeName}");
                        continue;
                    }

                    if (!typeof(IModule).IsAssignableFrom(moduleType))
                    {
                        Log($"Type {fullTypeName} does not implement IModule");
                        continue;
                    }

                    // Create instance of the module
                    Log($"Creating instance of {moduleInfo.Name}");
                    IModule module = (IModule)Activator.CreateInstance(moduleType, 
                        GraphicsDevice, _menuFont, GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width);
                    
                    _activeModules.Add(module);
                    Log($"Successfully loaded module: {moduleInfo.Name} v{moduleInfo.Version}");

                    // Store TaskBar reference
                    if (module is TaskBar taskBar)
                    {
                        Log("Found TaskBar module, storing reference");
                        _taskBar = taskBar;
                    }
                    // Store Desktop reference
                    else if (module is Desktop desktop)
                    {
                        Log("Found Desktop module, storing reference");
                        _desktop = desktop;
                    }
                }
                catch (Exception ex)
                {
                    Log($"Error loading module {moduleName}: {ex.Message}");
                    Log($"Stack trace: {ex.StackTrace}");
                }
            }

            // Connect TaskBar with WindowManagement instances
            if (_taskBar != null)
            {
                Log("Connecting TaskBar with WindowManagement instances");
                foreach (var module in _activeModules)
                {
                    if (module is IModule moduleWithTaskBar)
                    {
                        var setTaskBarMethod = moduleWithTaskBar.GetType().GetMethod("SetTaskBar");
                        if (setTaskBarMethod != null)
                        {
                            Log($"Connecting TaskBar to {moduleWithTaskBar.GetType().Name}");
                            setTaskBarMethod.Invoke(moduleWithTaskBar, new object[] { _taskBar });
                        }
                    }
                }
            }
            else
            {
                Log("WARNING: TaskBar reference is null after loading modules");
            }
        }
        catch (Exception ex)
        {
            Log($"Error scanning modules directory: {ex.Message}");
            Log($"Stack trace: {ex.StackTrace}");
        }
    }

    protected override void Initialize()
    {
        try
        {
            base.Initialize();
            Window.AllowUserResizing = true;
            
            // Set initial window size
            _graphics.IsFullScreen = false;
            _graphics.PreferredBackBufferWidth = InitialWindowWidth;
            _graphics.PreferredBackBufferHeight = InitialWindowHeight;
            _graphics.ApplyChanges();

            // Handle window size changes
            Window.ClientSizeChanged += (s, e) =>
            {
                foreach (var module in _activeModules)
                {
                    module.UpdateWindowWidth(Window.ClientBounds.Width);
                }
            };

            // Maximize after a frame
            System.Threading.Tasks.Task.Delay(16).ContinueWith(_ =>
            {
                _graphics.PreferredBackBufferWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
                _graphics.PreferredBackBufferHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
                _graphics.ApplyChanges();
                
                // Update all modules after maximizing
                foreach (var module in _activeModules)
                {
                    module.UpdateWindowWidth(Window.ClientBounds.Width);
                }
            });
        }
        catch (Exception ex)
        {
            Log($"Editor Initialize error: {ex.Message}");
            throw;
        }
    }

    protected override void LoadContent()
    {
        try
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            Log("Main: Created SpriteBatch");
            
            // Load the pixel font
            _menuFont = Content.Load<SpriteFont>("Fonts/SpriteFonts/pixel_font");
            Log("Main: Loaded pixel font");

            // Load all modules
            LoadModules();
            Log($"Main: Loaded {_activeModules.Count} modules");

            // First load TaskBar content
            if (_taskBar != null)
            {
                Log("Main: Loading TaskBar content first");
                try
                {
                    _taskBar.LoadContent(Content);
                    Log("Main: Successfully loaded TaskBar content");
                }
                catch (Exception ex)
                {
                    Log($"Main: ERROR loading TaskBar content: {ex.Message}");
                    Log($"Main: Stack trace: {ex.StackTrace}");
                }
            }

            // Then load other module content
            foreach (var module in _activeModules)
            {
                if (module != _taskBar) // Skip TaskBar as we already loaded it
                {
                    Log($"Main: Loading content for module: {module.GetType().Name}");
                    try
                    {
                        module.LoadContent(Content);
                        Log($"Main: Successfully loaded content for {module.GetType().Name}");
                    }
                    catch (Exception ex)
                    {
                        Log($"Main: ERROR loading content for {module.GetType().Name}: {ex.Message}");
                        Log($"Main: Stack trace: {ex.StackTrace}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log($"Main: ERROR in LoadContent: {ex.Message}");
            Log($"Main: Stack trace: {ex.StackTrace}");
        }
    }

    protected override void Update(GameTime gameTime)
    {
        try
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            // Get mouse state
            var mouseState = Mouse.GetState();
            var mousePosition = mouseState.Position;

            // First find TaskBar and TopBar
            TaskBar taskBar = null;
            TopBar topBar = null;
            foreach (var module in _activeModules)
            {
                if (module is TaskBar tb)
                {
                    taskBar = tb;
                }
                else if (module is TopBar tbar)
                {
                    topBar = tbar;
                }
            }

            // Update TopBar first
            if (topBar != null)
            {
                topBar.Update();
            }

            // Update Desktop next
            if (_desktop != null)
            {
                _desktop.Update();
            }

            // If we found TaskBar, update it next and check its state
            if (taskBar != null)
            {
                taskBar.Update();
                
                // If TaskBar is being dragged or mouse is over it, skip all other updates
                if (taskBar.IsDragging() || taskBar.IsMouseOver())
                {
                    base.Update(gameTime);
                    return;
                }
            }

            // Handle window interactions
            WindowManagement topMostWindow = null;
            int highestZOrder = -1;
            bool isAnyWindowDragging = false;

            // First pass: check for dragging windows
            foreach (var module in _activeModules)
            {
                if (module is IModule moduleWithWindow)
                {
                    var windowManagementField = moduleWithWindow.GetType().GetField("_windowManagement", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (windowManagementField != null)
                    {
                        var windowManagement = windowManagementField.GetValue(moduleWithWindow) as WindowManagement;
                        if (windowManagement != null && windowManagement.IsVisible())
                        {
                            if (windowManagement.IsDragging() || windowManagement.IsResizing())
                            {
                                isAnyWindowDragging = true;
                                topMostWindow = windowManagement;
                                break;
                            }
                        }
                    }
                }
            }

            // If no window is being dragged, find the top-most window under the mouse
            if (!isAnyWindowDragging)
            {
                foreach (var module in _activeModules)
                {
                    if (module is IModule moduleWithWindow)
                    {
                        var windowManagementField = moduleWithWindow.GetType().GetField("_windowManagement", 
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        
                        if (windowManagementField != null)
                        {
                            var windowManagement = windowManagementField.GetValue(moduleWithWindow) as WindowManagement;
                            if (windowManagement != null && windowManagement.IsVisible())
                            {
                                var windowBounds = windowManagement.GetWindowBounds();
                                if (windowBounds.Contains(mousePosition))
                                {
                                    int zOrder = windowManagement.GetZOrder();
                                    if (zOrder > highestZOrder)
                                    {
                                        highestZOrder = zOrder;
                                        topMostWindow = windowManagement;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Update modules based on window interaction
            foreach (var module in _activeModules)
            {
                if (module is IModule moduleWithWindow)
                {
                    var windowManagementField = moduleWithWindow.GetType().GetField("_windowManagement", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (windowManagementField != null)
                    {
                        var windowManagement = windowManagementField.GetValue(moduleWithWindow) as WindowManagement;
                        if (windowManagement != null)
                        {
                            // If we have a top-most window, only update that window's module
                            if (topMostWindow != null)
                            {
                                if (windowManagement == topMostWindow)
                                {
                                    module.Update();
                                    break;
                                }
                            }
                            else
                            {
                                // If no window is handling the click, update all modules
                                module.Update();
                            }
                        }
                    }
                }
                else if (!(module is TaskBar) && !(module is TopBar) && !(module is Desktop)) // Skip TaskBar, TopBar, and Desktop as they're already handled
                {
                    // Update non-window modules if no window is handling the click
                    if (topMostWindow == null)
                    {
                        module.Update();
                    }
                }
            }

            base.Update(gameTime);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Main: ERROR in Update: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Main: Stack trace: {ex.StackTrace}");
        }
    }

    protected override void Draw(GameTime gameTime)
    {
        try
        {
            // Get background color from Desktop module if available, otherwise use default
            Color backgroundColor = _desktop?.GetBackgroundColor() ?? _backgroundColor;
            GraphicsDevice.Clear(backgroundColor);

            _spriteBatch.Begin();
            System.Diagnostics.Debug.WriteLine($"Main: Drawing {_activeModules.Count} modules");
            
            // Draw Desktop background first
            if (_desktop != null)
            {
                _desktop.DrawBackground(_spriteBatch);
            }
            
            // Draw Desktop main content (including drag preview)
            if (_desktop != null)
            {
                _desktop.Draw(_spriteBatch);
            }
            
            // Draw TopBar next
            TopBar topBar = null;
            foreach (var module in _activeModules)
            {
                if (module is TopBar tb)
                {
                    topBar = tb;
                    module.Draw(_spriteBatch);
                    break;
                }
            }
            
            // Draw non-window modules next (except TopBar and Desktop)
            foreach (var module in _activeModules)
            {
                if (module is TopBar || module is Desktop) continue; // Skip TopBar and Desktop as they're already drawn
                
                var windowManagementField = module.GetType().GetField("_windowManagement", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (windowManagementField == null)
                {
                    module.Draw(_spriteBatch);
                }
            }

            // Then draw windows in z-order
            var windowModules = _activeModules
                .Where(m => m.GetType().GetField("_windowManagement", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) != null)
                .Select(m => new
                {
                    Module = m,
                    WindowManagement = m.GetType().GetField("_windowManagement", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        ?.GetValue(m) as WindowManagement
                })
                .Where(w => w.WindowManagement != null)
                .OrderBy(w => w.WindowManagement.GetZOrder());

            foreach (var windowModule in windowModules)
            {
                windowModule.Module.Draw(_spriteBatch);
            }

            // Draw TopBar dropdowns
            if (topBar != null)
            {
                topBar.DrawDropdowns(_spriteBatch);
            }

            // Draw Desktop context menu last to ensure it's on top
            if (_desktop != null)
            {
                _desktop.DrawContextMenu(_spriteBatch);
            }

            _spriteBatch.End();

            base.Draw(gameTime);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Main: ERROR in Draw: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Main: Stack trace: {ex.StackTrace}");
        }
    }

    protected override void UnloadContent()
    {
        foreach (var module in _activeModules)
        {
            if (module is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        _activeModules.Clear();
        base.UnloadContent();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _logFile?.Dispose();
        }
        base.Dispose(disposing);
    }

    public List<IModule> GetActiveModules()
    {
        return _activeModules;
    }
}
