using System;
using Avalonia.Controls;
using DrawingColor = System.Drawing.Color;
using Avalonia.Layout;
using SETUNA.Main.StyleItems;

namespace SETUNA.Avalonia.Views.Panels
{
    /// <summary>
    /// 保存类面板基（对齐 ImageBmp/Jpeg/PngStyleItemPanel 的公共结构）：
    /// 保存目录 / 文件名模式 / 重名策略 / 含边框 / 复制路径，Jpeg/Png 追加质量。
    /// </summary>
    public abstract class SaveImagePanelBase : StyleItemPanelBase
    {
        protected TextBox _txtFolder;
        protected TextBox _txtName;
        protected RadioButton _rdoSaveAs, _rdoScrapName, _rdoName;
        protected ComboBox _cmbDupli;
        protected CheckBox _chkWindow, _chkCopy;

        protected SaveImagePanelBase(CImageStyleItem item) : base(item)
        {
        }

        protected override Control BuildEditArea()
        {
            var panel = new StackPanel { Spacing = 8 };

            _txtFolder = new TextBox { Width = 260 };
            var browse = new Button { Content = "浏览…" };
            browse.Click += async (s, e) =>
            {
                var dialogs = TopLevel.GetTopLevel(this);
                var folders = new global::Avalonia.Platform.Storage.FolderPickerOpenOptions { Title = "选择保存目录" };
                var result = await dialogs.StorageProvider.OpenFolderPickerAsync(folders);
                if (result is { Count: > 0 })
                {
                    _txtFolder.Text = Uri.UnescapeDataString(result[0].Path.AbsolutePath);
                }
            };

            _rdoSaveAs = new RadioButton { Content = "每次询问文件名（另存为）", GroupName = "fname" };
            _rdoScrapName = new RadioButton { Content = "使用截图名（重名按策略）", GroupName = "fname" };
            _rdoName = new RadioButton { Content = "使用固定文件名", GroupName = "fname" };
            _txtName = new TextBox { Width = 200 };

            _cmbDupli = new ComboBox { Width = 160 };
            _cmbDupli.Items.Add("覆盖");
            _cmbDupli.Items.Add("自动加序号");
            _cmbDupli.Items.Add("每次询问");
            _cmbDupli.SelectedIndex = 0;

            _chkWindow = new CheckBox { Content = "包含边框（窗口模式）" };
            _chkCopy = new CheckBox { Content = "保存后复制路径到剪贴板" };

            panel.Children.Add(Header("保存目录"));
            panel.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children = { _txtFolder, browse },
            });
            panel.Children.Add(Header("文件名"));
            panel.Children.Add(_rdoSaveAs);
            panel.Children.Add(_rdoScrapName);
            panel.Children.Add(_rdoName);
            panel.Children.Add(Row("名称", _txtName));
            panel.Children.Add(Row("重名", _cmbDupli));
            panel.Children.Add(Header("选项"));
            panel.Children.Add(_chkWindow);
            panel.Children.Add(_chkCopy);

            var extra = BuildExtraArea();
            if (extra != null)
            {
                panel.Children.Add(extra);
            }
            return panel;
        }

        /// <summary>Jpeg/Png 的追加区（质量滑条等）。</summary>
        protected virtual Control BuildExtraArea() => null;

        protected (string Folder, bool HaveMargin, bool CopyPath, CImageStyleItem.EnumFileName NameType, CImageStyleItem.EnumDupliType Dupli, string Name) ReadCommon()
        {
            var nameType = _rdoSaveAs.IsChecked == true ? CImageStyleItem.EnumFileName.SaveAs
                : _rdoScrapName.IsChecked == true ? CImageStyleItem.EnumFileName.ScrapName
                : CImageStyleItem.EnumFileName.UseName;
            var dupli = _cmbDupli.SelectedIndex switch
            {
                1 => CImageStyleItem.EnumDupliType.Sequence,
                2 => CImageStyleItem.EnumDupliType.Select,
                _ => CImageStyleItem.EnumDupliType.OverWrite,
            };
            return (_txtFolder.Text ?? string.Empty, _chkWindow.IsChecked == true, _chkCopy.IsChecked == true, nameType, dupli, _txtName.Text ?? string.Empty);
        }

        protected void WriteCommon(CImageStyleItem item)
        {
            _txtFolder.Text = item.SaveFolder;
            _chkWindow.IsChecked = item.HaveMargin;
            _chkCopy.IsChecked = item.CopyPath;
            _rdoSaveAs.IsChecked = item.FileNameType == CImageStyleItem.EnumFileName.SaveAs;
            _rdoScrapName.IsChecked = item.FileNameType == CImageStyleItem.EnumFileName.ScrapName;
            _rdoName.IsChecked = item.FileNameType == CImageStyleItem.EnumFileName.UseName;
            _cmbDupli.SelectedIndex = (int)item.DupliType;
            _txtName.Text = item.FileName;
        }
    }

    public class ImageBmpStyleItemPanel : SaveImagePanelBase
    {
        public ImageBmpStyleItemPanel(CImageBmpStyleItem item) : base(item)
        {
        }

        protected override void SetStyleToForm(CStyleItem style) => WriteCommon((CImageStyleItem)style);

        protected override CStyleItem GetStyleFromForm()
        {
            var (folder, margin, copy, nameType, dupli, name) = ReadCommon();
            return new CImageBmpStyleItem
            {
                SaveFolder = folder,
                HaveMargin = margin,
                CopyPath = copy,
                FileNameType = nameType,
                DupliType = dupli,
                FileName = name,
            };
        }
    }

    public class ImagePngStyleItemPanel : SaveImagePanelBase
    {
        public ImagePngStyleItemPanel(CImagePngStyleItem item) : base(item)
        {
        }

        protected override void SetStyleToForm(CStyleItem style) => WriteCommon((CImageStyleItem)style);

        protected override CStyleItem GetStyleFromForm()
        {
            var (folder, margin, copy, nameType, dupli, name) = ReadCommon();
            return new CImagePngStyleItem
            {
                SaveFolder = folder,
                HaveMargin = margin,
                CopyPath = copy,
                FileNameType = nameType,
                DupliType = dupli,
                FileName = name,
            };
        }
    }

    public class ImageJpegStyleItemPanel : SaveImagePanelBase
    {
        private Slider _barQuality;
        private TextBlock _lblQuality;
        private CheckBox _chkShowPreview;
        private CImageJpegStyleItem _source;

        public ImageJpegStyleItemPanel(CImageJpegStyleItem item) : base(item)
        {
            _source = item;
        }

        protected override Control BuildExtraArea()
        {
            var panel = new StackPanel { Spacing = 8 };
            _barQuality = new Slider { Minimum = 1, Maximum = 100, Value = 100, Width = 200 };
            _lblQuality = new TextBlock { Text = "100" };
            _barQuality.PropertyChanged += (s, e) =>
            {
                if (e.Property == global::Avalonia.Controls.Slider.ValueProperty)
                {
                    _lblQuality.Text = ((int)_barQuality.Value).ToString();
                }
            };
            _chkShowPreview = new CheckBox { Content = "保存前预览质量" };
            panel.Children.Add(Row("质量", new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { _barQuality, _lblQuality } }));
            panel.Children.Add(_chkShowPreview);
            return panel;
        }

        protected override void SetStyleToForm(CStyleItem style)
        {
            var item = (CImageJpegStyleItem)style;
            WriteCommon(item);
            _barQuality.Value = item.Quality;
            _chkShowPreview.IsChecked = item.ShowPreview;
        }

        protected override CStyleItem GetStyleFromForm()
        {
            var (folder, margin, copy, nameType, dupli, name) = ReadCommon();
            return new CImageJpegStyleItem
            {
                SaveFolder = folder,
                HaveMargin = margin,
                CopyPath = copy,
                FileNameType = nameType,
                DupliType = dupli,
                FileName = name,
                Quality = (int)_barQuality.Value,
                ShowPreview = _chkShowPreview.IsChecked == true,
            };
        }
    }

    /// <summary>收缩面板（对齐 CompactStyleItemPanel；预览省略）。</summary>
    public class CompactStyleItemPanel : StyleItemPanelBase
    {
        private NumericUpDown _numOpacity;
        private global::Avalonia.Controls.Shapes.Rectangle _colorBox;
        private RadioButton _rdoSolid, _rdoDashed;
        private DrawingColor _lineColor = DrawingColor.Blue;

        public CompactStyleItemPanel(CCompactStyleItem item) : base(item)
        {
        }

        protected override Control BuildEditArea()
        {
            var panel = new StackPanel { Spacing = 8 };
            _numOpacity = new NumericUpDown { Minimum = CCompactStyleItem.OpacityMinValue, Maximum = CCompactStyleItem.OpacityMaxValue, Width = 120 };
            _colorBox = new global::Avalonia.Controls.Shapes.Rectangle { Width = 40, Height = 24, Stroke = global::Avalonia.Media.Brushes.Gray };
            var colorBtn = new Button { Content = "选择颜色" };
            colorBtn.Click += (s, e) =>
            {
                var palette = new[] { DrawingColor.Blue, DrawingColor.Red, DrawingColor.Black, DrawingColor.White, DrawingColor.Gray };
                _lineColor = palette[(Array.IndexOf(palette, _lineColor) + 1) % palette.Length];
                ApplyColor();
            };
            _rdoSolid = new RadioButton { Content = "实线", GroupName = "line" };
            _rdoDashed = new RadioButton { Content = "虚线", GroupName = "line" };
            panel.Children.Add(Row("不透明度", _numOpacity));
            panel.Children.Add(Row("线色", new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { _colorBox, colorBtn } }));
            panel.Children.Add(_rdoSolid);
            panel.Children.Add(_rdoDashed);
            return panel;
        }

        private void ApplyColor()
        {
            _colorBox.Fill = new global::Avalonia.Media.SolidColorBrush(
                global::Avalonia.Media.Color.FromRgb(_lineColor.R, _lineColor.G, _lineColor.B));
        }

        protected override void SetStyleToForm(CStyleItem style)
        {
            var item = (CCompactStyleItem)style;
            _numOpacity.Value = item.Opacity;
            _lineColor = DrawingColor.FromArgb(item.LineColor);
            ApplyColor();
            _rdoSolid.IsChecked = item.SoldLine;
            _rdoDashed.IsChecked = !item.SoldLine;
        }

        protected override CStyleItem GetStyleFromForm()
        {
            return new CCompactStyleItem
            {
                Opacity = (byte)_numOpacity.Value,
                LineColor = _lineColor.ToArgb(),
                SoldLine = _rdoSolid.IsChecked == true,
            };
        }
    }
}
