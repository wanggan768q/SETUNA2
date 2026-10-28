using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using SETUNA.Main;
using SETUNA.Main.Option;
using SETUNA.Main.Style;
using global::Avalonia.Input;
using AvaloniaInput = global::Avalonia.Input;
using SETUNA.Main.StyleItems;

namespace SETUNA.Avalonia.Views
{
    /// <summary>
    /// 设置窗口（对应旧 OptionForm 的期 6 核心子集）：
    /// 热键（截图/功能1）、样式列表（新建/编辑/删除）、贴图行为开关、晾绳开关。
    /// ponytail: 截图颜色/选区透明度/点击截图等次级项期后按需补；持久化字段与旧版一一对应。
    /// </summary>
    public class OptionWindow : Window
    {
        private readonly SetunaOption _working;   // 编辑副本（OK 才回写）
        private readonly Action _saved;

        private CheckBox _chkHotkeyEnable;
        private readonly HotkeyBox[] _hotkeyBoxes = new HotkeyBox[(int)SETUNA.Main.HotKeyID.__Count__];

        private ListBox _listStyles;
        private CheckBox _chkImageDrag;
        private CheckBox _chkInactiveAlpha;
        private NumericUpDown _numInactiveAlpha;
        private CheckBox _chkTopMost;
        private CheckBox _chkCursor;
        private CheckBox _chkClothesline;
        private ComboBox _cmbOrientation;
        private CheckBox _chkEdgeReveal;

        public OptionWindow(SetunaOption current, Action saved)
        {
            _working = current;
            _saved = saved;

            Title = "SETUNA 设置";
            Width = 520;
            Height = 560;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            var tabs = new TabControl();

            // === 贴图页 ===
            var scrapPage = new TabItem { Header = "贴图" };
            scrapPage.Content = BuildScrapPage();

            // === 样式页 ===
            var stylePage = new TabItem { Header = "样式" };
            stylePage.Content = BuildStylePage();

            // === 热键页 ===
            var hotkeyPage = new TabItem { Header = "热键" };
            hotkeyPage.Content = BuildHotkeyPage();

            // === 晾绳页 ===
            var clotheslinePage = new TabItem { Header = "晾绳" };
            clotheslinePage.Content = BuildClotheslinePage();

            tabs.Items.Add(scrapPage);
            tabs.Items.Add(stylePage);
            tabs.Items.Add(hotkeyPage);
            tabs.Items.Add(clotheslinePage);

            var root = new DockPanel { Margin = new Thickness(10) };
            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 8,
                Margin = new Thickness(0, 10, 0, 0),
            };
            var ok = new Button { Content = "确定", MinWidth = 80 };
            ok.Click += (s, e) => { WriteBack(); _saved?.Invoke(); Close(); };
            var cancel = new Button { Content = "取消", MinWidth = 80 };
            cancel.Click += (s, e) => Close();
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);

            DockPanel.SetDock(buttons, Dock.Bottom);
            root.Children.Add(buttons);
            root.Children.Add(tabs);
            Content = root;

            LoadOption();
        }

        // ===== 贴图页 =====

        private Control BuildScrapPage()
        {
            var panel = new StackPanel { Margin = new Thickness(10), Spacing = 8 };
            _chkImageDrag = new CheckBox { Content = "允许拖放图片文件到桌面创建贴图" };
            _chkInactiveAlpha = new CheckBox { Content = "失焦时降低不透明度" };
            _numInactiveAlpha = new NumericUpDown { Minimum = 1, Maximum = 100, Width = 100 };
            _chkTopMost = new CheckBox { Content = "贴图保持置顶（500ms 轮询）" };
            _chkCursor = new CheckBox { Content = "截图包含鼠标指针" };
            panel.Children.Add(_chkImageDrag);
            panel.Children.Add(_chkInactiveAlpha);
            panel.Children.Add(Row("不透明度(%)", _numInactiveAlpha));
            panel.Children.Add(new Separator());
            panel.Children.Add(_chkTopMost);
            panel.Children.Add(_chkCursor);
            return new ScrollViewer { Content = panel };
        }

        // ===== 样式页 =====

        private Control BuildStylePage()
        {
            var panel = new StackPanel { Margin = new Thickness(10), Spacing = 8 };

            _listStyles = new ListBox { Height = 260 };
            var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var btnEdit = new Button { Content = "编辑…" };
            btnEdit.Click += (s, e) => EditSelectedStyle();
            var btnDelete = new Button { Content = "删除" };
            btnDelete.Click += (s, e) => DeleteSelectedStyle();
            buttonRow.Children.Add(btnEdit);
            buttonRow.Children.Add(btnDelete);

            panel.Children.Add(new TextBlock { Text = "样式列表（双击编辑）" });
            panel.Children.Add(_listStyles);
            panel.Children.Add(buttonRow);
            _listStyles.DoubleTapped += (s, e) => EditSelectedStyle();

            return new ScrollViewer { Content = panel };
        }

        private void EditSelectedStyle()
        {
            if (_listStyles.SelectedItem is CStyle style)
            {
                var editor = new StyleEditorWindow(style);
                editor.ShowDialog(this);
                RefreshStyleList();
            }
        }

        private void DeleteSelectedStyle()
        {
            if (_listStyles.SelectedItem is CStyle style && style.StyleID > 0)
            {
                _working.Styles.Remove(style);
                RefreshStyleList();
            }
        }

        private void RefreshStyleList()
        {
            var selected = _listStyles.SelectedItem;
            _listStyles.Items.Clear();
            foreach (var style in _working.Styles)
            {
                _listStyles.Items.Add(style);
            }
            if (selected != null && _working.Styles.Contains(selected))
            {
                _listStyles.SelectedItem = selected;
            }
        }

        // ===== 热键页 =====

        private Control BuildHotkeyPage()
        {
            var panel = new StackPanel { Margin = new Thickness(10), Spacing = 8 };
            _chkHotkeyEnable = new CheckBox { Content = "启用全局热键" };
            panel.Children.Add(_chkHotkeyEnable);

            var names = new[] { "截图", "全部显示/隐藏" };
            for (var i = 0; i < (int)SETUNA.Main.HotKeyID.__Count__ && i < names.Length; i++)
            {
                _hotkeyBoxes[i] = new HotkeyBox();
                panel.Children.Add(Row(names[i], _hotkeyBoxes[i]));
            }
            return new ScrollViewer { Content = panel };
        }

        // ===== 晾绳页 =====

        private Control BuildClotheslinePage()
        {
            var panel = new StackPanel { Margin = new Thickness(10), Spacing = 8 };
            _chkClothesline = new CheckBox { Content = "启用照片晾绳模式" };
            _cmbOrientation = new ComboBox { Width = 160 };
            _cmbOrientation.Items.Add("自动（按屏幕宽高比）");
            _cmbOrientation.Items.Add("横向");
            _cmbOrientation.Items.Add("竖向");
            _cmbOrientation.SelectedIndex = 0;
            panel.Children.Add(_chkClothesline);
            panel.Children.Add(Row("绳子方向", _cmbOrientation));
            _chkEdgeReveal = new CheckBox { Content = "移到屏幕边缘自动呼出绳子" };
            panel.Children.Add(_chkEdgeReveal);
            return new ScrollViewer { Content = panel };
        }

        // ===== 读/写 =====

        private void LoadOption()
        {
            for (var i = 0; i < (int)SETUNA.Main.HotKeyID.__Count__; i++)
            {
                if (i < _hotkeyBoxes.Length && _hotkeyBoxes[i] != null && i < _working.ScrapHotKeys.Length)
                {
                    _hotkeyBoxes[i].Hotkey = _working.ScrapHotKeys[i];
                }
            }
            _chkHotkeyEnable.IsChecked = _working.ScrapHotKeyEnable;
            _chkImageDrag.IsChecked = _working.Scrap.ImageDrag;
            _chkInactiveAlpha.IsChecked = _working.Scrap.InactiveAlphaChange;
            _numInactiveAlpha.Value = _working.Scrap.InactiveAlphaValue;
            _chkTopMost.IsChecked = _working.Setuna.TopMostEnabled;
            _chkCursor.IsChecked = _working.Setuna.CursorEnabled;
            _chkClothesline.IsChecked = _working.ClotheslineEnabled;
            _cmbOrientation.SelectedIndex = (int)_working.ClotheslineRopeOrientation;
            _chkEdgeReveal.IsChecked = _working.ClotheslineEdgeReveal;
            RefreshStyleList();
        }

        private void WriteBack()
        {
            for (var i = 0; i < (int)SETUNA.Main.HotKeyID.__Count__; i++)
            {
                if (i < _hotkeyBoxes.Length && _hotkeyBoxes[i] != null && i < _working.ScrapHotKeys.Length)
                {
                    _working.ScrapHotKeys[i] = _hotkeyBoxes[i].Hotkey;
                }
            }
            _working.ScrapHotKeyEnable = _chkHotkeyEnable.IsChecked == true;
            _working.Scrap.ImageDrag = _chkImageDrag.IsChecked == true;
            _working.Scrap.InactiveAlphaChange = _chkInactiveAlpha.IsChecked == true;
            _working.Scrap.InactiveAlphaValue = (sbyte)_numInactiveAlpha.Value;
            _working.Setuna.TopMostEnabled = _chkTopMost.IsChecked == true;
            _working.Setuna.CursorEnabled = _chkCursor.IsChecked == true;
            _working.ClotheslineEnabled = _chkClothesline.IsChecked == true;
            _working.ClotheslineRopeOrientation = (ClotheslineRopeOrientation)Math.Max(0, _cmbOrientation.SelectedIndex);
            _working.ClotheslineEdgeReveal = _chkEdgeReveal.IsChecked == true;
        }

        private static StackPanel Row(string label, Control control)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            panel.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, MinWidth = 120 });
            panel.Children.Add(control);
            return panel;
        }
    }

    /// <summary>
    /// 热键录入框（对应旧 HotkeyControl）：焦点内按键捕获修饰键+键码显示；点击清除。
    /// </summary>
    public class HotkeyBox : TextBox
    {
        private System.Windows.Forms.Keys _hotkey = System.Windows.Forms.Keys.None;

        public System.Windows.Forms.Keys Hotkey
        {
            get => _hotkey;
            set
            {
                _hotkey = value;
                Text = ToString(_hotkey);
            }
        }

        public HotkeyBox()
        {
            IsReadOnly = true;
            Width = 160;
            Text = ToString(_hotkey);
            KeyDown += OnKeyDown;
            GotFocus += (s, e) => { Text = "按下快捷键…"; };
            LostFocus += (s, e) => { Text = ToString(_hotkey); };
            DoubleTapped += (s, e) => { _hotkey = System.Windows.Forms.Keys.None; Text = ToString(_hotkey); };
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            // Avalonia Key → Win32 VK：Avalonia Key 枚举即 VK 值（数字/字母/F 键一致）；
            // 少量差异键（ponytail：完整映射表期后按需补）
            var vk = (int)e.Key;
            if (vk == 0 || e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl
                || e.Key == Key.LeftShift || e.Key == Key.RightShift
                || e.Key == Key.LeftAlt || e.Key == Key.RightAlt
                || e.Key == Key.LWin || e.Key == Key.RWin)
            {
                e.Handled = true;
                return;
            }

            var keys = (System.Windows.Forms.Keys)vk;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                keys |= System.Windows.Forms.Keys.Control;
            }
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                keys |= System.Windows.Forms.Keys.Shift;
            }
            if (e.KeyModifiers.HasFlag(KeyModifiers.Alt))
            {
                keys |= System.Windows.Forms.Keys.Alt;
            }
            _hotkey = keys;
            Text = ToString(_hotkey);
            e.Handled = true;
        }

        private static string ToString(System.Windows.Forms.Keys keys)
        {
            if (keys == System.Windows.Forms.Keys.None)
            {
                return "（无）";
            }
            var str = "";
            if ((keys & System.Windows.Forms.Keys.Control) == System.Windows.Forms.Keys.Control)
            {
                str += "Ctrl+";
            }
            if ((keys & System.Windows.Forms.Keys.Shift) == System.Windows.Forms.Keys.Shift)
            {
                str += "Shift+";
            }
            if ((keys & System.Windows.Forms.Keys.Alt) == System.Windows.Forms.Keys.Alt)
            {
                str += "Alt+";
            }
            return str + (keys & System.Windows.Forms.Keys.KeyCode);
        }
    }

    /// <summary>单样式编辑窗（期 6 精简：样式名 + 按键提示；命令项编辑期后扩展）。</summary>
    public class StyleEditorWindow : Window
    {
        private readonly CStyle _style;
        private TextBox _txtName;

        public StyleEditorWindow(CStyle style)
        {
            _style = style;
            Title = "编辑样式";
            Width = 360;
            Height = 160;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var panel = new StackPanel { Margin = new Thickness(12), Spacing = 8 };
            _txtName = new TextBox { Text = style.StyleName, Width = 280 };
            panel.Children.Add(new TextBlock { Text = "样式名" });
            panel.Children.Add(_txtName);

            var items = new TextBlock
            {
                Text = "命令: " + string.Join(" → ", style.Items.Select(i => i.GetDisplayName())),
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.Gray,
            };
            panel.Children.Add(items);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
            var ok = new Button { Content = "确定", MinWidth = 70 };
            ok.Click += (s, e) => { _style.StyleName = _txtName.Text; Close(); };
            var cancel = new Button { Content = "取消", MinWidth = 70 };
            cancel.Click += (s, e) => Close();
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            panel.Children.Add(buttons);
            Content = panel;
        }
    }
}
