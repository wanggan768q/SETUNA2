# 晾绳绳子竖向方向支持 - 设计文档

日期：2026-10-25
状态：已批准（方案 A，用户确认）

## 需求

绳子目前只支持横向（照片单行从左到右，最新在右）。新增竖向支持并允许用户设置：

- 设置三选一：自动（默认）/ 横向 / 竖向
- 竖向排列：照片从上到下，最新在下；新图加入滚动到下端
- 竖向默认位置：主屏右边缘垂直居中（横向保持现状：顶部）
- 切换方向立即生效；拖动过的位置保留（换向后按工作区重新夹紧）
- 自动模式按绳子所在屏幕宽高比解析方向；解析结果不写回配置

## 方案（A：Form 内方向轴参数化）

方向差异是对称变换而非不同算法，用参数化而非策略类。`ClotheslineForm` 的布局/绘制/滚动/命中计算按 `IsVertical` 标志对调 X/Y 语义；纯函数加 orientation 参数，横竖共用一套实现。

不引入接口/策略类（一个接口两个实现、代码对称复制，违反最小实现原则）。

## 组件改动

### 1. `SetunaOption.cs`

- 新增嵌套枚举或命名空间级枚举 `ClotheslineRopeOrientation`：`Auto`（0，默认）/ `Horizontal` / `Vertical`（与 `ClotheslinePreviewLocation` 同级同风格，放命名空间级）
- 新增顶层字段 `public ClotheslineRopeOrientation ClotheslineRopeOrientation;`（默认 Auto）
- `Clone()` 复制该字段

### 2. `OptionForm.cs` + `OptionForm.Designer.cs`

- 现有晾绳设置组（groupBox17）内新增"绳子方向"三选一 radio：自动 / 横向 / 竖向
- 加载/写回沿用手工双向赋值模式（照抄 ClotheslinePreviewLocation 三 radio 的写法）

### 3. `ClotheslineForm.cs`

- `SetOrientation(bool vertical)`：保存标志 → 重排 → 滚动偏移清零 → 按新尺寸重新夹紧位置
- 布局 `LayoutItems()`：竖向时条目纵向排列（最新在下），绳线在条目左侧对称位（`RopeX = 18`，与横向 `RopeY` 对称），夹子在条目左侧压绳线；窗体尺寸按内容高度计算，宽度 = 绳区 + 最大条目宽 + padding
- 滚动：竖向滚 Y 偏移；`ScrollToLatest` 滚到底；`ScrollToIndex` 按 Y 边界判定
- 命中 `FindItemIndexAt`：矩形按 Y 偏移换算（加 orientation 参数）
- 绘制 `OnPaint`：竖向画垂直贝塞尔绳线；夹子/边框逻辑随矩形走，无需分叉
- 默认位置解析（新纯函数，供 Controller 调用）：`CalculateDefaultRopePosition(bool vertical, Rectangle workArea)` → 横=顶部偏左 `(workArea.X+100, Top+8)`；竖=右边缘垂直居中 `(workArea.Right-width-8, 垂直居中)`
- 纯函数签名变化：`CalculateThumbnailSize` 不变（尺寸计算与方向无关）；`FindItemIndexAt` 加 `bool vertical` 参数；布局矩形计算提取 `LayoutItemRects(items, bool vertical)` 纯函数供测试

### 4. `ClotheslineController.cs`

- 构造新增参数 `Func<ClotheslineRopeOrientation>`（宿主提供设置值）或 `Func<bool>` 直接给竖向标志——选前者，Controller 内解析
- 方向解析：`ResolveVertical(orientation, workArea)` → `orientation == Auto ? workArea.Height > workArea.Width : orientation == Vertical`（静态纯函数，可测）
-绳子窗体创建时按解析结果 `SetOrientation`；设置应用（OptionApply 链路）变更时对已存在的绳窗调 `SetOrientation`（立即生效）
- `Auto` 解析时机：绳子创建时 + 宿主显式调用刷新时。**不实时跟随**（拖到另一屏不翻转）

### 5. `Mainform.cs`

- 创建 Controller 时传入方向 getter（`(() => optSetuna.ClotheslineRopeOrientation)`）
- `OptionApply` 的晾绳分支：模式已开启且方向设置变化时，调 Controller 刷新方向（新增 `Controller.RefreshOrientation(orientation, workArea)` 或等价方法）
- 默认位置注入函数改造：`ClotheslineRopePositionSaved=false` 时按当前方向调 `CalculateDefaultRopePosition`（横=现状顶部，竖=右边缘居中）

## 行为细节

- **滚动偏移语义**：横向 `_scrollOffset` 是 X 偏移，竖向是 Y 偏移；`SetOrientation` 时清零（内容长度变化，旧偏移无意义）
- **位置夹紧**：`SetOrientation` 后窗体尺寸变化（宽度变窄高度变长），用现有夹紧逻辑按新尺寸重算，保证绳子完整落在工作区内
- **记住位置**：`ClotheslineRopePositionSaved` 与方向无关（同一位置字段横竖共用，切换后夹紧修正；用户再拖动即覆盖）
- **预览锚点**：`NearThumbnail` 模式的锚点用条目屏幕矩形（`GetItemScreenRectangle` 已按方向换算），无需额外处理
- **空格命中**：`FindThumbnailAt` → `FindItemAt` 链路自动继承方向（命中在 Form 内部）

## 测试

追加到 `SETUNATests/ClotheslineTests.cs`：

1. 方向解析：Auto + 宽屏 → 横；Auto + 竖屏（含负坐标工作区 1080x1920）→ 竖；Horizontal/Vertical 显式值
2. 布局纯函数：竖向条目矩形从上到下递增、最新在下；窗体尺寸 = 内容高度夹紧工作区
3. 命中：竖向滚动偏移后的命中换算（对称断言横向用例）
4. 默认位置：横向 = 顶部偏左（回归）；竖向 = 右边缘垂直居中（含负坐标工作区）
5. 设置往返：新字段序列化往返 + Clone 隔离（照抄现有 ClotheslineEnabled 测试模式）

## 明确不做

- 拖动排序、多根绳子、方向实时跟随窗口比例变化（Auto 只在绳子创建/显式刷新时解析）
- 竖向独立的记忆位置字段（与横向共用）
- 动画过渡

## 验证

```powershell
dotnet build SETUNA/SETUNA.csproj
dotnet test SETUNATests/SETUNATests.csproj
```

人工验收：切换横竖立即重排、竖绳右缘居中、拖动位置保留且夹紧、Auto 在竖屏上变竖绳、重启记住方向。
