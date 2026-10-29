using System;
using System.Runtime.InteropServices;

namespace SETUNA.Platform.Windows
{
    /// <summary>
    /// Shell_NotifyIcon 原生实现（替代 WinForms NotifyIcon）：
    /// 不依赖 WinForms 消息循环上下文，回调经回调消息（NIN_*）派发。
    /// </summary>
    internal sealed class ShellNotifyIcon : IDisposable
    {
        public const int WM_APP_TRAY = 0x8005; // 自定义回调消息（WM_APP 区段）

        public event Action<uint> MessageReceived; // lParam = 鼠标事件码

        private const int NIM_ADD = 0x0;
        private const int NIM_MODIFY = 0x1;
        private const int NIM_DELETE = 0x2;
        private const int NIM_SETVERSION = 0x4;

        private const int NOTIFYICON_VERSION_4 = 4;

        private const uint NIF_MESSAGE = 0x1;
        private const uint NIF_ICON = 0x2;
        private const uint NIF_TIP = 0x4;

        // NOTIFYICON_VERSION_4 事件码（lParam 低字）
        public const uint WM_LBUTTONCLICK = 0x202;   // WM_LBUTTONUP
        public const uint WM_RBUTTONUP = 0x205;
        public const uint NIN_SELECT = 0x400;        // 版本4：选择事件
        public const uint NIN_CONTEXTMENU = 0x405;   // 版本4：右键菜单事件

        private IntPtr _hwnd;
        private NativeMethods.WndProcDelegate _wndProc; // GC 根引用
        private NOTIFYICONDATA _data;
        private bool _added;
        private bool _disposed;

        public IntPtr WindowHandle => _hwnd;

        public void Create(string tipText, IntPtr hIcon)
        {
            const string className = "SETUNA_TrayMsgWindow";
            _wndProc = TrayWndProc;
            var wndClass = new NativeMethods.WNDCLASS
            {
                lpfnWndProc = _wndProc,
                lpszClassName = className,
                hInstance = NativeMethods.GetModuleHandle(null),
            };
            NativeMethods.RegisterClass(ref wndClass);

            _hwnd = NativeMethods.CreateWindowEx(0, className, "", 0,
                0, 0, 0, 0, new IntPtr(-3), IntPtr.Zero, wndClass.hInstance, IntPtr.Zero);
            if (_hwnd == IntPtr.Zero)
            {
                throw new InvalidOperationException("托盘消息窗口创建失败");
            }

            _data = new NOTIFYICONDATA
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = 1,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_APP_TRAY,
                hIcon = hIcon,
                szTip = tipText ?? string.Empty,
                uVersion = NOTIFYICON_VERSION_4,
            };
            _added = Shell_NotifyIcon(NIM_ADD, ref _data);
            if (_added)
            {
                Shell_NotifyIcon(NIM_SETVERSION, ref _data);
            }
        }

        public bool IsVisible => _added;

        private IntPtr TrayWndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_APP_TRAY)
            {
                // 版本 4：LOWORD(lParam)=事件, HIWORD(lParam)=图标 id
                var evt = (uint)(lParam.ToInt64() & 0xFFFF);
                MessageReceived?.Invoke(evt);
                return IntPtr.Zero;
            }
            return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
        }

        public void UpdateTip(string text)
        {
            if (!_added) return;
            _data.szTip = text ?? string.Empty;
            Shell_NotifyIcon(NIM_MODIFY, ref _data);
        }

        [DllImport("shell32.dll", SetLastError = true)]
        private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NOTIFYICONDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public uint dwState;
            public uint dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public uint uVersion; // union with uTimeout
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public uint dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_added && _hwnd != IntPtr.Zero)
            {
                Shell_NotifyIcon(NIM_DELETE, ref _data);
                _added = false;
            }
            if (_hwnd != IntPtr.Zero)
            {
                NativeMethods.DestroyWindow(_hwnd);
                _hwnd = IntPtr.Zero;
            }
        }
    }
}
