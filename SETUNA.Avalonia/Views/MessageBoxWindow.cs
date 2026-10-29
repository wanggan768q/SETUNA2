using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace SETUNA.Avalonia.Views
{
    /// <summary>
    /// 通用消息框（替代 WinForms MessageBox）：按 buttons 位掩码呈现按钮组。
    /// buttons 编码对齐 WinForms MessageBoxButtons：0=OK, 1=OKCancel, 4=YesNo, 0x44=YesNoCancel(简化)。
    /// 返回值对齐 WinForms DialogResult：OK=1, Cancel=2, Yes=6, No=7。
    /// </summary>
    public class MessageBoxWindow : Window
    {
        public MessageBoxWindow(string title, string message, int buttons)
        {
            Title = title;
            CanResize = false;
            SizeToContent = SizeToContent.WidthAndHeight;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            MinWidth = 320;
            MaxWidth = 480;

            var panel = new StackPanel { Margin = new Thickness(16), Spacing = 16 };
            panel.Children.Add(new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 440,
            });

            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 8,
            };

            void Add(string label, int result)
            {
                var b = new Button { Content = label, MinWidth = 72 };
                b.Click += (s, e) => Close(result);
                row.Children.Add(b);
            }

            switch (buttons)
            {
                case 4: // YesNo
                    Add("是", 6);
                    Add("否", 7);
                    break;
                case 3: // YesNoCancel（WinForms 0x3|0x100 简化）
                case 0x103:
                    Add("是", 6);
                    Add("否", 7);
                    Add("取消", 2);
                    break;
                case 1: // OKCancel
                    Add("确定", 1);
                    Add("取消", 2);
                    break;
                default: // OK
                    Add("确定", 1);
                    break;
            }

            panel.Children.Add(row);
            Content = panel;
        }
    }
}
