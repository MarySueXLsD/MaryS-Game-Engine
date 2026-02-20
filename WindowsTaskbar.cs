using System;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;

namespace MarySGameEngine
{
    public enum WindowsTaskbarPosition
    {
        Unknown = 0,
        Left = 1,
        Top = 2,
        Right = 3,
        Bottom = 4
    }

    public static class WindowsTaskbar
    {
        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct APPBARDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public uint uCallbackMessage;
            public uint uEdge;
            public RECT rc;
            public IntPtr lParam;
        }

        private const uint ABM_GETTASKBARPOS = 0x00000005;
        private const uint ABM_GETSTATE = 0x00000004;
        private const int ABS_AUTOHIDE = 0x0000001;
        private const int ABS_ALWAYSONTOP = 0x0000002;

        private static WindowsTaskbarPosition? _cachedPosition;
        private static Rectangle? _cachedBounds;
        private static DateTime _lastCheck = DateTime.MinValue;
        private static readonly TimeSpan CacheTimeout = TimeSpan.FromMilliseconds(50);

        /// <summary>
        /// Fetches position and bounds from a single API call so they are always in sync.
        /// </summary>
        private static void RefreshPositionAndBounds()
        {
            try
            {
                APPBARDATA data = new APPBARDATA();
                data.cbSize = Marshal.SizeOf(typeof(APPBARDATA));
                data.hWnd = FindWindow("Shell_TrayWnd", null);

                if (data.hWnd == IntPtr.Zero)
                {
                    _cachedPosition = WindowsTaskbarPosition.Bottom;
                    _cachedBounds = GetTaskbarBoundsFromWorkArea();
                    _lastCheck = DateTime.Now;
                    return;
                }

                IntPtr result = SHAppBarMessage(ABM_GETTASKBARPOS, ref data);
                if (result != IntPtr.Zero)
                {
                    int width = data.rc.Right - data.rc.Left;
                    int height = data.rc.Bottom - data.rc.Top;
                    _cachedBounds = new Rectangle(data.rc.Left, data.rc.Top, width, height);
                    // Derive position from the same rect so they never get out of sync
                    if (data.rc.Top == 0 && data.rc.Left == 0 && data.rc.Right > data.rc.Bottom)
                        _cachedPosition = WindowsTaskbarPosition.Top;
                    else if (data.rc.Top == 0 && data.rc.Left == 0 && data.rc.Bottom > data.rc.Right)
                        _cachedPosition = WindowsTaskbarPosition.Left;
                    else if (data.rc.Left > 0)
                        _cachedPosition = WindowsTaskbarPosition.Right;
                    else
                        _cachedPosition = WindowsTaskbarPosition.Bottom;
                }
                else
                {
                    _cachedPosition = WindowsTaskbarPosition.Bottom;
                    _cachedBounds = GetTaskbarBoundsFromWorkArea();
                }
                _lastCheck = DateTime.Now;
            }
            catch
            {
                _cachedPosition = WindowsTaskbarPosition.Bottom;
                _cachedBounds = GetTaskbarBoundsFromWorkArea();
                _lastCheck = DateTime.Now;
            }
        }

        /// <summary>
        /// Gets the position of the Windows taskbar
        /// </summary>
        public static WindowsTaskbarPosition GetPosition()
        {
            if (!_cachedPosition.HasValue || (DateTime.Now - _lastCheck) >= CacheTimeout)
                RefreshPositionAndBounds();
            return _cachedPosition ?? WindowsTaskbarPosition.Bottom;
        }

        /// <summary>
        /// Gets the bounds of the Windows taskbar in screen coordinates (actual size from API or working-area difference).
        /// </summary>
        public static Rectangle GetBounds()
        {
            if (!_cachedBounds.HasValue || (DateTime.Now - _lastCheck) >= CacheTimeout)
                RefreshPositionAndBounds();
            return _cachedBounds ?? GetTaskbarBoundsFromWorkArea();
        }

        /// <summary>
        /// Derives taskbar bounds from the difference between screen bounds and working area (actual taskbar size).
        /// </summary>
        private static Rectangle GetTaskbarBoundsFromWorkArea()
        {
            var bounds = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
            var work = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea;
            int bw = bounds.Width, bh = bounds.Height;
            int wx = work.X - bounds.X, wy = work.Y - bounds.Y;
            int ww = work.Width, wh = work.Height;
            if (wy > 0)
                return new Rectangle(0, 0, bw, wy);
            if (wx > 0)
                return new Rectangle(0, 0, wx, bh);
            if (wx + ww < bw)
                return new Rectangle(wx + ww, 0, bw - wx - ww, bh);
            if (wy + wh < bh)
                return new Rectangle(0, wy + wh, bw, bh - wy - wh);
            return new Rectangle(0, bh - 40, bw, 40);
        }

        /// <summary>
        /// Gets the available screen area excluding the taskbar
        /// </summary>
        public static Rectangle GetWorkArea()
        {
            try
            {
                var workArea = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea;
                return new Rectangle(workArea.X, workArea.Y, workArea.Width, workArea.Height);
            }
            catch
            {
                // Fallback to screen bounds if WorkingArea fails
                var screen = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
                return new Rectangle(screen.X, screen.Y, screen.Width, screen.Height);
            }
        }

        /// <summary>
        /// Checks if the taskbar is auto-hide
        /// </summary>
        public static bool IsAutoHide()
        {
            try
            {
                APPBARDATA data = new APPBARDATA();
                data.cbSize = Marshal.SizeOf(typeof(APPBARDATA));
                data.hWnd = FindWindow("Shell_TrayWnd", null);

                if (data.hWnd == IntPtr.Zero)
                    return false;

                IntPtr state = SHAppBarMessage(ABM_GETSTATE, ref data);
                return (state.ToInt32() & ABS_AUTOHIDE) != 0;
            }
            catch
            {
                return false;
            }
        }

        [DllImport("shell32.dll")]
        private static extern IntPtr SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

        /// <summary>
        /// Clears the cache to force a fresh check on next call
        /// </summary>
        public static void ClearCache()
        {
            _cachedPosition = null;
            _cachedBounds = null;
            _lastCheck = DateTime.MinValue;
        }
    }
}

