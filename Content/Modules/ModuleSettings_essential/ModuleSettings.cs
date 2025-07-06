using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Content;
using System.Collections.Generic;
using System;
using MarySGameEngine.Modules.WindowManagement_essential;
using MarySGameEngine.Modules.TaskBar_essential;

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

        public ModuleSettings(GraphicsDevice graphicsDevice, SpriteFont menuFont, int windowWidth)
        {
            _graphicsDevice = graphicsDevice;
            _menuFont = menuFont;
            _windowWidth = windowWidth;

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
        }

        public void SetTaskBar(TaskBar taskBar)
        {
            _taskBar = taskBar;
            _windowManagement.SetTaskBar(taskBar);
        }

        public void Update()
        {
            _windowManagement.Update();
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            _windowManagement.Draw(spriteBatch, "Module Settings");
        }

        public void UpdateWindowWidth(int newWidth)
        {
            _windowWidth = newWidth;
            _windowManagement.UpdateWindowWidth(newWidth);
            UpdateBounds(); // Maintain center positioning
        }

        public void LoadContent(ContentManager content)
        {
            _content = content;
            _windowManagement.LoadContent(content);
        }

        public void Dispose()
        {
            _windowManagement.Dispose();
        }

        private void UpdateBounds()
        {
            // Get the current window bounds
            Rectangle bounds = _windowManagement.GetWindowBounds();

            // Calculate the new position to be in the center of the screen
            int newX = (_windowWidth - bounds.Width) / 2;
            int newY = 100; // Start below the top bar

            // Update the window position without recreating the WindowManagement instance
            _windowManagement.SetPosition(new Vector2(newX, newY));
        }

        // Method to open the Module Settings window
        public void Open()
        {
            System.Diagnostics.Debug.WriteLine("ModuleSettings: Open() method called");
            
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