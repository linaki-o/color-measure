# ColorMeasure

显示器色彩 QA 工具，基于 Konica Minolta CA-410 SDK。

读取一份声明式 JSON 配方（patches + 测量条件 + 目标色域），驱动 CA-410 采集每个
patch 的 XYZ/亮度/色温等数据，对比 sRGB / DCI-P3 / AdobeRGB / Rec.2020 等
标准色域，输出 CSV / XLSX / PDF / **自包含交互式 HTML 报告**。

支持的能力：

- **色域覆盖率与 dE2000**：每个目标色域算 Coverage 和 R/G/B/W 的 ΔE2000
- **白点偏差 Δu'v' (W)**：CIE 1976 UCS 中白点到 D65 的距离
- **Gamma 拟合**：对灰阶 ramp 做 log-log OLS 拟合 γ、a、R²、Black/White cd/m²
- **CIE 1931 xy 色度图**：连续色彩场（每个 0.8% 栅格按 sRGB 等价色填色）、参考线、波长刻度
- **3 套报表**：CSV（原始）+ XLSX（摘要）+ PDF/A4；**HTML 报告支持交互**（切换图层、悬停标点、点击展开）
- **WPF 配方编辑器**（v1）：可视化编辑 JSON 配方，所见即所得的色彩选择器
- **GPU 全屏色板驱动**：自动把 patch 贴到屏幕上（不依赖外部 Pattern Generator）

## 快速开始

```powershell
# 构建 + 测试
dotnet build ColorMeasure.sln -c Debug
dotnet test  ColorMeasure.sln -c Debug

# 跑一次 dry-run（不接硬件，只解析 recipe）
src\ColorMeasure.Host.Cli\bin\x64\Debug\net10.0-windows\color-measure.exe `
  --plan recipes\color-space-coverage-srgb.json --dry-run

# 接 CA-410 真实测量
src\ColorMeasure.Host.Cli\bin\x64\Debug\net10.0-windows\color-measure.exe `
  --plan recipes\color-space-coverage-srgb.json --out runs\demo
```

> 第一次跑前确保 `native\CASDK2*.dll` 已 vendor（已包含）。CLI 端只产出**当前 recipe 已定义
> 的色域**——recipe 里没列的色域不会分析。

## 必备环境

| 项目 | 要求 |
|---|---|
| OS | Windows 10/11 x64 |
| SDK | Konica Minolta CA-410 SDK 装在 `C:\Program Files (x86)\KONICA MINOLTA\CA-S40` |
| .NET | .NET 8 SDK 或更新（在 .NET 10 SDK 上构建/运行） |
| 探针 | CA-410 通过 USB 连接；同时间占用探针的 CA-S40 主程序需要关掉 |

## 仓库结构

```
ColorMeasure.sln
├── src/
│   ├── ColorMeasure.Domain          实体 + 抽象接口（无外部依赖）
│   ├── ColorMeasure.Application     recipe 解析、色域分析、gamma、workflow
│   ├── ColorMeasure.Infrastructure  CA-SDK2 适配、GPU 全屏驱动、报表
│   ├── ColorMeasure.Host.Cli        CLI 入口（color-measure.exe）
│   └── ColorMeasure.Host.Wpf        WPF 配方编辑器（v1）
├── tests/                           62 个测试，覆盖 Ciede2000 / ColorAnalyzer / Gamma /
│                                   HtmlReport / RecipeSerializer
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
| [`docs/color-science.md`](docs/color-science.md) | Δu'v'、ΔE2000、gamma、色温概念与判读 |
| [`docs/recipes.md`](docs/recipes.md) | 3 套典型配方 + 写作技巧 |

## 关键术语

- **Coverage**：实测三原色三角形面积 / 目标色域三角形面积，clamp 到 1.0
- **Max ΔE2000**：实测 R / G / B 三个原色到标准原色的 CIEDE2000 最大值
- **Δu'v' (W)**：CIE 1976 UCS 中实测白点到 D65 (u'=0.1978, v'=0.4683) 的距离
- **Gamma**：log-log 线性拟合 Y = a·V^γ 的斜率；理想 sRGB ≈ 2.2，BT.1886 ≈ 2.4

详见 [docs/color-science.md](docs/color-science.md)。

## 现状

- 0.1+：色域覆盖 + ΔE2000 + Δu'v' + Gamma
- 1.0+（路线图）：Flicker、JSD/CI 校准、外部 Pattern Generator、ICC profile 输出、GUI 内直接运行测量

## 致谢

CIE 1931 / 1976 UCS 标准源自 CIE Publication 015。`cie1931xyz.png` 参考图由 Zhihu 公开资源。

## 许可

工具代码：MIT。CA-SDK2 native DLL：vendor 自 Konica Minolta，按其 EULA 使用。
