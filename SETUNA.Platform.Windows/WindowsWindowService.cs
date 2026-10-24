using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using SETUNA.Core.Platform;

namespace SETUNA.Platform.Windows
{
    /// <summary>窗口行为服务：置顶/穿透/分层/Z序/枚举，全部 P/Invoke 集中于 NativeMethods。</summary>
    public sealed class WindowsWindowService : IWindowService
    {
        public void SetTopmost(IntPtr hwnd, bool topmost)
        {
            var insertAfter = topmost ? NativeMethods.HWND_TOPMOST : NativeMethods.HWND_NOTOPMOST;
            NativeMethods.SetWindowPos(hwnd, insertAfter, 0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
        }

        public void SetClickThrough(IntPtr hwnd, bool through)
        {
            var exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
            if (through)
            {
                exStyle |= NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_LAYERED;
            }
            else
            {
                exStyle &= ~NativeMethods.WS_EX_TRANSPARENT;
            }
            NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, exStyle);
        }

        public void SetLayeredAlpha(IntPtr hwnd, byte alpha)
        {
            var exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
            if ((exStyle & NativeMethods.WS_EX_LAYERED) == 0)
            {
                NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, exStyle | NativeMethods.WS_EX_LAYERED);
            }
            NativeMethods.SetLayeredWindowAttributes(hwnd, 0, alpha, NativeMethods.LWA_ALPHA);
        }

        public void SetWindowBounds(IntPtr hwnd, Rectangle bounds)
        {
            NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, bounds.X, bounds.Y, bounds.Width, bounds.Height,
                NativeMethods.SWP_NOACTIVATE);
        }

        public Rectangle GetWindowBounds(IntPtr hwnd)
        {
            if (NativeMethods.GetWindowRect(hwnd, out var rect))
            {
                return Rectangle.FromLTRB(rect.left, rect.top, rect.right, rect.bottom);
            }
            return Rectangle.Empty;
        }

        public WindowInfo GetForegroundWindow()
        {
            var hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
            {
                return null;
            }
            return GetWindowInfo(hwnd);
        }

        public IReadOnlyList<WindowInfo> EnumTopLevelWindows(Func<WindowInfo, bool> filter)
        {
            var result = new List<WindowInfo>();
            var current = NativeMethods.GetTopWindow(NativeMethods.GetDesktopWindow());
            while (current != IntPtr.Zero)
            {
                if (NativeMethods.IsWindowVisible(current)
                    && NativeMethods.GetWindow(current, NativeMethods.GW_OWNER) == IntPtr.Zero)
                {
                    var info = GetWindowInfo(current);
                    if (filter == null || filter(info))
                    {
                        result.Add(info);
                    }
                }
                current = NativeMethods.GetNextWindow(current, (int)NativeMethods.GW_HWNDNEXT);
            }
            return result;
        }

        public string GetWindowModuleFileName(IntPtr hwnd)
        {
            return NativeMethods.GetModuleName(hwnd);
        }

        internal static WindowInfo GetWindowInfo(IntPtr hwnd)
        {
            NativeMethods.GetWindowZOrder(hwnd, out var zOrder);

            var bounds = Rectangle.Empty;
            if (NativeMethods.GetWindowRect(hwnd, out var rect))
            {
                bounds = Rectangle.FromLTRB(rect.left, rect.top, rect.right, rect.bottom);
            }

            return new WindowInfo
            {
                Handle = hwnd,
                Title = NativeMethods.GetWindowTitle(hwnd),
                ClassName = NativeMethods.GetWindowClassName(hwnd),
                ModuleFileName = NativeMethods.GetModuleName(hwnd),
                Bounds = bounds,
                IsVisible = NativeMethods.IsWindowVisible(hwnd),
            };
        }
    }
}
