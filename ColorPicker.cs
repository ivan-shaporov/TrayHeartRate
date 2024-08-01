using System.Runtime.InteropServices;

namespace TrayHeartRate
{
    public class ColorPicker
    {
        [DllImport("gdi32.dll")]
        private static extern uint GetPixel(IntPtr hdc, int nXPos, int nYPos);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        // Define the RECT structure
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private struct APPBARDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public int uCallbackMessage;
            public int uEdge;
            public RECT rc;
            public IntPtr lParam;
        }

        private const int ABM_GETTASKBARPOS = 5;

        [DllImport("shell32.dll")]
        private static extern IntPtr SHAppBarMessage(int msg, ref APPBARDATA data);

        private static Rectangle? GetTaskbarPosition()
        {
            var data = new APPBARDATA();
            data.cbSize = Marshal.SizeOf(data);

            IntPtr retval = SHAppBarMessage(ABM_GETTASKBARPOS, ref data);
            if (retval == IntPtr.Zero)
            {
                return null;
            }

            return new Rectangle(data.rc.Left, data.rc.Top, data.rc.Right - data.rc.Left, data.rc.Bottom - data.rc.Top);
        }

        public static Color GetTaskBarBackgroundColor()
        {
            // Get the device context of the screen
            IntPtr hdc = GetDC(IntPtr.Zero);

            var iconRect = GetTaskbarPosition();
            if (iconRect == null)
            {
                return Color.White;
            }

            int x = iconRect.Value.Right - 1;
            int y = iconRect.Value.Bottom - 1;

            uint pixel = GetPixel(hdc, x, y);
            ReleaseDC(IntPtr.Zero, hdc);

            Color color = Color.FromArgb(
                (int)(pixel & 0x000000FF),
                (int)(pixel & 0x0000FF00) >> 8,
                (int)(pixel & 0x00FF0000) >> 16);

            return color;
        }
    }
}
