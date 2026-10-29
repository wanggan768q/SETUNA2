# 晾绳效果升级设计（PicRope 风格）

日期：2026-10-27
分支：feature/avalonia（仅 SETUNA.Avalonia；旧 WinForms 冻结不动）
参考：https://github.com/Renly1994/PicRope

## 目标

给现有晾绳（`ClotheslineWindow`）加三类 PicRope 风格效果，**交互语义全部保留**（双击取下、整体拖动、右键菜单、滚轮滚动、空格预览不动）：

1. **图片摆动动画** — 图片像挂在绳上一样轻轻摇曳；悬停加剧、新挂上起摆
2. **视觉美化** — 木夹子、拧绳质感、白框投影（全部矢量绘制，零资源文件）
3. **边缘呼出** — 光标移到绳所在屏边缘自动展开，离开自动收起（可配置，默认开）

明确不做（YAGNI）：单击复��语义、图片篮子、文字信封、自动更新、多主题。

## 现状（改动锚点）

- `SETUNA.Avalonia/Views/ClotheslineWindow.cs`：自绘 `Render(DrawingContext)`；绳线=静态贝塞尔棕线，夹子=灰矩形；布局/命中/滚动为静态纯函数（`LayoutItemRects`/`FindItemIndexAt`/`CalculateThumbnailSize`，已有测试）
- `SETUNA.Avalonia/AvaloniaClotheslineController.cs`：Show/Hide + `_visibilitySnapshot`（钩子线程命中检测依赖快照，不能破坏）
- `SETUNA.Core/Main/Option/SetunaOption.cs:812` 附近：`ClotheslineEnabled` 等公开字段，`Clone()` 手工逐字段拷贝（**新字段必须加进 Clone**，否则选项窗确定后丢失）
- `SETUNA.Avalonia/Views/OptionWindow.cs:191` `BuildClotheslinePage()`：晾绳设置页
- `SETUNA.Avalonia/AvaloniaHost.cs:287` `ApplyClotheslineOption()`：模式开/关/同步入口
- 平台能力已有：`IScreenCaptureService.GetCursorPosition()`、`IWindowService.GetForegroundWindow()/GetWindowBounds()`（`IPlatformService.cs:45,69`）
- 配置：XmlSerializer 序列化 `SetunaOption` 公开字段；旧配置缺新字段 → 反序列化自动取默认值

## 设计

### 1. 摆动动画（ClotheslineWindow 内）

- **驱动**：`DispatcherTimer` 16ms tick → 重算相位 → `InvalidateVisual()`。窗口 Hide 时 Stop、Show 时 Start（不显示时 CPU 归零）。
- **每张图绕夹子顶部（绳接触点）旋转**：`angle = amplitude × sin(2π·t/period + phase)`，phase 按条目索引错开 0.7 rad，避免整齐划一。
- **振幅状态机**（每条目）：
  - 闲置：amp≈1.2°，period≈3.2s
  - 悬停条目：amp 平滑升至 4°（每 tick 线性逼近）
  - 新挂上（`SetItems` 新增索引）：amp 从 6° 指数衰减回闲置（"刚夹上去"的落定感）
- **Render 应用**：横绳绕图顶中点旋转，竖绳绕图左侧中点旋转（夹子位置）；`using` 推入 `RotateTransform` 画完释放。
- **命中检测不跟随摆动**（1.2° 偏移 2-3px 不可感；悬停 4° 约 8-10px 仍在白框内）。
  `ponytail:` 命中不做摆动补偿，肉眼无差；若将来做拖拽单图再算。
- **线程安全**：动画全部 UI 线程；钩子线程快照路径（`FindItemIndexAtPublic`）读的是静态布局矩形，与摆动无关，不受影响。

### 2. 视觉美化（全在 Render，矢量）

- **绳**：深棕 `#785028` 3px 底线 + 浅棕 `#A87848` 1px 高光沿曲线上移 1px 叠画 → 拧绳质感。
- **夹子**：木色 `#C89858` 圆角矩形（RoundRect r=2）+ 中缝深线 + 顶部弹簧箍 2 条深灰横线，替代灰矩形。位置不变（压绳线，横绳在图顶中、竖绳在图左中）。
- **白框投影**：画图前先用半透明黑 `#33000000` 在 rect 偏移 (2,3) 填一次。
- 几何常量（RopeY/ClipWidth 等）不动 → 布局与命中零改动。

### 3. 边缘呼出（Controller 加轮询）

- **轮询**：`DispatcherTimer` 100ms（UI 线程），晾绳模式激活且 `ClotheslineEdgeReveal=true` 时运行。
- **呼出条件**（全满足）：模式开、未捕获暂停（`_capturePaused=false`）、当前不可见、光标在绳窗所在屏边缘 4px 触发带内（横绳=顶部 4px，竖绳=右缘 4px，沿绳方向在屏范围内）、前台窗口未全屏覆盖该屏（`GetForegroundWindow`+`GetWindowBounds` 判定，约 5 行）。
- **收起条件**（全满足）：可见、非呼出触发带内、光标离开绳窗矩形外扩 12px 持续 300ms → Hide（先 `ClosePreview()`，对齐旧 Hide 语义）。
- **切换动画**：呼出淡入 150ms、收起淡出 150ms（`Window.Opacity` 动画，收起动画完成后才真 Hide）。
- **决策纯函数**（供测试）：
  ```csharp
  public enum EdgeRevealAction { None, Reveal, Retract }
  public static EdgeRevealAction DecideEdgeReveal(
      bool ropeVisible, bool capturePaused, bool edgeZoneHit,
      bool pointerNearRope, bool fullscreenCovers, TimeSpan awayDuration,
      bool withinManualGrace)
  ```
  呼出判定输入由轮询 tick 组装；时序状态（离开计时）留 Controller 字段。
- **门禁优先级**：手动呼出（托盘/热键 Show）后 3 秒内轮询不收起（用户主动行为优先）；`SetCapturePaused` 期间轮询只呼出检查跳过。
- 现有 `_visibilitySnapshot` 在 Show/Hide 更新点不变，边缘呼出的 Show/Hide 走同一路径 → 钩子命中自动正确。

### 4. 选项

- `SetunaOption` 加 `public bool ClotheslineEdgeReveal;`（bool 缺省 false，需在 `GetDefaultOption()` 显式置 true；**必须加进 `Clone()`**）。
- OptionWindow 晾绳页加复选框"移到屏幕边缘自动呼出绳子"，LoadOption/WriteBack 各一行。
- 旧配置无该字段 → XmlSerializer 用字段默认值 false → 需默认 true，所以在字段声明处 `= true;`（XmlSerializer 对已初始化字段的缺省节点会保留初始化值）并在 GetDefaultOption 保持一致。

## 测试

- `SETUNATests`（纯函数，STA 套件外即可跑）：
  - `DecideEdgeReveal`：呼出带命中→Reveal；未呼出带+超时→Retract；capturePaused→None；前台全屏→None；手动呼出宽限内→None
  - 摆动：`SwingAngle(amplitude, phase, t)` 与振幅逼近纯函数若干断言
- 视觉/动画/淡入淡出：人工验收（现有 build-avalonia.bat 流程）。

## 改动文件清单

| 文件 | 改动 |
|---|---|
| `SETUNA.Avalonia/Views/ClotheslineWindow.cs` | 动画 timer、摆动状态、Render 美化（绳/夹/投影）、淡入淡出 |
| `SETUNA.Avalonia/AvaloniaClotheslineController.cs` | 边缘轮询、DecideEdgeReveal 纯函数、呼出/收起编排、手动呼出宽限 |
| `SETUNA.Core/Main/Option/SetunaOption.cs` | `ClotheslineEdgeReveal` 字段 + Clone + 默认值 |
| `SETUNA.Avalonia/Views/OptionWindow.cs` | 晾绳页复选框 + Load/WriteBack |
| `SETUNA.Avalonia/AvaloniaHost.cs` | ApplyClotheslineOption 同步轮询开关（约 3 行） |
| `SETUNATests/ClotheslineTests.cs`（或新文件） | DecideEdgeReveal / 摆动纯函数测试 |
