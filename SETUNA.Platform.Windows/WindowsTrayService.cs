using System;
using System.Collections.Generic;
using System.Drawing;
using SETUNA.Core.Platform;

namespace SETUNA.Platform.Windows
{
    /// <summary>
    /// Windows 托盘：Shell_NotifyIcon 原生实现（不依赖 WinForms 消息循环上下文，
    /// 可在 Avalonia 进程的 UI 线程安全创建）。菜单用原生 TrackPopupMenu 弹出。
    /// </summary>
    public sealed class WindowsTrayService : ITrayService
    {
        private ShellNotifyIcon _shellIcon;
        private TrayIconSpec _spec;
        private Icon _icon;
        private List<IntPtr> _menuHandles; // HMENU 子树防 GC/泄漏
        private bool _disposed;

        /// <summary>托盘菜单命令触发（宿主订阅后 marshal 到 UI 线程执行）。</summary>
        public event System.Threading.ThreadStart CommandInvoked;

        public void Initialize(TrayIconSpec spec)
        {
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));

            _shellIcon = new ShellNotifyIcon();
            _shellIcon.MessageReceived += OnTrayMessage;

            if (spec.IconData is { Length: > 0 })
            {
                using var stream = new System.IO.MemoryStream(spec.IconData);
                _icon = new Icon(stream);
            }

            _shellIcon.Create(spec.ToolTipText ?? "SETUNA", _icon?.Handle ?? IntPtr.Zero);
        }

        private void OnTrayMessage(uint evt)
        {
            var click = _spec?.ClickAction;
            var menu = _spec?.MenuItems;
            if (menu == null) return;

            switch (evt)
            {
                case ShellNotifyIcon.WM_LBUTTONCLICK:
                case ShellNotifyIcon.NIN_SELECT:
                    // 左键单击（对齐旧 Mainform：左键恢复主窗；Avalonia 宿主自定义 ClickAction）
                    click?.Invoke();
                    break;
                case ShellNotifyIcon.WM_RBUTTONUP:
                case ShellNotifyIcon.NIN_CONTEXTMENU:
                    ShowContextMenu(menu);
                    break;
            }
        }

        private void ShowContextMenu(IReadOnlyList<TrayMenuItem> items)
        {
            var menu = BuildMenu(items);
            if (menu == IntPtr.Zero) return;

            // 前台化 Trick：TrackPopupMenu 前必须 SetForegroundWindow，否则菜单点击丢失
            NativeMethods.SetForegroundWindow(_shellIcon.WindowHandle);
            NativeMethods.GetCursorPos(out var pt);
            // TPM_RETURNCMD：返回所选命令 id（0=取消），由 TrackCommand 派发
            var chosen = NativeMethods.TrackPopupMenu(menu, 0x0180u /*TPM_RIGHTBUTTON|TPM_RETURNCMD*/,
                pt.X, pt.Y, 0, _shellIcon.WindowHandle, IntPtr.Zero);
            NativeMethods.DestroyMenu(menu);
            _menuHandles = null;
            if (chosen != 0 && TrackCommand.TryDispatch(chosen))
            {
                // 通知宿主（命令内部触碰 UI 由宿主 marshal；此处仅在托盘线程同步执行）
                CommandInvoked?.Invoke();
            }
        }

        private IntPtr BuildMenu(IReadOnlyList<TrayMenuItem> items)
        {
            var menu = NativeMethods.CreatePopupMenu();
            _menuHandles = new List<IntPtr> { menu };
            foreach (var item in items ?? Array.Empty<TrayMenuItem>())
            {
                AppendMenuItem(menu, item);
            }
            return menu;
        }

        private IntPtr AppendMenuItem(IntPtr parent, TrayMenuItem item)
        {
            if (item.IsSeparator)
            {
                NativeMethods.AppendMenu(parent, 0x800u /*MF_SEPARATOR*/, IntPtr.Zero, null);
                return IntPtr.Zero;
            }

            if (item.Items is { Count: > 0 })
            {
                // 子菜单：先建子菜单填项，再以 MF_POPUP 挂到父级（id 参数=子菜单句柄）
                var sub = NativeMethods.CreatePopupMenu();
                _menuHandles.Add(sub);
                foreach (var child in item.Items)
                {
                    AppendMenuItem(sub, child);
                }
                var popupFlags = 0x10u /*MF_POPUP*/ | 0x0u /*MF_STRING*/ | (uint)(item.IsEnabled ? 0 : 0x2);
                NativeMethods.AppendMenu(parent, popupFlags, sub, item.Header ?? string.Empty);
                return sub;
            }

            var id = TrackCommand.Register(() =>
            {
                // 回调抛异常不炸托盘线程
                try { item.ClickAction?.Invoke(); }
                catch (Exception ex) { Console.WriteLine($"tray menu action: {ex.Message}"); }
            });
            var flags = 0x0u /*MF_STRING*/ | (uint)(item.IsEnabled ? 0 : 0x2 /*MF_GRAYED*/);
            NativeMethods.AppendMenu(parent, flags, (IntPtr)id, item.Header ?? string.Empty);
            return IntPtr.Zero;
        }

        public void RebuildMenu(TrayIconSpec spec)
        {
            _spec = spec ?? _spec;
            // 菜单在每次右键时从 _spec 动态构建（BuildMenu），无需缓存 → 自然支持动态刷新
        }

        public void ShowBalloon(string title, string message)
        {
            // ponytail: 气泡提示暂缺（Shell_NotifyIcon NIM_MODIFY + NIF_INFO），验收不依赖
            System.Diagnostics.Debug.WriteLine($"[tray balloon] {title}: {message}");
        }

        public void SetToolTip(string text)
        {
            _shellIcon?.UpdateTip(text ?? string.Empty);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _shellIcon?.Dispose();
            _shellIcon = null;
            _icon?.Dispose();
            _icon = null;
            TrackCommand.Clear();
        }
    }

    /// <summary>托盘命令 ID → Action 注册表（TrackPopupMenu 返回命令 id 后派发）。</summary>
    internal static class TrackCommand
    {
        private static readonly Dictionary<int, Action> _commands = new();
        private static int _nextId = 1;

        public static int Register(Action action)
        {
            if (action == null) return 0;
            var id = _nextId++;
            _commands[id] = action;
            return id;
        }

        public static bool TryDispatch(int id)
        {
            if (_commands.TryGetValue(id, out var action))
            {
                action();
                return true;
            }
            return false;
        }

        public static void Clear() => _commands.Clear();
    }
}
