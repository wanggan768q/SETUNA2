using System;
using System.Drawing;
using SETUNA.Main.Cache;

namespace SETUNA.Main
{
    /// <summary>
    /// 贴图实体抽象：Core 的 Style 命令体系与 ScrapBook 操作的贴图表面。
    /// WinForms 宿主由 ScrapBase（Form）实现；Avalonia 宿主由 ScrapWindow 实现。
    /// 成员集合 = 原 CStyleItem/CStyle/ScrapBook 对 ScrapBase 的全部调用面，签名保持不变。
    /// </summary>
    public interface IScrap : IDisposable
    {
        // ===== 事件（委托定义见 ScrapDelegates.cs，签名对齐 ScrapBase 原事件） =====

        event ScrapEventHandler ScrapCloseEvent;
        event ScrapEventHandler ScrapCreateEvent;
        event ScrapEventHandler ScrapActiveEvent;
        event ScrapEventHandler ScrapInactiveEvent;
        event ScrapEventHandler ScrapInactiveEnterEvent;
        event ScrapEventHandler ScrapInactiveOutEvent;
        event ScrapSubMenuHandler ScrapSubMenuOpening;
        event ScrapEventHandler ScrapLocationChangedEvent;
        event ScrapEventHandler ScrapImageChangedEvent;
        event ScrapEventHandler ScrapStyleAppliedEvent;
        event ScrapEventHandler ScrapStyleRemovedEvent;
        event ScrapEventHandler ClotheslineHangingChanged;

        // ===== 事件注册（ScrapBook 调用） =====
        // 命名豁免：camelCase 系旧版 ScrapBase 事件注册方法名的迁移保真，
        // 改名会同时牵动 WinForms 宿主与反编译对照基线，维持原名。

        void addScrapStyleEvent(IScrapStyleListener listener);
        void addScrapMenuEvent(IScrapMenuListener listener);
        void addScrapLocationChangedEvent(IScrapLocationChangedListener listener);
        void addScrapImageChangedEvent(IScrapImageChangedListener listener);
        void addScrapStyleAppliedEvent(IScrapStyleAppliedListener listener);
        void addScrapStyleRemovedEvent(IScrapStyleRemovedListener listener);
        void OnScrapCreated();

        // ===== 窗口外观/行为（Style 命令操作面） =====

        /// <summary>激活态不透明度（0-1）。</summary>
        double ActiveOpacity { set; get; }
        double InactiveOpacity { set; get; }
        double RollOverOpacity { set; get; }
        int InactiveMargin { set; get; }
        int RollOverMargin { set; get; }
        int ActiveMargin { set; get; }
        bool SolidFrame { set; get; }
        object InterpolationMode { set; get; }
        bool TopMost { set; get; }
        bool ShowInTaskbar { set; get; }
        bool ControlBox { set; get; }
        /// <summary>窗体边框风格（int 对齐 WinForms FormBorderStyle）。</summary>
        int FormBorderStyle { set; get; }
        Color BackColor { set; get; }
        System.Windows.Forms.Padding Padding { set; get; }

        // ===== 几何 =====

        Point Location { set; get; }
        Point TargetLocation { set; get; }
        Size Size { set; get; }
        Size ClientSize { set; get; }
        int Left { set; get; }
        int Top { set; get; }
        int Width { get; }
        int Height { get; }
        int Scale { set; get; }
        void SetBounds(int x, int y, int width, int height, int specified);

        // ===== 生命周期 =====

        bool Visible { set; get; }
        void Show();
        void Hide();
        void Close();
        void Refresh();
        new bool Focus();
        void SuspendLayout();
        void ResumeLayout();

        // ===== 内容 =====

        /// <summary>贴图图像（可读写）。</summary>
        Image Image { set; get; }
        Image GetViewImage();
        string Name { set; get; }
        DateTime DateTime { set; get; }

        // ===== 关联 =====

        ScrapBook Manager { set; get; }
        CacheItem CacheItem { set; get; }
        int StyleID { get; }
        Point StyleClickPoint { get; }

        // ===== 样式与晾绳 =====

        void ApplyStyles(int styleID, SETUNA.Main.StyleItems.CStyleItem[] styleItems, Point clickpoint);
        void ApplyStylesFromCache(SETUNA.Main.Style.CStyle style, Point clickpoint, Action applyFinished);
        void RemoveStyle(Type styleItemType);
        void ScrapClose();
        bool GetVisibleFlag();
        bool IsClotheslineHanging { get; }
        void SetClotheslineHanging(bool hanging);
    }
}
