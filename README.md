# ColorMeasure

显示器色彩 QA 工具，基于 Konica Minolta CA-410 SDK。

读取一份声明式 JSON 配方（patches + 测量条件 + 目标色域），驱动 CA-410 采集每个
patch 的 XYZ/亮度/色温等数据，对比 sRGB / DCI-P3 / AdobeRGB / Rec.2020 等
标准色域，输出 CSV / XLSX / PDF / **自包含交互式 HTML 报告**。

支持的能力：

- **色域覆盖率与 dE2000**：每个目标色域算 Coverage 和 R/G/B/W 的 ΔE2000
- **白点偏差 Δu'v' (W)**：CIE 1976 UCS 中白点到 D65 的距离
- **Gamma 拟合**：对灰阶 ramp 做 log-log OLS 拟合 γ、a、R²、Black/White cd/m²
- **信号链路体检**：自动判断本次数据是否还代表面板原生色彩特性（识别 Windows HDR 开启导致的 SDR→HDR 转换改写）
- **CIE 1931 xy 色度图**：连续色彩场（每个 0.8% 栅格按 sRGB 等价色填色）、参考线、波长刻度
- **4 套输出文件**：CSV（原始样本）+ XLSX（摘要）+ PDF（A4 报告）+ **HTML（自包含交互式）**
- **WPF 配方编辑器**（v1）：可视化编辑 JSON 配方，所见即所得的色彩选择器
- **GPU 全屏色板驱动**：自动把 patch 贴到屏幕上（不依赖外部 Pattern Generator）

## 必备环境

| 项目 | 要求 | 说明 |
|---|---|---|
| OS | Windows 10 / 11 **x64** | WPF + 加载 x64 native DLL，不支持 ARM64 / x86 |
| **.NET SDK** | **.NET 10 SDK**（10.0.3xx 或更高） | `global.json` 锁 `10.0.302` + `rollForward: latestFeature` → **.NET 8/9 SDK 会被直接拒绝**（报 "指定的 SDK 版本找不到"）。装 SDK 就够了，SDK 自带对应 runtime |
| **.NET Runtime** | **.NET 10 Desktop Runtime** | ⚠️ CLI 也要 Desktop Runtime（不是普通 .NET Runtime）——因为 GPU 全屏色板用的是 WPF。只装 .NET Runtime 会报 "缺少 Microsoft.WindowsDesktop.App 10.0.0" |
| **CA-410 SDK** | 装在 `C:\Program Files (x86)\KONICA MINOLTA\CA-S40` | **运行时必需**，不只是开发依赖：它提供 USB 驱动（`Driver\KMMIUSB.INF`，让 Windows 能枚举到探针）和 `CA200Srvr.dll`（COM server） |
| 探针 | CA-410 通过 USB 连接 | 同时间占用探针的 **CA-S40 主程序需要关掉**（两者会抢设备） |

> **为什么 CLI 要 Desktop Runtime？** `ColorMeasure.Host.Cli` 引用了 Infrastructure 里的
> `GpuPaletteDisplayDriver`（WPF 全屏窗口，用来把 patch 贴到屏幕上）。所以 CLI 的
> `runtimeconfig.json` 同时声明了 `Microsoft.NETCore.App` 和 `Microsoft.WindowsDesktop.App`。
> 发布自包含版本可以绕开这个依赖：
> `dotnet publish src\ColorMeasure.Host.Cli -c Release -r win-x64 --self-contained true`

### 环境自检

```powershell
# 1) SDK 版本（必须输出 10.0.3xx 或更高；8.x/9.x 会报错）
dotnet --version

# 2) Desktop Runtime 是否在位（必须有 10.0.x 的 WindowsDesktop.App）
dotnet --list-runtimes | Select-String WindowsDesktop

# 3) CA-410 SDK 是否装好
Test-Path 'C:\Program Files (x86)\KONICA MINOLTA\CA-S40\CA-SDK2\x64\lib\CASDK2Net.dll'

# 4) 探针是否被系统枚举到（应在"设备管理器 → 通用串行总线控制器"下看到 CA-410）
Get-PnpDevice -PresentOnly | Where-Object { $_.FriendlyName -match 'CA-?410' } | Format-Table Status, FriendlyName
```

## 依赖安装

### NuGet 包（自动还原，无需手动装）

首次 `dotnet build` / `dotnet restore` 会自动从 nuget.org 还原 **19 个直接依赖**：

| 项目 | 包 |
|---|---|
| CLI | `System.CommandLine` 2.0.0-beta4、`Microsoft.Extensions.Hosting` 9.0、`Microsoft.Extensions.Logging.Console` 9.0、`Serilog.Extensions.Hosting` 8.0、`Serilog.Sinks.Console` 6.0 |
| Infrastructure | `CsvHelper` 33.0、`ClosedXML` 0.104、`QuestPDF` 2024.12、`SkiaSharp` 3.119、`Serilog` 4.0.2、`Microsoft.Extensions.DependencyInjection.Abstractions` 9.0、`Microsoft.Extensions.Options.ConfigurationExtensions` 9.0、`Microsoft.Extensions.Logging.Abstractions` 9.0 |
| WPF | `CommunityToolkit.Mvvm` 8.4、`System.Drawing.Common` 9.0（ColorDialog 用；NU1510 警告无害） |
| Application | `Microsoft.Extensions.Logging.Abstractions` 9.0 |
| 全部测试项目 | `Microsoft.NET.Test.Sdk` 17.12、`xunit` 2.9.2、`xunit.runner.visualstudio` 2.8.2、`FluentAssertions` 6.12.1 |

> SkiaSharp 3.x 的 native 资产（`libSkiaSharp.dll`）随包走，自动落在
> `bin\...\runtimes\win-x64\native\`，不需要额外装。

**离线 / 内网环境**：

```powershell
# 在有网机器上预还原，再拷过去
dotnet restore ColorMeasure.sln --packages D:\nuget-cache
# 目标机器
dotnet restore ColorMeasure.sln --packages D:\nuget-cache --source D:\nuget-cache
# 或设置全局离线源
dotnet nuget add source D:\nuget-cache -n local
```

### CA-SDK2 原生 DLL（已 vendor，无需额外操作）

仓库 `native/` 目录已包含 6 个必需 DLL，构建时自动复制到输出目录（`CASDK2Net.dll` 是托管包装，其余 5 个是原生）：

```
native\CASDK2.dll        native\CASDK2Calc.dll    native\CASDK2Comm.dll
native\CASDK2Net.dll     native\CASDK2Socket.dll  native\CA200Srvr.dll
```

`src\ColorMeasure.Infrastructure\ColorMeasure.Infrastructure.csproj` 和
`src\ColorMeasure.Host.Cli\ColorMeasure.Host.Wpf.csproj` 都挂了
`<None Include="..\..\native\*.dll"><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory></None>`，
**CLI 端必须自己再链一遍**——`dotnet run` 默认按 AnyCPU 输出到 `bin\Debug\...`，
而 Infrastructure 被 `<Platforms>x64</Platforms>` 固定到 `bin\x64\Debug\...`，
不重复链一次会报 `FileNotFoundException: CASDK2Net.dll`。

## 快速开始

```powershell
# ── 0) 首次 clone 后先还原依赖（后续 build 会自动做，这一步可跳过）
dotnet restore ColorMeasure.sln

# ── 1) 构建 + 跑测试（62 个）
dotnet build ColorMeasure.sln -c Debug
dotnet test  ColorMeasure.sln -c Debug

# ── 2) dry-run：不接硬件，只解析 recipe，验证配置通不通
dotnet run --project src\ColorMeasure.Host.Cli -- `
  --plan recipes\color-space-coverage-srgb.json --dry-run

# ── 3) 真实测量（需 CA-410 已 USB 连接、CA-S40 主程序已关闭）
dotnet run --project src\ColorMeasure.Host.Cli -- `
  --plan recipes\color-space-coverage-srgb.json --out runs\demo

# ── 4) 打开交互式报告
start runs\demo\report.html
```

### 声明显示器信号链路（重要）

工具送的是 **8-bit sRGB SDR** 信号。如果 Windows HDR 或显示器 OSD 的 HDR 模式开着，
这个信号会被 SDR→HDR 转换改写，测到的色域/亮度就不代表面板原生值了。
**声明当前状态**，工具会在报告里给出体检结论：

```powershell
# SDR 模式（Windows HDR 关 + 显示器 OSD HDR 关）
color-measure --plan recipes\gamma-ramp.json --out runs\sdr --display-state sdr

# HDR 模式
color-measure --plan recipes\gamma-ramp.json --out runs\hdr --display-state hdr
```

也可以写进配方（`recipes/*.json` 里的 `displayState` 字段），WPF 编辑器里也有对应下拉框。
体检结论会出现在 CLI 日志和 HTML 报告顶部的「信号链路体检」卡片里。
详见 [`docs/color-science.md#信号链路体检link-health`](docs/color-science.md#信号链路体检link-health)。

### 直接跑 exe（不想每次 build）

```powershell
# 走 dotnet build ColorMeasure.sln 的产物（x64 平台）
src\ColorMeasure.Host.Cli\bin\x64\Debug\net10.0-windows\color-measure.exe `
  --plan recipes\color-space-coverage-srgb.json --out runs\demo
```

> **两个输出目录的区别**——这是最容易踩的坑：
>
> | 你执行的命令 | 输出目录 |
> |---|---|
> | `dotnet build ColorMeasure.sln -c Debug` | `bin\x64\Debug\net10.0-windows\`（solution 配置是 `Debug\|x64`） |
> | `dotnet run --project src\ColorMeasure.Host.Cli` | `bin\Debug\net10.0-windows\`（单项目默认 AnyCPU） |
>
> 两个目录**都**能正常跑（native DLL 都拷齐了），但**内容不是同一份**。
> 混用会拿到旧构建。**建议统一用 `dotnet run --project`**，路径由 SDK 管理。

### 打开 WPF 配方编辑器

```powershell
dotnet run --project src\ColorMeasure.Host.Wpf
```

编辑器保存的 JSON 与 CLI 用同一份 schema，可直接喂给 `--plan`。
详见 [`docs/wpf-editor.md`](docs/wpf-editor.md)。

> CLI 端只产出**当前 recipe 已定义
> 的色域**——recipe 里没列的色域不会分析。

## 仓库结构

```
ColorMeasure.sln
├── src/
│   ├── ColorMeasure.Domain          实体 + 抽象接口（无外部依赖）
│   ├── ColorMeasure.Application     recipe 解析、色域分析、gamma、workflow
│   ├── ColorMeasure.Infrastructure  CA-SDK2 适配、GPU 全屏驱动、报表
│   ├── ColorMeasure.Host.Cli        CLI 入口（color-measure.exe）
│   └── ColorMeasure.Host.Wpf        WPF 配方编辑器（v1）
├── tests/                           75 个测试，覆盖 Ciede2000 / ColorAnalyzer / Gamma /
│                                   LinkHealth / TestPlanParser / HtmlReport /
│                                   CsvReport / RecipeSerializer
├── native/                           vendor 的 CASDK2*.dll + CA200Srvr.dll
├── recipes/                          测试配方 JSON 示例
└── docs/                              详细文档
    ├── architecture.md               系统设计、SDK 适配、模块划分
    ├── usage.md                      recipe JSON schema、CLI 参数
    ├── reports.md                    输出文件结构
    ├── wpf-editor.md                 WPF 配方编辑器使用
    ├── color-science.md              Δu'v' / ΔE2000 / gamma / 色温 背景
    └── recipes.md                    recipe 写作指南 + 3 套典型配方
```

## 文档导航

| 文档 | 内容 |
|---|---|
| [`docs/architecture.md`](docs/architecture.md) | Clean Architecture 分层、SDK 适配、模块划分 |
| [`docs/usage.md`](docs/usage.md) | recipe JSON schema + CLI 全部参数 |
| [`docs/reports.md`](docs/reports.md) | CSV / XLSX / PDF / HTML 4 套输出文件结构 |
| [`docs/wpf-editor.md`](docs/wpf-editor.md) | WPF 配方编辑器（v1）使用 |
| [`docs/color-science.md`](docs/color-science.md) | Δu'v'、ΔE2000、gamma、色温、**信号链路体检** |
| [`docs/recipes.md`](docs/recipes.md) | 3 套典型配方 + 写作技巧 |

## 关键术语

- **Coverage**：实测三原色三角形面积 / 目标色域三角形面积，clamp 到 1.0
- **Max ΔE2000**：实测 R / G / B 三个原色到标准原色的 CIEDE2000 最大值
- **Δu'v' (W)**：CIE 1976 UCS 中实测白点到 D65 (u'=0.1978, v'=0.4683) 的距离
- **Gamma**：log-log 线性拟合 Y = a·V^γ 的斜率；理想 sRGB ≈ 2.2，BT.1886 ≈ 2.4

详见 [docs/color-science.md](docs/color-science.md)。

## 现状

- **v0.1+**（已实现）：色域覆盖率 + ΔE2000 + Δu'v'(W) + Gamma 拟合 + **信号链路体检** + CIE 1931 连续色彩场 + 4 套报告 + WPF 配方编辑器
- **v1**（路线图）：WPF 内直接运行测量（Workflow 注入）、Flicker（JEITA/FMA/波形）、DDC/CI 控制、外部 Pattern Generator、ICC profile 输出、**真 HDR 信号输出（DXGI swapchain）** |

## 致谢与引用

- CIE 1931 (x, y) 与 CIE 1976 UCS (u', v') 标准源自 **CIE Publication 015**（Colorimetry）
- CIEDE2000 源自 **CIE 142:2001**；实现参考 **Wyszecki & Stiles, "Color Science" (2nd ed., 2000)**
- 仓库根目录 `cie1931xyz.png` 是 CIE 1931 色度图的参考示意图（用户提供的对比图），其色度图绘制风格作为本工具 xy 图的视觉基准
- CA-SDK2 native DLL 由 Konica Minolta 提供，vendor 自 `C:\Program Files (x86)\KONICA MINOLTA\CA-S40\CA-SDK2`

## 许可

工具代码：MIT。CA-SDK2 native DLL：vendor 自 Konica Minolta，按其 EULA 使用。
