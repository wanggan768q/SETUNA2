# 收缩窗裁剪语义对齐实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 收缩窗（双击贴图触发）从"整图等比缩小"改为旧版语义——以双击鼠标位置为中心裁原图 50×50 钉在原地。

**Architecture:** 裁剪几何抽为 Core 纯函数 `CompactGeometry`（TDD 可测）；`CompactWindow` 重写视觉与定位（固定 50×50 裁剪块 + 样式边框 + 鼠标点定位）；`AvaloniaHost` 透传 clickPoint（工厂签名已含，仅加参数）。

**Tech Stack:** .NET 9 / Avalonia 11 / System.Drawing（裁剪）/ MSTest。

**Spec:** `doc/plans/2026-10-28-compact-crop-design.md`

## Global Constraints

- 只改 `SETUNA.Core`、`SETUNA.Avalonia`、`SETUNATests`；`SETUNA`（旧 WinForms）冻结
- 代码风格 AGENTS.md：4 空格 CRLF、新行大括号、中文注释、`var`
- 旧版对照基准：`SETUNA/Main/StyleItems/CompactScrap.cs`（OnPaint/ConvertScrapPosToCompact/DoubleClick/KeyDown）与 Designer（固定 50×50）
- commit 消息**不加署名行**
- 构建验证：`dotnet build SETUNA.Avalonia/SETUNA.Avalonia.csproj`；测试：`dotnet test SETUNATests/SETUNATests.csproj --filter "FullyQualifiedName~CompactGeometryTests"`

---

### Task 1: CompactGeometry 裁剪纯函数（Core）+ 测试

**Files:**
- Create: `SETUNA.Core/Main/StyleItems/CompactGeometry.cs`
- Test: `SETUNATests/CompactGeometryTests.cs`

**Interfaces:**
- Consumes: 无
- Produces: `SETUNA.Main.StyleItems.CompactGeometry` 静态类：
  - `const int WindowSize = 50`
  - `static System.Drawing.Rectangle CalculateCropRect(System.Drawing.Size imageSize, System.Drawing.Point clickPoint)` — 源裁剪矩形；clickPoint==Empty 时取图中心

- [ ] **Step 1: 写失败测试**

新建 `SETUNATests/CompactGeometryTests.cs`：

```csharp
using System.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SETUNA.Main.StyleItems;

namespace SETUNATests
{
    /// <summary>CompactGeometry 裁剪几何纯函数测试（对齐旧 CompactScrap 裁剪语义）。</summary>
    [TestClass]
    public class CompactGeometryTests
    {
        [TestMethod]
        public void ClickPointInside_CropsAroundIt()
        {
            // 200×200 图，点 (100,100)：源矩形 = (75,75,50,50)
            var rect = CompactGeometry.CalculateCropRect(new Size(200, 200), new Point(100, 100));
            Assert.AreEqual(new Rectangle(75, 75, 50, 50), rect);
        }

        [TestMethod]
        public void ClickPointNearEdge_GoesNegativeLikeOldVersion()
        {
            // 旧版 DrawImageUnscaled 不 clamp：点 (10,10) → 源矩形左上 (-15,-15)，GDI 越界自然透明
            var rect = CompactGeometry.CalculateCropRect(new Size(200, 200), new Point(10, 10));
            Assert.AreEqual(new Rectangle(-15, -15, 50, 50), rect);
        }

        [TestMethod]
        public void ClickPointEmpty_UsesImageCenter()
        {
            // Empty → 图中心（旧版构造分支）；200×140 中心 = (100,70) → (75,45,50,50)
            var rect = CompactGeometry.CalculateCropRect(new Size(200, 140), Point.Empty);
            Assert.AreEqual(new Rectangle(75, 45, 50, 50), rect);
        }

        [TestMethod]
        public void ImageSmallerThanWindow_StillFullWindowRect()
        {
            // 图小于窗（30×30）：中心 (15,15) → 矩形 (-10,-10,50,50)；越界由绘制透明呈现
            var rect = CompactGeometry.CalculateCropRect(new Size(30, 30), Point.Empty);
            Assert.AreEqual(new Rectangle(-10, -10, 50, 50), rect);
        }
    }
}
```

- [ ] **Step 2: 跑测试确认失败**

Run: `dotnet test SETUNATests/SETUNATests.csproj --filter "FullyQualifiedName~CompactGeometryTests"`
Expected: 编译失败（`CompactGeometry` 不存在）

- [ ] **Step 3: 最小实现**

新建 `SETUNA.Core/Main/StyleItems/CompactGeometry.cs`：

```csharp
using System.Drawing;

namespace SETUNA.Main.StyleItems
{
    /// <summary>
    /// 收缩窗裁剪几何（纯函数，供测试）：对齐旧 CompactScrap 语义——
    /// 固定 50×50 窗，以双击点为中心取原图 50×50 源矩形；不 clamp（越界由透明绘制呈现）。
    /// </summary>
    public static class CompactGeometry
    {
        /// <summary>收缩窗边长（物理像素，对齐旧 Designer 固定 50×50）。</summary>
        public const int WindowSize = 50;

        /// <summary>计算源裁剪矩形；clickPoint 为 Empty 时取图中心（旧版构造分支）。</summary>
        public static Rectangle CalculateCropRect(Size imageSize, Point clickPoint)
        {
            var cx = clickPoint == Point.Empty ? imageSize.Width / 2 : clickPoint.X;
            var cy = clickPoint == Point.Empty ? imageSize.Height / 2 : clickPoint.Y;
            return new Rectangle(cx - WindowSize / 2, cy - WindowSize / 2, WindowSize, WindowSize);
        }
    }
}
```

- [ ] **Step 4: 跑测试确认通过**

Run: `dotnet test SETUNATests/SETUNATests.csproj --filter "FullyQualifiedName~CompactGeometryTests"`
Expected: PASS（4 个测试）

- [ ] **Step 5: Commit**

```bash
git add SETUNA.Core/Main/StyleItems/CompactGeometry.cs SETUNATests/CompactGeometryTests.cs
git commit -m "feat: 收缩窗裁剪几何纯函数 CompactGeometry"
```

---

### Task 2: CompactWindow 重写（裁剪视觉+定位）+ Host 透传

**Files:**
- Modify: `SETUNA.Avalonia/Views/CompactWindow.cs`（整文件重写视觉部分）
- Modify: `SETUNA.Avalonia/AvaloniaHost.cs:360-364,396-423`（透传 clickPoint）

**Interfaces:**
- Consumes: `CompactGeometry.CalculateCropRect(Size, Point)`、`CompactGeometry.WindowSize`（Task 1）
- Produces: `CompactWindow(IScrap scrap, CCompactStyleItem item, System.Drawing.Point clickPoint)` 新构造签名

- [ ] **Step 1: 重写 CompactWindow.cs**

整文件替换为：

```csharp
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SETUNA.Main;
using SETUNA.Main.StyleItems;

namespace SETUNA.Avalonia.Views
{
    /// <summary>
    /// 收缩窗（对齐旧 CompactScrap）：固定 50×50，以双击点为中心裁原图钉在原地；
    /// 样式边框（虚/实线+透明度），拖动同步写回原图位置，双击还原，Esc/Enter 关闭。
    /// </summary>
    public class CompactWindow : Window
    {
        private readonly IScrap scrap;
        private readonly CCompactStyleItem _item;
        private readonly System.Drawing.Point _clickPoint;

        public CompactWindow(IScrap scrap, CCompactStyleItem item, System.Drawing.Point clickPoint)
        {
            this.scrap = scrap;
            _item = item;
            _clickPoint = clickPoint;

            SystemDecorations = SystemDecorations.None;
            TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
            Topmost = true;
            ShowInTaskbar = false;
            CanResize = false;
            Title = "收缩";

            var image = scrap.Image;
            var size = CompactGeometry.WindowSize;
            if (image != null)
            {
                // 以双击点为中心裁原图 50×50（旧 OnPaint 语义）；越界部分透明呈现
                var crop = CompactGeometry.CalculateCropRect(image.Size, clickPoint);
                using var gdi = new System.Drawing.Bitmap(size, size);
                using (var g = System.Drawing.Graphics.FromImage(gdi))
                {
                    g.DrawImage((System.Drawing.Bitmap)image,
                        new System.Drawing.Rectangle(0, 0, size, size), crop,
                        System.Drawing.GraphicsUnit.Pixel);
                }
                using var stream = new System.IO.MemoryStream();
                gdi.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                stream.Position = 0;

                var img = new global::Avalonia.Controls.Image { Source = new Bitmap(stream) };
                var border = new Border
                {
                    Child = img,
                    BorderBrush = new SolidColorBrush(global::Avalonia.Media.Color.FromRgb(
                        (byte)((item.LineColor >> 16) & 0xFF),
                        (byte)((item.LineColor >> 8) & 0xFF),
                        (byte)(item.LineColor & 0xFF))),
                    BorderThickness = new Thickness(1),
                    BorderDashStyle = item.SoldLine ? BorderDashStyle.Solid : BorderDashStyle.Dash,
                    Opacity = Math.Max(0.1, item.Opacity / 100.0),
                    Background = Brushes.Transparent,
                };
                Content = border;
            }
            else
            {
                Background = Brushes.Gray;
            }

            // 物理像素 50×50 → DIP（对齐 ClotheslineWindow 布局换算）
            Width = size / (RenderScaling > 0 ? RenderScaling : 1.0);
            Height = size / (RenderScaling > 0 ? RenderScaling : 1.0);

            // 定位：原图位置 + 双击点 - 窗半径（旧 ConvertScrapPosToCompact）
            Position = new PixelPoint(
                scrap.Location.X + _clickPoint.X - size / 2,
                scrap.Location.Y + _clickPoint.Y - size / 2);

            // 拖动 + 双击还原 + 键盘关闭（旧 CompactScrap_KeyDown）
            PointerPressed += OnPressed;
            PointerMoved += OnMoved;
            PointerReleased += OnReleased;
            DoubleTapped += OnDoubleTapped;
            KeyDown += OnKeyDown;
        }

        private bool _dragging;
        private PixelPoint _dragStart;

        private void OnPressed(object sender, PointerPressedEventArgs e)
        {
            var props = e.GetCurrentPoint(this).Properties;
            if (props.IsLeftButtonPressed)
            {
                _dragging = true;
                var scale = RenderScaling > 0 ? RenderScaling : 1.0;
                var client = e.GetPosition(this);
                var screenPt = new PixelPoint(
                    Position.X + (int)Math.Round(client.X * scale),
                    Position.Y + (int)Math.Round(client.Y * scale));
                _dragStart = screenPt - Position;
                e.Pointer.Capture(this);
            }
        }

        private void OnMoved(object sender, PointerEventArgs e)
        {
            if (!_dragging || scrap.IsClotheslineHanging)
            {
                return;
            }
            var scale = RenderScaling > 0 ? RenderScaling : 1.0;
            var client = e.GetPosition(this);
            var screen = new PixelPoint(
                Position.X + (int)Math.Round(client.X * scale),
                Position.Y + (int)Math.Round(client.Y * scale));
            Position = new PixelPoint(screen.X - _dragStart.X, screen.Y - _dragStart.Y);
            // 反向写回原图位置（旧 ConvertCompactPosToScrap：原图跟随，双击点固定）
            scrap.Location = new System.Drawing.Point(
                Position.X - _clickPoint.X + CompactGeometry.WindowSize / 2,
                Position.Y - _clickPoint.Y + CompactGeometry.WindowSize / 2);
        }

        private void OnReleased(object sender, PointerReleasedEventArgs e)
        {
            _dragging = false;
            e.Pointer.Capture(null);
        }

        /// <summary>双击：Ctrl+双击 = 挂回绳子；普通双击 = 还原（对齐旧 CompactScrap）。</summary>
        private void OnDoubleTapped(object sender, global::Avalonia.Input.TappedEventArgs e)
        {
            if (e.KeyModifiers.HasFlag(global::Avalonia.Input.KeyModifiers.Control))
            {
                scrap.SetClotheslineHanging(true);
                Close();
                return;
            }
            if (scrap.IsClotheslineHanging)
            {
                scrap.SetClotheslineHanging(false);
            }
            else
            {
                scrap.Visible = true;
            }
            Close();
        }

        /// <summary>Esc/Enter 关闭（旧 CompactScrap_KeyDown 语义）。</summary>
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape || e.Key == Key.Enter)
            {
                Close();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
        }
    }
}
```

注意：`BorderDashStyle` 在 Avalonia 是 `Avalonia.Media.BorderDashStyle` 枚举（Solid/Dash 存在）。若编译报 `BorderDashStyle` 不在 `Avalonia.Media`，改用 `Avalonia.Media.BorderDashStyle` 全名。

- [ ] **Step 2: AvaloniaHost 透传 clickPoint**

`AvaloniaHost.cs` 两处：

360-364 行工厂 lambda 改为：

```csharp
            StyleUiFactory.CompactFormFactory = (scrap, item, clickPoint) =>
            {
                // 收缩窗：对齐旧 CompactScrap——隐藏原图 + 以点击点为中心的 50×50 裁剪窗
                CreateCompactWindow((IScrap)scrap, (CCompactStyleItem)item,
                    (System.Drawing.Point)clickPoint);
            };
```

`CreateCompactWindow` 签名与构造调用（396-416 行）改为：

```csharp
        private void CreateCompactWindow(IScrap scrap, CCompactStyleItem item, System.Drawing.Point clickPoint)
        {
```

以及内部 `var compact = new Views.CompactWindow(scrap, item);` 改为：

```csharp
                var compact = new Views.CompactWindow(scrap, item, clickPoint);
```

- [ ] **Step 3: 构建 + 全量测试**

Run: `dotnet build SETUNA.Avalonia/SETUNA.Avalonia.csproj`
Expected: Build succeeded（若 BorderDashStyle 报错按 Step 1 注意事项改全名）

Run: `dotnet test SETUNATests/SETUNATests.csproj`
Expected: 全部 PASS（含 CompactGeometryTests 4 个）

- [ ] **Step 4: Commit**

```bash
git add SETUNA.Avalonia/Views/CompactWindow.cs SETUNA.Avalonia/AvaloniaHost.cs
git commit -m "feat: 收缩窗对齐旧版裁剪语义（鼠标点 50×50 裁剪+样式边框+Esc 关闭）"
```

---

### Task 3: 人工验收

**Files:** 无新改动

- [ ] **Step 1: 打包 + 启动**

Run: `cmd /c "E:\Git\SETUNA2\build-avalonia.bat < nul"`（打包）后启动 `publish-framework\SETUNA.Avalonia.exe`

- [ ] **Step 2: 验收清单（用户执行）**

1. 截图 → 双击贴图：收缩窗 = 鼠标双击处 50×50 裁剪块（非整图缩小），虚线蓝框
2. 收缩窗中心 = 双击鼠标位置
3. 拖动收缩窗 → 双击还原后原图位置跟随
4. 双击收缩窗 → 还原原图；Ctrl+双击 → 挂绳；Esc → 关闭
5. 开晾绳模式后贴图右键首项 = "挂回绳子"

---

## 自查记录

- **Spec 覆盖**：裁剪视觉（Task 2 Step 1）、位置公式（同）、边框样式（BorderDashStyle+Opacity）、交互保留+Esc（同）、数据流透传（Step 2）、纯函数测试（Task 1）——spec 六节全覆盖
- **占位符**：无
- **类型一致性**：`CalculateCropRect(Size, Point)`（Task 1 定义 = Task 2 消费）；`CompactWindow` 新构造三参（Task 2 内定义 = Step 2 调用）；`CompactGeometry.WindowSize`（Task 1 = Task 2）
- **已知偏差声明**：无——DIP 换算已在代码中按 ClotheslineWindow 同模式处理
