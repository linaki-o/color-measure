# ColorMeasure

自动化显示器色彩 QA 工具，基于 Konica Minolta CA-410 SDK。

`color-measure` 接收一份声明式 JSON 测试计划，自动把每个 patch 贴到屏幕上、用 CA-410
测一遍，对比 sRGB / DCI-P3 / AdobeRGB / Rec.2020 等目标色域，输出 CSV / XLSX / PDF
/ 自包含交互式 HTML 报告。

> v0 范围：色域覆盖率 + USB AutoConnect + GPU 全屏色板 + 全套报告。
> 见 [`docs/architecture.md`](docs/architecture.md) 的 §0 "v0 Scope"。

---

## 1. 环境要求

| 项 | 要求 |
|---|---|
| OS | Windows 10/11 x64 |
| SDK | Konica Minolta CA-410 SDK 安装在 `C:\Program Files (x86)\KONICA MINOLTA\CA-S40` |
| .NET | .NET 8 SDK 或更新（实测在 .NET 10 SDK / 8.0 runtime 上跑通） |
| 探针 | CA-410 通过 USB 连接，关闭同时占用探针的 CA-S40 软件 |

## 2. 解决方案布局

```
ColorMeasure.sln
├── src/
│   ├── ColorMeasure.Domain          纯实体 + 抽象接口（无外部依赖）
│   ├── ColorMeasure.Application     计划解析、色域分析、workflow
│   ├── ColorMeasure.Infrastructure  CA-SDK2 适配、GPU 全屏驱动、报告
│   ├── ColorMeasure.Host.Cli        color-measure 命令行入口
│   └── ColorMeasure.Host.Wpf        WPF GUI 占位（v1 待填充）
├── tests/
│   ├── ColorMeasure.Domain.Tests           ΔE2000 数学边界用例
│   ├── ColorMeasure.Application.Tests      测试计划解析、分析逻辑、Sharma 测试向量
│   └── ColorMeasure.Infrastructure.Tests   HTML 报告快照
├── native/                       vendor 的 CASDK2*.dll + CA200Srvr.dll
├── recipes/                      测试计划 JSON 样例
└── docs/                          设计 / 用法 / 报告字段说明
```

## 3. 构建与测试

```powershell
dotnet build ColorMeasure.sln -c Debug
dotnet test  ColorMeasure.sln -c Debug
```

需要先安装的 NuGet 包（首次 `dotnet build` 会自动还原）：
`CsvHelper`, `ClosedXML`, `QuestPDF`, `SkiaSharp`, `Serilog`, `xUnit`, `System.CommandLine`。

## 4. 运行

最小可执行路径在：

```
src\ColorMeasure.Host.Cli\bin\x64\Debug\net10.0-windows\color-measure.exe
```

直接调用：

```powershell
# 推荐方式：dotnet run（自动 build）
dotnet run --project src\ColorMeasure.Host.Cli -- `
  --plan recipes\color-space-coverage-srgb.json `
  --out runs\demo

# 或先 build 再走裸 exe（产物路径稳定，避免 dotnet run 找不到 AnyCPU 输出）
& src\ColorMeasure.Host.Cli\bin\x64\Debug\net10.0-windows\color-measure.exe `
  --plan recipes\color-space-coverage-srgb.json `
  --out runs\demo

# 只解析计划、不连硬件（冒烟测试 recipe JSON）
dotnet run --project src\ColorMeasure.Host.Cli -- `
  --plan recipes\color-space-coverage-srgb.json --dry-run

# 把当前会话的输出目录加进 PATH，之后可以裸调 color-measure
$env:PATH += ";$PWD\src\ColorMeasure.Host.Cli\bin\x64\Debug\net10.0-windows"
color-measure --plan recipes\color-space-coverage-srgb.json --dry-run
```

### 4.1 CLI 选项

```
--plan <path>     测试计划 JSON（必填）
--out <dir>       输出目录（默认 runs\<时间戳>）
--connect <mode>  auto|usb（默认 auto，目前等价于 usb）
--dry-run         只解析 plan，不连硬件
--version
-? | -h | --help
```

退出码：`0` 成功；`1` 运行失败（错误码会出现在日志里：`NO_SAMPLES`、`MISSING_PRIMARIES`、`SDK_*` 等）。

## 5. 输出（默认在 `<--out>` 目录）

| 文件 | 内容 | 备注 |
|---|---|---|
| `samples.csv` | 原始样本：patch / x / y / Lv / X / Y / Z / duv / Tcp / probe SN / time | CsvHelper |
| `summary.xlsx` | samples + gamut 两张表 | ClosedXML，**当前不含图**，只是数据 |
| `report.pdf` | 封面 + CIE 1931 xy 图（目标实线、实测虚线、独立配色）+ 分析表 | QuestPDF + SkiaSharp |
| `report.html` | **自包含交互式报告**，浏览器打开即可 | 见 [`docs/reports.md`](docs/reports.md) |

## 6. 配方（recipe）JSON 最小骨架

```json
{
  "name": "MyPlan",
  "connect": "auto",
  "calibrateBeforeStart": true,
  "settleMs": 1500,
  "targets": ["sRGB", "DCI-P3", "AdobeRGB", "Rec.2020"],
  "patches": [
    { "id": "white", "hex": "#FFFFFF", "avg": 8 },
    { "id": "red",   "hex": "#FF0000", "avg": 8 },
    { "id": "green", "hex": "#00FF00", "avg": 8 },
    { "id": "blue",  "hex": "#0000FF", "avg": 8 }
  ],
  "pass": { "max_delta_e2000": 5.0, "min_coverage": 0.90 }
}
```

完整字段说明见 [`docs/architecture.md`](docs/architecture.md) §5.1。

## 7. 故障排查速查

| 现象 | 排查 |
|---|---|
| `Could not load file or assembly 'CASDK2Net.dll'` | 用裸 exe 跑（`bin\x64\Debug\net10.0-windows\color-measure.exe`），或在 `src\ColorMeasure.Host.Cli\ColorMeasure.Host.Cli.csproj` 检查 `<None Include="..\..\native\*.dll">` 是否齐 |
| `rc=426 --Measurement fail.` | 探针与显示器未对准 / CA-S40 主程序占用中 / 显示器色彩增强未关 / Windows HDR/护眼色温开启 |
| `[NO_SAMPLES]` 退出码 1 | 全部测量失败。先做：单个白板测试 + 关闭显示器色彩增强 + 确认探针 USB 灯稳定 |
| `[MISSING_PRIMARIES] red, green, blue, white` | plan 里至少缺一个基本色 patch；检查 recipe 的 `patches` 数组 |
| `Coverage` 全是 0 | 探针测到的是环境光，不是 patch；屏幕未被 GPU palette 窗口覆盖、或者探针位置不对 |

## 8. 文档

- [`docs/architecture.md`](docs/architecture.md) — 架构设计、SDK 适配、模块划分、技术选型
- [`docs/usage.md`](docs/usage.md) — 测试计划 JSON 完整字段、CLI 进阶用法
- [`docs/reports.md`](docs/reports.md) — 报告文件结构 + HTML 交互功能
