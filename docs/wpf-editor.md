# WPF 配方编辑器

> v1 — 加载/保存 JSON 配方，可视化编辑 patch 列表与设置。运行测量仍用 CLI，
> v2 会把 Workflow 注入并加 "Run" 按钮。

## 启动

```powershell
# 方式 1：dotnet run
dotnet run --project src\ColorMeasure.Host.Wpf

# 方式 2：直接跑 exe
src\ColorMeasure.Host.Wpf\bin\x64\Debug\net10.0-windows\ColorMeasure.Host.Wpf.exe
```

如果 WPF 进程起来了但看不到窗口，先 `Get-Process -Name 'ColorMeasure.Host.Wpf' | Stop-Process -Force` 清残留再起。常见原因是上一次没正常退出（XAML 错误 / Force 杀进程）锁了 apphost.exe。

## 界面布局

```
┌──────────────────────────────────────────────────────────────┐
│ 文件(F)  编辑(E)  帮助(H)                                  │  ← 菜单
├──────────────────────────────────────────────────────────────┤
│ [新建] [打开] [保存] | [+ Patch] [↑] [↓] [删除]            │  ← 工具栏
├──────────────────────────────────────────────────────────────┤
│  ID   色样  HEX    颜色…  Avg  Label                         │  ← Patch 列表
│  white  ⬜  #FFFFFF  …    8    white                         │     (DataGrid)
│  red    🟥  #FF0000  …    8    red primary                  │
│  green  🟩  #00FF00  …    8    green primary                │
│  ...                              │  配方 / 目标色域 / 通过条件 │  ← 右侧
│                                    │  名称: ...                 │     设置面板
│                                    │  连接: auto               │
│                                    │  ☐ ZeroCal                │     (ScrollViewer)
│                                    │  SettleMs: 1500           │
│                                    │  ─────────                │
│                                    │  目标色域                │
│                                    │  ☑ sRGB                  │
│                                    │  ☑ DCI-P3                │
│                                    │  ☑ AdobeRGB              │
│                                    │  ☑ Rec.2020              │
│                                    │  ─────────                │
│                                    │  通过条件                │
│                                    │  Max ΔE2000: 5           │
│                                    │  Min coverage: 0.9       │
├──────────────────────────────────────────────────────────────┤
│ 已加载 · 4 个 patch                                            │  ← 状态栏
└──────────────────────────────────────────────────────────────┘
```

## 关键交互

### Patch 列表

| 列 | 行为 |
|---|---|
| **ID** | 直接编辑（不能为空，重复会在保存时给同 id 加 `-N` 后缀） |
| **色样** | 实时根据 HEX 渲染：背景 = 当前 HEX；前景文字用 luma 选黑/白保持可读性 |
| **HEX** | 直接编辑，支持 `RGB` / `RRGGBB` / `#RGB` / `#RRGGBB` 多种输入（自动归一化为 `#RRGGBB` 大写） |
| **颜色…** | 打开 Windows ColorDialog（FullOpen 模式）选色，选完自动写回 HEX |
| **Avg** | 直接编辑（>0 整数） |
| **Label** | 直接编辑 |

| 操作 | 快捷键 / 工具栏 |
|---|---|
| 新增 patch（默认 `#888888` / `avg=8`） | `Insert` 键 或工具栏 "+ Patch" |
| 上下移动 patch | 工具栏 `↑` / `↓`（按选中行上下 1 位） |
| 删除选中 patch | `Delete` 键 或工具栏 "删除" |
| 加载现存 recipe | `Ctrl+O` 或菜单 文件→打开 |
| 保存 | `Ctrl+S`（无标题则弹另存为）|
| 另存为 | `Ctrl+Shift+S` |

### 设置面板

| 字段 | 说明 |
|---|---|
| 名称 | 出现在报告封面 |
| 连接 | 当前 v0 只识别 `auto` / `usb` |
| 测量前调零 | 勾上 → 跑 patch 前先调零 |
| SettleMs | 每 patch 切换后等待时间，ms |
| 目标色域 | 4 个复选框，可任意组合；空选 = 跑默认 sRGB（v1 提示） |
| Max ΔE2000 | 通过条件上限，留空 = 不参与判定 |
| Min coverage | 通过条件下限（0..1），留空 = 不参与判定 |

### Dirty 状态

未保存改动时，状态栏显示"已修改 (尚未保存) · N 个 patch"，并且 `New` / `Open` 会弹"放弃未保存的修改？"确认。已保存则显示"已加载 · N 个 patch"。

## JSON 兼容性

`RecipeSerializer` 写出的 JSON 与 CLI 的 `TestPlanParser` 用**同一份 schema**：

- `name` / `connect` / `calibrateBeforeStart` / `settleMs` / `targets` / `patches` / `pass`
- 字段命名：snake_case via `JsonNamingPolicy.SnakeCaseLower`
- patch 字段：`id` / `hex` / `avg` / `label`
- pass 字段：`max_delta_e2000` / `min_coverage`
- `pass` 整段缺省时**不写**（与 CLI 行为一致）
- 缺 `pass` key 的旧 recipe：加载时回退到 max=5.0、min=0.90

往返测试见 `tests/ColorMeasure.Host.Wpf.Tests/RecipeSerializerTests.cs`（4 case）。

## JSON 写入的微细节

- `hex` 永远写成 `#RRGGBB` 大写形式
- `label=null` 时**不写**该字段
- 整 `pass` 都不需要时**不写** `pass` 块
- 字段顺序按 Recipe.cs 的属性声明顺序稳定输出（System.Text.Json 默认行为）

## v1 已知限制（v2 计划）

| 限制 | 原因 | v2 计划 |
|---|---|---|
| 不能从 UI 跑测量 | v1 只做编辑 | 把 `MeasurementWorkflow` 注入 ViewModel，加 "Run" 按钮 + 进度条 + 结果内嵌展示 |
| 不能拖拽重排 | DataGrid 没启用拖拽 | 启用 `CanUserReorderColumns` + 集合重排 |
| 没有色度图预览 | 渲染比较重 | 嵌个小型 xy 缩略图（XyDiagramRenderer 缩到 200×150）|
| 没有撤销/重做 | v1 不做 | 加 `IUndoService` |

## 与 CLI 的关系

| 用例 | 推荐 |
|---|---|
| 一次性跑、CI/脚本 | CLI |
| 调试 recipe / 找好看的 patch 顺序 / 给非工程师分享 | WPF 编辑器 |
| 完整流程 | WPF 编辑器保存 → CLI 跑 |

CLI 和 WPF 走**同一个** `TestPlan` 抽象，WPF 存的 JSON 直接喂 `color-measure --plan x.json`。
