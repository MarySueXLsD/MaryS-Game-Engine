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

namespace MarySGameEngine.Modules.ModuleSettings_essential
{
    public class ModuleSettings : IModule
    {
        private WindowManagement _windowManagement;
        private GraphicsDevice _graphicsDevice;
        private SpriteFont _menuFont;
        private int _windowWidth;
        private TaskBar _taskBar;
        private ContentManager _content;
        private UIElements _uiElements;
        private GameEngine _engine;

        public ModuleSettings(GraphicsDevice graphicsDevice, SpriteFont menuFont, int windowWidth)
        {
            _graphicsDevice = graphicsDevice;
            _menuFont = menuFont;
            _windowWidth = windowWidth;
            _engine = (GameEngine)GameEngine.Instance;
            
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

        private void InitializeUIElements()
        {
            try
            {
                _engine.Log("ModuleSettings: Initializing UI Elements");
                
                // Load markdown layout
                string markdownLayout = LoadMarkdownLayout();
                _engine.Log($"ModuleSettings: Loaded markdown layout ({markdownLayout.Length} characters)");
                
                // Create UI elements with markdown content
                Rectangle uiBounds = new Rectangle(10, 50, _windowWidth - 20, _windowWidth - 60);
                string markdownFilePath = Path.Combine(Directory.GetCurrentDirectory(), "Content", "Modules", "ModuleSettings_essential", "ui_layout.md");
                
                _uiElements = new UIElements(_graphicsDevice, _menuFont, uiBounds, markdownLayout, markdownFilePath);
                
                // Set up callbacks
                _uiElements.SetSaveSettingsCallback(OnSaveSettings);
                _uiElements.SetResetToDefaultsCallback(OnResetToDefaults);
                
                _engine.Log("ModuleSettings: UI Elements initialized successfully");
            }
            catch (Exception ex)
            {
                _engine.Log($"ModuleSettings: Error initializing UI Elements: {ex.Message}");
                _engine.Log($"ModuleSettings: Stack trace: {ex.StackTrace}");
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
                _windowManagement.Update();
                
                // Initialize UI elements if window is visible and they haven't been initialized yet
                if (_windowManagement.IsVisible() && _uiElements == null)
                {
                    System.Diagnostics.Debug.WriteLine("ModuleSettings: Window is visible, initializing UI elements");
                    InitializeUIElements();
                }
                
                // Update UI elements if window is visible and initialized
                if (_windowManagement.IsVisible() && _uiElements != null)
                {
                    // Only update UI bounds if window bounds have changed
                    Rectangle windowBounds = _windowManagement.GetWindowBounds();
                    Rectangle contentBounds = new Rectangle(
                        windowBounds.X,
                        windowBounds.Y + 40, // Title bar height
                        windowBounds.Width,
                        windowBounds.Height - 40
                    );
                    
                    // Check if bounds have actually changed to avoid unnecessary layout updates
                    Rectangle currentBounds = _uiElements.GetBounds();
                    if (currentBounds != contentBounds)
                    {
                        System.Diagnostics.Debug.WriteLine($"ModuleSettings: Bounds changed from {currentBounds} to {contentBounds}");
                        _uiElements.SetBounds(contentBounds);
                    }
                    
                    _uiElements.Update();
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
                
                // Draw UI elements if window is visible and initialized
                if (_windowManagement.IsVisible() && _uiElements != null)
                {
                    _uiElements.Draw(spriteBatch);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ModuleSettings: Error in Draw: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"ModuleSettings: Stack trace: {ex.StackTrace}");
            }
        }

        public void UpdateWindowWidth(int newWidth)
        {
            _windowWidth = newWidth;
            _windowManagement.UpdateWindowWidth(newWidth);
            UpdateBounds(); // Maintain center positioning
            
            // Update UI elements bounds
            if (_uiElements != null)
            {
                Rectangle windowBounds = _windowManagement.GetWindowBounds();
                Rectangle contentBounds = new Rectangle(
                    windowBounds.X,
                    windowBounds.Y + 40, // Title bar height
                    windowBounds.Width,
                    windowBounds.Height - 40
                );
                _uiElements.SetBounds(contentBounds);
            }
        }

        public void LoadContent(ContentManager content)
        {
            _content = content;
            _windowManagement.LoadContent(content);
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

        private string LoadMarkdownLayout()
        {
            try
            {
                string layoutPath = Path.Combine(Directory.GetCurrentDirectory(), "Content", "Modules", "ModuleSettings_essential", "ui_layout.md");
                System.Diagnostics.Debug.WriteLine($"ModuleSettings: Attempting to load layout from: {layoutPath}");
                
                if (File.Exists(layoutPath))
                {
                    string content = File.ReadAllText(layoutPath);
                    System.Diagnostics.Debug.WriteLine($"ModuleSettings: Successfully loaded layout file, length: {content.Length}");
                    return content;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"ModuleSettings: Layout file not found at: {layoutPath}");
                    _engine.Log($"ModuleSettings: ERROR - ui_layout.md file not found at {layoutPath}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ModuleSettings: Error loading markdown layout: {ex.Message}");
                _engine.Log($"ModuleSettings: ERROR loading markdown layout: {ex.Message}");
                return null;
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
                string currentMarkdown = LoadMarkdownLayout();
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
    }
} 