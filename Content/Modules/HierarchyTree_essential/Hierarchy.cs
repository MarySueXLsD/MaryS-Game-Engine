using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Content;
using System.Collections.Generic;
using System;
using MarySGameEngine.Modules.WindowManagement_essential;
using MarySGameEngine.Modules.TaskBar_essential;

namespace MarySGameEngine.Modules.HierarchyTree_essential
{
    public class HierarchyTree : IModule
    {
        private WindowManagement _windowManagement;
        private GraphicsDevice _graphicsDevice;
        private SpriteFont _menuFont;
        private int _windowWidth;
        private TaskBar _taskBar;

        public HierarchyTree(GraphicsDevice graphicsDevice, SpriteFont menuFont, int windowWidth)
        {
            _graphicsDevice = graphicsDevice;
            _menuFont = menuFont;
            _windowWidth = windowWidth;

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

            // Position the window on the left side of the screen
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
            _windowManagement.Draw(spriteBatch, "Hierarchy Tree");
        }

        public void UpdateWindowWidth(int newWidth)
        {
            _windowWidth = newWidth;
            _windowManagement.UpdateWindowWidth(newWidth);
            UpdateBounds(); // Maintain left-side positioning
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

            // Calculate the new position to be on the left side
            // Leave a small gap from the left edge (10 pixels)
            int newX = 10;

            // Update the window position without recreating the WindowManagement instance
            _windowManagement.SetPosition(new Vector2(newX, bounds.Y));
        }
    }
}
