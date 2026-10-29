# 晾绳效果升级实施计划（PicRope 风格）

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 给 Avalonia 版晾绳加三类 PicRope 风格效果：图片摆动动画、木夹/拧绳视觉美化、屏幕边缘呼出收起（可配置）。

**Architecture:** 纯函数（摆动数学、边缘呼出决策）放 `SETUNA.Core/Main/Clothesline/`（测试项目只引用 Core，对齐 `ClotheslineCacheMigration` 先例）；视觉与动画全部在 `ClotheslineWindow.Render` + DispatcherTimer 内实现；边缘轮询在 `AvaloniaClotheslineController`。交互语义零改动。

**Tech Stack:** .NET 9 / Avalonia 11 / System.Drawing（几何）/ MSTest。

**Spec:** `doc/plans/2026-10-27-clothesline-rope-effects-design.md`

## Global Constraints

- 只改 `SETUNA.Core`、`SETUNA.Avalonia`、`SETUNATests`；`SETUNA`（旧 WinForms）冻结不动
- 代码风格按 AGENTS.md：4 空格 CRLF、新行大括号、PascalCase、`var`、公开字段中文注释
- 所有新代码注释用中文
- 布局/命中几何常量（RopeY/RopeX/ClipWidth 等）**不得改动**——命中检测与空格钩子快照依赖它们
- 钩子线程安全：任何 Avalonia Visual 属性禁止在钩子线程触碰；`_visibilitySnapshot` 更新点（Show/Hide 后）不变
- `SetunaOption` 新增字段必须进 `Clone()`（否则选项确定后丢失）
- 构建验证命令：`dotnet build SETUNA.Avalonia/SETUNA.Avalonia.csproj`（以及 Core/SETUNA）；测试：`dotnet test SETUNATests/SETUNATests.csproj --filter "FullyQualifiedName~<类名>"`
- 每个 Task 结束 commit（消息末尾加 `Co-Authored-By: Claude Code <noreply@anthropic.com>`）

---

### Task 1: RopeSwingMath 摆动纯函数（Core）

**Files:**
- Create: `SETUNA.Core/Main/Clothesline/RopeSwingMath.cs`
- Test: `SETUNATests/RopeSwingMathTests.cs`

**Interfaces:**
- Consumes: 无
- Produces: `SETUNA.Main.Clothesline.RopeSwingMath` 静态类：
  - 常量 `IdleAmplitudeDeg=1.2`、`HoverAmplitudeDeg=4.0`、`NewItemAmplitudeDeg=6.0`、`SwingPeriodSeconds=3.2`、`PhaseStepRad=0.7`、`ApproachStepDeg=0.08`
  - `double SwingAngle(double amplitudeDeg, double elapsedSeconds, int itemIndex)` — 返回度
  - `double ApproachAmplitude(double currentDeg, double targetDeg, double stepDeg)` — 每 tick 线性逼近

- [ ] **Step 1: 写失败测试**

新建 `SETUNATests/RopeSwingMathTests.cs`：

```csharp
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SETUNA.Main.Clothesline;

namespace SETUNATests
{
    /// <summary>RopeSwingMath 摆动数学纯函数测试（无 UI、无 STA）。</summary>
    [TestClass]
    public class RopeSwingMathTests
    {
        [TestMethod]
        public void SwingAngle_AtZeroElapsed_Index0_IsZero()
        {
            // sin(0 + 0·相位) = 0
            Assert.AreEqual(0.0, RopeSwingMath.SwingAngle(1.2, 0.0, 0), 0.0001);
        }

        [TestMethod]
        public void SwingAngle_NeverExceedsAmplitude()
        {
            for (var t = 0.0; t < 10.0; t += 0.05)
            {
                var angle = RopeSwingMath.SwingAngle(4.0, t, 2);
                Assert.IsTrue(Math.Abs(angle) <= 4.0 + 0.0001, $"t={t} angle={angle}");
            }
        }

        [TestMethod]
        public void SwingAngle_DifferentIndices_HaveDifferentPhase()
        {
            // 同一时刻不同条目相位错开，角度不同（选定采样点避开共同零点）
            var a = RopeSwingMath.SwingAngle(4.0, 0.8, 0);
            var b = RopeSwingMath.SwingAngle(4.0, 0.8, 1);
            Assert.AreNotEqual(a, b, 0.0001);
        }

        [TestMethod]
        public void SwingAngle_PeriodRecurs_AfterOnePeriod()
        {
            var a = RopeSwingMath.SwingAngle(4.0, 1.0, 0);
            var b = RopeSwingMath.SwingAngle(4.0, 1.0 + RopeSwingMath.SwingPeriodSeconds, 0);
            Assert.AreEqual(a, b, 0.0001);
        }

        [TestMethod]
        public void ApproachAmplitude_ConvergesToTarget()
        {
            // 多次逼近后应恰好到达目标
            var amp = 6.0;
            for (var i = 0; i < 1000; i++)
            {
                amp = RopeSwingMath.ApproachAmplitude(amp, 1.2, RopeSwingMath.ApproachStepDeg);
            }
            Assert.AreEqual(1.2, amp, 0.0001);
        }

        [TestMethod]
        public void ApproachAmplitude_MoveDirection_IsCorrect()
        {
            // 低于目标应上升，高于目标应下降
            Assert.IsTrue(RopeSwingMath.ApproachAmplitude(1.0, 4.0, 0.08) > 1.0);
            Assert.IsTrue(RopeSwingMath.ApproachAmplitude(5.0, 1.2, 0.08) < 5.0);
        }

        [TestMethod]
        public void ApproachAmplitude_AlreadyAtTarget_Stays()
        {
            Assert.AreEqual(4.0, RopeSwingMath.ApproachAmplitude(4.0, 4.0, 0.08), 0.0001);
        }
    }
}
```

- [ ] **Step 2: 跑测试确认失败**

Run: `dotnet test SETUNATests/SETUNATests.csproj --filter "FullyQualifiedName~RopeSwingMathTests"`
Expected: 编译失败（`RopeSwingMath` 不存在）

- [ ] **Step 3: 最小实现**

新建 `SETUNA.Core/Main/Clothesline/RopeSwingMath.cs`：

```csharp
using System;

namespace SETUNA.Main.Clothesline
{
    /// <summary>
    /// 晾绳摆动数学（纯函数，供测试）：图片绕夹子接触点的简谐摆动与振幅逼近。
    /// 振幅单位度；新挂条目以 6° 起摆、靠逼近自然衰减回 1.2°（约 1 秒落定）。
    /// </summary>
    public static class RopeSwingMath
    {
        /// <summary>闲置振幅（度）。</summary>
        public const double IdleAmplitudeDeg = 1.2;

        /// <summary>悬停振幅（度）。</summary>
        public const double HoverAmplitudeDeg = 4.0;

        /// <summary>新挂条目起摆振幅（度）。</summary>
        public const double NewItemAmplitudeDeg = 6.0;

        /// <summary>摆动周期（秒）。</summary>
        public const double SwingPeriodSeconds = 3.2;

        /// <summary>条目间相位错开（弧度）。</summary>
        public const double PhaseStepRad = 0.7;

        /// <summary>每 tick 振幅逼近步长（度；16ms tick × 0.08 ≈ 5°/秒）。</summary>
        public const double ApproachStepDeg = 0.08;

        /// <summary>摆角（度）：amp × sin(2π·t/周期 + index×相位步)。</summary>
        public static double SwingAngle(double amplitudeDeg, double elapsedSeconds, int itemIndex)
        {
            return amplitudeDeg * Math.Sin(2 * Math.PI * elapsedSeconds / SwingPeriodSeconds + itemIndex * PhaseStepRad);
        }

        /// <summary>振幅向目标每 tick 线性逼近一步，到达即等于目标。</summary>
        public static double ApproachAmplitude(double currentDeg, double targetDeg, double stepDeg)
        {
            var diff = targetDeg - currentDeg;
            if (Math.Abs(diff) <= stepDeg)
            {
                return targetDeg;
            }
            return currentDeg + Math.Sign(diff) * stepDeg;
        }
    }
}
```

- [ ] **Step 4: 跑测试确认通过**

Run: `dotnet test SETUNATests/SETUNATests.csproj --filter "FullyQualifiedName~RopeSwingMathTests"`
Expected: PASS（7 个测试）

- [ ] **Step 5: Commit**

```bash
git add SETUNA.Core/Main/Clothesline/RopeSwingMath.cs SETUNATests/RopeSwingMathTests.cs
git commit -m "feat: 晾绳摆动数学纯函数 RopeSwingMath

Co-Authored-By: Claude Code <noreply@anthropic.com>"
```

---

### Task 2: 边缘呼出决策纯函数（Core）

**Files:**
- Create: `SETUNA.Core/Main/Clothesline/ClotheslineEdgeReveal.cs`
- Test: `SETUNATests/ClotheslineEdgeRevealTests.cs`

**Interfaces:**
- Consumes: 无
- Produces:
  - `enum EdgeRevealAction { None, Reveal, Retract }`
  - `static class ClotheslineEdgeReveal`：
    - `TimeSpan RetractDelay`（300ms）、`TimeSpan ManualGrace`（3s）
    - `EdgeRevealAction DecideEdgeReveal(bool ropeVisible, bool capturePaused, bool edgeZoneHit, bool pointerNearRope, bool fullscreenCovers, TimeSpan awayDuration, bool withinManualGrace)`
  - `static class ClotheslineEdgeGeometry`：
    - `bool IsEdgeZoneHit(Point cursor, Rectangle workArea, bool vertical)` — 4px 触发带
    - `bool CoversWorkArea(Rectangle windowBounds, Rectangle workArea)` — 前台窗口全屏判定

- [ ] **Step 1: 写失败测试**

新建 `SETUNATests/ClotheslineEdgeRevealTests.cs`：

```csharp
using System;
using System.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SETUNA.Main.Clothesline;

namespace SETUNATests
{
    /// <summary>边缘呼出决策纯函数测试（spec 测试清单全覆盖）。</summary>
    [TestClass]
    public class ClotheslineEdgeRevealTests
    {
        private static readonly TimeSpan Away300 = TimeSpan.FromMilliseconds(300);
        private static readonly TimeSpan Away100 = TimeSpan.FromMilliseconds(100);

        [TestMethod]
        public void Hidden_AtEdgeZone_NoFullscreen_Reveals()
        {
            var action = ClotheslineEdgeReveal.DecideEdgeReveal(
                ropeVisible: false, capturePaused: false, edgeZoneHit: true,
                pointerNearRope: false, fullscreenCovers: false, awayDuration: Away300, withinManualGrace: false);
            Assert.AreEqual(EdgeRevealAction.Reveal, action);
        }

        [TestMethod]
        public void Hidden_AtEdgeZone_FullscreenCovers_None()
        {
            var action = ClotheslineEdgeReveal.DecideEdgeReveal(
                false, false, true, false, true, Away300, false);
            Assert.AreEqual(EdgeRevealAction.None, action);
        }

        [TestMethod]
        public void Hidden_NotAtEdgeZone_None()
        {
            var action = ClotheslineEdgeReveal.DecideEdgeReveal(
                false, false, false, false, false, Away300, false);
            Assert.AreEqual(EdgeRevealAction.None, action);
        }

        [TestMethod]
        public void Visible_WithinManualGrace_None()
        {
            // 手动呼出 3 秒宽限内，即使光标早已离开也不收起
            var action = ClotheslineEdgeReveal.DecideEdgeReveal(
                true, false, false, false, false, Away300, true);
            Assert.AreEqual(EdgeRevealAction.None, action);
        }

        [TestMethod]
        public void Visible_AtEdgeZone_None()
        {
            var action = ClotheslineEdgeReveal.DecideEdgeReveal(
                true, false, true, false, false, Away300, false);
            Assert.AreEqual(EdgeRevealAction.None, action);
        }

        [TestMethod]
        public void Visible_NearRope_None()
        {
            var action = ClotheslineEdgeReveal.DecideEdgeReveal(
                true, false, false, true, false, Away300, false);
            Assert.AreEqual(EdgeRevealAction.None, action);
        }

        [TestMethod]
        public void Visible_FullscreenCovers_None()
        {
            var action = ClotheslineEdgeReveal.DecideEdgeReveal(
                true, false, false, false, true, Away300, false);
            Assert.AreEqual(EdgeRevealAction.None, action);
        }

        [TestMethod]
        public void Visible_AwayPastDelay_Retracts()
        {
            var action = ClotheslineEdgeReveal.DecideEdgeReveal(
                true, false, false, false, false, Away300, false);
            Assert.AreEqual(EdgeRevealAction.Retract, action);
        }

        [TestMethod]
        public void Visible_AwayBelowDelay_None()
        {
            var action = ClotheslineEdgeReveal.DecideEdgeReveal(
                true, false, false, false, false, Away100, false);
            Assert.AreEqual(EdgeRevealAction.None, action);
        }

        [TestMethod]
        public void CapturePaused_AlwaysNone()
        {
            Assert.AreEqual(EdgeRevealAction.None, ClotheslineEdgeReveal.DecideEdgeReveal(
                false, true, true, false, false, Away300, false));
            Assert.AreEqual(EdgeRevealAction.None, ClotheslineEdgeReveal.DecideEdgeReveal(
                true, true, false, false, false, Away300, false));
        }

        [TestMethod]
        public void EdgeZone_Horizontal_Top4px_WithinWorkX()
        {
            var work = new Rectangle(0, 0, 1920, 1040);
            Assert.IsTrue(ClotheslineEdgeGeometry.IsEdgeZoneHit(new Point(500, 2), work, vertical: false));
            Assert.IsFalse(ClotheslineEdgeGeometry.IsEdgeZoneHit(new Point(500, 10), work, vertical: false));
            // 工作区 X 范围外不算
            Assert.IsFalse(ClotheslineEdgeGeometry.IsEdgeZoneHit(new Point(-5, 2), work, vertical: false));
        }

        [TestMethod]
        public void EdgeZone_Vertical_Right4px_WithinWorkY()
        {
            var work = new Rectangle(0, 0, 1920, 1040);
            Assert.IsTrue(ClotheslineEdgeGeometry.IsEdgeZoneHit(new Point(1918, 500), work, vertical: true));
            Assert.IsFalse(ClotheslineEdgeGeometry.IsEdgeZoneHit(new Point(1900, 500), work, vertical: true));
        }

        [TestMethod]
        public void CoversWorkArea_ContainsOrNot()
        {
            var work = new Rectangle(0, 0, 1920, 1040);
            Assert.IsTrue(ClotheslineEdgeGeometry.CoversWorkArea(new Rectangle(0, 0, 1920, 1080), work));
            Assert.IsFalse(ClotheslineEdgeGeometry.CoversWorkArea(new Rectangle(100, 100, 800, 600), work));
            Assert.IsFalse(ClotheslineEdgeGeometry.CoversWorkArea(Rectangle.Empty, work));
        }
    }
}
```

- [ ] **Step 2: 跑测试确认失败**

Run: `dotnet test SETUNATests/SETUNATests.csproj --filter "FullyQualifiedName~ClotheslineEdgeRevealTests"`
Expected: 编译失败

- [ ] **Step 3: 最小实现**

新建 `SETUNA.Core/Main/Clothesline/ClotheslineEdgeReveal.cs`：

```csharp
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

        /// <summary>横绳：光标在绳所在屏顶部触发带内且 X 在工作区范围；竖绳：右缘触发带。</summary>
        public static bool IsEdgeZoneHit(Point cursor, Rectangle workArea, bool vertical)
        {
            if (vertical)
            {
                return cursor.X >= workArea.Right - EdgeZonePx
                    && cursor.Y >= workArea.Top && cursor.Y <= workArea.Bottom;
            }
            return cursor.Y <= workArea.Top + EdgeZonePx
                && cursor.X >= workArea.Left && cursor.X <= workArea.Right;
        }

        /// <summary>前台窗口完全覆盖绳所在屏工作区（全屏应用）判定。</summary>
        public static bool CoversWorkArea(Rectangle windowBounds, Rectangle workArea)
        {
            return !windowBounds.IsEmpty && windowBounds.Contains(workArea);
        }
    }
}
```

- [ ] **Step 4: 跑测试确认通过**

Run: `dotnet test SETUNATests/SETUNATests.csproj --filter "FullyQualifiedName~ClotheslineEdgeRevealTests"`
Expected: PASS（13 个测试）

- [ ] **Step 5: Commit**

```bash
git add SETUNA.Core/Main/Clothesline/ClotheslineEdgeReveal.cs SETUNATests/ClotheslineEdgeRevealTests.cs
git commit -m "feat: 边缘呼出决策与几何纯函数 ClotheslineEdgeReveal

Co-Authored-By: Claude Code <noreply@anthropic.com>"
```

---

### Task 3: SetunaOption 新增 ClotheslineEdgeReveal 字段

**Files:**
- Modify: `SETUNA.Core/Main/Option/SetunaOption.cs:824` 附近（ClotheslineRopeOrientation 字段后）
- Modify: 同文件 `Clone()`（约 753-773 行）
- Test: `SETUNATests/ClotheslineEdgeRevealTests.cs`（追加）

**Interfaces:**
- Consumes: 无
- Produces: `SetunaOption.ClotheslineEdgeReveal`（public bool，默认 true）——Task 7/8 消费

- [ ] **Step 1: 写失败测试（追加到 ClotheslineEdgeRevealTests.cs 末尾类内）**

```csharp
        [TestMethod]
        public void SetunaOption_EdgeReveal_DefaultsTrue()
        {
            // 旧配置无该字段 → 字段初始化器生效（XmlSerializer 缺节点保留初始化值）
            Assert.IsTrue(new SETUNA.Main.Option.SetunaOption().ClotheslineEdgeReveal);
        }

        [TestMethod]
        public void SetunaOption_Clone_PreservesEdgeReveal()
        {
            var option = new SETUNA.Main.Option.SetunaOption { ClotheslineEdgeReveal = false };
            var clone = (SETUNA.Main.Option.SetunaOption)option.Clone();
            Assert.IsFalse(clone.ClotheslineEdgeReveal);
        }
```

- [ ] **Step 2: 跑测试确认失败**

Run: `dotnet test SETUNATests/SETUNATests.csproj --filter "FullyQualifiedName~ClotheslineEdgeRevealTests"`
Expected: 编译失败（`ClotheslineEdgeReveal` 不存在）

- [ ] **Step 3: 实现**

`SETUNA.Core/Main/Option/SetunaOption.cs` —— 在 `ClotheslineRopeOrientation` 字段（824 行）后加：

```csharp
        /// <summary>移到屏幕边缘自动呼出绳子（晾绳模式，PicRope 交互）。</summary>
        public bool ClotheslineEdgeReveal = true;
```

`Clone()` 方法内（`ClotheslineRopeOrientation = ClotheslineRopeOrientation` 后）加一行：

```csharp
                ClotheslineEdgeReveal = ClotheslineEdgeReveal
```

（前一行 `ClotheslineRopeOrientation = ClotheslineRopeOrientation` 末尾补逗号）

说明：字段初始化器 `= true` 同时覆盖 `GetDefaultOption()`（内部就是 `new SetunaOption()`）与反序列化缺节点两条路径，无需改 `GetDefaultOption()`。

- [ ] **Step 4: 跑测试确认通过**

Run: `dotnet test SETUNATests/SETUNATests.csproj --filter "FullyQualifiedName~ClotheslineEdgeRevealTests"`
Expected: PASS（15 个测试）

- [ ] **Step 5: Commit**

```bash
git add SETUNA.Core/Main/Option/SetunaOption.cs SETUNATests/ClotheslineEdgeRevealTests.cs
git commit -m "feat: SetunaOption 新增 ClotheslineEdgeReveal 字段（默认开，Clone 保留）

Co-Authored-By: Claude Code <noreply@anthropic.com>"
```

---

### Task 4: ClotheslineWindow 视觉美化（拧绳/木夹/投影）

**Files:**
- Modify: `SETUNA.Avalonia/Views/ClotheslineWindow.cs`（Render 方法 368-440 行区域，新增两个私有方法）

**Interfaces:**
- Consumes: 无新依赖（纯 Avalonia.Media 矢量）
- Produces: `StreamGeometry BuildRopeGeometry(double offsetX, double offsetY)`、`void DrawClip(...)` 私有方法（Task 5 的 Render 改造基于本任务产物）

- [ ] **Step 1: 替换 Render 中的绳线与夹子绘制**

把 `Render(DrawingContext context)` 中从"绳线：贝塞尔"注释到 `context.DrawGeometry(null, ropePen, geometry);` 的整段（约 372-397 行）替换为：

```csharp
            // 绳线：深棕底线 + 浅棕高光错位叠画（拧绳质感）
            var ropePen = new Pen(new SolidColorBrush(Color.FromRgb(0x78, 0x50, 0x28)), 3);
            var ropeHighlightPen = new Pen(new SolidColorBrush(Color.FromRgb(0xA8, 0x78, 0x48)), 1);
            context.DrawGeometry(null, ropePen, BuildRopeGeometry(0, 0));
            context.DrawGeometry(null, ropeHighlightPen,
                BuildRopeGeometry(_vertical ? -1 : 0, _vertical ? 0 : -1));
```

并把条目循环里的夹子两处 `context.FillRectangle(clipBrush, ...)` 调用删除（横竖两个分支内各一处），改为在 `context.DrawImage(thumbnail, avaloniaRect);` 之后加：

```csharp
                DrawClip(context, rx, ry, rect, drawScale);
```

循环开头的美化变量替换为：

```csharp
            var framePen = new Pen(Brushes.White, FramePadding * 2 - 2);
            var shadowBrush = new SolidColorBrush(Color.FromArgb(0x33, 0, 0, 0));
```

（原 `clipBrush` 声明删除——夹子颜色移入 DrawClip）

在图片绘制前（`var avaloniaRect = ...` 之后）加投影：

```csharp
                // 白框投影（环境光方向固定，不随摆动转）
                context.FillRectangle(shadowBrush, new Rect(avaloniaRect.X + 2, avaloniaRect.Y + 3, avaloniaRect.Width, avaloniaRect.Height));
```

- [ ] **Step 2: 新增两个私有方法（Render 方法后、类结尾前）**

```csharp
        /// <summary>绳线几何（错位偏移用于高光叠画）。</summary>
        private StreamGeometry BuildRopeGeometry(double offsetX, double offsetY)
        {
            var geometry = new StreamGeometry();
            using (var gctx = geometry.Open())
            {
                if (_vertical)
                {
                    var h = Height;
                    gctx.BeginFigure(new global::Avalonia.Point(RopeX + offsetX, -10 + offsetY), false);
                    gctx.CubicBezierTo(
                        new global::Avalonia.Point(RopeX - 4 + offsetX, h / 3 + offsetY),
                        new global::Avalonia.Point(RopeX + 4 + offsetX, h * 2 / 3 + offsetY),
                        new global::Avalonia.Point(RopeX + offsetX, h + 10 + offsetY));
                }
                else
                {
                    var w = Width;
                    gctx.BeginFigure(new global::Avalonia.Point(-10 + offsetX, RopeY + offsetY), false);
                    gctx.CubicBezierTo(
                        new global::Avalonia.Point(w / 3 + offsetX, RopeY - 4 + offsetY),
                        new global::Avalonia.Point(w * 2 / 3 + offsetX, RopeY + 4 + offsetY),
                        new global::Avalonia.Point(w + 10 + offsetX, RopeY + offsetY));
                }
                gctx.EndFigure(false);
            }
            return geometry;
        }

        /// <summary>木夹子：木色圆角身 + 中缝 + 弹簧箍，压在绳线接触点（随 Task 5 的摆动一起旋转）。</summary>
        private void DrawClip(DrawingContext context, double rx, double ry, DrawingRect rect, double drawScale)
        {
            var bodyBrush = new SolidColorBrush(Color.FromRgb(0xC8, 0x98, 0x58));
            var seamPen = new Pen(new SolidColorBrush(Color.FromRgb(0x8A, 0x62, 0x30)), 1);
            var springBrush = new SolidColorBrush(Color.FromRgb(0x50, 0x50, 0x50));

            Rect bodyRect;   // 物理像素 → DIP
            if (_vertical)
            {
                // 图左中压绳（与原灰矩形位置一致）
                bodyRect = new Rect(
                    (RopeX - 2) / drawScale,
                    (ry + rect.Height / 2.0 - ClipWidth / 2.0) / drawScale,
                    ClipHeight / drawScale, ClipWidth / drawScale);
            }
            else
            {
                // 图顶中压绳
                bodyRect = new Rect(
                    (rx + rect.Width / 2.0 - ClipWidth / 2.0) / drawScale,
                    (RopeY - 2) / drawScale,
                    ClipWidth / drawScale, ClipHeight / drawScale);
            }

            context.FillRectangle(bodyBrush, bodyRect, 2);

            // 中缝：沿夹子长轴中线
            if (_vertical)
            {
                var midX = bodyRect.X + bodyRect.Width / 2;
                context.DrawLine(seamPen, new global::Avalonia.Point(midX, bodyRect.Y + 2),
                    new global::Avalonia.Point(midX, bodyRect.Bottom - 2));
            }
            else
            {
                var midY = bodyRect.Y + bodyRect.Height / 2;
                context.DrawLine(seamPen, new global::Avalonia.Point(bodyRect.X + 2, midY),
                    new global::Avalonia.Point(bodyRect.Right - 2, midY));
            }

            // 弹簧箍：短轴方向两条（约 1/4 与 3/4 长轴处）
            for (var i = 1; i <= 2; i++)
            {
                var f = i / 3.0;
                if (_vertical)
                {
                    var y = bodyRect.Y + bodyRect.Height * f;
                    context.FillRectangle(springBrush, new Rect(bodyRect.X, y, bodyRect.Width, 1.5));
                }
                else
                {
                    var x = bodyRect.X + bodyRect.Width * f;
                    context.FillRectangle(springBrush, new Rect(x, bodyRect.Y, 1.5, bodyRect.Height));
                }
            }
        }
```

- [ ] **Step 3: 构建 + 目测**

Run: `dotnet build SETUNA.Avalonia/SETUNA.Avalonia.csproj`
Expected: Build succeeded

Run: `build-avalonia.bat` 启动应用，开启晾绳模式挂几张图，目测：绳有拧纹高光、夹子是木色圆角带中缝与弹簧箍、图有淡投影；命中/双击/拖动/滚轮行为与之前完全一致。

- [ ] **Step 4: Commit**

```bash
git add SETUNA.Avalonia/Views/ClotheslineWindow.cs
git commit -m "feat: 晾绳视觉美化——拧绳高光/木夹子/白框投影

Co-Authored-By: Claude Code <noreply@anthropic.com>"
```

---

### Task 5: 图片摆动动画

**Files:**
- Modify: `SETUNA.Avalonia/Views/ClotheslineWindow.cs`

**Interfaces:**
- Consumes: `RopeSwingMath`（Task 1，`SETUNA.Main.Clothesline` 命名空间）
- Produces: 窗口内私有摆动状态（`_swingTimer`/`_amplitudes`/`_hoverIndex`）；`public new void Show()/Hide()` 承担 timer 启停（Task 7 的 Reveal/Retract 复用）

- [ ] **Step 1: 加 using 与字段**

文件头部 using 区加（System.Diagnostics 与 SETUNA.Main.Clothesline 现无）：

```csharp
using System.Diagnostics;
using SETUNA.Main.Clothesline;
```

类字段区（`private static ClotheslineWindow InstanceRef;` 前）加：

```csharp
        // ===== 摆动动画状态 =====
        private global::Avalonia.Threading.DispatcherTimer _swingTimer;
        private readonly Stopwatch _swingClock = Stopwatch.StartNew();
        private List<double> _amplitudes = new();
        private int _hoverIndex = -1;
```

- [ ] **Step 2: Show/Hide 启停 timer（构造函数后加）**

```csharp
        public new void Show()
        {
            base.Show();
            EnsureSwingTimer().Start();
        }

        public new void Hide()
        {
            _swingTimer?.Stop();
            base.Hide();
        }

        private global::Avalonia.Threading.DispatcherTimer EnsureSwingTimer()
        {
            if (_swingTimer == null)
            {
                _swingTimer = new global::Avalonia.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(16),
                };
                _swingTimer.Tick += (s, e) =>
                {
                    UpdateAmplitudes();
                    InvalidateVisual();
                };
            }
            return _swingTimer;
        }

        private void UpdateAmplitudes()
        {
            for (var i = 0; i < _amplitudes.Count; i++)
            {
                var target = i == _hoverIndex ? RopeSwingMath.HoverAmplitudeDeg : RopeSwingMath.IdleAmplitudeDeg;
                _amplitudes[i] = RopeSwingMath.ApproachAmplitude(_amplitudes[i], target, RopeSwingMath.ApproachStepDeg);
            }
        }

        /// <summary>条目当前摆角（度）；无动画状态返回 0。</summary>
        private double SwingAngleFor(int index)
        {
            if (index < 0 || index >= _amplitudes.Count || _amplitudes[index] <= 0.01)
            {
                return 0;
            }
            return RopeSwingMath.SwingAngle(_amplitudes[index], _swingClock.Elapsed.TotalSeconds, index);
        }
```

- [ ] **Step 3: SetItems 同步振幅表**

`SetItems` 方法体（`_items.AddRange(items);` 与 `LayoutItems();` 之间）插入：

```csharp
            // 振幅跟随条目数：尾部新增以 6° 起摆；收缩截断。
            // ponytail: 不做逐条目 diff 对齐，中间删除时振幅错位视觉无感；需要时改按 Scrap 引用字典。
            while (_amplitudes.Count < _items.Count)
            {
                _amplitudes.Add(RopeSwingMath.NewItemAmplitudeDeg);
            }
            if (_amplitudes.Count > _items.Count)
            {
                _amplitudes.RemoveRange(_items.Count, _amplitudes.Count - _items.Count);
            }
```

- [ ] **Step 4: PointerMoved 悬停检测**

`OnMoved` 方法替换为（保留拖动逻辑，非拖动时检测悬停）：

```csharp
        private void OnMoved(object sender, PointerEventArgs e)
        {
            var pos = e.GetPosition(this);
            if (!_draggingRope)
            {
                // 悬停检测（驱动摆动加剧；命中不跟随摆动，spec 已定）
                var scale = RenderScaling > 0 ? RenderScaling : 1.0;
                var clientPt = new DrawingPoint((int)Math.Round(pos.X * scale), (int)Math.Round(pos.Y * scale));
                _hoverIndex = FindItemIndexAt(_itemRects, clientPt, _scrollOffset, _vertical);
                return;
            }
            var screenScale = RenderScaling > 0 ? RenderScaling : 1.0;
            var screen = new PixelPoint(
                Position.X + (int)Math.Round(pos.X * screenScale),
                Position.Y + (int)Math.Round(pos.Y * screenScale));
            Position = new PixelPoint(screen.X - _dragStart.X, screen.Y - _dragStart.Y);
        }
```

构造函数的事件订阅区加：

```csharp
            PointerExited += (s, e) => { _hoverIndex = -1; };
```

- [ ] **Step 5: Render 应用旋转**

Render 条目循环里，投影之后、`context.DrawRectangle(null, framePen, avaloniaRect);` 起（含 DrawRectangle/DrawImage/DrawClip 三行）包进摆动变换：

```csharp
                // 摆动：绕绳接触点旋转（横=图顶中，竖=图左中）；夹子与图同变换
                var angle = SwingAngleFor(i);
                var pivotX = _vertical ? RopeX / drawScale : avaloniaRect.X + avaloniaRect.Width / 2;
                var pivotY = _vertical ? avaloniaRect.Y + avaloniaRect.Height / 2 : RopeY / drawScale;
                using (Math.Abs(angle) > 0.05
                    ? context.PushTransform(new RotateTransform(angle, pivotX, pivotY))
                    : null)
                {
                    context.DrawRectangle(null, framePen, avaloniaRect);
                    var thumbnail = _items[i].Thumbnail;
                    if (thumbnail != null)
                    {
                        context.DrawImage(thumbnail, avaloniaRect);
                    }
                    DrawClip(context, rx, ry, rect, drawScale);
                }
```

（原三行裸语句删除，仅包壳；投影保持旋转外）

- [ ] **Step 6: 构建 + 目测**

Run: `dotnet build SETUNA.Avalonia/SETUNA.Avalonia.csproj`
Expected: Build succeeded

启动应用目测：挂图后图片轻微摇曳（各图相位不同）；鼠标悬停某张时摆动加剧、移开衰减；新挂上的图明显起摆后 1 秒内落定；拖动绳子/滚轮/双击取下不受影响；隐藏绳��后 CPU 占用回落（任务管理器确认）。

- [ ] **Step 7: Commit**

```bash
git add SETUNA.Avalonia/Views/ClotheslineWindow.cs
git commit -m "feat: 晾绳图片摆动动画（闲置微摆/悬停加剧/新挂起摆）

Co-Authored-By: Claude Code <noreply@anthropic.com>"
```

---

### Task 6: 淡入淡出辅助（边缘呼出用）

**Files:**
- Modify: `SETUNA.Avalonia/Views/ClotheslineWindow.cs`

**Interfaces:**
- Consumes: 无
- Produces: `public void AnimateOpacity(bool fadeIn, Action completed)` — 150ms 步进淡入/淡出；**切向时旧回调作废**（fadeOut 中转 fadeIn 则不执行旧 completed），Task 7 依赖此语义

- [ ] **Step 1: 实现方法（摆动方法区后加）**

```csharp
        // ===== 淡入淡出（边缘呼出用；15ms × 10 步 ≈ 150ms，对齐 ScrapWindow opacityTimer 模式）=====

        private global::Avalonia.Threading.DispatcherTimer _fadeTimer;
        private bool _fadeDirection;   // true=淡入
        private Action _fadeCompleted;

        /// <summary>窗口不透明度淡入/淡出；中途反向调用时旧回调作废（不会执行）。</summary>
        public void AnimateOpacity(bool fadeIn, Action completed)
        {
            _fadeDirection = fadeIn;
            _fadeCompleted = completed;
            if (_fadeTimer == null)
            {
                _fadeTimer = new global::Avalonia.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(15),
                };
                _fadeTimer.Tick += (s, e) =>
                {
                    const double step = 0.1;
                    Opacity = _fadeDirection
                        ? Math.Min(1, Opacity + step)
                        : Math.Max(0, Opacity - step);
                    if (Opacity <= 0 || Opacity >= 1)
                    {
                        _fadeTimer.Stop();
                        var done = _fadeCompleted;
                        _fadeCompleted = null;
                        done?.Invoke();
                    }
                };
            }
            _fadeTimer.Start();
        }
```

`Hide()`（Task 5 的 new Hide）内 `_swingTimer?.Stop();` 后加一行：

```csharp
            _fadeTimer?.Stop();
```

- [ ] **Step 2: 构建**

Run: `dotnet build SETUNA.Avalonia/SETUNA.Avalonia.csproj`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add SETUNA.Avalonia/Views/ClotheslineWindow.cs
git commit -m "feat: ClotheslineWindow 淡入淡出辅助（切向作废旧回调）

Co-Authored-By: Claude Code <noreply@anthropic.com>"
```

---

### Task 7: Controller 边缘呼出轮询 + 宿主接线

**Files:**
- Modify: `SETUNA.Avalonia/AvaloniaClotheslineController.cs`
- Modify: `SETUNA.Avalonia/AvaloniaHost.cs:287-324`（ApplyClotheslineOption）

**Interfaces:**
- Consumes: `ClotheslineEdgeReveal.DecideEdgeReveal`、`ClotheslineEdgeGeometry`（Task 2）；`ClotheslineWindow.AnimateOpacity`（Task 6）；`IScreenCaptureService.GetCursorPosition/GetMonitorFromPoint`、`IWindowService.GetForegroundWindow`（已有平台接口）
- Produces: `public void SetEdgeRevealEnabled(bool enabled)` — Task 8 选项开关的宿主同步入口

- [ ] **Step 1: Controller 加字段与轮询**

`AvaloniaClotheslineController.cs` using 区确认含 `SETUNA.Main.Clothesline`（已有）与 `SETUNA.Core.Platform`（已有 via 全名）。字段区（`private bool _disposed;` 后）加：

```csharp
        // ===== 边缘呼出（PicRope 交互）=====

        private global::Avalonia.Threading.DispatcherTimer _edgeTimer;
        private bool _edgeRevealEnabled;
        private DateTime _awaySince = DateTime.MaxValue;    // MaxValue=光标在绳/边缘上
        private DateTime _manualShowAt = DateTime.MinValue; // 手动呼出宽限起点
```

- [ ] **Step 2: Show() 记宽限起点**

`public void Show()` 方法体首行加：

```csharp
            _manualShowAt = DateTime.Now;
```

- [ ] **Step 3: 加 SetEdgeRevealEnabled / EdgeTick / 几何辅助 / 淡入淡出编排（UpdateVisibilitySnapshot 方法前）**

```csharp
        /// <summary>开/关边缘呼出轮询（宿主在模式应用与选项同步时调用）。</summary>
        public void SetEdgeRevealEnabled(bool enabled)
        {
            if (_edgeRevealEnabled == enabled)
            {
                return;
            }
            _edgeRevealEnabled = enabled;
            if (enabled)
            {
                EnsureEdgeTimer().Start();
            }
            else
            {
                _edgeTimer?.Stop();
            }
        }

        private global::Avalonia.Threading.DispatcherTimer EnsureEdgeTimer()
        {
            if (_edgeTimer == null)
            {
                _edgeTimer = new global::Avalonia.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(100),
                };
                _edgeTimer.Tick += (s, e) => EdgeTick();
            }
            return _edgeTimer;
        }

        /// <summary>100ms 轮询：组装决策输入 → 纯函数决策 → 执行呼出/收起。</summary>
        private void EdgeTick()
        {
            if (_disposed || _capturePaused)
            {
                return;
            }
            var capture = SETUNA.Core.Platform.PlatformServices.IsRegistered
                ? SETUNA.Core.Platform.PlatformServices.Instance.ScreenCapture
                : null;
            if (capture == null)
            {
                return;
            }

            var cursor = capture.GetCursorPosition();
            var ropeLoc = _ropeForm != null ? _ropeForm.RopeLocation : System.Drawing.Point.Empty;
            var vertical = IsRopeVertical;
            var work = capture.GetMonitorFromPoint(ropeLoc).WorkingArea;
            var edgeZoneHit = ClotheslineEdgeGeometry.IsEdgeZoneHit(cursor, work, vertical);
            var pointerNearRope = _ropeForm != null && IsPointerNearRopeForm(cursor);
            var fg = SETUNA.Core.Platform.PlatformServices.Instance.Window.GetForegroundWindow();
            var fullscreenCovers = fg != null && ClotheslineEdgeGeometry.CoversWorkArea(fg.Bounds, work);

            if (edgeZoneHit || pointerNearRope)
            {
                _awaySince = DateTime.MaxValue;
            }
            else if (_awaySince == DateTime.MaxValue)
            {
                _awaySince = DateTime.Now;
            }
            var awayDuration = _awaySince == DateTime.MaxValue ? TimeSpan.Zero : DateTime.Now - _awaySince;
            var withinManualGrace = DateTime.Now - _manualShowAt < ClotheslineEdgeReveal.ManualGrace;

            var action = ClotheslineEdgeReveal.DecideEdgeReveal(
                _visible, _capturePaused, edgeZoneHit, pointerNearRope, fullscreenCovers,
                awayDuration, withinManualGrace);

            if (action == EdgeRevealAction.Reveal)
            {
                RevealWithFade();
            }
            else if (action == EdgeRevealAction.Retract)
            {
                RetractWithFade();
            }
        }

        /// <summary>光标是否在绳窗矩形外扩 12px 内（物理像素）。</summary>
        private bool IsPointerNearRopeForm(System.Drawing.Point cursor)
        {
            var scale = _ropeForm.RenderScaling > 0 ? _ropeForm.RenderScaling : 1.0;
            var rect = new Rectangle(
                _ropeForm.Position.X,
                _ropeForm.Position.Y,
                (int)Math.Round(_ropeForm.Width * scale),
                (int)Math.Round(_ropeForm.Height * scale));
            rect.Inflate(12, 12);
            return rect.Contains(cursor);
        }

        /// <summary>边缘呼出：先零透明再 Show（立即路径），再淡入。</summary>
        private void RevealWithFade()
        {
            _ropeForm = EnsureRopeForm();
            _ropeForm.Opacity = 0;
            _visible = true;
            _ropeForm.Show();
            UpdateRopeItems();
            UpdateVisibilitySnapshot();
            _ropeForm.AnimateOpacity(fadeIn: true, completed: null);
        }

        /// <summary>边缘收起：关预览 → 淡出 → 完成回调真 Hide（更新可见性快照）。</summary>
        private void RetractWithFade()
        {
            if (_ropeForm == null || !_visible)
            {
                return;
            }
            ClosePreview();
            _ropeForm.AnimateOpacity(fadeIn: false, completed: Hide);
        }
```

`Dispose()` 内 `ClosePreview();` 后加：

```csharp
            _edgeTimer?.Stop();
```

- [ ] **Step 4: AvaloniaHost 同步开关**

`AvaloniaHost.cs` `ApplyClotheslineOption()`：`if (Option.ClotheslineEnabled)` 分支内 `_clotheslineController.Show();` 前加：

```csharp
                _clotheslineController.SetEdgeRevealEnabled(Option.ClotheslineEdgeReveal);
```

`else if (_clotheslineModeActive)` 分支内 `_clotheslineController.Hide();` 前加：

```csharp
                _clotheslineController.SetEdgeRevealEnabled(false);
```

- [ ] **Step 5: 构建 + 目测**

Run: `dotnet build SETUNA.Avalonia/SETUNA.Avalonia.csproj`
Expected: Build succeeded

启动应用目测（晾绳模式开、ClotheslineEdgeReveal 默认 true）：
- 绳子可见时光标离开绳子+边缘 300ms → 淡出收起；关掉预览
- 收起后光标移到绳所在屏顶缘（横绳）4px 内 → 淡入展开
- 全屏播放视频时移到边缘不呼出
- 托盘/热键手动呼出后 3 秒内不自动收起
- 捕获暂停（截图流程）期间不呼出不收起
- 选项里关掉晾绳模式 → 轮询停止（绳子彻底 Hide 不再呼出）

- [ ] **Step 6: Commit**

```bash
git add SETUNA.Avalonia/AvaloniaClotheslineController.cs SETUNA.Avalonia/AvaloniaHost.cs
git commit -m "feat: 晾绳边缘呼出/收起（100ms 轮询+淡入淡出+手动宽限+全屏门禁）

Co-Authored-By: Claude Code <noreply@anthropic.com>"
```

---

### Task 8: OptionWindow 复选框

**Files:**
- Modify: `SETUNA.Avalonia/Views/OptionWindow.cs`

**Interfaces:**
- Consumes: `SetunaOption.ClotheslineEdgeReveal`（Task 3）
- Produces: 无（UI 读写）

- [ ] **Step 1: 加控件字段**

类字段区（`_cmbOrientation` 声明附近）加：

```csharp
        private CheckBox _chkEdgeReveal;
```

- [ ] **Step 2: BuildClotheslinePage 加复选框**

`BuildClotheslinePage()` 内 `panel.Children.Add(Row("绳子方向", _cmbOrientation));` 后加：

```csharp
            _chkEdgeReveal = new CheckBox { Content = "移到屏幕边缘自动呼出绳子" };
            panel.Children.Add(_chkEdgeReveal);
```

- [ ] **Step 3: LoadOption / WriteBack 各一行**

`LoadOption()` 内 `_cmbOrientation.SelectedIndex = ...;` 后加：

```csharp
            _chkEdgeReveal.IsChecked = _working.ClotheslineEdgeReveal;
```

`WriteBack()` 内 `_working.ClotheslineRopeOrientation = ...;` 后加：

```csharp
            _working.ClotheslineEdgeReveal = _chkEdgeReveal.IsChecked == true;
```

- [ ] **Step 4: 构建 + 目测**

Run: `dotnet build SETUNA.Avalonia/SETUNA.Avalonia.csproj`
Expected: Build succeeded

打开选项 → 晾绳页看到新复选框且状态正确；取消勾选 → 确定 → 绳子不再自动呼出（常显语义回归）；重新打开选项勾选状态保持。

- [ ] **Step 5: Commit**

```bash
git add SETUNA.Avalonia/Views/OptionWindow.cs
git commit -m "feat: 选项晾绳页加边缘呼出复选框

Co-Authored-By: Claude Code <noreply@anthropic.com>"
```

---

### Task 9: 全量构建 + 测试 + 人工验收

**Files:**
- 无新改动（验证任务；发现问题回改后重跑本任务）

- [ ] **Step 1: 全项目构建（冻结基线不破坏）**

Run: `dotnet build SETUNA/SETUNA.csproj && dotnet build SETUNA.Avalonia/SETUNA.Avalonia.csproj`
Expected: 两个都 Build succeeded

- [ ] **Step 2: 全量测试**

Run: `dotnet test SETUNATests/SETUNATests.csproj`
Expected: 全部 PASS（含新增 RopeSwingMathTests 7 个、ClotheslineEdgeRevealTests 15 个；存量测试无回归）

- [ ] **Step 3: 人工验收清单**

启动 `build-avalonia.bat`，逐项核对：
1. 摆动：闲置微摆、相位错开；悬停加剧；新挂起摆落定；隐藏后 CPU 回落
2. 美化：拧绳高光、木夹子（中缝+弹簧箍）、投影；100% 与 150% DPI 下无错位
3. 边缘呼出：顶缘/右缘呼出、离开收起、全屏不呼出、手动宽限 3 秒、截图期间不动
4. 语义回归：双击取下、整体拖动、滚轮、右键菜单、空格预览三态、钩子线程命中（空格在缩略图上仍开预览）
5. 选项：复选框读写、Clone 不丢字段（改设置→确定→重开选项值保持）

- [ ] **Step 4: Commit（如有回改）**

```bash
git add -A
git commit -m "fix: 晾绳效果升级验收回改

Co-Authored-By: Claude Code <noreply@anthropic.com>"
```

---

## 自查记录

- **Spec 覆盖**：摆动（Task 1+5）、美化（Task 4）、边缘呼出（Task 2+6+7）、选项（Task 3+8）——spec 四节全有任务；测试清单（DecideEdgeReveal 九分支/摆动六断言）对应 Task 1/2 测试
- **占位符**：无 TBD/TODO；所有代码步骤含完整代码
- **类型一致性**：`SwingAngle/ApproachAmplitude`（Task 1 定义 → Task 5 消费）、`DecideEdgeReveal` 七参签名（Task 2 定义 = Task 7 调用）、`AnimateOpacity(bool, Action)`（Task 6 定义 = Task 7 调用）、`SetEdgeRevealEnabled`（Task 7 定义 = Task 7 Step 4 宿主消费）、`ClotheslineEdgeReveal` 字段（Task 3 = Task 7/8）
- **测试可跑性**：新纯函数均在 Core（测试项目引用范围），Avalonia 窗体改动靠构建+人工（测试项目未引用 Avalonia 工程，无法单测）
