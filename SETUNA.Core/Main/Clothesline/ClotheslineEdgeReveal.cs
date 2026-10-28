using System;
using System.Drawing;

namespace SETUNA.Main.Clothesline
{
    /// <summary>边缘呼出轮询的单次决策结果。</summary>
    public enum EdgeRevealAction
    {
        // 不动
        None,
        // 呼出（淡入）
        Reveal,
        // 收起（淡出）
        Retract
    }

    /// <summary>
    /// 边缘呼出决策（纯函数，供测试）：呼出=不可见+边缘命中+无全屏遮挡；
    /// 收起=可见+手动宽限外+远离绳与边缘持续超过延迟；捕获暂停一律不动。
    /// </summary>
    public static class ClotheslineEdgeReveal
    {
        /// <summary>光标远离后多久收起。</summary>
        public static readonly TimeSpan RetractDelay = TimeSpan.FromMilliseconds(300);

        /// <summary>手动呼出（托盘/热键）后多久内轮询不收起。</summary>
        public static readonly TimeSpan ManualGrace = TimeSpan.FromSeconds(3);

        public static EdgeRevealAction DecideEdgeReveal(
            bool ropeVisible, bool capturePaused, bool edgeZoneHit,
            bool pointerNearRope, bool fullscreenCovers, TimeSpan awayDuration, bool withinManualGrace)
        {
            if (capturePaused)
            {
                return EdgeRevealAction.None;
            }
            if (!ropeVisible)
            {
                return edgeZoneHit && !fullscreenCovers ? EdgeRevealAction.Reveal : EdgeRevealAction.None;
            }
            if (withinManualGrace)
            {
                return EdgeRevealAction.None;
            }
            if (edgeZoneHit || pointerNearRope || fullscreenCovers)
            {
                return EdgeRevealAction.None;
            }
            return awayDuration >= RetractDelay ? EdgeRevealAction.Retract : EdgeRevealAction.None;
        }
    }

    /// <summary>边缘呼出几何判定（纯函数，供测试）。</summary>
    public static class ClotheslineEdgeGeometry
    {
        /// <summary>呼出触发带宽度（物理像素）。</summary>
        public const int EdgeZonePx = 4;

        /// <summary>横绳：光标在绳所在屏顶部触发带内且 X 在工作区范围；竖绳：右缘触发带。对侧边界防止相邻堆叠显示器误命中。</summary>
        public static bool IsEdgeZoneHit(Point cursor, Rectangle workArea, bool vertical)
        {
            if (vertical)
            {
                return cursor.X >= workArea.Right - EdgeZonePx && cursor.X <= workArea.Right
                    && cursor.Y >= workArea.Top && cursor.Y <= workArea.Bottom;
            }
            return cursor.Y >= workArea.Top && cursor.Y <= workArea.Top + EdgeZonePx
                && cursor.X >= workArea.Left && cursor.X <= workArea.Right;
        }

        /// <summary>前台窗口完全覆盖绳所在屏工作区（全屏应用）判定。</summary>
        public static bool CoversWorkArea(Rectangle windowBounds, Rectangle workArea)
        {
            return !windowBounds.IsEmpty && windowBounds.Contains(workArea);
        }
    }
}
