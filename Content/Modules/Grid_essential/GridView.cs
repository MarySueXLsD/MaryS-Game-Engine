using System;
using Microsoft.Xna.Framework;

namespace MarySGameEngine.Modules.Grid_essential
{
    /// <summary>
    /// Isometric coordinate conversion and view settings for the grid window.
    /// Grid cells are indexed by (column, row). Screen origin is top-left.
    /// </summary>
    public static class GridView
    {
        private const float BaseTileWidth = 128f;

        /// <summary>Zoom scale for grid. 1 = default, >1 = zoom in, &lt;1 = zoom out.</summary>
        public static float Zoom { get; set; } = 1f;

        /// <summary>Width of one tile diamond (horizontal extent).</summary>
        public static float TileWidth => BaseTileWidth * Zoom;

        /// <summary>View angle in degrees (diamond edge from horizontal).</summary>
        public static float ViewAngleDegrees { get; set; } = 28f;

        /// <summary>Height of one tile diamond (vertical extent).</summary>
        public static float TileHeight => TileWidth * MathF.Tan(MathF.Max(1f, ViewAngleDegrees) * MathF.PI / 180f);

        public static float HalfTileWidth => TileWidth * 0.5f;
        public static float HalfTileHeight => TileHeight * 0.5f;

        /// <summary>Convert grid cell (column, row) to screen position (center of tile).</summary>
        public static Vector2 GridToScreen(int column, int row)
        {
            return GridToScreen(column, (float)row);
        }

        /// <summary>Convert grid position (float) to screen position.</summary>
        public static Vector2 GridToScreen(float column, float row)
        {
            float x = (column - row) * HalfTileWidth;
            float y = (column + row) * HalfTileHeight;
            return new Vector2(x, y);
        }

        /// <summary>Convert screen position to continuous grid coordinates.</summary>
        public static Vector2 ScreenToGridContinuous(float screenX, float screenY)
        {
            float halfH = HalfTileHeight;
            if (halfH < 0.001f) halfH = 0.001f;
            float col = (screenX / HalfTileWidth + screenY / halfH) * 0.5f;
            float row = (screenY / halfH - screenX / HalfTileWidth) * 0.5f;
            return new Vector2(col, row);
        }

        /// <summary>Convert screen position to grid cell (column, row).</summary>
        public static Point ScreenToGrid(float screenX, float screenY)
        {
            Vector2 c = ScreenToGridContinuous(screenX, screenY);
            return new Point((int)MathF.Round(c.X), (int)MathF.Round(c.Y));
        }

        /// <summary>Get the four screen corners of a tile (top, right, bottom, left).</summary>
        public static void GetTileCorners(int column, int row, out Vector2 top, out Vector2 right, out Vector2 bottom, out Vector2 left)
        {
            Vector2 center = GridToScreen(column, row);
            top = center + new Vector2(0, -HalfTileHeight);
            right = center + new Vector2(HalfTileWidth, 0);
            bottom = center + new Vector2(0, HalfTileHeight);
            left = center + new Vector2(-HalfTileWidth, 0);
        }
    }
}
