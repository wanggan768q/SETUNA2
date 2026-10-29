# 收缩窗裁剪语义对齐设计（PicRope/旧版行为）

日期：2026-10-28
分支：feature/avalonia

## 背景

用户验收反馈：双击贴图收缩时，Avalonia 版把整图等比缩小到 50×50 内；旧版（`CompactScrap`）语义是**以双击鼠标位置为中心，把原图 50×50 那一小块裁出来**钉在原地。另确认：右键"挂回绳子"菜单项已存在，只是晾绳模式未开启时按设计隐藏（不改代码，开模式即可）。

## 旧版语义（对照基准，`SETUNA/Main/StyleItems/CompactScrap.cs`）

- 窗口固定 50×50（Designer `ClientSize=50,50` + Max/MinimumSize）
- `OnPaint`：`DrawImageUnscaled(_thumbnail, -clickpoint + (25,25))` → 显示原图以鼠标点为中心的 50×50 块；越界部分因 TransparencyKey 透明
- 边框：先白 1px 再样式线（`LineColor` 1px；`SoldLine=false` 虚线 DashPattern 4,4）
- `Opacity = item.Opacity / 100`
- 位置：`scrap.Location + clickpoint - (25,25)`；拖动反向换算写回 `scrap.Location`
- 双击关闭还原、Esc/Enter 关闭、拖动移动

## 设计（Avalonia 版 CompactWindow 改造）

1. **窗口**：固定 50×50 物理像素（DIP = 50/ScaleFactor）
2. **裁剪**：构造时对 `scrap.Image`（GDI）做一次 `DrawImage(src→dest50×50, srcRect=(cx-25, cy-25, 50, 50))`；`clickPoint==Empty` 取图中心。裁剪区越界照旧版不 clamp（透明窗自然呈现）
3. **边框**：Border 1px `LineColor`；`SoldLine?实:虚(DashStyle.Dash 4,4)`；`Opacity=item.Opacity/100`
4. **位置**：`scrap.Location + clickPoint - (25,25)`；拖动写回 `scrap.Location = Position - clickPoint + (25,25)`（clickPoint 固定）
5. **交互**：拖动/双击还原/Ctrl+双击挂绳保留；补 Esc/Enter 关闭（旧版 CompactScrap_KeyDown）
6. **数据流**：`CompactWindow` 构造加 `Point clickPoint` 参数；`AvaloniaHost.CreateCompactWindow` 透传（工厂签名已含，Core 零改动）

## 测试

- 纯函数 `CompactGeometry.CalculateCropRect(Size imageSize, Point clickPoint, int windowSize=50)` → 源矩形；边界用例（clickPoint 中心/角上/Empty）进 SETUNATests
- 视觉与交互人工验收

## 不做

- 右键菜单改动（已有，开晾绳模式即显示）
- 收缩窗右键菜单（旧版也没有）
