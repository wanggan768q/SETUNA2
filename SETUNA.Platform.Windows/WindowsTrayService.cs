using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using SETUNA.Core.Platform;

namespace SETUNA.Platform.Windows
{
    /// <summary>
    /// Windows 托盘：WinForms NotifyIcon 实现。
    /// （期 3 Avalonia 端若改用 Avalonia TrayIcon，可在此替换实现，接口不变。）
    /// </summary>
    public sealed class WindowsTrayService : ITrayService
    {
        private System.Windows.Forms.NotifyIcon _notifyIcon;
        private TrayIconSpec _spec;

        public void Initialize(TrayIconSpec spec)
        {
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));

            _notifyIcon = new System.Windows.Forms.NotifyIcon
            {
                Text = spec.ToolTipText ?? "SETUNA",
                Visible = true,
            };

            if (spec.IconData is { Length: > 0 })
            {
                using var stream = new System.IO.MemoryStream(spec.IconData);
                _notifyIcon.Icon = new Icon(stream);
            }

            if (spec.MenuItems is { Count: > 0 })
            {
                var menu = new System.Windows.Forms.ContextMenuStrip();
                foreach (var item in spec.MenuItems)
                {
                    menu.Items.Add(BuildItem(item));
                }
                _notifyIcon.ContextMenuStrip = menu;
            }

            if (spec.ClickAction != null)
            {
                _notifyIcon.MouseClick += (s, e) =>
                {
                    if (e.Button == System.Windows.Forms.MouseButtons.Left)
                    {
                        spec.ClickAction();
                    }
                };
            }
        }

        private static System.Windows.Forms.ToolStripItem BuildItem(TrayMenuItem item)
        {
            if (item.IsSeparator)
            {
                return new System.Windows.Forms.ToolStripSeparator();
            }

            var menuItem = new System.Windows.Forms.ToolStripMenuItem(item.Header ?? string.Empty)
            {
                Enabled = item.IsEnabled,
            };
            if (item.ClickAction != null)
            {
                menuItem.Click += (s, e) => item.ClickAction();
            }
            foreach (var child in item.Items ?? Array.Empty<TrayMenuItem>())
            {
                menuItem.DropDownItems.Add(BuildItem(child));
            }
            return menuItem;
        }

        public void ShowBalloon(string title, string message)
        {
            if (_notifyIcon == null)
            {
                return;
            }
            _notifyIcon.BalloonTipTitle = title ?? string.Empty;
            _notifyIcon.BalloonTipText = message ?? string.Empty;
            _notifyIcon.ShowBalloonTip(2000);
        }

        public void SetToolTip(string text)
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Text = text ?? string.Empty;
            }
        }

        public void Dispose()
        {
            _notifyIcon?.Dispose();
            _notifyIcon = null;
        }
    }
}
