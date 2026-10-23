using System;
using System.Runtime.InteropServices;

namespace SETUNA.Main.Clothesline
{
    /// <summary>
    /// WH_KEYBOARD_LL 空格键钩子：仅在鼠标命中小照片缩略图（或预览已打开）且无修饰键时
    /// 消费空格用于切换预览。回调只做轻量判断，UI 切换通过 ToggleRequested 事件由宿主
    /// BeginInvoke 投递。一个物理轮次（down→repeat→up）由 ClotheslineSpaceKeyStateMachine
    /// 决定透传/消费；命中检测在 down 时刻完成，非命中轮整轮透传。
    /// </summary>
    public class ClotheslineSpaceKeyHook : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_KEYUP = 0x0105;
        private const int WM_SYSKEYUP = 0x0108;
        private const int VK_SPACE = 0x20;
        private const int VK_SHIFT = 0x10;
        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12;
        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;

        private readonly Func<bool> _isHit;

        private IntPtr _hookHandle;
        private LowLevelKeyboardProc _hookProc;
        private readonly ClotheslineSpaceKeyStateMachine _stateMachine = new ClotheslineSpaceKeyStateMachine();
        private bool _disposed;

        /// <summary>本轮首个 down 命中且无修饰键：宿主投递到 UI 线程切换预览（每轮至多一次）。</summary>
        public event Action ToggleRequested;

        public ClotheslineSpaceKeyHook(Func<bool> isHit)
        {
            _isHit = isHit ?? (() => false);
        }

        /// <summary>安装低级键盘钩子；失败返回 false（调用方提示并回滚模式）。</summary>
        public bool Install()
        {
            if (_hookHandle != IntPtr.Zero)
            {
                return true;
            }

            _hookProc = HookProc;
            _hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, GetModuleHandle(null), 0);
            if (_hookHandle == IntPtr.Zero)
            {
                _hookProc = null;
                return false;
            }

            // 安装时物理键已按住：本轮无 down 可见，整轮透传直到该轮 keyup
            if ((GetAsyncKeyState(VK_SPACE) & 0x8000) != 0)
            {
                _stateMachine.MarkAlreadyDown();
            }
            return true;
        }

        public void Uninstall()
        {
            if (_hookHandle != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookHandle);
                _hookHandle = IntPtr.Zero;
            }
            _hookProc = null;
            _stateMachine.Reset();
        }

        /// <summary>状态机是否仍处已消费 down、等待配对 keyup 的轮次（延迟卸钩判定）。</summary>
        public bool IsConsumingRound => _stateMachine.ConsumingRound;

        /// <summary>钩子是否已安装。</summary>
        public bool IsInstalled => _hookHandle != IntPtr.Zero;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            Uninstall();
        }

        private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode < 0)
            {
                return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
            }

            var msg = wParam.ToInt64();
            var isSpace = false;
            var isDown = false;
            switch ((int)msg)
            {
                case WM_KEYDOWN:
                case WM_SYSKEYDOWN:
                    if (ReadVirtualKeyCode(lParam) == VK_SPACE)
                    {
                        isSpace = true;
                        isDown = true;
                    }
                    break;
                case WM_KEYUP:
                case WM_SYSKEYUP:
                    if (ReadVirtualKeyCode(lParam) == VK_SPACE)
                    {
                        isSpace = true;
                    }
                    break;
            }

            if (!isSpace)
            {
                return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
            }

            var action = isDown ? _stateMachine.OnKeyDown(CanConsume()) : _stateMachine.OnKeyUp();
            switch (action)
            {
                case SpaceKeyAction.TogglePreview:
                    ToggleRequested?.Invoke();
                    return (IntPtr)1;
                case SpaceKeyAction.Consume:
                    return (IntPtr)1;
                default:
                    return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
            }
        }

        /// <summary>本轮首个 down 的消费判定：无修饰键按下且鼠标命中缩略图。</summary>
        private bool CanConsume()
        {
            if (IsModifierDown())
            {
                return false;
            }
            return _isHit();
        }

        private static bool IsModifierDown()
        {
            return (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0
                || (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0
                || (GetAsyncKeyState(VK_MENU) & 0x8000) != 0
                || (GetAsyncKeyState(VK_LWIN) & 0x8000) != 0
                || (GetAsyncKeyState(VK_RWIN) & 0x8000) != 0;
        }

        private static int ReadVirtualKeyCode(IntPtr lParam)
        {
            // KBDLLHOOKSTRUCT.vkCode 位于结构体首字段
            return Marshal.ReadInt32(lParam);
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
    }
}
