# 晾绳绳子竖向方向支持 - 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 绳子支持竖向排列（最新在下）并允许用户设置（自动/横向/竖向��选一，默认自动），切换立即生效。

**Architecture:** 方案 A——方向是对称变换。`ClotheslineForm` 布局/绘制/滚动/命中按 `IsVertical` 标志对调 X/Y 语义，纯函数加 orientation 参数，横竖共用一套实现。方向解析（Auto→按屏幕宽高比）在 Controller，宿主 Mainform 注入设置值。

**Tech Stack:** WinForms（.NET 9.0-windows），MSTest，无新依赖。

## Global Constraints

- 竖向排列最新在下；新图加入滚动到下端（`ScrollToLatest` 语义对称）。
- 竖向默认位置：主屏右边缘垂直居中；横向保持现状顶部偏左 `(WorkingArea.X+100, Top+8)`。
- 切换方向立即生效：滚动偏移清零，位置按工作区重新夹紧；`ClotheslineRopePositionSaved` 与方向无关（横竖共用一个位置字段）。
- Auto 解析：绳子所在屏幕 `WorkingArea.Height > Width` → 竖；解析结果不写回配置；只在绳子创建/宿主显式刷新时解析，不实时跟随。
- 新增字段 `ClotheslineRopeOrientation` 顶层值类型，`Clone()` 复制，旧配置缺字段默认 `Auto`。
- 不引入新依赖；不重构无关代码；代码风格遵循仓库根 `AGENTS.md`（PascalCase、4 空格、CRLF、新行大括号、var）。
- 测试命名 `Method_Scenario_ExpectedResult`，MSTest，追加到现有 `SETUNATests/ClotheslineTests.cs`（不破坏既有 67 个测试）。
- 测试只测纯计算（避免 STA 窗体死锁坑：`ScrapBase.OnPaint` 解引用 `Mainform.Instance`）。

---

### Task 1: 设置字段 + 方向解析纯函数

**Files:**
- Modify: `SETUNA/Main/Option/SetunaOption.cs`（枚举 + 字段 + Clone，参照 811-820 行现有字段模式）
- Modify: `SETUNA/Main/Clothesline/ClotheslineController.cs`（新增静态纯函数 `ResolveVertical`；只加函数，不改构造）
- Test: `SETUNATests/ClotheslineTests.cs`（追加新测试类或方法）

**Interfaces:**
- Consumes: 现有 `SetunaOption` 字段模式（SetunaOption.cs:811 `public bool ClotheslineEnabled;`）、`ClotheslinePreviewLocation` 枚举模式（SetunaOption.cs 附近命名空间级）
- Produces:
  - `enum ClotheslineRopeOrientation { Auto, Horizontal, Vertical }`（SETUNA.Main.Option 命名空间级，Auto=0）
  - `SetunaOption.ClotheslineRopeOrientation` 字段（public field，默认 Auto）
  - `ClotheslineController.ResolveVertical(ClotheslineRopeOrientation orientation, Rectangle workArea) → bool`（public static；Auto→`workArea.Height > workArea.Width`，Horizontal→false，Vertical→true）
  - `ClotheslineController.CalculateDefaultRopePosition(bool vertical, Rectangle workArea) → Point`（public static；横=`(workArea.X+100, workArea.Y+8)`；竖=`(workArea.Right-width-8, workArea.Y + (workArea.Height-height)/2)`，其中 width=60、height=200 为竖绳估算尺寸——与 ClotheslineForm 的 RopeX/夹子/MaxThumbWidth 对应，取保守估计值，实际尺寸由 LayoutItems 决定，默认位置只需落在屏幕内）

- [ ] **Step 1: 写失败测试**

追加到 `SETUNATests/ClotheslineTests.cs` 末尾（新 `[TestClass]`，命名 `ClotheslineOrientationTests`，带临时缓存 Setup/Cleanup 照抄现有模式）：

```csharp
[TestClass]
[SupportedOSPlatform("windows")]
public class ClotheslineOrientationTests
{
    [TestMethod]
    public void ResolveVertical_AutoOnLandscapeWorkArea_IsFalse()
    {
        var wa = new Rectangle(0, 0, 1920, 1032);
        Assert.IsFalse(ClotheslineController.ResolveVertical(ClotheslineRopeOrientation.Auto, wa));
    }

    [TestMethod]
    public void ResolveVertical_AutoOnPortraitWorkArea_IsTrue()
    {
        // 负坐标竖屏（旋转显示器）
        var wa = new Rectangle(-1080, 0, 1080, 1920);
        Assert.IsTrue(ClotheslineController.ResolveVertical(ClotheslineRopeOrientation.Auto, wa));
    }

    [TestMethod]
    public void ResolveVertical_ExplicitValues_OverrideWorkArea()
    {
        var wa = new Rectangle(0, 0, 1920, 1032);
        Assert.IsFalse(ClotheslineController.ResolveVertical(ClotheslineRopeOrientation.Horizontal, wa));
        Assert.IsTrue(ClotheslineController.ResolveVertical(ClotheslineRopeOrientation.Vertical, wa));
    }

    [TestMethod]
    public void ResolveVertical_AutoSquareWorkArea_IsFalse()
    {
        // 正方形工作区按横（Height > Width 严格判定）
        var wa = new Rectangle(0, 0, 1000, 1000);
        Assert.IsFalse(ClotheslineController.ResolveVertical(ClotheslineRopeOrientation.Auto, wa));
    }

    [TestMethod]
    public void CalculateDefaultRopePosition_Horizontal_MatchesCurrentTopLeft()
    {
        var wa = new Rectangle(0, 0, 1920, 1032);
        var p = ClotheslineController.CalculateDefaultRopePosition(false, wa);
        Assert.AreEqual(new Point(100, 8), p);
    }

    [TestMethod]
    public void CalculateDefaultRopePosition_Vertical_RightEdgeCentered()
    {
        var wa = new Rectangle(0, 0, 1920, 1032);
        var p = ClotheslineController.CalculateDefaultRopePosition(true, wa);
        // 右边缘留 8px 间距，垂直居中
        Assert.AreEqual(wa.Right - 60 - 8, p.X);
        Assert.AreEqual(wa.Y + (wa.Height - 200) / 2, p.Y);
    }

    [TestMethod]
    public void CalculateDefaultRopePosition_Vertical_NegativeOriginWorkArea_StaysInside()
    {
        var wa = new Rectangle(-1080, 0, 1080, 1920);
        var p = ClotheslineController.CalculateDefaultRopePosition(true, wa);
        Assert.IsTrue(wa.Contains(new Rectangle(p, new Size(60, 200))));
    }

    [TestMethod]
    public void SetunaOption_OrientationRoundTrip_PreservesValue()
    {
        var so = new SetunaOption();
        Assert.AreEqual(ClotheslineRopeOrientation.Auto, so.ClotheslineRopeOrientation); // 默认 Auto
        so.ClotheslineRopeOrientation = ClotheslineRopeOrientation.Vertical;
        var clone = (SetunaOption)so.Clone();
        Assert.AreEqual(ClotheslineRopeOrientation.Vertical, clone.ClotheslineRopeOrientation);
        clone.ClotheslineRopeOrientation = ClotheslineRopeOrientation.Horizontal;
        Assert.AreEqual(ClotheslineRopeOrientation.Vertical, so.ClotheslineRopeOrientation); // 隔离
    }
}
```

注意：`using SETUNA.Main.Option;`（枚举所在命名空间）测试文件已引用；`ClotheslineController` 已在 `using SETUNA.Main.Clothesline;`。

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test SETUNATests/SETUNATests.csproj --filter "FullyQualifiedName~ClotheslineOrientationTests"`
Expected: 编译错误 CS0117/CS0103（`ClotheslineRopeOrientation` 未定义、`ResolveVertical` 不存在）

- [ ] **Step 3: 实现**

`SETUNA/Main/Option/SetunaOption.cs`——在 `ClotheslinePreviewLocation` 枚举（命名空间级，约 1175 行）旁新增：

```csharp
    /// <summary>绳子方向：自动按屏幕宽高比 / 强制横向 / 强制竖向。</summary>
    public enum ClotheslineRopeOrientation
    {
        Auto,
        Horizontal,
        Vertical
    }
```

在字段区（820 行 `ClotheslineRopePositionSaved` 之后）新增：

```csharp
        /// <summary>绳子方向（Auto 按屏幕宽高比解析）。</summary>
        public ClotheslineRopeOrientation ClotheslineRopeOrientation;
```

在 `Clone()`（762-765 行新字段复制区）追加一行：

```csharp
                ClotheslineRopeOrientation = ClotheslineRopeOrientation,
```

`SETUNA/Main/Clothesline/ClotheslineController.cs`——类内（`DecideModeAction` 附近）新增两个静态纯函数：

```csharp
        /// <summary>方向解析：Auto 按工作区宽高比，显式值直接生效。竖屏（高>宽）为竖。</summary>
        public static bool ResolveVertical(ClotheslineRopeOrientation orientation, Rectangle workArea)
        {
            switch (orientation)
            {
                case ClotheslineRopeOrientation.Horizontal:
                    return false;
                case ClotheslineRopeOrientation.Vertical:
                    return true;
                default:
                    return workArea.Height > workArea.Width;
            }
        }

        /// <summary>默认绳子位置：横=顶部偏左；竖=右边缘垂直居中（估算尺寸 60x200，仅保证落在工作区内）。</summary>
        public static Point CalculateDefaultRopePosition(bool vertical, Rectangle workArea)
        {
            if (!vertical)
            {
                return new Point(workArea.X + 100, workArea.Y + 8);
            }
            const int estimatedWidth = 60;
            const int estimatedHeight = 200;
            var height = Math.Min(estimatedHeight, workArea.Height);
            return new Point(workArea.Right - estimatedWidth - 8, workArea.Y + (workArea.Height - height) / 2);
        }
```

需要 `using SETUNA.Main.Option;`（已有）。

- [ ] **Step 4: 运行测试确认通过**

Run: `dotnet test SETUNATests/SETUNATests.csproj --filter "FullyQualifiedName~ClotheslineOrientationTests"`
Expected: PASS 8 个

- [ ] **Step 5: 全套回归 + 提交**

Run: `dotnet test SETUNATests/SETUNATests.csproj`
Expected: PASS 75 个（67 旧 + 8 新）

```bash
git add SETUNA/Main/Option/SetunaOption.cs SETUNA/Main/Clothesline/ClotheslineController.cs SETUNATests/ClotheslineTests.cs
git commit -m "新增绳子方向设置字段与方向解析纯函数"
```

---

### Task 2: ClotheslineForm 竖向布局/绘制/滚动/命中

**Files:**
- Modify: `SETUNA/Main/Clothesline/ClotheslineForm.cs`（全文件核心改造，316 行）
- Test: `SETUNATests/ClotheslineTests.cs`（追加）

**Interfaces:**
- Consumes: Task 1 无直接依赖（本任务独立可测）
- Produces:
  - `ClotheslineForm.SetOrientation(bool vertical)`：保存标志 → `LayoutItems()` → 滚动清零 → 位置夹紧
  - `ClotheslineForm.IsVertical` 属性（bool，只读）
  - `ClotheslineForm.LayoutItemRects(List<ClotheslineItem> items, bool vertical) → List<Rectangle>`（public static 纯函数，供测试）
  - `FindItemIndexAt(List<Rectangle> itemRects, Point clientPoint, int scrollOffset, bool vertical)`（现有签名加第 4 参数）
  - 竖向常量：`RopeX = 18`（对称 `RopeY`）、`EstimatedRopeWidth = RopeX + ClipHeight + MaxThumbWidth + ContentPadding + 8`（竖向窗体宽度估算）

- [ ] **Step 1: 写失败测试**

追加到 `ClotheslineOrientationTests` 类内：

```csharp
    [TestMethod]
    public void LayoutItemRects_Vertical_StacksTopToBottomLatestLast()
    {
        var items = new List<ClotheslineItem>();
        var bmp1 = new Bitmap(100, 50);   // 横图 → 缩略 100x50
        var bmp2 = new Bitmap(50, 100);   // 竖图 → 缩略按上限
        items.Add(new ClotheslineItem(null, bmp1));
        items.Add(new ClotheslineItem(null, bmp2));
        var rects = ClotheslineForm.LayoutItemRects(items, vertical: true);
        Assert.AreEqual(2, rects.Count);
        // 从上往下：第二个条目（最新）在更下方
        Assert.IsTrue(rects[1].Y > rects[0].Y);
        // X 全部对齐（同一列）
        Assert.AreEqual(rects[0].X, rects[1].X);
        bmp1.Dispose(); bmp2.Dispose();
    }

    [TestMethod]
    public void LayoutItemRects_Horizontal_Regression_RowLeftToRight()
    {
        var items = new List<ClotheslineItem>();
        var bmp1 = new Bitmap(100, 50);
        var bmp2 = new Bitmap(100, 50);
        items.Add(new ClotheslineItem(null, bmp1));
        items.Add(new ClotheslineItem(null, bmp2));
        var rects = ClotheslineForm.LayoutItemRects(items, vertical: false);
        Assert.AreEqual(2, rects.Count);
        Assert.IsTrue(rects[1].X > rects[0].X);   // 最新在右
        Assert.AreEqual(rects[0].Y, rects[1].Y);  // 同一行
        bmp1.Dispose(); bmp2.Dispose();
    }

    [TestMethod]
    public void FindItemIndexAt_VerticalAppliesScrollOffset()
    {
        var rects = new List<Rectangle>
        {
            new Rectangle(20, 30, 100, 50),
            new Rectangle(20, 100, 100, 50)
        };
        // 无偏移：命中第一个
        Assert.AreEqual(0, ClotheslineForm.FindItemIndexAt(rects, new Point(50, 50), 0, vertical: true));
        // Y 偏移 70：第一个条目移出，第二个进入
        Assert.AreEqual(1, ClotheslineForm.FindItemIndexAt(rects, new Point(50, 50), 70, vertical: true));
        Assert.AreEqual(-1, ClotheslineForm.FindItemIndexAt(rects, new Point(50, 90), 70, vertical: true)); // 间隙
    }
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test SETUNATests/SETUNATests.csproj --filter "FullyQualifiedName~ClotheslineOrientationTests"`
Expected: 编译错误（`LayoutItemRects` 不存在、`FindItemIndexAt` 无 4 参重载）

- [ ] **Step 3: 实现对称化改造**

`ClotheslineForm.cs` 全文件按以下要点改造（保持既有横向行为回归不变）：

a) 新增字段与常量：

```csharp
        public const int RopeX = 18;   // 竖向绳线 X 位（对称 RopeY）
        private bool _vertical;
        public bool IsVertical => _vertical;
```

b) 抽取布局纯函数（现 `LayoutItems` 的矩形计算部分）：

```csharp
        /// <summary>布局纯函数：条目矩形序列。横向从左到右（最新在右）；竖向从上到下（最新在下）。</summary>
        public static List<Rectangle> LayoutItemRects(List<ClotheslineItem> items, bool vertical)
        {
            var rects = new List<Rectangle>();
            var x = ContentPadding;
            var y = RopeY + ClipHeight;   // 横向：绳线+夹子高度；竖向同样偏移作为顶部起点
            foreach (var item in items)
            {
                var size = item.Thumbnail != null
                    ? CalculateThumbnailSize(item.Thumbnail.Size)
                    : new Size(MaxThumbWidth, MaxThumbHeight);
                if (vertical)
                {
                    rects.Add(new Rectangle(RopeX + ClipHeight, y, size.Width, size.Height));
                    y += size.Height + ItemGap;
                }
                else
                {
                    rects.Add(new Rectangle(x, RopeY + ClipHeight, size.Width, size.Height));
                    x += size.Width + ItemGap;
                }
            }
            return rects;
        }
```

c) `LayoutItems()` 改用纯函数并按方向计算窗体尺寸：

```csharp
        private void LayoutItems()
        {
            _itemRects = LayoutItemRects(_items, _vertical);

            int contentLength;    // 内容主轴长度（横=宽，竖=高）
            int maxCross;         // 最大交叉轴尺寸
            if (_vertical)
            {
                var last = _itemRects.Count > 0 ? _itemRects[_itemRects.Count - 1] : Rectangle.Empty;
                contentLength = _items.Count > 0 ? last.Bottom - RopeY + ContentPadding : 0;
                maxCross = _itemRects.Count > 0 ? _itemRects.Max(r => r.Width) : 0;
            }
            else
            {
                contentLength = _items.Count > 0 ? _itemRects[_itemRects.Count - 1].Right - RopeY + ContentPadding : 0;
                maxCross = _itemRects.Count > 0 ? _itemRects.Max(r => r.Height) : 0;
            }
            _contentLength = contentLength;   // 重命名 _contentWidth → _contentLength（横=内容宽，竖=内容高）

            var workArea = Screen.FromPoint(Location).WorkingArea;
            int width, height;
            if (_vertical)
            {
                width = Math.Max(Math.Min(RopeX + ClipHeight + maxCross + ContentPadding, workArea.Width), _items.Count > 0 ? ContentPadding * 2 : 0);
                height = Math.Max(Math.Min(_contentLength, workArea.Height), 40);
            }
            else
            {
                width = Math.Max(Math.Min(_contentLength, workArea.Width), _items.Count > 0 ? ContentPadding * 2 : 0);
                height = RopeY + ClipHeight + maxCross + BottomMargin;
                if (height < 40) { height = 40; }
            }
            var location = Location;
            var newX = Math.Max(workArea.Left, Math.Min(location.X, workArea.Right - width));
            var newY = Math.Max(workArea.Top, Math.Min(location.Y, workArea.Bottom - height));
            SetBounds(newX, newY, width, height);
        }
```

（`_contentWidth` 字段重命名为 `_contentLength`；`MaxScrollOffset()` 改用 `_contentLength - 主轴客户区尺寸`：横 `ClientSize.Width`，竖 `ClientSize.Height`。）

d) `SetOrientation`：

```csharp
        /// <summary>设置方向并立即重排：滚动清零（内容长度变化），位置按工作区重新夹紧。</summary>
        public void SetOrientation(bool vertical)
        {
            if (_vertical == vertical)
            {
                return;
            }
            _vertical = vertical;
            LayoutItems();
            _scrollOffset = 0;
            Invalidate();
        }
```

e) 滚动/命中/绘制按方向分支：

- `OnMouseWheel`：`MaxScrollOffset()` 已按方向取主轴；`Math.Sign(e.Delta) * 120` 不变（偏移量语义主轴通用）
- `ScrollToLatest`：`_scrollOffset = MaxScrollOffset()` 不变（自动适配竖向=滚到底）
- `ScrollToIndex`：边界判定按方向换轴（`rect.Y - _scrollOffset < ContentPadding` / `rect.Bottom - _scrollOffset > ClientSize.Height - ContentPadding`）
- `GetItemScreenRectangle`：`rect.Offset(0, -_scrollOffset)`（竖向）
- `FindItemIndexAt` 纯函数加第 4 参 `bool vertical`：`scrolled = vertical ? new Rectangle(rect.X, rect.Y - scrollOffset, rect.Width, rect.Height) : new Rectangle(rect.X - scrollOffset, rect.Y, ...)`；现有 3 参调用点（`FindItemAt`/`OnDoubleClick`）传 `_vertical`
- `OnPaint`：绳线贝塞尔按方向画——横：现有水平贝塞尔；竖：`DrawBezier(ropePen, RopeX, -10, RopeX - 4, h/3, RopeX + 4, h*2/3, RopeX, h+10)`；条目矩形偏移 `rect.Offset(0, -_scrollOffset)`（竖向）/ `rect.Offset(-_scrollOffset, 0)`（横向）；视口外跳过条件换轴；夹子位置：横=条目上方 `(rect.X + rect.Width/2 - ClipWidth/2, RopeY - 2)`，竖=条目左侧 `(RopeX - 2, rect.Y + rect.Height/2 - ClipWidth/2)`（夹子旋转 90°：宽 ClipHeight 高 ClipWidth）
- 白框绘制与缩略图绘制随矩形走，无需分叉

f) 竖向窗体高度夹紧用 `BottomMargin` 对称（`RightMargin = 8`）——新增常量或复用 `BottomMargin` 值。

- [ ] **Step 4: 运行测试确认通过**

Run: `dotnet test SETUNATests/SETUNATests.csproj --filter "FullyQualifiedName~ClotheslineOrientationTests"`
Expected: PASS 11 个（8 + 3）

- [ ] **Step 5: 全套回归 + 提交**

Run: `dotnet test SETUNATests/SETUNATests.csproj`
Expected: PASS 78 个

```bash
git add SETUNA/Main/Clothesline/ClotheslineForm.cs SETUNATests/ClotheslineTests.cs
git commit -m "ClotheslineForm 支持竖向布局与对称绘制"
```

---

### Task 3: Controller/Mainform 接线 + OptionForm UI

**Files:**
- Modify: `SETUNA/Main/Clothesline/ClotheslineController.cs`（构造参数 + RefreshOrientation）
- Modify: `SETUNA/Mainform.cs`（注入方向 getter + OptionApply 刷新 + 默认位置函数改造，涉及 481-530 行 EnsureClotheslineController、375-400 行 ApplyClotheslineOption、900-935 行 Mainform_Load）
- Modify: `SETUNA/Main/Option/OptionForm.cs:85-88`（加载）与 `:166-178`（写回）
- Modify: `SETUNA/Main/Option/OptionForm.Designer.cs`（groupBox17 内三个新 radio，参照 1494-1560 行现有模式）
- Test: `SETUNATests/ClotheslineTests.cs`（追加）

**Interfaces:**
- Consumes:
  - Task 1: `ClotheslineRopeOrientation` 枚举、`ResolveVertical(orientation, workArea)`、`CalculateDefaultRopePosition(vertical, workArea)`
  - Task 2: `ClotheslineForm.SetOrientation(bool)`、`IsVertical`
- Produces:
  - `ClotheslineController` 构造第 3 参：`Func<ClotheslineRopeOrientation> getOrientation`（可空，null 时恒 Auto）
  - `ClotheslineController.RefreshOrientation()`：按当前设置+绳窗所在屏幕重解析并 `SetOrientation`（绳窗不存在时只记标志，下次 EnsureRopeForm 应用）
  - Mainform 默认位置注入改为：`ClotheslineRopePositionSaved ? saved : CalculateDefaultRopePosition(ResolveVertical(orientation, PrimaryScreen.WorkingArea), PrimaryScreen.WorkingArea)`

- [ ] **Step 1: 写失败测试**

追加到 `ClotheslineOrientationTests`：

```csharp
    [TestMethod]
    public void RefreshOrientation_UpdatesFormVerticalFlag()
    {
        RunOnStaWithMessagePump(() =>
        {
            var mainform = new Mainform();
            ClotheslineController controller = null;
            try
            {
                controller = new ClotheslineController(
                    () => new Point(100, 8),
                    _ => { },
                    () => ClotheslineRopeOrientation.Vertical);
                controller.Show();
                Assert.IsTrue(controller.IsRopeVertical);

                controller.SetOrientationSource(() => ClotheslineRopeOrientation.Horizontal);
                controller.RefreshOrientation();
                Assert.IsFalse(controller.IsRopeVertical);
            }
            finally
            {
                controller?.Dispose();
            }
        });
    }
```

注意：本测试用 `RunOnStaWithMessagePump`（ClotheslineTests.cs:545 已有，消息循环 + Mainform 实例防 OnPaint 死锁）。Controller 需暴露 `IsRopeVertical`（只读）与 `SetOrientationSource(Func<ClotheslineRopeOrientation>)`（测试换源用；生产路径源恒定不需要）。

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test SETUNATests/SETUNATests.csproj --filter "FullyQualifiedName~RefreshOrientation"`
Expected: 编译错误（构造无 3 参、`IsRopeVertical`/`SetOrientationSource`/`RefreshOrientation` 不存在）

- [ ] **Step 3: 实现 Controller 改造**

`ClotheslineController.cs`：

```csharp
        private readonly Func<ClotheslineRopeOrientation> _getOrientation;
        private bool _ropeVertical;

        /// <summary>绳窗当前方向（测试与宿主断言用）。</summary>
        public bool IsRopeVertical => _ropeVertical;

        public ClotheslineController(
            Func<Point> getRopeInitialPosition,
            Action<Point> ropePositionChanged,
            Func<ClotheslineRopeOrientation> getOrientation = null)
        {
            _getRopeInitialPosition = getRopeInitialPosition ?? ...;
            _ropePositionChanged = ropePositionChanged;
            _getOrientation = getOrientation;
        }

        /// <summary>测试用：更换方向设置源。</summary>
        public void SetOrientationSource(Func<ClotheslineRopeOrientation> getOrientation)
        {
            _getOrientationSource = getOrientation;   // 注意：readonly 字段改普通字段 _getOrientationSource
        }

        /// <summary>按当前设置与绳窗所在屏幕重解析方向并立即生效（绳窗未建时仅记标志）。</summary>
        public void RefreshOrientation()
        {
            var orientation = _getOrientationSource != null ? _getOrientationSource() : ClotheslineRopeOrientation.Auto;
            var workArea = _ropeForm != null ? Screen.FromControl(_ropeForm).WorkingArea : Screen.PrimaryScreen.WorkingArea;
            var vertical = ResolveVertical(orientation, workArea);
            if (_ropeForm != null)
            {
                _ropeForm.SetOrientation(vertical);
            }
            _ropeVertical = vertical;
        }
```

`EnsureRopeForm()` 内创建窗体后立即应用方向：

```csharp
                _ropeForm = new ClotheslineForm { Location = _getRopeInitialPosition() };
                _ropeVertical = ResolveVertical(CurrentOrientation(), Screen.FromControl(_ropeForm).WorkingArea);
                _ropeForm.SetOrientation(_ropeVertical);
```

（`CurrentOrientation()` = `_getOrientationSource?.Invoke() ?? Auto` 的小封装。）

- [ ] **Step 4: 实现 Mainform 接线**

`EnsureClotheslineController()`（Mainform.cs:481）构造调用加第 3 参：

```csharp
            clotheslineController = new ClotheslineController(
                () =>
                {
                    return optSetuna.ClotheslineRopePositionSaved
                        ? optSetuna.ClotheslineRopePosition
                        : ClotheslineController.CalculateDefaultRopePosition(
                            ClotheslineController.ResolveVertical(optSetuna.ClotheslineRopeOrientation, Screen.PrimaryScreen.WorkingArea),
                            Screen.PrimaryScreen.WorkingArea);
                },
                position => { ...现有... },
                () => optSetuna.ClotheslineRopeOrientation);
```

`ApplyClotheslineOption()` 的 `None` 分支（幂等路径，Mainform.cs:389-393）追加方向同步：

```csharp
                case ClotheslineModeAction.None:
                    if (enabled && clotheslineController != null)
                    {
                        clotheslineController.PreviewMode = optSetuna.ClotheslinePreviewLocation;
                        clotheslineController.RefreshOrientation();   // 方向设置变更立即生效（未变时 SetOrientation 早退）
                    }
                    return;
```

- [ ] **Step 5: 实现 OptionForm UI**

`OptionForm.Designer.cs` groupBox17 区（1494 行起）照抄现有三 radio 模式新增（radioButton4/5/6_clotheslineOrientation，Text 自动/横向/竖向，radioButton4 Checked=true，坐标排在现有预览三 radio 之后纵向排列）：

```csharp
            this.radioButton4_clotheslineOrientation = new System.Windows.Forms.RadioButton();
            this.radioButton5_clotheslineOrientation = new System.Windows.Forms.RadioButton();
            this.radioButton6_clotheslineOrientation = new System.Windows.Forms.RadioButton();
            // groupBox17.Controls.Add 三行
            // 各 radio 的 AutoSize/Location/Name/Size/Text/UseVisualStyleBackColor/TabBlockSize 六件套
```

`OptionForm.cs` 加载（88 行后）：

```csharp
            radioButton4_clotheslineOrientation.Checked = _so.ClotheslineRopeOrientation == ClotheslineRopeOrientation.Auto;
            radioButton5_clotheslineOrientation.Checked = _so.ClotheslineRopeOrientation == ClotheslineRopeOrientation.Horizontal;
            radioButton6_clotheslineOrientation.Checked = _so.ClotheslineRopeOrientation == ClotheslineRopeOrientation.Vertical;
```

写回（178 行后）：

```csharp
            if (radioButton4_clotheslineOrientation.Checked)
            {
                _so.ClotheslineRopeOrientation = ClotheslineRopeOrientation.Auto;
            }
            else if (radioButton5_clotheslineOrientation.Checked)
            {
                _so.ClotheslineRopeOrientation = ClotheslineRopeOrientation.Horizontal;
            }
            else
            {
                _so.ClotheslineRopeOrientation = ClotheslineRopeOrientation.Vertical;
            }
```

- [ ] **Step 6: 运行测试确认通过**

Run: `dotnet test SETUNATests/SETUNATests.csproj --filter "FullyQualifiedName~ClotheslineOrientationTests"`
Expected: PASS 12 个

- [ ] **Step 7: 全套回归 + 构建 + 提交**

Run: `dotnet build SETUNA/SETUNA.csproj --configuration Release && dotnet test SETUNATests/SETUNATests.csproj`
Expected: 构建无错误；PASS 79 个

```bash
git add SETUNA/Main/Clothesline/ClotheslineController.cs SETUNA/Mainform.cs SETUNA/Main/Option/OptionForm.cs SETUNA/Main/Option/OptionForm.Designer.cs SETUNATests/ClotheslineTests.cs
git commit -m "接入绳子方向设置：Controller 解析、OptionApply 即时刷新与设置界面"
```

---

## 人工验收清单（实现后）

- 设置切竖向 → 绳子立即竖排（最新在下）、右缘垂直居中（首次）；切回横向恢复顶部
- 拖动绳子 → 换方向 → 位置保留且夹紧屏内
- Auto 模式：主横屏 → 横绳；拖到竖屏重启 → 竖绳
- 竖向滚轮上下滚、空格命中竖向缩略图、预览 NearThumbnail 锚点正确
- 重启记住方向设置与绳子位置
