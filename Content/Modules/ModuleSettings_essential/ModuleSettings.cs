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
using System.Text.Json;
using System.Linq;

namespace MarySGameEngine.Modules.ModuleSettings_essential
{
    public class ModuleSettings : IModule
    {
        private WindowManagement _windowManagement;
        private GraphicsDevice _graphicsDevice;
        private SpriteFont _menuFont;
        private SpriteFont _uiFont; // Separate font for UI elements
        private int _windowWidth;
        private TaskBar _taskBar;
        private ContentManager _content;
        private UIElements _uiElements;
        private GameEngine _engine;
        private Texture2D _pixel;
        private MouseState _prevMouseState;

        private class ModuleTab
        {
            public string Name;
            public string SettingsPath;
            public string BridgePath;
            public string FolderName;
        }
        private List<ModuleTab> _tabs = new List<ModuleTab>();
        private int _activeTabIndex = 0;
        private int _firstVisibleTabIndex = 0;
        private int _maxVisibleTabs = 1;
        private SpriteFont _tabFont;
        private Texture2D _arrowTexture;
        private int _tabBarHeight = 38; // 30px + padding
        private int _tabPadding = 10;
        private int _tabOverlap = 18; // How much tabs overlap each other
        private int _arrowSize = 28;
        private Rectangle _tabBarBounds;
        private int _tabBarSpacing = 8; // Spacing between title bar and tab bar, and between tab bar and UI
        private int _titleBarHeight = 40; // Should match WindowManagement's title bar height

        // Store clickable tab rectangles for click detection
        private List<(Rectangle rect, int tabIndex)> _tabRects = new List<(Rectangle, int)>();

        public ModuleSettings(GraphicsDevice graphicsDevice, SpriteFont menuFont, int windowWidth)
        {
            _graphicsDevice = graphicsDevice;
            _menuFont = menuFont;
            _windowWidth = windowWidth;
            _engine = (GameEngine)GameEngine.Instance;
            
            // Create 1x1 white pixel texture
            _pixel = new Texture2D(graphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
            
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
                Rectangle windowBounds = _windowManagement.GetWindowBounds();
                // Tab bar is now inside the window, just below the title bar
                _tabBarBounds = new Rectangle(
                    windowBounds.X,
                    windowBounds.Y + _titleBarHeight + _tabBarSpacing,
                    windowBounds.Width,
                    _tabBarHeight
                );
                UpdateTabBarInput();

                // If window is visible and UIElements is null, load the current tab's settings_ui.md
                if (_windowManagement.IsVisible() && _uiElements == null)
                {
                    LoadTabUI(_activeTabIndex);
                }
                
                // Update UI elements if window is visible and initialized
                if (_windowManagement.IsVisible() && _uiElements != null)
                {
                    // UI area starts below the tab bar
                    Rectangle uiBounds = new Rectangle(
                        windowBounds.X,
                        windowBounds.Y + _titleBarHeight + _tabBarSpacing + _tabBarHeight + _tabBarSpacing,
                        windowBounds.Width,
                        windowBounds.Height - _titleBarHeight - _tabBarHeight - 2 * _tabBarSpacing
                    );
                    Rectangle currentBounds = _uiElements.GetBounds();
                    if (currentBounds != uiBounds)
                    {
                        _uiElements.SetBounds(uiBounds);
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
                if (_windowManagement.IsVisible())
                {
                    DrawTabBar(spriteBatch);
                    if (_uiElements != null)
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
                _uiElements.SetBounds(windowBounds);
            }
        }

        public void LoadContent(ContentManager content)
        {
            _content = content;
            _windowManagement.LoadContent(content);
            // Try to load Roboto for UIElements
            try {
                _uiFont = content.Load<SpriteFont>("Fonts/SpriteFonts/roboto/regular");
            } catch {
                _uiFont = _menuFont;
            }
            _tabFont = _menuFont;
            _arrowTexture = null; // No arrow texture, skip drawing arrows
            BuildTabs();
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

        private void BuildTabs()
        {
            _tabs.Clear();
            string modulesRoot = Path.Combine(Directory.GetCurrentDirectory(), "Content", "Modules");
            foreach (var dir in Directory.GetDirectories(modulesRoot))
            {
                string folderName = Path.GetFileName(dir);
                string bridgePath = Path.Combine(dir, "bridge.json");
                string settingsPath = Path.Combine(dir, "settings_ui.md");
                if (File.Exists(bridgePath) && File.Exists(settingsPath))
                {
                    string json = File.ReadAllText(bridgePath);
                    string name = "";
                    try
                    {
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("name", out var n))
                            name = n.GetString();
                    }
                    catch { name = folderName; }
                    _tabs.Add(new ModuleTab { Name = name, SettingsPath = settingsPath, BridgePath = bridgePath, FolderName = folderName });
                }
            }
            // Always include ModuleSettings itself, even if not found above
            if (!_tabs.Any(t => t.FolderName == "ModuleSettings_essential"))
            {
                string dir = Path.Combine(modulesRoot, "ModuleSettings_essential");
                string bridgePath = Path.Combine(dir, "bridge.json");
                string settingsPath = Path.Combine(dir, "settings_ui.md");
                string name = "Module Settings";
                if (File.Exists(bridgePath))
                {
                    string json = File.ReadAllText(bridgePath);
                    try
                    {
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("name", out var n))
                            name = n.GetString();
                    }
                    catch { }
                }
                _tabs.Insert(0, new ModuleTab { Name = name, SettingsPath = settingsPath, BridgePath = bridgePath, FolderName = "ModuleSettings_essential" });
            }
            _engine.Log($"[ModuleSettingsTabs] Built tab list. Count: {_tabs.Count}");
            foreach (var t in _tabs)
                _engine.Log($"[ModuleSettingsTabs] Tab: {t.Name} | Path: {t.SettingsPath}");
            if (_tabs.Count == 0)
                _engine.Log("[ModuleSettingsTabs] WARNING: No tabs found!");
            _activeTabIndex = 0;
            _firstVisibleTabIndex = 0;
            LoadTabUI(_activeTabIndex);
        }

        private void LoadTabUI(int tabIndex)
        {
            if (tabIndex < 0 || tabIndex >= _tabs.Count) return;
            var tab = _tabs[tabIndex];
            _engine.Log($"[ModuleSettingsTabs] Loading tab {tabIndex}: {tab.Name} | File: {tab.SettingsPath}");
            string markdown = File.Exists(tab.SettingsPath) ? File.ReadAllText(tab.SettingsPath) : "# No settings found";
            if (!File.Exists(tab.SettingsPath))
                _engine.Log($"[ModuleSettingsTabs] WARNING: settings_ui.md not found for {tab.Name} at {tab.SettingsPath}");
            if (string.IsNullOrWhiteSpace(markdown))
                _engine.Log($"[ModuleSettingsTabs] WARNING: settings_ui.md is empty for {tab.Name} at {tab.SettingsPath}");
            // UI area starts below the tab bar
            Rectangle windowBounds = _windowManagement.GetWindowBounds();
            Rectangle uiBounds = new Rectangle(
                windowBounds.X,
                windowBounds.Y + _titleBarHeight + _tabBarSpacing + _tabBarHeight + _tabBarSpacing,
                windowBounds.Width,
                windowBounds.Height - _titleBarHeight - _tabBarHeight - 2 * _tabBarSpacing
            );
            _uiElements = new UIElements(_graphicsDevice, _uiFont, uiBounds, markdown, tab.SettingsPath);
            _uiElements.SetSaveSettingsCallback(OnSaveSettings);
            _uiElements.SetResetToDefaultsCallback(OnResetToDefaults);
            if (_uiElements == null)
                _engine.Log($"[ModuleSettingsTabs] ERROR: UIElements is null after loading tab {tab.Name}");
        }

        private void UpdateTabBarInput()
        {
            var mouse = Mouse.GetState();
            var mousePos = mouse.Position;
            bool leftPressed = mouse.LeftButton == ButtonState.Pressed;
            bool leftJustPressed = leftPressed && _prevMouseState.LeftButton == ButtonState.Released;
            if (!_windowManagement.IsVisible() || !_tabBarBounds.Contains(mousePos)) { _prevMouseState = mouse; return; }
            // Use _tabRects for click detection
            if (leftJustPressed)
            {
                foreach (var (rect, tabIndex) in _tabRects)
                {
                    if (rect.Contains(mousePos))
                    {
                        if (_activeTabIndex != tabIndex)
                        {
                            _activeTabIndex = tabIndex;
                            LoadTabUI(tabIndex);
                        }
                        _prevMouseState = mouse;
                        return;
                    }
                }
            }
            _prevMouseState = mouse;
        }

        private int GetTabWidth(string text)
        {
            return (int)(_tabFont.MeasureString(text).X * 0.7f) + 2 * _tabPadding;
        }
        private int GetMaxVisibleTabs(int availableWidth)
        {
            int total = 0, count = 0;
            for (int i = 0; i < _tabs.Count; i++)
            {
                int w = GetTabWidth(_tabs[i].Name);
                if (total + w - (count > 0 ? _tabOverlap : 0) > availableWidth - 2 * _arrowSize - 2 * _tabPadding)
                    break;
                total += w - (count > 0 ? _tabOverlap : 0);
                count++;
            }
            return Math.Max(1, count);
        }

        // Helper to draw a polygonal tab with a cut top-left corner and gradient
        private void DrawTabPolygon(SpriteBatch spriteBatch, Rectangle rect, bool isActive, Color color1, Color color2, int cutSize = 12, int borderThickness = 2)
        {
            // We'll draw the tab as two rectangles: top (with cut) and bottom
            // For a real polygon, you'd use a custom vertex buffer, but we'll fake it with rectangles
            // Top rectangle (with cut)
            Rectangle topRect = new Rectangle(rect.X + cutSize, rect.Y, rect.Width - cutSize, rect.Height / 2);
            Rectangle cutRect = new Rectangle(rect.X, rect.Y + cutSize, cutSize, rect.Height / 2 - cutSize);
            Rectangle bottomRect = new Rectangle(rect.X, rect.Y + rect.Height / 2, rect.Width, rect.Height / 2);
            // Gradient: draw top with color1, bottom with color2
            spriteBatch.Draw(_pixel, topRect, color1);
            spriteBatch.Draw(_pixel, cutRect, color1);
            spriteBatch.Draw(_pixel, bottomRect, color2);
            // Border: left, right, bottom
            Color border = isActive ? Color.White : new Color(80, 60, 120);
            // Left border (slanted)
            for (int i = 0; i < borderThickness; i++)
                spriteBatch.Draw(_pixel, new Rectangle(rect.X + i, rect.Y + cutSize, borderThickness, rect.Height - cutSize), border);
            // Top border (horizontal, after cut)
            spriteBatch.Draw(_pixel, new Rectangle(rect.X + cutSize, rect.Y, rect.Width - cutSize, borderThickness), border);
            // Right border
            for (int i = 0; i < borderThickness; i++)
                spriteBatch.Draw(_pixel, new Rectangle(rect.Right - borderThickness + i, rect.Y, borderThickness, rect.Height), border);
            // Bottom border
            spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - borderThickness, rect.Width, borderThickness), border);
        }

        private void DrawTabBar(SpriteBatch spriteBatch)
        {
            if (_tabFont == null || _tabs.Count == 0) return;
            _engine.Log($"[ModuleSettingsTabs] Drawing tab bar. Tab count: {_tabs.Count}, Active: {_activeTabIndex}");
            spriteBatch.Draw(_pixel, _tabBarBounds, new Color(60, 40, 90, 220));
            int cutSize = 5;
            int borderThickness = 2;
            int minSliver = 24; // px minimum visible for any tab
            int[] tabWidths = _tabs.Select(t => GetTabWidth(t.Name)).ToArray();
            int totalTabs = _tabs.Count;
            int availableWidth = _tabBarBounds.Width - 2 * _tabPadding;
            int active = _activeTabIndex;
            // 1. Assign full width to active and its neighbors, minSliver to others
            int[] visibleWidths = new int[totalTabs];
            int fullTabsWidth = 0;
            for (int i = 0; i < totalTabs; i++)
            {
                if (i == active || i == active - 1 || i == active + 1)
                {
                    visibleWidths[i] = tabWidths[i];
                    fullTabsWidth += tabWidths[i];
                }
                else
                {
                    visibleWidths[i] = minSliver;
                }
            }
            int minTotalWidth = fullTabsWidth + minSliver * (totalTabs - 3);
            int extraSpace = availableWidth - minTotalWidth;
            // 2. Distribute extra space to sliver tabs if any
            if (extraSpace > 0)
            {
                int sliverCount = totalTabs - 3;
                if (sliverCount > 0)
                {
                    int addPerSliver = extraSpace / sliverCount;
                    for (int i = 0; i < totalTabs; i++)
                    {
                        if (!(i == active || i == active - 1 || i == active + 1))
                            visibleWidths[i] += addPerSliver;
                    }
                }
            }
            // 3. Calculate starting X so all tabs fit
            int totalWidth = visibleWidths.Sum();
            int startX = _tabBarBounds.X + _tabPadding;
            if (totalWidth < availableWidth)
                startX += (availableWidth - totalWidth) / 2;
            // 4. Layout: store rectangles for each tab
            int[] tabXs = new int[totalTabs];
            int x = startX;
            for (int i = 0; i < totalTabs; i++)
            {
                tabXs[i] = x;
                x += visibleWidths[i];
            }
            // 5. Draw tabs in pyramid z-order: furthest first, active last
            _tabRects.Clear();
            // Build draw order: for each distance from active, left first then right
            List<int> drawOrder = new List<int>();
            int maxDist = Math.Max(active, totalTabs - 1 - active);
            for (int d = maxDist; d > 0; d--)
            {
                int left = active - d;
                int right = active + d;
                if (left >= 0) drawOrder.Add(left);
                if (right < totalTabs) drawOrder.Add(right);
            }
            // Then immediate neighbors
            if (active - 1 >= 0) drawOrder.Add(active - 1);
            if (active + 1 < totalTabs) drawOrder.Add(active + 1);
            // Then active tab last (on top)
            drawOrder.Add(active);
            // Draw in this order
            foreach (int i in drawOrder)
            {
                int width = tabWidths[i];
                int visible = visibleWidths[i];
                int tabX = tabXs[i];
                Rectangle tabRect = new Rectangle(tabX, _tabBarBounds.Y + 2, width, _tabBarHeight - 4);
                bool isActive = (i == active);
                DrawTabPolygon(spriteBatch, tabRect, isActive, isActive ? new Color(180, 145, 250) : new Color(147, 112, 219), new Color(110, 70, 180), cutSize, borderThickness);
                // Only the visible part is clickable (not covered by the next tab)
                Rectangle clickRect = new Rectangle(tabX + width - visible, tabRect.Y, visible, tabRect.Height);
                if (_tabRects.Count <= i) _tabRects.Add((clickRect, i));
                else _tabRects[i] = (clickRect, i);
                // Draw text if enough space
                if (visible > 32) {
                    Vector2 textPos = new Vector2(tabX + _tabPadding, tabRect.Y + (tabRect.Height - _tabFont.LineSpacing * 0.7f) / 2);
                    spriteBatch.DrawString(_tabFont, _tabs[i].Name, textPos + new Vector2(1, 1), Color.Black * (isActive ? 0.7f : 0.5f), 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
                    spriteBatch.DrawString(_tabFont, _tabs[i].Name, textPos, Color.White, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
                }
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
                string currentMarkdown = File.Exists(_tabs[_activeTabIndex].SettingsPath) ? File.ReadAllText(_tabs[_activeTabIndex].SettingsPath) : "";
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