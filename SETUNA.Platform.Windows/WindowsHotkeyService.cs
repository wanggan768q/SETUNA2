using System;
using System.Runtime.InteropServices;
using SETUNA.Core.Platform;

namespace SETUNA.Platform.Windows
{
    /// <summary>
    /// 全局热键服务：message-only window 接收 WM_HOTKEY，独立于任何 UI 框架的窗口生命周期。
    /// 线程模型：Register/Unregister 必须与消息泵在同一线程（服务构造时自建线程）。
    /// 低级键盘过滤（原 ClotheslineSpaceKeyHook）合并进本服务，同一消息线程安装钩子。
    /// </summary>
    public sealed class WindowsHotkeyService : IHotkeyService, IDisposable
    {
        private const int ModAlt = 1;
        private const int ModControl = 2;
        private const int ModShift = 4;

        private sealed class Registration
        {
            public HotkeySpec Spec;
            public Action Callback;
        }

        private readonly object _lock = new object();
        private readonly System.Collections.Generic.Dictionary<HotkeyId, Registration> _registrations = new();

        private IntPtr _hwnd;
        private uint _threadId;
        private bool _ready;
        private readonly ManualResetEvent _readyEvent = new ManualResetEvent(false);
        private bool _disposed;

        // 低级键盘钩子
        private IntPtr _hookHandle;
        private NativeMethods.LowLevelKeyboardProc _hookProc;
        private Func<int, bool> _keyFilter;

        public bool Register(HotkeyId id, HotkeySpec spec, Action callback)
        {
            EnsureThread();
            // 与原 SetunaOption.RegistHotKey 一致：先注销再注册
            Unregister(id);

            if (spec == null || spec.VirtualKey == 0)
            {
                return true; // 与原实现一致：Keys.None 不注册但也算成功
            }

            lock (_lock)
            {
                if (NativeMethods.RegisterHotKey(_hwnd, (int)id, spec.Modifiers, spec.VirtualKey) == 0)
                {
                    return false;
                }
                _registrations[id] = new Registration { Spec = spec, Callback = callback };
            }
            return true;
        }

        public void Unregister(HotkeyId id)
        {
            if (!_ready)
            {
                return;
            }

            lock (_lock)
            {
                if (NativeMethods.UnregisterHotKey(_hwnd, (int)id) != 0)
                {
                    _registrations.Remove(id);
                }
                else if (_registrations.ContainsKey(id))
                {
                    // 注销失败但表里有：清表（原实现 UnregistHotKey 无条件调用，不查注册状态）
                    _registrations.Remove(id);
                }
            }
        }

        public void UnregisterAll()
        {
            if (!_ready)
            {
                return;
            }

            lock (_lock)
            {
                foreach (var id in _registrations.Keys)
                {
                    NativeMethods.UnregisterHotKey(_hwnd, (int)id);
                }
                _registrations.Clear();
            }
        }

        public void SetKeyFilter(Func<int, bool> virtualKeyFilter)
        {
            EnsureThread();
            _keyFilter = virtualKeyFilter;
            if (virtualKeyFilter != null && _hookHandle == IntPtr.Zero)
            {
                _hookProc = KeyHookProc;
                _hookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _hookProc, NativeMethods.GetModuleHandle(null), 0);
                if (_hookHandle == IntPtr.Zero)
                {
                    _hookProc = null;
                    Console.WriteLine("WindowsHotkeyService: 低级键盘钩子安装失败");
                }
            }
            else if (virtualKeyFilter == null && _hookHandle != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hookHandle);
                _hookHandle = IntPtr.Zero;
                _hookProc = null;
            }
        }

        public int GetModifierState()
        {
            var state = 0;
            if ((NativeMethods.GetAsyncKeyState(NativeMethods.VK_MENU) & 0x8000) != 0)
            {
                state |= 1;
            }
            if ((NativeMethods.GetAsyncKeyState(NativeMethods.VK_CONTROL) & 0x8000) != 0)
            {
                state |= 2;
            }
            if ((NativeMethods.GetAsyncKeyState(NativeMethods.VK_SHIFT) & 0x8000) != 0)
            {
                state |= 4;
            }
            if ((NativeMethods.GetAsyncKeyState(NativeMethods.VK_LWIN) & 0x8000) != 0
                || (NativeMethods.GetAsyncKeyState(NativeMethods.VK_RWIN) & 0x8000) != 0)
            {
                state |= 8;
            }
            return state;
        }

        private void EnsureThread()
        {
            if (_ready)
            {
                return;
            }

            var thread = new Thread(MessageLoop)
            {
                Name = "SETUNA.HotkeyThread",
                IsBackground = true,
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            _readyEvent.WaitOne(5000);
            if (!_ready)
            {
                throw new InvalidOperationException("热键消息线程启动失败");
            }
        }

        private void MessageLoop()
        {
            _threadId = NativeMethods.GetModuleHandle(null) != IntPtr.Zero ? GetCurrentThreadId() : 0;

            const string className = "SETUNA_HotkeyMsgWindow";
            var wndClass = new NativeMethods.WNDCLASS
            {
                lpfnWndProc = WndProc,
                lpszClassName = className,
                hInstance = NativeMethods.GetModuleHandle(null),
            };
            NativeMethods.RegisterClass(ref wndClass);

            // message-only window：HWND_MESSAGE 父窗口，不接收广播、不可见
            _hwnd = NativeMethods.CreateWindowEx(0, className, "", 0,
                0, 0, 0, 0, new IntPtr(-3), IntPtr.Zero, wndClass.hInstance, IntPtr.Zero);
            if (_hwnd == IntPtr.Zero)
            {
                Console.WriteLine("WindowsHotkeyService: message-only window 创建失败");
                _readyEvent.Set();
                return;
            }

            _ready = true;
            _readyEvent.Set();

            while (NativeMethods.GetMessage(out var msg, IntPtr.Zero, 0, 0))
            {
                NativeMethods.TranslateMessage(ref msg);
                NativeMethods.DispatchMessage(ref msg);
            }

            NativeMethods.DestroyWindow(_hwnd);
        }

        private IntPtr WndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == NativeMethods.WM_HOTKEY)
            {
                var id = (HotkeyId)wParam.ToInt64();
                Registration registration;
                lock (_lock)
                {
                    _registrations.TryGetValue(id, out registration);
                }
                registration?.Callback?.Invoke();
                return IntPtr.Zero;
            }
            return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
        }

        private IntPtr KeyHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode < 0)
            {
                return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
            }

            var filter = _keyFilter;
            if (filter != null)
            {
                var vk = Marshal.ReadInt32(lParam); // KBDLLHOOKSTRUCT.vkCode 位于结构体首字段
                // 仅在 down 消息上判定；过滤消费则吞掉，否则放行
                var msg = wParam.ToInt64();
                const int WM_KEYDOWN = 0x0100;
                const int WM_SYSKEYDOWN = 0x0104;
                const int WM_KEYUP = 0x0105;
                const int WM_SYSKEYUP = 0x0108;

                if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN || msg == WM_KEYUP || msg == WM_SYSKEYUP)
                {
                    if (filter(vk))
                    {
                        return (IntPtr)1;
                    }
                }
            }

            return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;

            if (_hookHandle != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hookHandle);
                _hookHandle = IntPtr.Zero;
            }
            UnregisterAll();
            if (_hwnd != IntPtr.Zero)
            {
                NativeMethods.PostQuitMessage(0);
                _hwnd = IntPtr.Zero;
            }
        }
    }
}
