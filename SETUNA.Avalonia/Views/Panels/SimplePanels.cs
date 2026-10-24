using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using Avalonia.Controls;
using DrawingColor = System.Drawing.Color;
using Avalonia.Layout;
using SETUNA.Main.StyleItems;

namespace SETUNA.Avalonia.Views.Panels
{
    /// <summary>缩放面板：固定/相对 + 插值模式（对齐 ScaleStyleItemPanel）。</summary>
    public class ScaleStyleItemPanel : StyleItemPanelBase
    {
        private RadioButton _rdoFixed, _rdoIncrement;
        private NumericUpDown _numFixed, _numRelative;
        private ComboBox _cmbInterpolation;
        private readonly List<(string Text, InterpolationMode Mode)> _modes = new()
        {
            ("不要更改", InterpolationMode.Invalid),
            ("最近邻法", InterpolationMode.NearestNeighbor),
            ("标准", InterpolationMode.High),
            ("双线性", InterpolationMode.HighQualityBilinear),
            ("两次立方", InterpolationMode.HighQualityBicubic),
        };

        public ScaleStyleItemPanel(CScaleStyleItem item) : base(item)
        {
        }

        protected override Control BuildEditArea()
        {
            var panel = new StackPanel { Spacing = 8 };

            _rdoFixed = new RadioButton { Content = "固定缩放（10-200%）", GroupName = "scaleType" };
            _numFixed = new NumericUpDown { Minimum = 10, Maximum = 200, Value = 100, Width = 120 };
            _rdoIncrement = new RadioButton { Content = "相对缩放（-190-190%）", GroupName = "scaleType" };
            _numRelative = new NumericUpDown { Minimum = -190, Maximum = 190, Value = 0, Width = 120 };

            _rdoFixed.IsCheckedChanged += (s, e) => UpdateEnabled();
            _rdoIncrement.IsCheckedChanged += (s, e) => UpdateEnabled();

            _cmbInterpolation = new ComboBox { Width = 160 };
            foreach (var (text, _) in _modes)
            {
                _cmbInterpolation.Items.Add(text);
            }

            panel.Children.Add(_rdoFixed);
            panel.Children.Add(Row("倍率", _numFixed));
            panel.Children.Add(_rdoIncrement);
            panel.Children.Add(Row("增量", _numRelative));
            panel.Children.Add(Row("插值", _cmbInterpolation));
            return panel;
        }

        private void UpdateEnabled()
        {
            var isFixed = _rdoFixed.IsChecked == true;
            _numFixed.IsEnabled = isFixed;
            _numRelative.IsEnabled = !isFixed;
        }

        protected override void SetStyleToForm(CStyleItem style)
        {
            var item = (CScaleStyleItem)style;
            _rdoFixed.IsChecked = item.SetType == CScaleStyleItem.ScaleSetType.Fixed;
            _rdoIncrement.IsChecked = item.SetType == CScaleStyleItem.ScaleSetType.Increment;
            UpdateEnabled();
            if (item.SetType == CScaleStyleItem.ScaleSetType.Fixed)
            {
                _numFixed.Value = item.Value;
            }
            else
            {
                _numRelative.Value = item.Value;
            }

            _cmbInterpolation.SelectedIndex = 0;
            for (var i = 0; i < _modes.Count; i++)
            {
                if (_modes[i].Mode == item.InterpolationMode)
                {
                    _cmbInterpolation.SelectedIndex = i;
                }
            }
        }

        protected override CStyleItem GetStyleFromForm()
        {
            var mode = _cmbInterpolation.SelectedIndex is >= 0 and var idx ? _modes[idx].Mode : InterpolationMode.Invalid;
            return new CScaleStyleItem
            {
                Value = _rdoFixed.IsChecked == true ? (int)_numFixed.Value : (int)_numRelative.Value,
                SetType = _rdoFixed.IsChecked == true ? CScaleStyleItem.ScaleSetType.Fixed : CScaleStyleItem.ScaleSetType.Increment,
                InterpolationMode = mode,
            };
        }
    }

    /// <summary>透明度面板：绝对/相对（对齐 OpacityStyleItemPanel；预览截图省略，期 6 可补）。</summary>
    public class OpacityStyleItemPanel : StyleItemPanelBase
    {
        private RadioButton _rdoFixed, _rdoIncrement;
        private NumericUpDown _numFixed, _numRelative;

        public OpacityStyleItemPanel(COpacityStyleItem item) : base(item)
        {
        }

        protected override Control BuildEditArea()
        {
            var panel = new StackPanel { Spacing = 8 };
            _rdoFixed = new RadioButton { Content = "固定不透明度（1-100%）", GroupName = "opType" };
            _numFixed = new NumericUpDown { Minimum = 1, Maximum = 100, Value = 100, Width = 120 };
            _rdoIncrement = new RadioButton { Content = "相对不透明度（-99-99%）", GroupName = "opType" };
            _numRelative = new NumericUpDown { Minimum = -99, Maximum = 99, Value = 0, Width = 120 };
            _rdoFixed.IsCheckedChanged += (s, e) => UpdateEnabled();
            _rdoIncrement.IsCheckedChanged += (s, e) => UpdateEnabled();
            panel.Children.Add(_rdoFixed);
            panel.Children.Add(Row("值", _numFixed));
            panel.Children.Add(_rdoIncrement);
            panel.Children.Add(Row("值", _numRelative));
            return panel;
        }

        private void UpdateEnabled()
        {
            var isFixed = _rdoFixed.IsChecked == true;
            _numFixed.IsEnabled = isFixed;
            _numRelative.IsEnabled = !isFixed;
        }

        protected override void SetStyleToForm(CStyleItem style)
        {
            var item = (COpacityStyleItem)style;
            _rdoFixed.IsChecked = item.Absolute;
            _rdoIncrement.IsChecked = !item.Absolute;
            UpdateEnabled();
            if (item.Absolute)
            {
                _numFixed.Value = item.Opacity;
            }
            else
            {
                _numRelative.Value = item.Opacity;
            }
        }

        protected override CStyleItem GetStyleFromForm()
        {
            return new COpacityStyleItem
            {
                Opacity = _rdoFixed.IsChecked == true ? (int)_numFixed.Value : (int)_numRelative.Value,
                Absolute = _rdoFixed.IsChecked == true,
            };
        }
    }

    /// <summary>旋转面板：角度 + 翻转（对齐 RotateStyleItemPanel；预览省略）。</summary>
    public class RotateStyleItemPanel : StyleItemPanelBase
    {
        private RadioButton _rdoNone, _rdoRight90, _rdo180, _rdoLeft90;
        private CheckBox _chkVertical, _chkHorizon;

        public RotateStyleItemPanel(CRotateStyleItem item) : base(item)
        {
        }

        protected override Control BuildEditArea()
        {
            var panel = new StackPanel { Spacing = 8 };
            _rdoNone = new RadioButton { Content = "不旋转", GroupName = "rot" };
            _rdoRight90 = new RadioButton { Content = "右转 90°", GroupName = "rot" };
            _rdo180 = new RadioButton { Content = "旋转 180°", GroupName = "rot" };
            _rdoLeft90 = new RadioButton { Content = "左转 90°", GroupName = "rot" };
            _chkVertical = new CheckBox { Content = "垂直翻转" };
            _chkHorizon = new CheckBox { Content = "水平翻转" };
            panel.Children.Add(_rdoNone);
            panel.Children.Add(_rdoRight90);
            panel.Children.Add(_rdo180);
            panel.Children.Add(_rdoLeft90);
            panel.Children.Add(_chkVertical);
            panel.Children.Add(_chkHorizon);
            return panel;
        }

        protected override void SetStyleToForm(CStyleItem style)
        {
            var item = (CRotateStyleItem)style;
            _rdoNone.IsChecked = item.Rotate == 0;
            _rdoRight90.IsChecked = item.Rotate == 90;
            _rdo180.IsChecked = item.Rotate == 180;
            _rdoLeft90.IsChecked = item.Rotate == 270;
            _chkVertical.IsChecked = item.VerticalReflection;
            _chkHorizon.IsChecked = item.HorizonReflection;
        }

        protected override CStyleItem GetStyleFromForm()
        {
            return new CRotateStyleItem
            {
                Rotate = _rdoRight90.IsChecked == true ? 90 : _rdo180.IsChecked == true ? 180 : _rdoLeft90.IsChecked == true ? 270 : 0,
                VerticalReflection = _chkVertical.IsChecked == true,
                HorizonReflection = _chkHorizon.IsChecked == true,
            };
        }
    }

    /// <summary>复制面板：来源选择（对齐 CopyStyleItemPanel）。</summary>
    public class CopyStyleItemPanel : StyleItemPanelBase
    {
        private CheckBox _chkWindow;

        public CopyStyleItemPanel(CCopyStyleItem item) : base(item)
        {
        }

        protected override Control BuildEditArea()
        {
            _chkWindow = new CheckBox { Content = "复制当前显示（含边框）" };
            return _chkWindow;
        }

        protected override void SetStyleToForm(CStyleItem style)
        {
            var item = (CCopyStyleItem)style;
            _chkWindow.IsChecked = !item.CopyFromSource;
        }

        protected override CStyleItem GetStyleFromForm()
        {
            return new CCopyStyleItem { CopyFromSource = _chkWindow.IsChecked != true };
        }
    }

    /// <summary>定时器面板（对齐 TimerStyleItemPanel）。</summary>
    public class TimerStyleItemPanel : StyleItemPanelBase
    {
        private NumericUpDown _numInterval;

        public TimerStyleItemPanel(CTimerStyleItem item) : base(item)
        {
        }

        protected override Control BuildEditArea()
        {
            _numInterval = new NumericUpDown
            {
                Minimum = CTimerStyleItem.MIN_INTERVAL,
                Maximum = CTimerStyleItem.MAX_INTERVAL,
                Width = 140,
            };
            return Row("间隔(ms)", _numInterval);
        }

        protected override void SetStyleToForm(CStyleItem style)
        {
            _numInterval.Value = ((CTimerStyleItem)style).Interval;
        }

        protected override CStyleItem GetStyleFromForm()
        {
            return new CTimerStyleItem { Interval = (uint)_numInterval.Value };
        }
    }

    /// <summary>窗口化面板（对齐 WindowStyleItemPanel）。</summary>
    public class WindowStyleItemPanel : StyleItemPanelBase
    {
        private RadioButton _rdoScrap, _rdoWindow;

        public WindowStyleItemPanel(CWindowStyleItem item) : base(item)
        {
        }

        protected override Control BuildEditArea()
        {
            var panel = new StackPanel { Spacing = 8 };
            _rdoScrap = new RadioButton { Content = "贴图模式（无边框置顶）", GroupName = "win" };
            _rdoWindow = new RadioButton { Content = "窗口模式（可拖动系统边框）", GroupName = "win" };
            panel.Children.Add(_rdoScrap);
            panel.Children.Add(_rdoWindow);
            return panel;
        }

        protected override void SetStyleToForm(CStyleItem style)
        {
            var item = (CWindowStyleItem)style;
            _rdoScrap.IsChecked = !item.IsWindow;
            _rdoWindow.IsChecked = item.IsWindow;
        }

        protected override CStyleItem GetStyleFromForm()
        {
            return new CWindowStyleItem { IsWindow = _rdoWindow.IsChecked == true };
        }
    }

    /// <summary>边框面板（对齐 MarginStyleItemPanel；预览省略）。</summary>
    public class MarginStyleItemPanel : StyleItemPanelBase
    {
        private NumericUpDown _numMargin;
        private RadioButton _rdo3D, _rdoSolid, _rdoWindow;
        private global::Avalonia.Controls.Shapes.Rectangle _colorBox;
        private DrawingColor _marginColor;
        private CheckBox _chkTopMost;

        public MarginStyleItemPanel(CMarginStyleItem item) : base(item)
        {
        }

        protected override Control BuildEditArea()
        {
            var panel = new StackPanel { Spacing = 8 };
            _numMargin = new NumericUpDown { Minimum = 0, Maximum = 200, Width = 120 };
            _rdo3D = new RadioButton { Content = "3D 边框", GroupName = "border" };
            _rdoSolid = new RadioButton { Content = "实线边框", GroupName = "border" };
            _rdoWindow = new RadioButton { Content = "窗口模式", GroupName = "border" };
            _colorBox = new global::Avalonia.Controls.Shapes.Rectangle { Width = 40, Height = 24, Stroke = global::Avalonia.Media.Brushes.Gray };
            var colorBtn = new Button { Content = "选择颜色" };
            colorBtn.Click += async (s, e) =>
            {
                // Avalonia 无内置 ColorPicker 对话框；简化为预设循环（ponytail：真拾色器期 6 换社区包）
                var palette = new[] { DrawingColor.Black, DrawingColor.DarkBlue, DrawingColor.Orange, DrawingColor.White, DrawingColor.Gray };
                _marginColor = palette[(Array.IndexOf(palette, _marginColor) + 1) % palette.Length];
                ApplyColor();
            };
            _chkTopMost = new CheckBox { Content = "保持置顶" };
            panel.Children.Add(Row("边距", _numMargin));
            panel.Children.Add(_rdo3D);
            panel.Children.Add(_rdoSolid);
            panel.Children.Add(_rdoWindow);
            panel.Children.Add(Row("颜色", new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { _colorBox, colorBtn } }));
            panel.Children.Add(_chkTopMost);
            return panel;
        }

        private void ApplyColor()
        {
            _colorBox.Fill = new global::Avalonia.Media.SolidColorBrush(
                global::Avalonia.Media.Color.FromRgb(_marginColor.R, _marginColor.G, _marginColor.B));
        }

        protected override void SetStyleToForm(CStyleItem style)
        {
            var item = (CMarginStyleItem)style;
            _numMargin.Value = item.MarginSize;
            _marginColor = item.GetMarginColor();
            ApplyColor();
            _chkTopMost.IsChecked = item.TopMost;
            _rdo3D.IsChecked = item.BorderStyle == 0;
            _rdoSolid.IsChecked = item.BorderStyle == 1;
            _rdoWindow.IsChecked = item.BorderStyle == 2;
        }

        protected override CStyleItem GetStyleFromForm()
        {
            var item = new CMarginStyleItem
            {
                MarginSize = (int)_numMargin.Value,
                TopMost = _chkTopMost.IsChecked == true,
                BorderStyle = _rdoWindow.IsChecked == true ? 2 : _rdoSolid.IsChecked == true ? 1 : 0,
            };
            item.SetMarginColor(_marginColor);
            return item;
        }
    }
}
