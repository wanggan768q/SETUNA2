using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace SETUNA.Main.StyleItems
{

    // Token: 0x0200005E RID: 94
    public class CImageJpegStyleItem : CImageStyleItem
    {
        /// <summary>宿主面板键：宿主映射到对应编辑面板。</summary>
        public const string ImageJpegStyleItemPanelKey = "ImageJpegStyleItemPanel";
        // Token: 0x06000346 RID: 838 RVA: 0x00015751 File Offset: 0x00013951
        public CImageJpegStyleItem()
        {
            Quality = 100;
            ShowPreview = true;
        }

        // Token: 0x06000347 RID: 839 RVA: 0x00015768 File Offset: 0x00013968
        protected override EncoderParameter[] GetEncoderParams(ref IScrap scrap)
        {
            var list = new List<EncoderParameter>();
            if (ShowPreview)
            {
                using var preview = (SETUNA.Main.Style.StyleUiFactory.PreviewDialogFactory?.Invoke(this,
                    HaveMargin ? scrap.GetViewImage() : (Image)(Bitmap)scrap.Image.Clone())) as SETUNA.Main.Style.StyleUiFactory.IStyleItemEditDialog;
                if (preview == null || preview.ShowEditDialog() != SETUNA.Main.Style.StyleUiFactory.DialogResultOK)
                {
                    return null;
                }
                var cimageJpegStyleItem = (CImageJpegStyleItem)preview.EditedStyleItem;
                list.Add(new EncoderParameter(Encoder.Quality, cimageJpegStyleItem.Quality));
            }
            else
            {
                list.Add(new EncoderParameter(Encoder.Quality, Quality));
            }
            return list.ToArray();
        }

        // Token: 0x06000348 RID: 840 RVA: 0x0001580E File Offset: 0x00013A0E
        protected override ImageFormat GetImageFormat()
        {
            return ImageFormat.Jpeg;
        }

        // Token: 0x06000349 RID: 841 RVA: 0x00015815 File Offset: 0x00013A15
        public override string GetName()
        {
            return "ImageJpeg";
        }

        // Token: 0x0600034A RID: 842 RVA: 0x0001581C File Offset: 0x00013A1C
        public override string GetDisplayName()
        {
            return "保存为JPEG格式";
        }

        // Token: 0x0600034B RID: 843 RVA: 0x00015823 File Offset: 0x00013A23
        public override string GetDescription()
        {
            return "用JPEG格式的图像保存参考图。";
        }

        // Token: 0x0600034C RID: 844 RVA: 0x0001582A File Offset: 0x00013A2A
        protected override object GetToolBoxForm() => ImageJpegStyleItemPanelKey;

        // Token: 0x0600034D RID: 845 RVA: 0x00015834 File Offset: 0x00013A34
        protected override void SetTunedStyleItem(CStyleItem newOwn)
        {
            var cimageJpegStyleItem = (CImageJpegStyleItem)newOwn;
            SaveFolder = cimageJpegStyleItem.SaveFolder;
            Quality = cimageJpegStyleItem.Quality;
            ShowPreview = cimageJpegStyleItem.ShowPreview;
            HaveMargin = cimageJpegStyleItem.HaveMargin;
            CopyPath = cimageJpegStyleItem.CopyPath;
            FileNameType = cimageJpegStyleItem.FileNameType;
            DupliType = cimageJpegStyleItem.DupliType;
            FileName = cimageJpegStyleItem.FileName;
        }

        // Token: 0x1700007E RID: 126
        // (get) Token: 0x0600034E RID: 846 RVA: 0x000158A8 File Offset: 0x00013AA8
        protected override string Extension => "jpg";

        // Token: 0x1700007F RID: 127
        // (get) Token: 0x0600034F RID: 847 RVA: 0x000158AF File Offset: 0x00013AAF
        protected override string FileFilter => "Jpeg格式 (.jpg)|*.jpg|所有文件 (*.*)|*.*";


        // Token: 0x040001D9 RID: 473
        public int Quality;

        // Token: 0x040001DA RID: 474
        public bool ShowPreview;
    }
}
