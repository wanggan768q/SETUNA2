
namespace SETUNA.Main.Style
{
    // Token: 0x02000059 RID: 89
    public interface IStyleMenu
    {
        // Token: 0x06000326 RID: 806
        /// <summary>返回宿主菜单项（WinForms=ToolStripItem；Avalonia=NativeMenuItem 等）。</summary>
        object GetToolStrip();
    }
}
