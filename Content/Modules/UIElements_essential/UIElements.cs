using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Content;
using System.Collections.Generic;
using System;
using System.Text.RegularExpressions;
using System.Linq;
using System.IO;

namespace MarySGameEngine.Modules.UIElements_essential
{
    // UI Theme colors (matching your Miami theme)
    public static class UITheme
    {
        public static readonly Color Background = new Color(40, 40, 40);
        public static readonly Color Primary = new Color(147, 112, 219);
        public static readonly Color Hover = new Color(180, 145, 250);
        public static readonly Color Text = new Color(220, 220, 220);
        public static readonly Color Border = new Color(147, 112, 219);
        public static readonly Color Disabled = new Color(128, 128, 128);
        public static readonly Color InputBackground = new Color(60, 60, 60);
        public static readonly Color InputBorder = new Color(100, 100, 100);
    }

    public class UIElements
    {
        private GraphicsDevice _graphicsDevice;
        private SpriteFont _font;
        private Texture2D _pixel;
        private List<UIComponent> _components;
        private MouseState _currentMouseState;
        private MouseState _previousMouseState;
        private Rectangle _bounds;
        private GameEngine _engine;
        private string _markdownContent;
        private string _markdownFilePath;
        private Dictionary<string, UIComponent> _namedComponents;
        private Action<Dictionary<string, object>> _onSaveSettings;
        private Action _onResetToDefaults;
        private bool _isHoveringOverButton = false;

        public UIElements(GraphicsDevice graphicsDevice, SpriteFont font, Rectangle bounds, string markdownContent = null, string markdownFilePath = null)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("UIElements: Constructor called");
                
                _graphicsDevice = graphicsDevice;
                _font = font;
                _bounds = bounds;
                _components = new List<UIComponent>();
                _namedComponents = new Dictionary<string, UIComponent>();
                _markdownContent = markdownContent;
                _markdownFilePath = markdownFilePath;
                _isHoveringOverButton = false;
                
                System.Diagnostics.Debug.WriteLine($"UIElements: GraphicsDevice: {graphicsDevice != null}, Font: {font != null}, Bounds: {bounds}");
                
                // Safely get the engine instance
                try
                {
                    _engine = (GameEngine)GameEngine.Instance;
                    System.Diagnostics.Debug.WriteLine($"UIElements: Engine instance: {_engine != null}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"UIElements: Error getting engine instance: {ex.Message}");
                    _engine = null;
                }

                // Create a 1x1 white texture for drawing rectangles
                try
                {
                    _pixel = new Texture2D(graphicsDevice, 1, 1);
                    _pixel.SetData(new[] { Color.White });
                    System.Diagnostics.Debug.WriteLine("UIElements: Pixel texture created successfully");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"UIElements: Error creating pixel texture: {ex.Message}");
                    _pixel = null;
                }
                
                if (!string.IsNullOrEmpty(markdownContent))
                {
                    ParseMarkdown(markdownContent);
                }
                
                System.Diagnostics.Debug.WriteLine("UIElements: Constructor completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UIElements: Error in constructor: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"UIElements: Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        public void Update()
        {
            if (_pixel == null) return; // Don't update if pixel texture failed to create
            
            System.Diagnostics.Debug.WriteLine($"UIElements: Updating {_components.Count} components");
            
            _previousMouseState = _currentMouseState;
            _currentMouseState = Mouse.GetState();

            // Create a copy of the component list to prevent modification during iteration
            var componentsCopy = _components.ToList();

            // Track if we're hovering over any interactive element
            bool wasHoveringOverInteractive = _isHoveringOverButton;
            _isHoveringOverButton = false;

            // Update all components
            foreach (var component in componentsCopy)
            {
                try
                {
                    component.Update(_currentMouseState, _previousMouseState, _bounds.Location, _pixel);
                    
                    // Check if this component is interactive and we're hovering over it
                    if (component is UIButton || 
                        component is UICheckbox || 
                        component is UITextInput || 
                        component is UIColorPicker || 
                        component is UISlider)
                    {
                        _isHoveringOverButton = true;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"UIElements: Error updating component: {ex.Message}");
                }
            }

            // Update cursor based on hover state
            UpdateCursor(wasHoveringOverInteractive);
        }

        private void UpdateCursor(bool wasHoveringOverInteractive)
        {
            try
            {
                // Only change cursor if the hover state actually changed
                if (_isHoveringOverButton != wasHoveringOverInteractive)
                {
                    if (_isHoveringOverButton)
                    {
                        // Request hand cursor when hovering over interactive elements
                        _engine.RequestHandCursor();
                        System.Diagnostics.Debug.WriteLine("UIElements: Requested hand cursor (hovering over interactive element)");
                    }
                    else
                    {
                        // Release hand cursor when not hovering over interactive elements
                        _engine.ReleaseHandCursor();
                        System.Diagnostics.Debug.WriteLine("UIElements: Released hand cursor (not hovering over interactive element)");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UIElements: Error updating cursor: {ex.Message}");
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            if (_pixel == null) return; // Don't draw if pixel texture failed to create
            
            System.Diagnostics.Debug.WriteLine($"UIElements: Drawing {_components.Count} components");
            
            // Create a copy of the component list to prevent modification during iteration
            var componentsCopy = _components.ToList();
            
            // Draw all components
            foreach (var component in componentsCopy)
            {
                try
                {
                    component.Draw(spriteBatch, _bounds.Location, _pixel);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"UIElements: Error drawing component: {ex.Message}");
                }
            }
        }

        public void SetBounds(Rectangle bounds)
        {
            _bounds = bounds;
            // Update component positions if needed
            UpdateComponentLayout();
        }

        public Rectangle GetBounds()
        {
            return _bounds;
        }

        public void LoadFromMarkdown(string markdown)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("UIElements: LoadFromMarkdown called");
                _components.Clear();
                _namedComponents.Clear();
                System.Diagnostics.Debug.WriteLine($"UIElements: Markdown length: {markdown?.Length ?? 0}");
                ParseMarkdown(markdown);
                System.Diagnostics.Debug.WriteLine($"UIElements: LoadFromMarkdown completed, {_components.Count} components created");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UIElements: Error in LoadFromMarkdown: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"UIElements: Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        public void SetSaveSettingsCallback(Action<Dictionary<string, object>> callback)
        {
            _onSaveSettings = callback;
        }

        public void SetResetToDefaultsCallback(Action callback)
        {
            _onResetToDefaults = callback;
        }

        public UIComponent GetComponentByName(string name)
        {
            return _namedComponents.ContainsKey(name) ? _namedComponents[name] : null;
        }

        public Dictionary<string, object> GetAllValues()
        {
            var values = new Dictionary<string, object>();
            
            foreach (var kvp in _namedComponents)
            {
                string name = kvp.Key;
                UIComponent component = kvp.Value;
                
                if (component is UICheckbox checkbox)
                {
                    values[name] = checkbox.IsChecked();
                }
                else if (component is UITextInput textInput)
                {
                    values[name] = textInput.GetText();
                }
                else if (component is UIColorPicker colorPicker)
                {
                    values[name] = colorPicker.GetColor();
                }
                else if (component is UISlider slider)
                {
                    values[name] = slider.GetValue();
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"UIElements: GetAllValues returning {values.Count} values");
            foreach (var kvp in values)
            {
                System.Diagnostics.Debug.WriteLine($"UIElements: {kvp.Key} = {kvp.Value}");
            }
            
            return values;
        }

        private void ParseMarkdown(string markdown)
        {
            if (_font == null)
            {
                System.Diagnostics.Debug.WriteLine("UIElements: Font is null, cannot parse markdown");
                return;
            }

            string[] lines = markdown.Split('\n');
            int currentY = 10; // Start with some padding
            int padding = 10;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                try
                {
                    // Parse different markdown elements
                    if (line.StartsWith("# "))
                    {
                        // H1 Title - largest scale
                        string title = line.Substring(2);
                        _components.Add(new UITitle(title, new Vector2(padding, currentY), _font, 1.5f));
                        currentY += (int)(_font.MeasureString(title).Y * 1.5f) + 15;
                    }
                    else if (line.StartsWith("## "))
                    {
                        // H2 Subtitle - large scale
                        string subtitle = line.Substring(3);
                        _components.Add(new UITitle(subtitle, new Vector2(padding, currentY), _font, 1.25f));
                        currentY += (int)(_font.MeasureString(subtitle).Y * 1.25f) + 12;
                    }
                    else if (line.StartsWith("### "))
                    {
                        // H3 Subtitle - medium-large scale
                        string subtitle = line.Substring(4);
                        _components.Add(new UITitle(subtitle, new Vector2(padding, currentY), _font, 1.1f));
                        currentY += (int)(_font.MeasureString(subtitle).Y * 1.1f) + 10;
                    }
                    else if (line.StartsWith("#### "))
                    {
                        // H4 Subtitle - medium scale
                        string subtitle = line.Substring(5);
                        _components.Add(new UITitle(subtitle, new Vector2(padding, currentY), _font, 1.0f));
                        currentY += (int)(_font.MeasureString(subtitle).Y * 1.0f) + 8;
                    }
                    else if (line.StartsWith("##### "))
                    {
                        // H5 Subtitle - small-medium scale
                        string subtitle = line.Substring(6);
                        _components.Add(new UITitle(subtitle, new Vector2(padding, currentY), _font, 0.9f));
                        currentY += (int)(_font.MeasureString(subtitle).Y * 0.9f) + 6;
                    }
                    else if (line.StartsWith("###### "))
                    {
                        // H6 Subtitle - smallest scale
                        string subtitle = line.Substring(7);
                        _components.Add(new UITitle(subtitle, new Vector2(padding, currentY), _font, 0.8f));
                        currentY += (int)(_font.MeasureString(subtitle).Y * 0.8f) + 5;
                    }
                    else if (line.StartsWith("- [x] "))
                    {
                        // Checked checkbox
                        string text = line.Substring(6);
                        var checkbox = new UICheckbox(text, new Vector2(padding, currentY), _font, true);
                        checkbox.SetOnChanged((isChecked) => OnCheckboxChanged(i, isChecked));
                        _components.Add(checkbox);
                        _namedComponents[$"checkbox_{i}"] = checkbox;
                        currentY += Math.Max(16, (int)_font.MeasureString(text).Y) + 5;
                    }
                    else if (line.StartsWith("- [ ] "))
                    {
                        // Unchecked checkbox
                        string text = line.Substring(6);
                        var checkbox = new UICheckbox(text, new Vector2(padding, currentY), _font, false);
                        checkbox.SetOnChanged((isChecked) => OnCheckboxChanged(i, isChecked));
                        _components.Add(checkbox);
                        _namedComponents[$"checkbox_{i}"] = checkbox;
                        currentY += Math.Max(16, (int)_font.MeasureString(text).Y) + 5;
                    }
                    else if (line.StartsWith("Input: "))
                    {
                        // Text input
                        string placeholder = line.Substring(7);
                        var textInput = new UITextInput(placeholder, new Vector2(padding, currentY), _font, 200);
                        textInput.SetOnTextChanged((text) => OnTextInputChanged(i, text));
                        _components.Add(textInput);
                        _namedComponents[$"input_{i}"] = textInput;
                        currentY += 25 + 5;
                    }
                    else if (line.StartsWith("Color: "))
                    {
                        // Color picker
                        string colorText = line.Substring(7);
                        string colorHex = "";
                        string label = colorText;
                        
                        // Parse color hex if present
                        if (colorText.Contains(" "))
                        {
                            var parts = colorText.Split(' ', 2);
                            colorHex = parts[0];
                            label = parts[1];
                        }
                        
                        var colorPicker = new UIColorPicker(label, new Vector2(padding, currentY), _font);
                        if (!string.IsNullOrEmpty(colorHex) && colorHex.StartsWith("#"))
                        {
                            colorPicker.SetColor(ParseHexColor(colorHex));
                        }
                        colorPicker.SetOnColorChanged((color) => OnColorChanged(i, color));
                        _components.Add(colorPicker);
                        _namedComponents[$"color_{i}"] = colorPicker;
                        currentY += 20 + 5;
                    }
                    else if (line.StartsWith("Scale ["))
                    {
                        // Scale with new format: "Scale [min=X, max=Y, step=Z, value=W] Label"
                        string sliderText = line.Trim();
                        var slider = ParseSlider(sliderText, new Vector2(padding, currentY));
                        if (slider != null)
                        {
                            slider.SetOnValueChanged((value) => OnSliderChanged(i, value));
                            _components.Add(slider);
                            _namedComponents[$"slider_{i}"] = slider;
                            currentY += slider.GetHeight() + 5;
                        }
                    }
                    else if (line.StartsWith("Button: "))
                    {
                        // Button
                        string buttonText = line.Substring(8);
                        var button = new UIButton(buttonText, new Vector2(padding, currentY), _font);
                        button.SetOnClick(() => OnButtonClicked(buttonText));
                        _components.Add(button);
                        currentY += button.GetHeight() + 5;
                    }
                    else if (line.StartsWith("---"))
                    {
                        // Separator
                        _components.Add(new UISeparator(new Vector2(padding, currentY), 300));
                        currentY += 10 + 5;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"UIElements: Error parsing line {i}: {line} - {ex.Message}");
                }
            }
        }

        private void UpdateComponentLayout()
        {
            System.Diagnostics.Debug.WriteLine($"UIElements: UpdateComponentLayout called with {_components.Count} components");
            
            // Recalculate component positions based on new bounds
            // This is a simple implementation - could be more sophisticated
            int currentY = 10;
            int padding = 10;

            foreach (var component in _components)
            {
                Vector2 oldPosition = component.GetPosition();
                Vector2 newPosition = new Vector2(padding, currentY);
                
                // Use SetPositionPreservingState for checkboxes, regular SetPosition for others
                if (component is UICheckbox checkbox)
                {
                    checkbox.SetPositionPreservingState(newPosition);
                }
                else
                {
                    component.SetPosition(newPosition);
                }
                
                System.Diagnostics.Debug.WriteLine($"UIElements: Component moved from {oldPosition} to ({padding}, {currentY})");
                currentY += component.GetHeight() + 5;
            }
            
            System.Diagnostics.Debug.WriteLine($"UIElements: UpdateComponentLayout completed");
        }

        public void Dispose()
        {
            // Reset cursor to default when disposing
            try
            {
                _engine.ReleaseHandCursor();
                System.Diagnostics.Debug.WriteLine("UIElements: Reset cursor to arrow on dispose");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UIElements: Error resetting cursor on dispose: {ex.Message}");
            }
            
            _pixel?.Dispose();
        }

        public void ResetCursor()
        {
            try
            {
                _engine.ReleaseHandCursor();
                _isHoveringOverButton = false;
                System.Diagnostics.Debug.WriteLine("UIElements: Reset cursor to arrow");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UIElements: Error resetting cursor: {ex.Message}");
            }
        }

        // Callback methods for component changes
        private void OnCheckboxChanged(int lineIndex, bool isChecked)
        {
            System.Diagnostics.Debug.WriteLine($"UIElements: Checkbox changed at line {lineIndex}, checked: {isChecked}");
            UpdateMarkdownFile(lineIndex, isChecked ? "- [x] " : "- [ ] ");
        }

        private void OnTextInputChanged(int lineIndex, string text)
        {
            System.Diagnostics.Debug.WriteLine($"UIElements: Text input changed at line {lineIndex}, text: {text}");
            UpdateMarkdownFile(lineIndex, $"Input: {text}");
        }

        private void OnColorChanged(int lineIndex, Color color)
        {
            System.Diagnostics.Debug.WriteLine($"UIElements: Color changed at line {lineIndex}, color: {color}");
            string hexColor = ColorToHex(color);
            UpdateMarkdownFile(lineIndex, $"Color: {hexColor} ");
        }

        private void OnSliderChanged(int lineIndex, float value)
        {
            System.Diagnostics.Debug.WriteLine($"UIElements: Slider changed at line {lineIndex}, value: {value}");
            // Get the slider component to get its current configuration
            if (lineIndex < _components.Count && _components[lineIndex] is UISlider slider)
            {
                int currentValue = slider.GetCurrentValue();
                // We need to reconstruct the slider line with the new value
                // This is a bit complex, so we'll just log for now
                System.Diagnostics.Debug.WriteLine($"UIElements: Slider value updated to {currentValue}");
            }
        }

        private void OnButtonClicked(string buttonText)
        {
            System.Diagnostics.Debug.WriteLine($"UIElements: Button clicked: {buttonText}");
            
            if (buttonText == "Save Settings")
            {
                var values = GetAllValues();
                _onSaveSettings?.Invoke(values);
                System.Diagnostics.Debug.WriteLine($"UIElements: Save Settings called with {values.Count} values");
            }
            else if (buttonText == "Reset to Defaults")
            {
                _onResetToDefaults?.Invoke();
                System.Diagnostics.Debug.WriteLine($"UIElements: Reset to Defaults called");
            }
        }

        private void UpdateMarkdownFile(int lineIndex, string newLineStart)
        {
            if (string.IsNullOrEmpty(_markdownFilePath) || string.IsNullOrEmpty(_markdownContent))
                return;

            try
            {
                string[] lines = _markdownContent.Split('\n');
                if (lineIndex < lines.Length)
                {
                    string line = lines[lineIndex];
                    string newLine = newLineStart + line.Substring(line.IndexOf(' ') + 1);
                    lines[lineIndex] = newLine;
                    
                    string newContent = string.Join("\n", lines);
                    File.WriteAllText(_markdownFilePath, newContent);
                    _markdownContent = newContent;
                    
                    System.Diagnostics.Debug.WriteLine($"UIElements: Updated markdown file at line {lineIndex}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UIElements: Error updating markdown file: {ex.Message}");
            }
        }

        private string ColorToHex(Color color)
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        // Helper methods for parsing
        private UISlider ParseSlider(string sliderText, Vector2 position)
        {
            try
            {
                // Format: "Scale [min=X, max=Y, step=Z, value=W] Label"
                // Example: "Scale [min=1, max=100, step=1, value=35] UI Slider"
                
                // Find the label part (after the closing bracket)
                int closingBracketIndex = sliderText.IndexOf(']');
                if (closingBracketIndex == -1) return null;
                
                // The label starts after the closing bracket and a space
                int labelStartIndex = closingBracketIndex + 1;
                if (labelStartIndex < sliderText.Length && sliderText[labelStartIndex] == ' ')
                {
                    labelStartIndex++; // Skip the space after the bracket
                }
                
                string label = sliderText.Substring(labelStartIndex);
                string configPart = sliderText.Substring(0, closingBracketIndex + 1);
                
                // Parse the configuration part
                if (!configPart.StartsWith("Scale [")) return null;
                
                // Extract the parameters from [min=X, max=Y, step=Z, value=W]
                string paramsPart = configPart.Substring(7, configPart.Length - 8); // Remove "Scale [" and "]"
                var paramArray = paramsPart.Split(',');
                
                int min = 0, max = 100, step = 1, value = 50;
                
                foreach (var param in paramArray)
                {
                    var trimmed = param.Trim();
                    if (trimmed.StartsWith("min="))
                        min = int.Parse(trimmed.Substring(4));
                    else if (trimmed.StartsWith("max="))
                        max = int.Parse(trimmed.Substring(4));
                    else if (trimmed.StartsWith("step="))
                        step = int.Parse(trimmed.Substring(5));
                    else if (trimmed.StartsWith("value="))
                        value = int.Parse(trimmed.Substring(6));
                }
                
                // Create slider
                var slider = new UISlider(label, position, _font, 200);
                slider.SetRange(min, max, step);
                slider.SetValue((float)(value - min) / (max - min)); // Convert to 0-1 range
                
                return slider;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UIElements: Error parsing slider '{sliderText}': {ex.Message}");
                return null;
            }
        }

        private Color ParseHexColor(string hexColor)
        {
            try
            {
                // Remove # if present
                if (hexColor.StartsWith("#"))
                    hexColor = hexColor.Substring(1);
                
                // Parse RGB values
                int r = Convert.ToInt32(hexColor.Substring(0, 2), 16);
                int g = Convert.ToInt32(hexColor.Substring(2, 2), 16);
                int b = Convert.ToInt32(hexColor.Substring(4, 2), 16);
                
                return new Color(r, g, b);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UIElements: Error parsing hex color '{hexColor}': {ex.Message}");
                return Color.White;
            }
        }
    }

    // Base UI Component class
    public abstract class UIComponent
    {
        protected Vector2 _position;
        protected bool _isHovered;
        protected bool _isFocused;
        protected bool _isVisible = true;

        public UIComponent(Vector2 position)
        {
            _position = position;
        }

        public abstract void Update(MouseState currentMouse, MouseState previousMouse, Point offset, Texture2D pixel);
        public abstract void Draw(SpriteBatch spriteBatch, Point offset, Texture2D pixel);
        public abstract int GetHeight();
        public abstract Rectangle GetBounds(Point offset);

        public virtual void SetPosition(Vector2 position)
        {
            _position = position;
        }

        public virtual Vector2 GetPosition()
        {
            return _position;
        }

        public virtual bool IsHovered(Point mousePos, Point offset)
        {
            return GetBounds(offset).Contains(mousePos);
        }
    }

    // Title component
    public class UITitle : UIComponent
    {
        private string _text;
        private SpriteFont _font;
        private Color _color;
        private float _scale;

        public UITitle(string text, Vector2 position, SpriteFont font, float scale = 1.0f) : base(position)
        {
            _text = text;
            _font = font;
            _color = UITheme.Primary;
            _scale = scale;
        }

        public override void Update(MouseState currentMouse, MouseState previousMouse, Point offset, Texture2D pixel)
        {
            _isHovered = IsHovered(currentMouse.Position, offset);
        }

        public override void Draw(SpriteBatch spriteBatch, Point offset, Texture2D pixel)
        {
            Vector2 drawPos = _position + new Vector2(offset.X, offset.Y);
            spriteBatch.DrawString(_font, _text, drawPos, _color, 0f, Vector2.Zero, _scale, SpriteEffects.None, 0f);
        }

        public override int GetHeight()
        {
            return (int)(_font.MeasureString(_text).Y * _scale);
        }

        public override Rectangle GetBounds(Point offset)
        {
            Vector2 size = _font.MeasureString(_text) * _scale;
            return new Rectangle((int)(_position.X + offset.X), (int)(_position.Y + offset.Y), (int)size.X, (int)size.Y);
        }
    }

    // Text component
    public class UIText : UIComponent
    {
        private string _text;
        private SpriteFont _font;
        private Color _color;

        public UIText(string text, Vector2 position, SpriteFont font, Color color) : base(position)
        {
            _text = text;
            _font = font;
            _color = color;
        }

        public override void Update(MouseState currentMouse, MouseState previousMouse, Point offset, Texture2D pixel)
        {
            _isHovered = IsHovered(currentMouse.Position, offset);
        }

        public override void Draw(SpriteBatch spriteBatch, Point offset, Texture2D pixel)
        {
            Vector2 drawPos = _position + new Vector2(offset.X, offset.Y);
            spriteBatch.DrawString(_font, _text, drawPos, _color);
        }

        public override int GetHeight()
        {
            return (int)_font.MeasureString(_text).Y;
        }

        public override Rectangle GetBounds(Point offset)
        {
            Vector2 size = _font.MeasureString(_text);
            return new Rectangle((int)(_position.X + offset.X), (int)(_position.Y + offset.Y), (int)size.X, (int)size.Y);
        }
    }

    // Button component
    public class UIButton : UIComponent
    {
        private string _text;
        private SpriteFont _font;
        private Rectangle _bounds;
        private bool _isPressed;
        private Action _onClick;

        public UIButton(string text, Vector2 position, SpriteFont font) : base(position)
        {
            _text = text;
            _font = font;
            UpdateBounds();
        }

        private void UpdateBounds()
        {
            Vector2 textSize = _font.MeasureString(_text);
            _bounds = new Rectangle((int)_position.X, (int)_position.Y, (int)textSize.X + 20, (int)textSize.Y + 10);
        }

        public override void Update(MouseState currentMouse, MouseState previousMouse, Point offset, Texture2D pixel)
        {
            Rectangle bounds = GetBounds(offset);
            _isHovered = bounds.Contains(currentMouse.Position);

            if (_isHovered && currentMouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released)
            {
                _onClick?.Invoke();
            }
        }

        public override void Draw(SpriteBatch spriteBatch, Point offset, Texture2D pixel)
        {
            Rectangle drawBounds = GetBounds(offset);
            
            // Draw button background
            Color bgColor = _isPressed ? UITheme.Primary : (_isHovered ? UITheme.Hover : UITheme.InputBackground);
            spriteBatch.Draw(pixel, drawBounds, bgColor);
            
            // Draw button border
            spriteBatch.Draw(pixel, new Rectangle(drawBounds.X, drawBounds.Y, drawBounds.Width, 1), UITheme.Border);
            spriteBatch.Draw(pixel, new Rectangle(drawBounds.X, drawBounds.Bottom - 1, drawBounds.Width, 1), UITheme.Border);
            spriteBatch.Draw(pixel, new Rectangle(drawBounds.X, drawBounds.Y, 1, drawBounds.Height), UITheme.Border);
            spriteBatch.Draw(pixel, new Rectangle(drawBounds.Right - 1, drawBounds.Y, 1, drawBounds.Height), UITheme.Border);
            
            // Draw text
            Vector2 textPos = new Vector2(
                drawBounds.X + (drawBounds.Width - _font.MeasureString(_text).X) / 2,
                drawBounds.Y + (drawBounds.Height - _font.MeasureString(_text).Y) / 2
            );
            spriteBatch.DrawString(_font, _text, textPos, UITheme.Text);
        }

        public override int GetHeight()
        {
            return 30;
        }

        public override Rectangle GetBounds(Point offset)
        {
            return new Rectangle(_bounds.X + offset.X, _bounds.Y + offset.Y, _bounds.Width, _bounds.Height);
        }

        public void SetOnClick(Action onClick)
        {
            _onClick = onClick;
        }
    }

    // Checkbox component
    public class UICheckbox : UIComponent
    {
        private string _text;
        private SpriteFont _font;
        private bool _isChecked;
        private Rectangle _checkboxBounds;
        private Action<bool> _onChanged;

        public UICheckbox(string text, Vector2 position, SpriteFont font, bool isChecked = false) : base(position)
        {
            _text = text;
            _font = font;
            _isChecked = isChecked;
            _checkboxBounds = new Rectangle((int)position.X, (int)position.Y, 16, 16);
        }

        public override void Update(MouseState currentMouse, MouseState previousMouse, Point offset, Texture2D pixel)
        {
            Rectangle bounds = GetBounds(offset);
            _isHovered = bounds.Contains(currentMouse.Position);

            if (_isHovered && currentMouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released)
            {
                bool oldChecked = _isChecked;
                _isChecked = !_isChecked;
                if (oldChecked != _isChecked)
                {
                    _onChanged?.Invoke(_isChecked);
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch, Point offset, Texture2D pixel)
        {
            Rectangle drawBounds = GetBounds(offset);
            Rectangle checkboxDrawBounds = new Rectangle(
                (int)(_position.X + offset.X), 
                (int)(_position.Y + offset.Y), 
                16, 16
            );
            
            System.Diagnostics.Debug.WriteLine($"UICheckbox: Drawing '{_text}', checked: {_isChecked}, position: {_position}, offset: {offset}");
            
            // Safety checks
            if (_font == null)
            {
                System.Diagnostics.Debug.WriteLine($"UICheckbox: Font is null for '{_text}'");
                return;
            }
            
            if (string.IsNullOrEmpty(_text))
            {
                System.Diagnostics.Debug.WriteLine($"UICheckbox: Text is null or empty");
                return;
            }
            
            // Draw checkbox background - fill with color when checked
            Color bgColor;
            if (_isChecked)
            {
                bgColor = UITheme.Primary; // Fill with purple when checked
            }
            else
            {
                bgColor = _isHovered ? UITheme.Hover : UITheme.InputBackground;
            }
            spriteBatch.Draw(pixel, checkboxDrawBounds, bgColor);
            
            // Draw checkbox border
            Color borderColor = _isChecked ? UITheme.Text : UITheme.Border; // White border when checked
            spriteBatch.Draw(pixel, new Rectangle(checkboxDrawBounds.X, checkboxDrawBounds.Y, checkboxDrawBounds.Width, 1), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(checkboxDrawBounds.X, checkboxDrawBounds.Bottom - 1, checkboxDrawBounds.Width, 1), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(checkboxDrawBounds.X, checkboxDrawBounds.Y, 1, checkboxDrawBounds.Height), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(checkboxDrawBounds.Right - 1, checkboxDrawBounds.Y, 1, checkboxDrawBounds.Height), borderColor);
            
            // Draw checkmark if checked
            if (_isChecked)
            {
                System.Diagnostics.Debug.WriteLine($"UICheckbox: Drawing checkmark for '{_text}'");
                // Use a white "X" on the purple background - more reliable than "✓"
                spriteBatch.DrawString(_font, "X", new Vector2(checkboxDrawBounds.X + 2, checkboxDrawBounds.Y - 2), Color.White);
            }
            
            // Draw text - always draw it regardless of checkbox state
            Vector2 textPos = new Vector2(checkboxDrawBounds.Right + 8, checkboxDrawBounds.Y);
            System.Diagnostics.Debug.WriteLine($"UICheckbox: Drawing text '{_text}' at position {textPos}");
            spriteBatch.DrawString(_font, _text, textPos, UITheme.Text);
        }

        public override int GetHeight()
        {
            return Math.Max(16, (int)_font.MeasureString(_text).Y);
        }

        public override Rectangle GetBounds(Point offset)
        {
            Vector2 textSize = _font.MeasureString(_text);
            return new Rectangle(
                (int)(_position.X + offset.X), 
                (int)(_position.Y + offset.Y), 
                16 + 8 + (int)textSize.X, 
                Math.Max(16, (int)textSize.Y)
            );
        }

        public void SetOnChanged(Action<bool> onChanged)
        {
            _onChanged = onChanged;
        }

        public void SetPositionPreservingState(Vector2 position)
        {
            // Preserve the checked state when position changes
            bool wasChecked = _isChecked;
            _position = position;
            _isChecked = wasChecked;
            // Update checkbox bounds to match new position
            _checkboxBounds = new Rectangle((int)position.X, (int)position.Y, 16, 16);
            System.Diagnostics.Debug.WriteLine($"UICheckbox: Position changed for '{_text}', preserving checked state: {_isChecked}, new bounds: {_checkboxBounds}");
        }

        public bool IsChecked()
        {
            return _isChecked;
        }

        public void SetChecked(bool isChecked)
        {
            _isChecked = isChecked;
        }
    }

    // Text input component
    public class UITextInput : UIComponent
    {
        private string _placeholder;
        private string _text;
        private SpriteFont _font;
        private int _maxWidth;
        private bool _isFocused;
        private float _cursorBlinkTimer;
        private bool _showCursor;
        private Action<string> _onTextChanged;

        public UITextInput(string placeholder, Vector2 position, SpriteFont font, int maxWidth) : base(position)
        {
            _placeholder = placeholder;
            _font = font;
            _maxWidth = maxWidth;
            _text = "";
            _isFocused = false;
            _cursorBlinkTimer = 0f;
            _showCursor = false;
        }

        public override void Update(MouseState currentMouse, MouseState previousMouse, Point offset, Texture2D pixel)
        {
            Rectangle bounds = GetBounds(offset);
            _isHovered = bounds.Contains(currentMouse.Position);

            if (_isHovered && currentMouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released)
            {
                _isFocused = true;
            }
            else if (!_isHovered && currentMouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released)
            {
                _isFocused = false;
            }

            if (_isFocused)
            {
                // Handle text input (simplified - you can enhance this)
                var keyboardState = Keyboard.GetState();
                if (keyboardState.IsKeyDown(Keys.Back) && _text.Length > 0)
                {
                    _text = _text.Substring(0, _text.Length - 1);
                    _onTextChanged?.Invoke(_text);
                }
                else if (keyboardState.IsKeyDown(Keys.Space))
                {
                    _text += " ";
                    _onTextChanged?.Invoke(_text);
                }
                // Add more key handling as needed
            }

            // Update cursor blink
            _cursorBlinkTimer += (float)GameEngine.Instance.TargetElapsedTime.TotalSeconds;
            if (_cursorBlinkTimer >= 0.5f)
            {
                _showCursor = !_showCursor;
                _cursorBlinkTimer = 0f;
            }
        }

        public override void Draw(SpriteBatch spriteBatch, Point offset, Texture2D pixel)
        {
            Rectangle drawBounds = GetBounds(offset);
            
            // Draw input background
            Color bgColor = _isFocused ? UITheme.Hover : (_isHovered ? UITheme.InputBackground : UITheme.InputBackground);
            spriteBatch.Draw(pixel, drawBounds, bgColor);
            
            // Draw input border
            Color borderColor = _isFocused ? UITheme.Primary : UITheme.InputBorder;
            spriteBatch.Draw(pixel, new Rectangle(drawBounds.X, drawBounds.Y, drawBounds.Width, 1), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(drawBounds.X, drawBounds.Bottom - 1, drawBounds.Width, 1), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(drawBounds.X, drawBounds.Y, 1, drawBounds.Height), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(drawBounds.Right - 1, drawBounds.Y, 1, drawBounds.Height), borderColor);
            
            // Draw text or placeholder
            string displayText = string.IsNullOrEmpty(_text) ? _placeholder : _text;
            Color textColor = string.IsNullOrEmpty(_text) ? UITheme.Disabled : UITheme.Text;
            
            Vector2 textPos = new Vector2(drawBounds.X + 5, drawBounds.Y + (drawBounds.Height - _font.MeasureString(displayText).Y) / 2);
            spriteBatch.DrawString(_font, displayText, textPos, textColor);
            
            // Draw cursor if focused and showing
            if (_isFocused && _showCursor)
            {
                Vector2 cursorPos = new Vector2(
                    drawBounds.X + 5 + _font.MeasureString(_text).X,
                    drawBounds.Y + (drawBounds.Height - _font.MeasureString("|").Y) / 2
                );
                spriteBatch.DrawString(_font, "|", cursorPos, UITheme.Primary);
            }
        }

        public override int GetHeight()
        {
            return 25; // Fixed height for text input
        }

        public override Rectangle GetBounds(Point offset)
        {
            return new Rectangle((int)(_position.X + offset.X), (int)(_position.Y + offset.Y), _maxWidth, 25);
        }

        public string GetText()
        {
            return _text;
        }

        public void SetText(string text)
        {
            _text = text;
        }

        public void SetOnTextChanged(Action<string> onTextChanged)
        {
            _onTextChanged = onTextChanged;
        }
    }

    // Color picker component
    public class UIColorPicker : UIComponent
    {
        private string _label;
        private SpriteFont _font;
        private Color _selectedColor = Color.White;
        private Rectangle _colorBoxBounds;
        private bool _isColorPickerOpen;
        private Action<Color> _onColorChanged;

        public UIColorPicker(string label, Vector2 position, SpriteFont font) : base(position)
        {
            _label = label;
            _font = font;
            _colorBoxBounds = new Rectangle((int)position.X, (int)position.Y, 30, 20);
        }

        public override void Update(MouseState currentMouse, MouseState previousMouse, Point offset, Texture2D pixel)
        {
            Rectangle bounds = GetBounds(offset);
            _isHovered = bounds.Contains(currentMouse.Position);

            if (_isHovered && currentMouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released)
            {
                _isColorPickerOpen = !_isColorPickerOpen;
            }

            if (_isColorPickerOpen)
            {
                // Handle color picker interaction
                Rectangle pickerBounds = new Rectangle(bounds.X, bounds.Bottom + 5, 200, 100);
                if (pickerBounds.Contains(currentMouse.Position) && currentMouse.LeftButton == ButtonState.Pressed)
                {
                    // Simple color selection (you can enhance this)
                    Color newColor = GetColorFromPosition(currentMouse.Position, pickerBounds);
                    if (newColor != _selectedColor)
                    {
                        _selectedColor = newColor;
                        _onColorChanged?.Invoke(_selectedColor);
                    }
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch, Point offset, Texture2D pixel)
        {
            Rectangle drawBounds = GetBounds(offset);
            Rectangle colorBoxDrawBounds = new Rectangle(
                (int)(_position.X + offset.X), 
                (int)(_position.Y + offset.Y), 
                30, 20
            );
            
            // Draw label
            Vector2 labelPos = new Vector2(colorBoxDrawBounds.Right + 10, colorBoxDrawBounds.Y);
            spriteBatch.DrawString(_font, _label, labelPos, UITheme.Text);
            
            // Draw color box
            spriteBatch.Draw(pixel, colorBoxDrawBounds, _selectedColor);
            
            // Draw color box border
            spriteBatch.Draw(pixel, new Rectangle(colorBoxDrawBounds.X, colorBoxDrawBounds.Y, colorBoxDrawBounds.Width, 1), UITheme.Border);
            spriteBatch.Draw(pixel, new Rectangle(colorBoxDrawBounds.X, colorBoxDrawBounds.Bottom - 1, colorBoxDrawBounds.Width, 1), UITheme.Border);
            spriteBatch.Draw(pixel, new Rectangle(colorBoxDrawBounds.X, colorBoxDrawBounds.Y, 1, colorBoxDrawBounds.Height), UITheme.Border);
            spriteBatch.Draw(pixel, new Rectangle(colorBoxDrawBounds.Right - 1, colorBoxDrawBounds.Y, 1, colorBoxDrawBounds.Height), UITheme.Border);
        }

        public override int GetHeight()
        {
            return Math.Max(20, (int)_font.MeasureString(_label).Y);
        }

        public override Rectangle GetBounds(Point offset)
        {
            Vector2 labelSize = _font.MeasureString(_label);
            return new Rectangle(
                (int)(_position.X + offset.X), 
                (int)(_position.Y + offset.Y), 
                30 + 10 + (int)labelSize.X, 
                Math.Max(20, (int)labelSize.Y)
            );
        }

        public void SetColor(Color color)
        {
            _selectedColor = color;
        }

        public Color GetColor()
        {
            return _selectedColor;
        }

        public void SetOnColorChanged(Action<Color> onColorChanged)
        {
            _onColorChanged = onColorChanged;
        }

        private Color GetColorFromPosition(Point position, Rectangle pickerBounds)
        {
            // Simple color mapping based on position
            float relativeX = (position.X - pickerBounds.X) / (float)pickerBounds.Width;
            float relativeY = (position.Y - pickerBounds.Y) / (float)pickerBounds.Height;
            
            // Create a simple color palette
            int r = (int)(relativeX * 255);
            int g = (int)(relativeY * 255);
            int b = (int)((relativeX + relativeY) / 2 * 255);
            
            return new Color(r, g, b);
        }
    }

    // Slider component
    public class UISlider : UIComponent
    {
        private string _label;
        private SpriteFont _font;
        private float _value = 0.5f;
        private int _maxWidth;
        private bool _isDragging;
        private Rectangle _sliderBounds;
        private Action<float> _onValueChanged;
        private int _minValue = 0;
        private int _maxValue = 100;
        private int _step = 1;

        public UISlider(string label, Vector2 position, SpriteFont font, int maxWidth) : base(position)
        {
            _label = label;
            _font = font;
            _maxWidth = maxWidth;
            _sliderBounds = new Rectangle((int)position.X, (int)position.Y, maxWidth, 20);
        }

        public void SetRange(int min, int max, int step)
        {
            _minValue = min;
            _maxValue = max;
            _step = step;
        }

        public int GetCurrentValue()
        {
            return _minValue + (int)(_value * (_maxValue - _minValue));
        }

        public override void Update(MouseState currentMouse, MouseState previousMouse, Point offset, Texture2D pixel)
        {
            Rectangle bounds = GetBounds(offset);
            _isHovered = bounds.Contains(currentMouse.Position);

            if (_isHovered && currentMouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released)
            {
                _isDragging = true;
            }

            if (_isDragging)
            {
                if (currentMouse.LeftButton == ButtonState.Released)
                {
                    _isDragging = false;
                }
                else
                {
                    float relativeX = (currentMouse.Position.X - bounds.X) / (float)bounds.Width;
                    float oldValue = _value;
                    _value = MathHelper.Clamp(relativeX, 0f, 1f);
                    
                    // Apply step increments
                    int currentValue = GetCurrentValue();
                    int steppedValue = (currentValue / _step) * _step;
                    _value = (float)(steppedValue - _minValue) / (_maxValue - _minValue);
                    
                    if (Math.Abs(_value - oldValue) > 0.001f)
                    {
                        _onValueChanged?.Invoke(_value);
                    }
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch, Point offset, Texture2D pixel)
        {
            Rectangle drawBounds = GetBounds(offset);
            
            // Draw slider background
            spriteBatch.Draw(pixel, drawBounds, UITheme.InputBackground);
            
            // Draw slider border
            spriteBatch.Draw(pixel, new Rectangle(drawBounds.X, drawBounds.Y, drawBounds.Width, 1), UITheme.Border);
            spriteBatch.Draw(pixel, new Rectangle(drawBounds.X, drawBounds.Bottom - 1, drawBounds.Width, 1), UITheme.Border);
            spriteBatch.Draw(pixel, new Rectangle(drawBounds.X, drawBounds.Y, 1, drawBounds.Height), UITheme.Border);
            spriteBatch.Draw(pixel, new Rectangle(drawBounds.Right - 1, drawBounds.Y, 1, drawBounds.Height), UITheme.Border);
            
            // Draw slider handle
            int handleWidth = 10;
            int handleX = drawBounds.X + (int)(_value * (drawBounds.Width - handleWidth));
            Rectangle handleBounds = new Rectangle(handleX, drawBounds.Y, handleWidth, drawBounds.Height);
            Color handleColor = _isDragging ? UITheme.Primary : (_isHovered ? UITheme.Hover : UITheme.Border);
            spriteBatch.Draw(pixel, handleBounds, handleColor);
            
            // Draw label and current value
            string displayText = $"{_label}: {GetCurrentValue()}";
            Vector2 textPos = new Vector2(drawBounds.X, drawBounds.Y - _font.LineSpacing - 2);
            spriteBatch.DrawString(_font, displayText, textPos, UITheme.Text);
        }

        public override int GetHeight()
        {
            return 40; // Label height + slider height
        }

        public override Rectangle GetBounds(Point offset)
        {
            return new Rectangle((int)(_position.X + offset.X), (int)(_position.Y + offset.Y), _maxWidth, 20);
        }

        public float GetValue()
        {
            return _value;
        }

        public void SetValue(float value)
        {
            _value = MathHelper.Clamp(value, 0f, 1f);
        }

        public void SetOnValueChanged(Action<float> onValueChanged)
        {
            _onValueChanged = onValueChanged;
        }
    }

    // Separator component
    public class UISeparator : UIComponent
    {
        private int _width;

        public UISeparator(Vector2 position, int width) : base(position)
        {
            _width = width;
        }

        public override void Update(MouseState currentMouse, MouseState previousMouse, Point offset, Texture2D pixel)
        {
            // Separators don't need interaction
        }

        public override void Draw(SpriteBatch spriteBatch, Point offset, Texture2D pixel)
        {
            Rectangle drawBounds = GetBounds(offset);
            spriteBatch.Draw(pixel, drawBounds, UITheme.Border);
        }

        public override int GetHeight()
        {
            return 2; // 2 pixel height for separator
        }

        public override Rectangle GetBounds(Point offset)
        {
            return new Rectangle((int)(_position.X + offset.X), (int)(_position.Y + offset.Y), _width, 2);
        }
    }
} 