using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ScreenManager
{
    // The Display class based on the example from Simon @ https://purplestoat.wordpress.com/tag/pinvoke/
    // Thanks for the greate work!
    // The Display class retrieves information about the virtual
    // desktop and monitors in use by the current user.
    public static class Display
    {
        internal delegate bool EnumMonitorsDelegate(IntPtr hMonitor, IntPtr hdcMonitor,
            ref RectStruct lprcMonitor, IntPtr dwData);

        private const int CCHDEVICENAME = 32; // size of a device name string
        private const uint MONITORINFOF_PRIMARY = 1; // this is the primary display monitor.

        // monitorFromWindow dwFlags
        private const int MONITOR_DEFAULTTONULL = 0;
        private const int MONITOR_DEFAULTTOPRIMARY = 1;
        private const int MONITOR_DEFAULTTONEAREST = 2;

        public static Int32 MonitorCount { get; private set; }
        public static Int32 VirtualLeft { get; private set; }
        public static Int32 VirtualTop { get; private set; }
        public static Int32 VirtualWidth { get; private set; }
        public static Int32 VirtualHeight { get; private set; }
        public static Boolean MonitorsHaveSameDisplayFormat { get; private set; }
        public static ScreenInfoCollection Screens { get; private set; }

        static Display()
        {
            Display.Refresh();
        }

        // The Refresh method updates the values to reflect the current
        // situation.  Run Refresh if you have changed the number or
        // configuration of monitors in the display.
        public static void Refresh()
        {
            Display.VirtualLeft = Display.GetSystemMetrics(SystemMetric.SM_XVIRTUALSCREEN);
            Display.VirtualTop = Display.GetSystemMetrics(SystemMetric.SM_YVIRTUALSCREEN);
            Display.VirtualWidth = Display.GetSystemMetrics(SystemMetric.SM_CXVIRTUALSCREEN);
            Display.VirtualHeight = Display.GetSystemMetrics(SystemMetric.SM_CYVIRTUALSCREEN);
            Display.MonitorCount = Display.GetSystemMetrics(SystemMetric.SM_CMONITORS);
            Display.MonitorsHaveSameDisplayFormat = (Display.GetSystemMetrics(SystemMetric.SM_SAMEDISPLAYFORMAT) != 0);
            Display.Screens = new ScreenInfoCollection();
            Display.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                new EnumMonitorsDelegate(EnumMonitorsProc), IntPtr.Zero);
        }

        // Return the name of a screen that is nearest to the window
        public static String GetScreenNameFromWindow(IntPtr hwnd)
        {
            IntPtr hMonitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            MonitorInfoEx mi = new MonitorInfoEx();
            mi.Size = (uint)Marshal.SizeOf(mi);

            bool success = GetMonitorInfo(hMonitor, ref mi);
            if (success)
            {
                return mi.DeviceName;
            }
            else
            {
                return "";
            }
        }

        [AllowReversePInvokeCalls]
        private static bool EnumMonitorsProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RectStruct lprcMonitor, IntPtr dwData)
        {
            MonitorInfoEx mi = new MonitorInfoEx();
            mi.Size = (uint)Marshal.SizeOf(mi);

            bool success = GetMonitorInfo(hMonitor, ref mi);
            if (success)
            {
                ScreenInfo si = new ScreenInfo();
                si.ScreenWidth = (mi.Monitor.right - mi.Monitor.left).ToString();
                si.ScreenHeight = (mi.Monitor.bottom - mi.Monitor.top).ToString();
                si.MonitorArea = Oblong.FromRectStruct(mi.Monitor);
                si.WorkArea = Oblong.FromRectStruct(mi.WorkArea);
                si.DeviceName = mi.DeviceName;
                si.IsPrimaryScreen = ((mi.Flags & MONITORINFOF_PRIMARY) == 1); ;

                DEVMODE DeviceMode = new DEVMODE();
                DeviceMode.Initialize();

                if (EnumDisplaySettingsEx(ToLPTStr(mi.DeviceName), -1, ref DeviceMode))
                {
                    si.NativeHeight = DeviceMode.dmPelsHeight;
                    si.NativeWidth = DeviceMode.dmPelsWidth;
                    si.Scaling = Math.Round(((double)DeviceMode.dmPelsHeight / (mi.Monitor.bottom - mi.Monitor.top)) * 100);
                }

                Display.Screens.Add(si);
            }

            return true;
        }

        // The class that contains the screen information
        public class ScreenInfo
        {
            public String ScreenWidth { get; set; }
            public String ScreenHeight { get; set; }
            public Oblong MonitorArea { get; set; }
            public Oblong WorkArea { get; set; }
            public Boolean IsPrimaryScreen { get; set; }
            public String DeviceName { get; set; }
            public uint NativeHeight { get; set; }
            public uint NativeWidth { get; set; }
            public double Scaling { get; set; }
        }

        // Collection of screen information
        public class ScreenInfoCollection : List<ScreenInfo> { }

        // A struct to hold the defining corners of the rectangle.
        public struct Oblong : IEquatable<Oblong>
        {
            // The value for left side of the rectangle.
            public readonly int Left;
            // The value for the top of the rectangle.
            public readonly int Top;
            // The value for the right side of the rectangle.
            // Unlike Win32 RECT structs, this should be considered inclusive so
            // the Right value is the last pixel on the right side of the rectangle.
            public readonly int Right;
            // The value for the bottom of the rectangle.
            // Unlike Win32 RECT structs, this should be considered inclusive so
            // the Bottom value is the last pixel at the bottom of the rectangle.
            public readonly int Bottom;

            // Used to contruct a Rect struct.
            public Oblong(int left, int top, int right, int bottom)
            {
                this.Left = left;
                this.Top = top;
                this.Right = right;
                this.Bottom = bottom;
            }

            // This method takes a Win32 RECT-style struct (with exclusive values
            // for Right and Bottom) and returns a new Oblong (with inclusive values
            // for Right and Bottom).
            public static Oblong FromRectStruct(RectStruct rectStruct)
            {
                return new Oblong(rectStruct.left, rectStruct.top,
                    rectStruct.right - 1, rectStruct.bottom - 1);
            }

            public override string ToString()
            {
                return String.Format("({0},{1})-({2},{3})",
                    this.Left, this.Top, this.Right, this.Bottom);
            }

            public override int GetHashCode()
            {
                // From: http://stackoverflow.com/questions/263400/what-is-the-best-algorithm-for-an-overridden-system-object-gethashcode
                unchecked // Overflow is fine, just wrap
                {
                    int hash = 17;
                    hash = hash * 23 + this.Left.GetHashCode();
                    hash = hash * 23 + this.Top.GetHashCode();
                    hash = hash * 23 + this.Right.GetHashCode();
                    hash = hash * 23 + this.Bottom.GetHashCode();
                    return hash;
                }
            }

            public override bool Equals(object obj)
            {
                if (obj is Oblong)
                {
                    return this.Equals((Oblong)obj);
                }
                return false;
            }

            #region IEquatable<Rect> Members

            public bool Equals(Oblong other)
            {
                return (this.Left == other.Left) &&
                    (this.Top == other.Top) &&
                    (this.Right == other.Right) &&
                    (this.Bottom == other.Bottom);
            }

            #endregion

            public static bool operator ==(Oblong lhs, Oblong rhs)
            {
                return lhs.Equals(rhs);
            }

            public static bool operator !=(Oblong lhs, Oblong rhs)
            {
                return !(lhs.Equals(rhs));
            }
        }

        // To Convert the Displaname https://stackoverflow.com/a/24314568
        private static byte[] ToLPTStr(string str)
        {
            var lptArray = new byte[str.Length + 1];

            var index = 0;
            foreach (char c in str.ToCharArray())
                lptArray[index++] = Convert.ToByte(c);

            lptArray[index] = Convert.ToByte('\0');

            return lptArray;
        }

        #region ----- DllImports -----

        // http://www.pinvoke.net/default.aspx/user32.getsystemmetrics
        // Retrieves the specified system metric or system configuration setting.
        // Note that all dimensions retrieved by GetSystemMetrics are in pixels.
        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(SystemMetric smIndex);

        // http://www.pinvoke.net/default.aspx/user32.getmonitorinfo
        // The GetMonitorInfo function retrieves information about a display monitor.
        [DllImport("user32.dll")]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfoEx lpmi);

        // http://www.pinvoke.net/default.aspx/user32.getmonitorinfo
        // The GetMonitorInfo function retrieves information about a display monitor.
        [DllImport("user32.dll")]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

        // http://www.pinvoke.net/default.aspx/user32/monitorfromwindow.html?diff=y
        // retrieves a handle to the display monitor that has the largest area of intersection with the bounding rectangle of a specified window.
        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        // http://www.pinvoke.net/default.aspx/user32.enumdisplaysettings
        // Enumerates display settings and reference those to a DEVMODE Struct
        [DllImport("User32.dll", SetLastError = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern Boolean EnumDisplaySettingsEx(
            byte[] lpszDeviceName,  // display device
            [param: MarshalAs(UnmanagedType.U4)]
            Int32 iModeNum,         // graphics mode
            [In, Out]
            ref DEVMODE lpDevMode       // graphics mode settings
        );

        // http://pinvoke.net/default.aspx/user32.EnumDisplayMonitors
        // The EnumDisplayMonitors function enumerates display monitors (including
        // invisible pseudo-monitors associated with the mirroring drivers) that
        // intersect a region formed by the intersection of a specified clipping
        // rectangle and the visible region of a device context. EnumDisplayMonitors
        // calls an application-defined MonitorEnumProc callback function once for
        // each monitor that is enumerated. Note that GetSystemMetrics (SM_CMONITORS)
        // counts only the display monitors.
        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip,
            EnumMonitorsDelegate lpfnEnum, IntPtr dwData);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RectStruct lpRect);

        [DllImport("User32.dll")]
        public extern static bool MoveWindow(IntPtr handle, int x, int y, int width, int height, bool redraw);

        #endregion ----- DllImports -----

        #region ----- P/Invoke Structs -----

        // http://pinvoke.net/default.aspx/user32.MONITORINFO
        // The MONITORINFO structure contains information about a display monitor.
        // The GetMonitorInfo function stores information in a MONITORINFO
        // structure or a MONITORINFOEX structure.
        // The MONITORINFO structure is a subset of the MONITORINFOEX structure.
        // The MONITORINFOEX structure adds a string member to contain a name
        // for the display monitor.
        [StructLayout(LayoutKind.Sequential)]
        public struct MonitorInfo
        {
            public uint Size;
            public RectStruct Monitor;
            public RectStruct WorkArea;
            public uint Flags;
            public void Init()
            {
                this.Size = 40;
            }
        }

        // http://pinvoke.net/default.aspx/user32.MONITORINFO
        // The MONITORINFOEX structure contains information about a display monitor.
        // The GetMonitorInfo function stores information into a MONITORINFOEX
        // structure or a MONITORINFO structure.
        // The MONITORINFOEX structure is a superset of the MONITORINFO structure.
        // The MONITORINFOEX structure adds a string member to contain a name
        // for the display monitor.
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct MonitorInfoEx
        {
            public uint Size;
            public RectStruct Monitor;
            public RectStruct WorkArea;
            public uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHDEVICENAME)]
            public string DeviceName;
            public void Init()
            {
                this.Size = 40 + 2 * CCHDEVICENAME;
                this.DeviceName = string.Empty;
            }
        }

        // http://pinvoke.net/default.aspx/user32.EnumDisplayMonitors
        // The RECT structure defines the coordinates of the upper-left and lower-right corners of a rectangle.
        [StructLayout(LayoutKind.Sequential)]
        public struct RectStruct
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        // http://www.pinvoke.net/default.aspx/Structures.DEVMODE
        // Contains information about the initialization and environment of a printer or a display device.
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct DEVMODE
        {
            // You can define the following constant
            // but OUTSIDE the structure because you know
            // that size and layout of the structure
            // is very important
            // CCHDEVICENAME = 32 = 0x50
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            // In addition you can define the last character array
            // as following:
            //[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            //public Char[] dmDeviceName;

            // After the 32-bytes array
            [MarshalAs(UnmanagedType.U2)]
            public UInt16 dmSpecVersion;

            [MarshalAs(UnmanagedType.U2)]
            public UInt16 dmDriverVersion;

            [MarshalAs(UnmanagedType.U2)]
            public UInt16 dmSize;

            [MarshalAs(UnmanagedType.U2)]
            public UInt16 dmDriverExtra;

            [MarshalAs(UnmanagedType.U4)]
            public UInt32 dmFields;

            public POINTL dmPosition;

            [MarshalAs(UnmanagedType.U4)]
            public UInt32 dmDisplayOrientation;

            [MarshalAs(UnmanagedType.U4)]
            public UInt32 dmDisplayFixedOutput;

            [MarshalAs(UnmanagedType.I2)]
            public Int16 dmColor;

            [MarshalAs(UnmanagedType.I2)]
            public Int16 dmDuplex;

            [MarshalAs(UnmanagedType.I2)]
            public Int16 dmYResolution;

            [MarshalAs(UnmanagedType.I2)]
            public Int16 dmTTOption;

            [MarshalAs(UnmanagedType.I2)]
            public Int16 dmCollate;

            // CCHDEVICENAME = 32 = 0x50
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            // Also can be defined as
            //[MarshalAs(UnmanagedType.ByValArray,
            //    SizeConst = 32, ArraySubType = UnmanagedType.U1)]
            //public Byte[] dmFormName;

            [MarshalAs(UnmanagedType.U2)]
            public UInt16 dmLogPixels;

            [MarshalAs(UnmanagedType.U4)]
            public UInt32 dmBitsPerPel;

            [MarshalAs(UnmanagedType.U4)]
            public UInt32 dmPelsWidth;

            [MarshalAs(UnmanagedType.U4)]
            public UInt32 dmPelsHeight;

            [MarshalAs(UnmanagedType.U4)]
            public UInt32 dmDisplayFlags;

            [MarshalAs(UnmanagedType.U4)]
            public UInt32 dmDisplayFrequency;

            [MarshalAs(UnmanagedType.U4)]
            public UInt32 dmICMMethod;

            [MarshalAs(UnmanagedType.U4)]
            public UInt32 dmICMIntent;

            [MarshalAs(UnmanagedType.U4)]
            public UInt32 dmMediaType;

            [MarshalAs(UnmanagedType.U4)]
            public UInt32 dmDitherType;

            [MarshalAs(UnmanagedType.U4)]
            public UInt32 dmReserved1;

            [MarshalAs(UnmanagedType.U4)]
            public UInt32 dmReserved2;

            [MarshalAs(UnmanagedType.U4)]
            public UInt32 dmPanningWidth;

            [MarshalAs(UnmanagedType.U4)]
            public UInt32 dmPanningHeight;

            public void Initialize()
            {
                this.dmDeviceName = new string(new char[32]);
                this.dmFormName = new string(new char[32]);
                this.dmSize = (ushort)Marshal.SizeOf(this);
            }
        }

        // http://www.pinvoke.net/default.aspx/Structures.DEVMODE
        [StructLayout(LayoutKind.Sequential)]
        public struct POINTL
        {
            [MarshalAs(UnmanagedType.I4)]
            public int x;
            [MarshalAs(UnmanagedType.I4)]
            public int y;
        }

        #endregion ----- P/Invoke Structs -----

        #region ----- enums -----

        // Flags used with the Windows API (User32.dll):GetSystemMetrics(SystemMetric smIndex)
        //
        // This Enum and declaration signature was written by Gabriel T. Sharp
        // ai_productions@verizon.net or osirisgothra@hotmail.com
        // Obtained on pinvoke.net, please contribute your code to support the wiki!
        private enum SystemMetric : int
        {
            //
            //  Width of the screen of the primary display monitor, in pixels. This is the same values obtained by calling GetDeviceCaps as follows: GetDeviceCaps( hdcPrimaryMonitor, HORZRES).
            //
            SM_CXSCREEN = 0,
            //
            // Height of the screen of the primary display monitor, in pixels. This is the same values obtained by calling GetDeviceCaps as follows: GetDeviceCaps( hdcPrimaryMonitor, VERTRES).
            //
            SM_CYSCREEN = 1,
            //
            // Height of the arrow bitmap on a vertical scroll bar, in pixels.
            //
            SM_CYVSCROLL = 20,
            //
            // Width of a vertical scroll bar, in pixels.
            //
            SM_CXVSCROLL = 2,
            //
            // Height of a caption area, in pixels.
            //
            SM_CYCAPTION = 4,
            //
            // Width of a window border, in pixels. This is equivalent to the SM_CXEDGE value for windows with the 3-D look.
            //
            SM_CXBORDER = 5,
            //
            // Height of a window border, in pixels. This is equivalent to the SM_CYEDGE value for windows with the 3-D look.
            //
            SM_CYBORDER = 6,
            //
            // Thickness of the frame around the perimeter of a window that has a caption but is not sizable, in pixels. SM_CXFIXEDFRAME is the height of the horizontal border and SM_CYFIXEDFRAME is the width of the vertical border.
            //
            SM_CXDLGFRAME = 7,
            //
            // Thickness of the frame around the perimeter of a window that has a caption but is not sizable, in pixels. SM_CXFIXEDFRAME is the height of the horizontal border and SM_CYFIXEDFRAME is the width of the vertical border.
            //
            SM_CYDLGFRAME = 8,
            //
            // Height of the thumb box in a vertical scroll bar, in pixels
            //
            SM_CYVTHUMB = 9,
            //
            // Width of the thumb box in a horizontal scroll bar, in pixels.
            //
            SM_CXHTHUMB = 10,
            //
            // Default width of an icon, in pixels. The LoadIcon function can load only icons with the dimensions specified by SM_CXICON and SM_CYICON
            //
            SM_CXICON = 11,
            //
            // Default height of an icon, in pixels. The LoadIcon function can load only icons with the dimensions SM_CXICON and SM_CYICON.
            //
            SM_CYICON = 12,
            //
            // Width of a cursor, in pixels. The system cannot create cursors of other sizes.
            //
            SM_CXCURSOR = 13,
            //
            // Height of a cursor, in pixels. The system cannot create cursors of other sizes.
            //
            SM_CYCURSOR = 14,
            //
            // Height of a single-line menu bar, in pixels.
            //
            SM_CYMENU = 15,
            //
            // Width of the client area for a full-screen window on the primary display monitor, in pixels. To get the coordinates of the portion of the screen not obscured by the system taskbar or by application desktop toolbars, call the SystemParametersInfo function with the SPI_GETWORKAREA value.
            //
            SM_CXFULLSCREEN = 16,
            //
            // Height of the client area for a full-screen window on the primary display monitor, in pixels. To get the coordinates of the portion of the screen not obscured by the system taskbar or by application desktop toolbars, call the SystemParametersInfo function with the SPI_GETWORKAREA value.
            //
            SM_CYFULLSCREEN = 17,
            //
            // For double byte character set versions of the system, this is the height of the Kanji window at the bottom of the screen, in pixels
            //
            SM_CYKANJIWINDOW = 18,
            //
            // Nonzero if a mouse with a wheel is installed; zero otherwise
            //
            SM_MOUSEWHEELPRESENT = 75,
            //
            // Height of a horizontal scroll bar, in pixels.
            //
            SM_CYHSCROLL = 3,
            //
            // Width of the arrow bitmap on a horizontal scroll bar, in pixels.
            //
            SM_CXHSCROLL = 21,
            //
            // Nonzero if the debug version of User.exe is installed; zero otherwise.
            //
            SM_DEBUG = 22,
            //
            // Nonzero if the left and right mouse buttons are reversed; zero otherwise.
            //
            SM_SWAPBUTTON = 23,
            //
            // Reserved for future use
            //
            SM_RESERVED1 = 24,
            //
            // Reserved for future use
            //
            SM_RESERVED2 = 25,
            //
            // Reserved for future use
            //
            SM_RESERVED3 = 26,
            //
            // Reserved for future use
            //
            SM_RESERVED4 = 27,
            //
            // Minimum width of a window, in pixels.
            //
            SM_CXMIN = 28,
            //
            // Minimum height of a window, in pixels.
            //
            SM_CYMIN = 29,
            //
            // Width of a button in a window's caption or title bar, in pixels.
            //
            SM_CXSIZE = 30,
            //
            // Height of a button in a window's caption or title bar, in pixels.
            //
            SM_CYSIZE = 31,
            //
            // Thickness of the sizing border around the perimeter of a window that can be resized, in pixels. SM_CXSIZEFRAME is the width of the horizontal border, and SM_CYSIZEFRAME is the height of the vertical border.
            //
            SM_CXFRAME = 32,
            //
            // Thickness of the sizing border around the perimeter of a window that can be resized, in pixels. SM_CXSIZEFRAME is the width of the horizontal border, and SM_CYSIZEFRAME is the height of the vertical border.
            //
            SM_CYFRAME = 33,
            //
            // Minimum tracking width of a window, in pixels. The user cannot drag the window frame to a size smaller than these dimensions. A window can override this value by processing the WM_GETMINMAXINFO message.
            //
            SM_CXMINTRACK = 34,
            //
            // Minimum tracking height of a window, in pixels. The user cannot drag the window frame to a size smaller than these dimensions. A window can override this value by processing the WM_GETMINMAXINFO message
            //
            SM_CYMINTRACK = 35,
            //
            // Width of the rectangle around the location of a first click in a double-click sequence, in pixels. The second click must occur within the rectangle defined by SM_CXDOUBLECLK and SM_CYDOUBLECLK for the system to consider the two clicks a double-click
            //
            SM_CXDOUBLECLK = 36,
            //
            // Height of the rectangle around the location of a first click in a double-click sequence, in pixels. The second click must occur within the rectangle defined by SM_CXDOUBLECLK and SM_CYDOUBLECLK for the system to consider the two clicks a double-click. (The two clicks must also occur within a specified time.)
            //
            SM_CYDOUBLECLK = 37,
            //
            // Width of a grid cell for items in large icon view, in pixels. Each item fits into a rectangle of size SM_CXICONSPACING by SM_CYICONSPACING when arranged. This value is always greater than or equal to SM_CXICON
            //
            SM_CXICONSPACING = 38,
            //
            // Height of a grid cell for items in large icon view, in pixels. Each item fits into a rectangle of size SM_CXICONSPACING by SM_CYICONSPACING when arranged. This value is always greater than or equal to SM_CYICON.
            //
            SM_CYICONSPACING = 39,
            //
            // Nonzero if drop-down menus are right-aligned with the corresponding menu-bar item; zero if the menus are left-aligned.
            //
            SM_MENUDROPALIGNMENT = 40,
            //
            // Nonzero if the Microsoft Windows for Pen computing extensions are installed; zero otherwise.
            //
            SM_PENWINDOWS = 41,
            //
            // Nonzero if User32.dll supports DBCS; zero otherwise. (WinMe/95/98): Unicode
            //
            SM_DBCSENABLED = 42,
            //
            // Number of buttons on mouse, or zero if no mouse is installed.
            //
            SM_CMOUSEBUTTONS = 43,
            //
            // Identical Values Changed After Windows NT 4.0
            //
            SM_CXFIXEDFRAME = SM_CXDLGFRAME,
            //
            // Identical Values Changed After Windows NT 4.0
            //
            SM_CYFIXEDFRAME = SM_CYDLGFRAME,
            //
            // Identical Values Changed After Windows NT 4.0
            //
            SM_CXSIZEFRAME = SM_CXFRAME,
            //
            // Identical Values Changed After Windows NT 4.0
            //
            SM_CYSIZEFRAME = SM_CYFRAME,
            //
            // Nonzero if security is present; zero otherwise.
            //
            SM_SECURE = 44,
            //
            // Width of a 3-D border, in pixels. This is the 3-D counterpart of SM_CXBORDER
            //
            SM_CXEDGE = 45,
            //
            // Height of a 3-D border, in pixels. This is the 3-D counterpart of SM_CYBORDER
            //
            SM_CYEDGE = 46,
            //
            // Width of a grid cell for a minimized window, in pixels. Each minimized window fits into a rectangle this size when arranged. This value is always greater than or equal to SM_CXMINIMIZED.
            //
            SM_CXMINSPACING = 47,
            //
            // Height of a grid cell for a minimized window, in pixels. Each minimized window fits into a rectangle this size when arranged. This value is always greater than or equal to SM_CYMINIMIZED.
            //
            SM_CYMINSPACING = 48,
            //
            // Recommended width of a small icon, in pixels. Small icons typically appear in window captions and in small icon view
            //
            SM_CXSMICON = 49,
            //
            // Recommended height of a small icon, in pixels. Small icons typically appear in window captions and in small icon view.
            //
            SM_CYSMICON = 50,
            //
            // Height of a small caption, in pixels
            //
            SM_CYSMCAPTION = 51,
            //
            // Width of small caption buttons, in pixels.
            //
            SM_CXSMSIZE = 52,
            //
            // Height of small caption buttons, in pixels.
            //
            SM_CYSMSIZE = 53,
            //
            // Width of menu bar buttons, such as the child window close button used in the multiple document interface, in pixels.
            //
            SM_CXMENUSIZE = 54,
            //
            // Height of menu bar buttons, such as the child window close button used in the multiple document interface, in pixels.
            //
            SM_CYMENUSIZE = 55,
            //
            // Flags specifying how the system arranged minimized windows
            //
            SM_ARRANGE = 56,
            //
            // Width of a minimized window, in pixels.
            //
            SM_CXMINIMIZED = 57,
            //
            // Height of a minimized window, in pixels.
            //
            SM_CYMINIMIZED = 58,
            //
            // Default maximum width of a window that has a caption and sizing borders, in pixels. This metric refers to the entire desktop. The user cannot drag the window frame to a size larger than these dimensions. A window can override this value by processing the WM_GETMINMAXINFO message.
            //
            SM_CXMAXTRACK = 59,
            //
            // Default maximum height of a window that has a caption and sizing borders, in pixels. This metric refers to the entire desktop. The user cannot drag the window frame to a size larger than these dimensions. A window can override this value by processing the WM_GETMINMAXINFO message.
            //
            SM_CYMAXTRACK = 60,
            //
            // Default width, in pixels, of a maximized top-level window on the primary display monitor.
            //
            SM_CXMAXIMIZED = 61,
            //
            // Default height, in pixels, of a maximized top-level window on the primary display monitor.
            //
            SM_CYMAXIMIZED = 62,
            //
            // Least significant bit is set if a network is present; otherwise, it is cleared. The other bits are reserved for future use
            //
            SM_NETWORK = 63,
            //
            // Value that specifies how the system was started: 0-normal, 1-failsafe, 2-failsafe /w net
            //
            SM_CLEANBOOT = 67,
            //
            // Width of a rectangle centered on a drag point to allow for limited movement of the mouse pointer before a drag operation begins, in pixels.
            //
            SM_CXDRAG = 68,
            //
            // Height of a rectangle centered on a drag point to allow for limited movement of the mouse pointer before a drag operation begins. This value is in pixels. It allows the user to click and release the mouse button easily without unintentionally starting a drag operation.
            //
            SM_CYDRAG = 69,
            //
            // Nonzero if the user requires an application to present information visually in situations where it would otherwise present the information only in audible form; zero otherwise.
            //
            SM_SHOWSOUNDS = 70,
            //
            // Width of the default menu check-mark bitmap, in pixels.
            //
            SM_CXMENUCHECK = 71,
            //
            // Height of the default menu check-mark bitmap, in pixels.
            //
            SM_CYMENUCHECK = 72,
            //
            // Nonzero if the computer has a low-end (slow) processor; zero otherwise
            //
            SM_SLOWMACHINE = 73,
            //
            // Nonzero if the system is enabled for Hebrew and Arabic languages, zero if not.
            //
            SM_MIDEASTENABLED = 74,
            //
            // Nonzero if a mouse is installed; zero otherwise. This value is rarely zero, because of support for virtual mice and because some systems detect the presence of the port instead of the presence of a mouse.
            //
            SM_MOUSEPRESENT = 19,
            //
            // Windows 2000 (v5.0+) Coordinate of the top of the virtual screen
            //
            SM_XVIRTUALSCREEN = 76,
            //
            // Windows 2000 (v5.0+) Coordinate of the left of the virtual screen
            //
            SM_YVIRTUALSCREEN = 77,
            //
            // Windows 2000 (v5.0+) Width of the virtual screen
            //
            SM_CXVIRTUALSCREEN = 78,
            //
            // Windows 2000 (v5.0+) Height of the virtual screen
            //
            SM_CYVIRTUALSCREEN = 79,
            //
            // Number of display monitors on the desktop
            //
            SM_CMONITORS = 80,
            //
            // Windows XP (v5.1+) Nonzero if all the display monitors have the same color format, zero otherwise. Note that two displays can have the same bit depth, but different color formats. For example, the red, green, and blue pixels can be encoded with different numbers of bits, or those bits can be located in different places in a pixel's color value.
            //
            SM_SAMEDISPLAYFORMAT = 81,
            //
            // Windows XP (v5.1+) Nonzero if Input Method Manager/Input Method Editor features are enabled; zero otherwise
            //
            SM_IMMENABLED = 82,
            //
            // Windows XP (v5.1+) Width of the left and right edges of the focus rectangle drawn by DrawFocusRect. This value is in pixels.
            //
            SM_CXFOCUSBORDER = 83,
            //
            // Windows XP (v5.1+) Height of the top and bottom edges of the focus rectangle drawn by DrawFocusRect. This value is in pixels.
            //
            SM_CYFOCUSBORDER = 84,
            //
            // Nonzero if the current operating system is the Windows XP Tablet PC edition, zero if not.
            //
            SM_TABLETPC = 86,
            //
            // Nonzero if the current operating system is the Windows XP, Media Center Edition, zero if not.
            //
            SM_MEDIACENTER = 87,
            //
            // Metrics Other
            //
            SM_CMETRICS_OTHER = 76,
            //
            // Metrics Windows 2000
            //
            SM_CMETRICS_2000 = 83,
            //
            // Metrics Windows NT
            //
            SM_CMETRICS_NT = 88,
            //
            // Windows XP (v5.1+) This system metric is used in a Terminal Services environment. If the calling process is associated with a Terminal Services client session, the return value is nonzero. If the calling process is associated with the Terminal Server console session, the return value is zero. The console session is not necessarily the physical console - see WTSGetActiveConsoleSessionId for more information.
            //
            SM_REMOTESESSION = 0x1000,
            //
            // Windows XP (v5.1+) Nonzero if the current session is shutting down; zero otherwise
            //
            SM_SHUTTINGDOWN = 0x2000,
            //
            // Windows XP (v5.1+) This system metric is used in a Terminal Services environment. Its value is nonzero if the current session is remotely controlled; zero otherwise
            //
            SM_REMOTECONTROL = 0x2001,
        }

        #endregion ----- enums -----
    }
}
