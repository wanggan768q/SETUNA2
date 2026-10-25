using System;
using System.Runtime.InteropServices;
using SETUNA.Core.Platform;

namespace SETUNA.Platform.Windows
{
    /// <summary>
    /// 全局热键服务：message-only window 接收 WM_HOTKEY，独立于任何 UI 框架的窗口生命周期。
    ///
    /// 线程模型：Register/Unregister/SetKeyFilter 可从任意线程调用，内部封送到服务线程执行
    /// （RegisterHotKey/UnregisterHotKey 的窗口句柄方案要求窗口与热键同线程；封送经
    /// PostThreadMessage 自定义消息 + 服务线程内同步等待完成）。
    /// 低级键盘过滤（原 ClotheslineSpaceKeyHook）合并进本服务，同一服务线程安装钩子。
    /// </summary>
    public sealed class WindowsHotkeyService : IHotkeyService, IDisposable
    {
        // 自定义封送消息（WM_APP 区段，不与系统/框架冲突）
        private const int WM_APP_REGISTER = 0x8001;      // wParam=&Op, lParam=0；结果写 Op.Result
        private const int WM_APP_UNREGISTER = 0x8002;    // wParam=id, lParam=&Op
        private const int WM_APP_UNREGISTER_ALL = 0x8003;
        private const int WM_APP_SET_KEY_FILTER = 0x8004; // wParam=&Op(Func<int,bool>)

        private sealed class Registration
        {
            public HotkeySpec Spec;
            public Action Callback;
        }

        /// <summary>跨线程封送的操作单元：服务线程执行，调用线程等完成信号。</summary>
        private sealed class PendingOp
        {
            public HotkeyId Id;
            public HotkeySpec Spec;
            public Action Callback;
            public Func<int, bool> KeyFilter;
            public bool BoolResult;
            public readonly ManualResetEvent Done = new ManualResetEvent(false);
        }

        private readonly object _lock = new object();
        private readonly System.Collections.Generic.Dictionary<HotkeyId, Registration> _registrations = new();

        private IntPtr _hwnd;
        private uint _threadId;
        private volatile bool _ready;
        private readonly ManualResetEvent _readyEvent = new ManualResetEvent(false);
        private bool _disposed;

        // native 回调必须保持根引用（GC 回收 thunk = 窗口消息 AV），review 严重项 #2
        private NativeMethods.WndProcDelegate _wndProc;

        // 低级键盘钩子
        private IntPtr _hookHandle;
        private NativeMethods.LowLevelKeyboardProc _hookProc;
        private volatile Func<int, bool> _keyFilter;

        public bool Register(HotkeyId id, HotkeySpec spec, Action callback)
        {
            // 与原 SetunaOption.RegistHotKey 一致：先注销再注册（在服务线程上执行）
            var op = new PendingOp { Id = id, Spec = spec, Callback = callback };
            var unregistered = MarshalToServiceThread(WM_APP_UNREGISTER, op, wait: false);
            var registered = MarshalToServiceThread(WM_APP_REGISTER, op, wait: true);
            if (!registered)
            {
                var err = Marshal.GetLastWin32Error();
                Console.WriteLine($"RegisterHotKey FAILED id={id} mods={spec?.Modifiers} vk={spec?.VirtualKey} err={err}");
            }
            return registered;
        }

        public void Unregister(HotkeyId id)
        {
            if (!_ready)
            {
                return;
            }
            MarshalToServiceThread(WM_APP_UNREGISTER, new PendingOp { Id = id }, wait: false);
        }

        public void UnregisterAll()
        {
            if (!_ready)
            {
                return;
            }
            MarshalToServiceThread(WM_APP_UNREGISTER_ALL, new PendingOp(), wait: false);
        }

        public void SetKeyFilter(Func<int, bool> virtualKeyFilter)
        {
            MarshalToServiceThread(WM_APP_SET_KEY_FILTER, new PendingOp { KeyFilter = virtualKeyFilter }, wait: false);
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

        /// <summary>把操作封送到服务线程。wait=true 时同步等待结果；否则 fire-and-forget。</summary>
        private bool MarshalToServiceThread(int message, PendingOp op, bool wait)
        {
            EnsureThread();

            if (GetCurrentThreadId() == _threadId)
            {
                // 已在服务线程（钩子回调重入等）：直接执行
                ExecuteOp(message, op);
                return message == WM_APP_REGISTER ? op.BoolResult : true;
            }

            var handle = GCHandle.Alloc(op, GCHandleType.Normal);
            try
            {
                if (!NativeMethods.PostThreadMessage(_threadId, message, (IntPtr)message, GCHandle.ToIntPtr(handle)))
                {
                    handle.Free();
                    return false;
                }
                if (wait)
                {
                    op.Done.WaitOne(5000);
                }
                else
                {
                    // fire-and-forget：完成信号由服务线程释放 GCHandle
                    return true;
                }
            }
            finally
            {
                if (wait && op.Done.WaitOne(0))
                {
                    handle.Free();
                }
            }
            return op.BoolResult;
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
            _threadId = GetCurrentThreadId();

            const string className = "SETUNA_HotkeyMsgWindow";
            // 委托存字段保持根引用（review 严重项 #2：局部变量可被 GC 回收 → native thunk 悬空 AV）
            _wndProc = WndProc;
            var wndClass = new NativeMethods.WNDCLASS
            {
                lpfnWndProc = _wndProc,
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

            while (NativeMethods.GetMessageInt(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                if (msg.hwnd == IntPtr.Zero && (int)msg.message >= 0x8001 && (int)msg.message <= 0x8004)
                {
                    // 封送操作：lParam = GCHandle → PendingOp
                    var op = GCHandle.FromIntPtr(msg.lParam).Target as PendingOp;
                    ExecuteOp((int)msg.message, op);
                    op?.Done.Set();
                    var handle = GCHandle.FromIntPtr(msg.lParam);
                    handle.Free();
                    continue;
                }
                NativeMethods.TranslateMessage(ref msg);
                NativeMethods.DispatchMessage(ref msg);
            }

            NativeMethods.DestroyWindow(_hwnd);
        }

        /// <summary>在服务线程上执行封送操作（Register/Unregister 真正落地点）。</summary>
        private void ExecuteOp(int message, PendingOp op)
        {
            switch (message)
            {
                case WM_APP_REGISTER:
                    if (op.Spec == null || op.Spec.VirtualKey == 0)
                    {
                        // 与原实现一致：Keys.None 不注册但也算成功
                        op.BoolResult = true;
                        break;
                    }
                    if (NativeMethods.RegisterHotKey(_hwnd, (int)op.Id, op.Spec.Modifiers, op.Spec.VirtualKey) != 0)
                    {
                        lock (_lock)
                        {
                            _registrations[op.Id] = new Registration { Spec = op.Spec, Callback = op.Callback };
                        }
                        op.BoolResult = true;
                    }
                    else
                    {
                        op.BoolResult = false;
                    }
                    break;

                case WM_APP_UNREGISTER:
                    NativeMethods.UnregisterHotKey(_hwnd, (int)op.Id);
                    lock (_lock)
                    {
                        _registrations.Remove(op.Id);
                    }
                    break;

                case WM_APP_UNREGISTER_ALL:
                    lock (_lock)
                    {
                        foreach (var id in _registrations.Keys)
                        {
                            NativeMethods.UnregisterHotKey(_hwnd, (int)id);
                        }
                        _registrations.Clear();
                    }
                    break;

                case WM_APP_SET_KEY_FILTER:
                    _keyFilter = op.KeyFilter;
                    if (op.KeyFilter != null && _hookHandle == IntPtr.Zero)
                    {
                        _hookProc = KeyHookProc;
                        _hookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _hookProc, NativeMethods.GetModuleHandle(null), 0);
                        if (_hookHandle == IntPtr.Zero)
                        {
                            _hookProc = null;
                            Console.WriteLine("WindowsHotkeyService: 低级键盘钩子安装失败");
                        }
                    }
                    else if (op.KeyFilter == null && _hookHandle != IntPtr.Zero)
                    {
                        NativeMethods.UnhookWindowsHookEx(_hookHandle);
                        _hookHandle = IntPtr.Zero;
                        _hookProc = null;
                    }
                    break;
            }
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
                var msg = wParam.ToInt64();
                const int WM_KEYDOWN = 0x0100;
                const int WM_SYSKEYDOWN = 0x0104;

                // 仅在 down 上判定消费（up 恒放行，配对语义由状态机消费方维护）
                if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
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
            // WM_QUIT 发给服务线程（PostQuitMessage 只作用于调用线程，review 低危项）
            if (_threadId != 0)
            {
                NativeMethods.PostThreadMessage(_threadId, 0x0012 /*WM_QUIT*/, IntPtr.Zero, IntPtr.Zero);
            }
        }
    }
}
