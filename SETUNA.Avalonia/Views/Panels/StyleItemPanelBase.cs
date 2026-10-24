using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using SETUNA.Main.Style;
using SETUNA.Main.StyleItems;

namespace SETUNA.Avalonia.Views.Panels
{
    /// <summary>
    /// 样式编辑对话框基类（对应旧 ToolBoxForm）：SetStyleToForm/GetStyleFromForm 契约一致，
    /// OK/Cancel 按钮统一。每个面板一个子类，纯代码布局（对应原 Designer）。
    /// </summary>
    public abstract class StyleItemPanelBase : Window
    {
        protected CStyleItem _styleItem;
        private object _editedStyleItem;

        public object EditedStyleItem => _editedStyleItem;

        protected StyleItemPanelBase(CStyleItem styleItem)
        {
            _styleItem = styleItem;
            Title = styleItem?.GetDisplayName() ?? "SETUNA";
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            MinWidth = 320;

            var root = new StackPanel { Margin = new Thickness(12), Spacing = 10 };
            var editArea = BuildEditArea();
            if (editArea != null)
            {
                root.Children.Add(editArea);
            }
            root.Children.Add(BuildButtonRow());
            Content = new ScrollViewer { Content = root, MaxHeight = 560 };

            if (styleItem != null)
            {
                SetStyleToForm(styleItem);
            }
        }

        /// <summary>面板主体控件（子类构建）。</summary>
        protected abstract Control BuildEditArea();

        /// <summary>从数据填充控件（对齐旧 SetStyleToForm）。</summary>
        protected abstract void SetStyleToForm(CStyleItem style);

        /// <summary>从控件读取新数据（对齐旧 GetStyleFromForm）。返回 null 表示校验失败不关闭。</summary>
        protected abstract CStyleItem GetStyleFromForm();

        /// <summary>OK 前校验钩子（对齐旧 OKCheck）；cancel=true 则不关闭。</summary>
        protected virtual void OKCheck(ref bool cancel) { }

        private Control BuildButtonRow()
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 8,
            };

            var ok = new Button { Content = "确定", MinWidth = 80 };
            ok.Click += (s, e) =>
            {
                var cancel = false;
                OKCheck(ref cancel);
                if (cancel)
                {
                    return;
                }
                _editedStyleItem = GetStyleFromForm();
                if (_editedStyleItem != null)
                {
                    Close(1);
                }
            };

            var cancelBtn = new Button { Content = "取消", MinWidth = 80 };
            cancelBtn.Click += (s, e) => Close(2);

            panel.Children.Add(ok);
            panel.Children.Add(cancelBtn);
            return panel;
        }

        // ===== 共用小部件 =====

        protected static StackPanel Row(string label, Control control)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
            panel.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, MinWidth = 64 });
            panel.Children.Add(control);
            return panel;
        }

        protected static TextBlock Header(string text)
        {
            return new TextBlock { Text = text, FontWeight = global::Avalonia.Media.FontWeight.Bold, Margin = new Thickness(0, 4, 0, 0) };
        }
    }
}
