using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Content;
using System.Collections.Generic;
using System;
using MarySGameEngine.Modules.WindowManagement_essential;
using MarySGameEngine.Modules.TaskBar_essential;

namespace MarySGameEngine.Modules.AssetBrowser_essential
{
    public class AssetBrowser : IModule
    {
        private WindowManagement _windowManagement;
        private GraphicsDevice _graphicsDevice;
        private SpriteFont _menuFont;
        private int _windowWidth;
        private TaskBar _taskBar;

        public AssetBrowser(GraphicsDevice graphicsDevice, SpriteFont menuFont, int windowWidth)
        {
            _graphicsDevice = graphicsDevice;
            _menuFont = menuFont;

            // Create window properties from bridge.json
            var properties = new WindowProperties
            {
                IsVisible = true,
                IsMovable = true,
                IsResizable = true
            };

            // Initialize window management
            _windowManagement = new WindowManagement(graphicsDevice, menuFont, windowWidth, properties);
            _windowManagement.SetVisible(true);  // Explicitly set visible

            // Position the window on the right side of the screen
            // We'll do this by updating the window width after initialization
            UpdateWindowWidth(windowWidth);
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
            _windowManagement.Draw(spriteBatch, "Asset Browser");
        }

        public void UpdateWindowWidth(int newWidth)
        {
            _windowWidth = newWidth;
            _windowManagement.UpdateWindowWidth(newWidth);
            UpdateBounds(); // Ensure proper positioning after width update
        }

        public void LoadContent(ContentManager content)
        {
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

            // Calculate the new position to be on the right side
            // Leave a small gap from the right edge (10 pixels)
            int newX = _windowWidth - bounds.Width - 10;

            // Update the window position without recreating the WindowManagement instance
            _windowManagement.SetPosition(new Vector2(newX, bounds.Y));
        }
    }
} 