namespace SETUNA.Main.Style
{
    // Token: 0x02000049 RID: 73
    public class CDustScrapStyle : CPreStyle
    {
        // Token: 0x060002BE RID: 702 RVA: 0x0000F520 File Offset: 0x0000D720
        public CDustScrapStyle()
        {
            _styleid = -12;
            _stylename = "关闭所有截图";
        }

        // Token: 0x060002BF RID: 703 RVA: 0x0000F53B File Offset: 0x0000D73B
        public override void Apply(ref IScrap scrap)
        {
            // 对话框异步泵（UI 线程不能同步等待）：确认后回调关闭
            var manager = scrap.Manager;
            StyleUiFactory.MessageBox("是否关闭所有截图", "提示", 0x4)
                .ContinueWith(t =>
                {
                    if (t.Result == StyleUiFactory.DialogResultYes)
                    {
                        manager?.CloseAllScrap();
                    }
                }, System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
        }
    }
}
