using System;
using Avalonia.Controls;
using Avalonia.Layout;
using SETUNA.Main.StyleItems;

namespace SETUNA.Avalonia.Views.Panels
{
    /// <summary>移动面板：上下左右方向 + 像素量（对齐 MoveStyleItemPanel）。</summary>
    public class MoveStyleItemPanel : StyleItemPanelBase
    {
        private CheckBox _chkUp, _chkDown, _chkLeft, _chkRight;
        private NumericUpDown _numUp, _numDown, _numLeft, _numRight;

        public MoveStyleItemPanel(CMoveStyleItem item) : base(item)
        {
        }

        protected override Control BuildEditArea()
        {
            var grid = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("Auto,Auto,Auto,Auto"), RowDefinitions = RowDefinitions.Parse("Auto,Auto"), };

            _chkUp = new CheckBox { Content = "上" };
            _numUp = NewNum((decimal)CMoveStyleItem.MAX_MOVE);
            _chkDown = new CheckBox { Content = "下" };
            _numDown = NewNum((decimal)CMoveStyleItem.MAX_MOVE);
            _chkLeft = new CheckBox { Content = "左" };
            _numLeft = NewNum((decimal)CMoveStyleItem.MAX_MOVE);
            _chkRight = new CheckBox { Content = "右" };
            _numRight = NewNum((decimal)CMoveStyleItem.MAX_MOVE);

            AttachExclusive(_chkUp, _chkDown);
            AttachExclusive(_chkDown, _chkUp);
            AttachExclusive(_chkLeft, _chkRight);
            AttachExclusive(_chkRight, _chkLeft);

            Grid.SetRow(_chkUp, 0); Grid.SetColumn(_chkUp, 0);
            Grid.SetRow(_numUp, 0); Grid.SetColumn(_numUp, 1);
            Grid.SetRow(_chkDown, 0); Grid.SetColumn(_chkDown, 2);
            Grid.SetRow(_numDown, 0); Grid.SetColumn(_numDown, 3);
            Grid.SetRow(_chkLeft, 1); Grid.SetColumn(_chkLeft, 0);
            Grid.SetRow(_numLeft, 1); Grid.SetColumn(_numLeft, 1);
            Grid.SetRow(_chkRight, 1); Grid.SetColumn(_chkRight, 2);
            Grid.SetRow(_numRight, 1); Grid.SetColumn(_numRight, 3);

            foreach (var c in new Control[] { _chkUp, _numUp, _chkDown, _numDown, _chkLeft, _numLeft, _chkRight, _numRight })
            {
                grid.Children.Add(c);
            }
            return grid;
        }

        private static NumericUpDown NewNum(decimal max)
        {
            return new NumericUpDown { Minimum = 0, Maximum = max, Value = 0, Width = 100 };
        }

        private static void AttachExclusive(CheckBox self, CheckBox other)
        {
            self.IsCheckedChanged += (s, e) =>
            {
                // 互斥：两者同真时关掉对方；都假时强制自己
                if (self.IsChecked == true && other.IsChecked == true)
                {
                    other.IsChecked = false;
                }
                else if (self.IsChecked != true && other.IsChecked != true)
                {
                    self.IsChecked = true;
                }
            };
        }

        protected override void SetStyleToForm(CStyleItem style)
        {
            var item = (CMoveStyleItem)style;
            _chkUp.IsChecked = true;
            _chkDown.IsChecked = false;
            _chkLeft.IsChecked = true;
            _chkRight.IsChecked = false;

            if (item.Top <= 0)
            {
                _numUp.Value = Math.Abs(item.Top);
            }
            else
            {
                _numDown.Value = Math.Abs(item.Top);
            }
            if (item.Left <= 0)
            {
                _numLeft.Value = Math.Abs(item.Left);
            }
            else
            {
                _numRight.Value = Math.Abs(item.Left);
            }
        }

        protected override CStyleItem GetStyleFromForm()
        {
            var item = new CMoveStyleItem
            {
                Top = _chkUp.IsChecked == true ? -(int)_numUp.Value : (int)_numDown.Value,
                Left = _chkLeft.IsChecked == true ? -(int)_numLeft.Value : (int)_numRight.Value,
            };
            return item;
        }
    }
}
