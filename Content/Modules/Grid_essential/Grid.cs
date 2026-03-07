using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Content;
using MarySGameEngine.Modules.WindowManagement_essential;
using MarySGameEngine.Modules.TaskBar_essential;
using MarySGameEngine;

namespace MarySGameEngine.Modules.Grid_essential
{
    /// <summary>
    /// Window module that displays an empty isometric grid with zoom and pan.
    /// </summary>
    public class Grid : IModule
    {
        private const int TitleBarHeight = 40;
        private const int ToolbarHeight = 36;
        private const int SidebarWidth = 56;
        private const float ZoomMin = 0.5f;
        private const float ZoomMax = 5f;
        private const float ZoomStep = 0.1f;
        private const float ViewAngleMin = 10f;
        private const float ViewAngleMax = 60f;
        private const float ViewAngleStep = 0.5f;
        private const int GridLineThickness = 2;
        private const int MaxVisibleCellsPerAxis = 120;
        private const int WindowMinWidth = 640;
        private const int WindowMinHeight = 240;
        private static readonly Color GridLineColor = new Color(180, 200, 220);
        private static readonly Color HoverCellFill = new Color(60, 120, 200, 140);
        private static readonly Color ContentBackground = new Color(30, 35, 45);
        private static readonly Color ToolbarBackground = new Color(50, 55, 65);
        private static readonly Color SidebarBackground = new Color(45, 50, 58);
        private static readonly Color ButtonBackground = new Color(70, 75, 85);
        private static readonly Color ButtonHover = new Color(147, 112, 219);
        private static readonly Color CoordTextColor = new Color(200, 100, 255);

        private WindowManagement _windowManagement;
        private GraphicsDevice _graphicsDevice;
        private SpriteFont _menuFont;
        private SpriteFont _coordFont;
        private SpriteFont _toolbarFont;
        private int _windowWidth;
        private TaskBar _taskBar;
        private GameEngine _engine;
        private Texture2D _pixel;

        private Vector2 _origin;
        private BasicEffect _effect;
        private bool _isPanning;
        private Vector2 _panStartMouse;
        private Vector2 _panStartOrigin;

        private Rectangle _contentBounds;
        private Rectangle _toolbarBounds;
        private Rectangle _sidebarBounds;
        private Rectangle _gridBounds;
        private Rectangle _toolbarButton1;
        private Rectangle _toolbarButton2;
        private Rectangle _toolbarButton3;
        private Rectangle _toolbarButton4;
        private Rectangle[] _sidebarButtons;
        private bool _anglePickerVisible;
        private Rectangle _anglePickerBounds;
        private Rectangle _angleSliderTrackBounds;
        private bool _isDraggingAngleSlider;
        private static readonly string[] SidebarLabels = { "X", "Y", "Z", "A", "B" };
        private static readonly string[] ToolbarButtonLabels = { "New Scene", "Load Scene", "Save Scene/Structure", "Change angle" };
        private const float ToolbarButtonTextScale = 0.85f;
        private const int ToolbarButtonPaddingX = 16;
        private const int ToolbarButtonMinWidth = 56;
        private MouseState _prevMouseState;
        private KeyboardState _prevKeyboardState;
        private bool _hasInitialOrigin;
        private Point _lastHoveredCell;
        private bool _hasHoveredCell;

        public Grid(GraphicsDevice graphicsDevice, SpriteFont menuFont, int windowWidth)
        {
            _graphicsDevice = graphicsDevice;
            _menuFont = menuFont;
            _windowWidth = windowWidth;
            _engine = (GameEngine)GameEngine.Instance;
            _pixel = new Texture2D(graphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
            _effect = new BasicEffect(graphicsDevice)
            {
                VertexColorEnabled = true,
                View = Matrix.Identity,
                World = Matrix.Identity
            };

            var windowProperties = new WindowProperties
            {
                IsVisible = false,
                IsMovable = true,
                IsResizable = true
            };
            _windowManagement = new WindowManagement(graphicsDevice, menuFont, windowWidth, windowProperties);
            _windowManagement.SetWindowTitle("Grid");
            _windowManagement.SetDefaultSize(1280, 480);
            _windowManagement.SetCustomMinimumSize(WindowMinWidth, WindowMinHeight);
            _windowManagement.SetPosition(new Vector2(150, 60));

            GridView.Zoom = 1f;
            _origin = Vector2.Zero;
        }

        public void SetTaskBar(TaskBar taskBar)
        {
            _taskBar = taskBar;
            _windowManagement?.SetTaskBar(taskBar);
        }

        private void UpdateContentBounds()
        {
            if (_windowManagement == null || !_windowManagement.IsVisible())
            {
                _anglePickerBounds = Rectangle.Empty;
                _angleSliderTrackBounds = Rectangle.Empty;
                return;
            }
            var windowBounds = _windowManagement.GetWindowBounds();
            _contentBounds = new Rectangle(
                windowBounds.X,
                windowBounds.Y + TitleBarHeight,
                windowBounds.Width,
                windowBounds.Height - TitleBarHeight);
            _toolbarBounds = new Rectangle(
                _contentBounds.X,
                _contentBounds.Y,
                _contentBounds.Width,
                ToolbarHeight);
            _sidebarBounds = new Rectangle(
                _contentBounds.X,
                _contentBounds.Y + ToolbarHeight,
                SidebarWidth,
                _contentBounds.Height - ToolbarHeight);
            _gridBounds = new Rectangle(
                _contentBounds.X + SidebarWidth,
                _contentBounds.Y + ToolbarHeight,
                _contentBounds.Width - SidebarWidth,
                _contentBounds.Height - ToolbarHeight);

            const int btnH = 26;
            const int pad = 8;
            int startX = _toolbarBounds.X + pad;
            int btnY = _toolbarBounds.Y + (ToolbarHeight - btnH) / 2;
            SpriteFont toolbarFont = _toolbarFont ?? _menuFont;
            int x = startX;
            for (int i = 0; i < 4; i++)
            {
                int w = ToolbarButtonMinWidth;
                if (toolbarFont != null && i < ToolbarButtonLabels.Length)
                {
                    float textW = toolbarFont.MeasureString(ToolbarButtonLabels[i]).X * ToolbarButtonTextScale;
                    w = Math.Max(ToolbarButtonMinWidth, (int)Math.Ceiling(textW) + ToolbarButtonPaddingX);
                }
                var rect = new Rectangle(x, btnY, w, btnH);
                switch (i)
                {
                    case 0: _toolbarButton1 = rect; break;
                    case 1: _toolbarButton2 = rect; break;
                    case 2: _toolbarButton3 = rect; break;
                    case 3: _toolbarButton4 = rect; break;
                }
                x += w + pad;
            }

            const int sideBtnSize = 40;
            const int sidePad = 8;
            _sidebarButtons = new Rectangle[5];
            for (int i = 0; i < 5; i++)
            {
                _sidebarButtons[i] = new Rectangle(
                    _sidebarBounds.X + (SidebarWidth - sideBtnSize) / 2,
                    _sidebarBounds.Y + sidePad + i * (sideBtnSize + sidePad),
                    sideBtnSize,
                    sideBtnSize);
            }

            const int pickerW = 240;
            const int pickerH = 56;
            const int trackH = 12;
            const int trackPad = 8;
            _anglePickerBounds = new Rectangle(
                _toolbarButton4.X,
                _toolbarBounds.Bottom,
                pickerW,
                pickerH);
            int trackY = _anglePickerBounds.Y + pickerH - trackPad - trackH;
            _angleSliderTrackBounds = new Rectangle(
                _anglePickerBounds.X + trackPad,
                trackY,
                _anglePickerBounds.Width - trackPad * 2,
                trackH);
        }

        public void Update()
        {
            var mouse = Mouse.GetState();
            if (_windowManagement != null)
                _windowManagement.Update();

            UpdateContentBounds();
            if (_windowManagement?.IsVisible() != true)
            {
                _hasInitialOrigin = false;
                _anglePickerVisible = false;
                _isDraggingAngleSlider = false;
                _prevMouseState = mouse;
                _prevKeyboardState = Keyboard.GetState();
                return;
            }

            if (!_hasInitialOrigin && _gridBounds.Width > 0 && _gridBounds.Height > 0)
            {
                // Center view on grid origin (0,0)
                Vector2 gridCenterLocal = GridView.GridToScreen(0, 0);
                float gridCenterX = _gridBounds.Width * 0.5f;
                float gridCenterY = _gridBounds.Height * 0.5f;
                _origin = new Vector2(gridCenterX - gridCenterLocal.X, gridCenterY - gridCenterLocal.Y);
                _hasInitialOrigin = true;
            }

            float gridLocalMouseX = mouse.X - _gridBounds.X;
            float gridLocalMouseY = mouse.Y - _gridBounds.Y;
            bool mouseInGridArea = _gridBounds.Contains(mouse.Position);
            bool thisWindowTopmost = _windowManagement.IsThisWindowTopmostUnderMouse(mouse.Position);
            bool inGridArea = mouseInGridArea && thisWindowTopmost;

            if (mouseInGridArea)
            {
                float worldX = gridLocalMouseX - _origin.X;
                float worldY = gridLocalMouseY - _origin.Y;
                _lastHoveredCell = GridView.ScreenToGrid(worldX, worldY);
                _hasHoveredCell = true;
            }

            if (inGridArea && !_windowManagement.IsDragging() && !_windowManagement.IsResizing())
            {
                int scroll = mouse.ScrollWheelValue - _prevMouseState.ScrollWheelValue;
                if (scroll != 0)
                {
                    float worldX = gridLocalMouseX - _origin.X;
                    float worldY = gridLocalMouseY - _origin.Y;
                    Point cell = GridView.ScreenToGrid(worldX, worldY);
                    float zoomDelta = scroll > 0 ? ZoomStep : -ZoomStep;
                    float newZoom = MathHelper.Clamp(GridView.Zoom + zoomDelta, ZoomMin, ZoomMax);
                    GridView.Zoom = newZoom;
                    Vector2 cellScreen = GridView.GridToScreen(cell.X, cell.Y);
                    _origin = new Vector2(gridLocalMouseX - cellScreen.X, gridLocalMouseY - cellScreen.Y);
                }

                if (mouse.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released)
                {
                    _isPanning = true;
                    _panStartMouse = new Vector2(mouse.X, mouse.Y);
                    _panStartOrigin = _origin;
                }
            }

            if (mouse.LeftButton == ButtonState.Released)
                _isPanning = false;

            if (_isPanning && mouse.LeftButton == ButtonState.Pressed)
            {
                float dx = mouse.X - _panStartMouse.X;
                float dy = mouse.Y - _panStartMouse.Y;
                _origin = _panStartOrigin + new Vector2(dx, dy);
            }

            bool leftPressed = mouse.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released;
            bool leftReleased = mouse.LeftButton == ButtonState.Released && _prevMouseState.LeftButton == ButtonState.Pressed;

            var keyState = Keyboard.GetState();
            bool shiftGA = (keyState.IsKeyDown(Keys.LeftShift) || keyState.IsKeyDown(Keys.RightShift)) && keyState.IsKeyDown(Keys.G) && keyState.IsKeyDown(Keys.A) && !_prevKeyboardState.IsKeyDown(Keys.A) && thisWindowTopmost;
            if (shiftGA)
                _anglePickerVisible = !_anglePickerVisible;

            if (leftPressed && thisWindowTopmost && _toolbarButton4.Contains(mouse.Position))
                _anglePickerVisible = !_anglePickerVisible;

            if (thisWindowTopmost)
            {
                if (_anglePickerVisible)
                {
                    if (_isDraggingAngleSlider)
                    {
                        if (mouse.LeftButton == ButtonState.Pressed && _angleSliderTrackBounds.Width > 0)
                        {
                            float t = MathHelper.Clamp((mouse.X - _angleSliderTrackBounds.X) / (float)_angleSliderTrackBounds.Width, 0f, 1f);
                            float raw = ViewAngleMin + t * (ViewAngleMax - ViewAngleMin);
                            GridView.ViewAngleDegrees = MathF.Round(raw / ViewAngleStep) * ViewAngleStep;
                        }
                        else
                            _isDraggingAngleSlider = false;
                    }
                    else if (leftPressed)
                    {
                        if (_angleSliderTrackBounds.Width > 0 && _angleSliderTrackBounds.Contains(mouse.Position))
                        {
                            _isDraggingAngleSlider = true;
                            float t = MathHelper.Clamp((mouse.X - _angleSliderTrackBounds.X) / (float)_angleSliderTrackBounds.Width, 0f, 1f);
                            float raw = ViewAngleMin + t * (ViewAngleMax - ViewAngleMin);
                            GridView.ViewAngleDegrees = MathF.Round(raw / ViewAngleStep) * ViewAngleStep;
                        }
                        else if (!_anglePickerBounds.Contains(mouse.Position) && !_toolbarButton4.Contains(mouse.Position))
                            _anglePickerVisible = false;
                    }
                }

                if (leftReleased)
                    _isDraggingAngleSlider = false;
            }

            _prevKeyboardState = Keyboard.GetState();
            _prevMouseState = mouse;
        }

        private void DrawLine(SpriteBatch spriteBatch, Vector2 a, Vector2 b, Color color, int thickness)
        {
            Vector2 d = b - a;
            float len = d.Length();
            if (len < 0.001f) return;
            float angle = (float)Math.Atan2(d.Y, d.X);
            Vector2 scale = new Vector2(len, thickness);
            spriteBatch.Draw(_pixel, a, null, color, angle, new Vector2(0, 0.5f), scale, SpriteEffects.None, 0f);
        }

        private void DrawGridLines(SpriteBatch spriteBatch)
        {
            float gridX = _gridBounds.X;
            float gridY = _gridBounds.Y;
            Vector2 topLeft = GridView.ScreenToGridContinuous(-_origin.X, -_origin.Y);
            Vector2 topRight = GridView.ScreenToGridContinuous(_gridBounds.Width - _origin.X, -_origin.Y);
            Vector2 bottomLeft = GridView.ScreenToGridContinuous(-_origin.X, _gridBounds.Height - _origin.Y);
            Vector2 bottomRight = GridView.ScreenToGridContinuous(_gridBounds.Width - _origin.X, _gridBounds.Height - _origin.Y);
            int minC = (int)MathF.Floor(Math.Min(Math.Min(topLeft.X, topRight.X), Math.Min(bottomLeft.X, bottomRight.X))) - 1;
            int maxC = (int)MathF.Ceiling(Math.Max(Math.Max(topLeft.X, topRight.X), Math.Max(bottomLeft.X, bottomRight.X))) + 1;
            int minR = (int)MathF.Floor(Math.Min(Math.Min(topLeft.Y, topRight.Y), Math.Min(bottomLeft.Y, bottomRight.Y))) - 1;
            int maxR = (int)MathF.Ceiling(Math.Max(Math.Max(topLeft.Y, topRight.Y), Math.Max(bottomLeft.Y, bottomRight.Y))) + 1;
            int rangeC = maxC - minC + 1;
            int rangeR = maxR - minR + 1;
            if (rangeC > MaxVisibleCellsPerAxis || rangeR > MaxVisibleCellsPerAxis)
            {
                int half = MaxVisibleCellsPerAxis / 2;
                int centerC = (minC + maxC) / 2;
                int centerR = (minR + maxR) / 2;
                minC = centerC - half;
                maxC = centerC + half;
                minR = centerR - half;
                maxR = centerR + half;
            }
            for (int c = minC; c <= maxC; c++)
            {
                for (int r = minR; r <= maxR; r++)
                {
                    GridView.GetTileCorners(c, r, out Vector2 top, out Vector2 right, out Vector2 bottom, out Vector2 left);
                    top.X = gridX + _origin.X + top.X;
                    top.Y = gridY + _origin.Y + top.Y;
                    right.X = gridX + _origin.X + right.X;
                    right.Y = gridY + _origin.Y + right.Y;
                    bottom.X = gridX + _origin.X + bottom.X;
                    bottom.Y = gridY + _origin.Y + bottom.Y;
                    left.X = gridX + _origin.X + left.X;
                    left.Y = gridY + _origin.Y + left.Y;

                    DrawLine(spriteBatch, left, top, GridLineColor, GridLineThickness);
                    DrawLine(spriteBatch, top, right, GridLineColor, GridLineThickness);
                    DrawLine(spriteBatch, right, bottom, GridLineColor, GridLineThickness);
                    DrawLine(spriteBatch, bottom, left, GridLineColor, GridLineThickness);
                }
            }
        }

        private void DrawHoverCellFill()
        {
            if (_effect == null) return;
            int gridX = _gridBounds.X;
            int gridY = _gridBounds.Y;
            GridView.GetTileCorners(_lastHoveredCell.X, _lastHoveredCell.Y, out Vector2 top, out Vector2 right, out Vector2 bottom, out Vector2 left);
            float ox = gridX + _origin.X;
            float oy = gridY + _origin.Y;
            var vp = _graphicsDevice.Viewport;
            _effect.Projection = Matrix.CreateOrthographicOffCenter(0, vp.Width, vp.Height, 0, 0, 1);
            var vertices = new[]
            {
                new VertexPositionColor(new Vector3(ox + top.X, oy + top.Y, 0), HoverCellFill),
                new VertexPositionColor(new Vector3(ox + right.X, oy + right.Y, 0), HoverCellFill),
                new VertexPositionColor(new Vector3(ox + bottom.X, oy + bottom.Y, 0), HoverCellFill),
                new VertexPositionColor(new Vector3(ox + left.X, oy + left.Y, 0), HoverCellFill)
            };
            var indices = new short[] { 0, 1, 2, 0, 2, 3 };
            _effect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, vertices, 0, 4, indices, 0, 2);
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            if (_windowManagement?.IsVisible() != true) return;

            _windowManagement.Draw(spriteBatch, "Grid");

            if (_contentBounds.Width <= 0 || _contentBounds.Height <= 0) return;

            SpriteFont toolbarFont = _toolbarFont ?? _menuFont;
            spriteBatch.Draw(_pixel, _toolbarBounds, ToolbarBackground);
            bool hover1 = _toolbarButton1.Contains(Mouse.GetState().Position) && _windowManagement.IsThisWindowTopmostUnderMouse(Mouse.GetState().Position);
            bool hover2 = _toolbarButton2.Contains(Mouse.GetState().Position) && _windowManagement.IsThisWindowTopmostUnderMouse(Mouse.GetState().Position);
            bool hover3 = _toolbarButton3.Contains(Mouse.GetState().Position) && _windowManagement.IsThisWindowTopmostUnderMouse(Mouse.GetState().Position);
            bool hover4 = _toolbarButton4.Contains(Mouse.GetState().Position) && _windowManagement.IsThisWindowTopmostUnderMouse(Mouse.GetState().Position);
            spriteBatch.Draw(_pixel, _toolbarButton1, hover1 ? ButtonHover : ButtonBackground);
            spriteBatch.Draw(_pixel, _toolbarButton2, hover2 ? ButtonHover : ButtonBackground);
            spriteBatch.Draw(_pixel, _toolbarButton3, hover3 ? ButtonHover : ButtonBackground);
            spriteBatch.Draw(_pixel, _toolbarButton4, hover4 ? ButtonHover : ButtonBackground);
            if (toolbarFont != null)
            {
                for (int i = 0; i < 4 && i < ToolbarButtonLabels.Length; i++)
                {
                    Rectangle r = i == 0 ? _toolbarButton1 : i == 1 ? _toolbarButton2 : i == 2 ? _toolbarButton3 : _toolbarButton4;
                    float lineH = toolbarFont.LineSpacing * ToolbarButtonTextScale;
                    Vector2 pos = new Vector2(r.X + (r.Width - toolbarFont.MeasureString(ToolbarButtonLabels[i]).X * ToolbarButtonTextScale) / 2f, r.Y + (r.Height - lineH) / 2f);
                    spriteBatch.DrawString(toolbarFont, ToolbarButtonLabels[i], pos, Color.White, 0f, Vector2.Zero, ToolbarButtonTextScale, SpriteEffects.None, 0f);
                }
            }

            spriteBatch.Draw(_pixel, _sidebarBounds, SidebarBackground);
            var mousePos = Mouse.GetState().Position;
            bool topmost = _windowManagement.IsThisWindowTopmostUnderMouse(mousePos);
            if (_sidebarButtons != null)
            {
            for (int i = 0; i < 5 && i < _sidebarButtons.Length; i++)
            {
                bool hover = topmost && _sidebarButtons[i].Contains(mousePos);
                spriteBatch.Draw(_pixel, _sidebarButtons[i], hover ? ButtonHover : ButtonBackground);
                if (_menuFont != null)
                {
                    Vector2 labelSize = _menuFont.MeasureString(SidebarLabels[i]) * 0.9f;
                    Vector2 pos = new Vector2(
                        _sidebarButtons[i].X + (_sidebarButtons[i].Width - labelSize.X) / 2f,
                        _sidebarButtons[i].Y + (_sidebarButtons[i].Height - labelSize.Y) / 2f);
                    spriteBatch.DrawString(_menuFont, SidebarLabels[i], pos, Color.White, 0f, Vector2.Zero, 0.9f, SpriteEffects.None, 0f);
                }
            }
            }

            if (_gridBounds.Width <= 0 || _gridBounds.Height <= 0)
            {
                _windowManagement.DrawOverlay(spriteBatch);
                return;
            }

            Rectangle savedScissor = spriteBatch.GraphicsDevice.ScissorRectangle;
            RasterizerState savedRasterizer = spriteBatch.GraphicsDevice.RasterizerState;

            spriteBatch.End();
            spriteBatch.GraphicsDevice.ScissorRectangle = _gridBounds;
            spriteBatch.GraphicsDevice.RasterizerState = new RasterizerState
            {
                ScissorTestEnable = true,
                CullMode = CullMode.None
            };
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, spriteBatch.GraphicsDevice.RasterizerState);

            spriteBatch.Draw(_pixel, _gridBounds, ContentBackground);
            if (_hasHoveredCell && _gridBounds.Contains(Mouse.GetState().Position) && _windowManagement.IsThisWindowTopmostUnderMouse(Mouse.GetState().Position))
            {
                spriteBatch.End();
                DrawHoverCellFill();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, spriteBatch.GraphicsDevice.RasterizerState);
            }
            DrawGridLines(spriteBatch);

            // Coordinates on top of grid (world position from 0,0, bigger font)
            SpriteFont coordFont = _coordFont ?? _menuFont;
            if (coordFont != null && _gridBounds.Contains(mousePos))
            {
                const float coordScale = 1.35f;
                const int coordPad = 10;
                float worldLocalX = mousePos.X - _gridBounds.X - _origin.X;
                float worldLocalY = mousePos.Y - _gridBounds.Y - _origin.Y;
                Vector2 worldF = GridView.ScreenToGridContinuous(worldLocalX, worldLocalY);
                float worldX = worldF.X;
                float worldY = worldF.Y;
                string mouseStr = $"Mouse: ({worldX:F1}, {worldY:F1})";
                string cellStr = _hasHoveredCell ? $"Cell: ({_lastHoveredCell.X}, {_lastHoveredCell.Y})" : "Cell: (-, -)";
                Vector2 mouseSize = coordFont.MeasureString(mouseStr) * coordScale;
                Vector2 cellSize = coordFont.MeasureString(cellStr) * coordScale;
                float rightX = _gridBounds.Right - coordPad - Math.Max(mouseSize.X, cellSize.X);
                float y1 = _gridBounds.Y + coordPad;
                float y2 = y1 + mouseSize.Y + 4;
                spriteBatch.DrawString(coordFont, mouseStr, new Vector2(rightX, y1), CoordTextColor, 0f, Vector2.Zero, coordScale, SpriteEffects.None, 0f);
                spriteBatch.DrawString(coordFont, cellStr, new Vector2(rightX, y2), CoordTextColor, 0f, Vector2.Zero, coordScale, SpriteEffects.None, 0f);
            }

            spriteBatch.End();
            spriteBatch.GraphicsDevice.ScissorRectangle = savedScissor;
            spriteBatch.GraphicsDevice.RasterizerState = savedRasterizer ?? new RasterizerState { ScissorTestEnable = false };
            spriteBatch.Begin();

            // Draw angle picker on top of grid so it is never covered
            if (_anglePickerVisible && toolbarFont != null && _pixel != null)
            {
                const int pickerW = 240;
                const int pickerH = 56;
                Rectangle pickerRect = _anglePickerBounds.Width > 0 && _anglePickerBounds.Height > 0
                    ? _anglePickerBounds
                    : new Rectangle(_toolbarButton4.X, _toolbarBounds.Bottom, pickerW, pickerH);
                if (pickerRect.Width <= 0 || pickerRect.Height <= 0) pickerRect = new Rectangle(_toolbarButton4.X, _toolbarBounds.Bottom, pickerW, pickerH);

                spriteBatch.Draw(_pixel, pickerRect, new Color(45, 50, 58));
                var borderColor = new Color(90, 95, 110);
                int b = 1;
                spriteBatch.Draw(_pixel, new Rectangle(pickerRect.X, pickerRect.Y, pickerRect.Width, b), borderColor);
                spriteBatch.Draw(_pixel, new Rectangle(pickerRect.X, pickerRect.Bottom - b, pickerRect.Width, b), borderColor);
                spriteBatch.Draw(_pixel, new Rectangle(pickerRect.X, pickerRect.Y, b, pickerRect.Height), borderColor);
                spriteBatch.Draw(_pixel, new Rectangle(pickerRect.Right - b, pickerRect.Y, b, pickerRect.Height), borderColor);

                const int trackPad = 8;
                const int trackH = 12;
                int trackY = pickerRect.Y + pickerRect.Height - trackPad - trackH;
                Rectangle trackRect = new Rectangle(pickerRect.X + trackPad, trackY, pickerRect.Width - trackPad * 2, trackH);
                if (trackRect.Width > 0 && trackRect.Height > 0)
                {
                    spriteBatch.Draw(_pixel, trackRect, new Color(60, 65, 75));
                    float t = (GridView.ViewAngleDegrees - ViewAngleMin) / (ViewAngleMax - ViewAngleMin);
                    t = MathHelper.Clamp(t, 0f, 1f);
                    int thumbW = 14;
                    int thumbX = trackRect.X + (int)(t * (trackRect.Width - thumbW));
                    var thumbRect = new Rectangle(thumbX, trackRect.Y - 2, thumbW, trackRect.Height + 4);
                    spriteBatch.Draw(_pixel, thumbRect, ButtonHover);
                }

                string angleLabel = $"Grid angle: {GridView.ViewAngleDegrees:F1} deg";
                Vector2 labelPos = new Vector2(pickerRect.X + 8, pickerRect.Y + 6);
                spriteBatch.DrawString(toolbarFont, angleLabel, labelPos, Color.White, 0f, Vector2.Zero, 0.85f, SpriteEffects.None, 0f);
            }

            _windowManagement.DrawOverlay(spriteBatch);
        }

        public void UpdateWindowWidth(int width)
        {
            _windowWidth = width;
            _windowManagement?.UpdateWindowWidth(width);
            UpdateContentBounds();
        }

        public void LoadContent(ContentManager content)
        {
            _windowManagement?.LoadContent(content);
            if (_taskBar != null)
                _taskBar.EnsureModuleIconExists("Grid", content);
            try
            {
                _coordFont = content.Load<SpriteFont>("Fonts/SpriteFonts/inconsolata/bold");
            }
            catch
            {
                try
                {
                    _coordFont = content.Load<SpriteFont>("Fonts/SpriteFonts/inconsolata/regular");
                }
                catch
                {
                    _coordFont = null;
                }
            }
            try
            {
                _toolbarFont = content.Load<SpriteFont>("Fonts/SpriteFonts/roboto/medium");
            }
            catch
            {
                _toolbarFont = null;
            }
        }

        public void Dispose()
        {
            _pixel?.Dispose();
            _effect?.Dispose();
            _windowManagement?.Dispose();
        }
    }
}
