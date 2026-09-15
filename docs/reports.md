# 报告文件

每次运行（`--out <dir>`）默认生成 4 个文件。同一目录可放多份历史报告，HTML 不依赖外部资源，PDF/XLSX/CSV 是只读快照。

## `samples.csv`

CSV，CsvHelper 写出，列：

```
patch_id, x, y, Lv_cd_m2, X, Y, Z, duv, Tcp_K, probe_serial, measured_at_utc
```

- `x` `y`：CIE 1931 色度坐标
- `Lv_cd_m2`：亮度（cd/m²）
- `X` `Y` `Z`：CIE XYZ 三刺激值（Y = Lv）
- `duv`：偏离黑体轨迹距离
- `Tcp_K`：相关色温（K）
- `probe_serial`：探针 SN
- `measured_at_utc`：ISO-8601 时间戳

每个 gamut report 一段后置（target / channel / dE2000 / coverage / white_duv / passed）。

## `summary.xlsx`

ClosedXML 写出，两个 sheet：

- `samples`：与 samples.csv 一致（patch_id / x / y / Lv / Tcp / duv）
- `gamut`：target / coverage / white_dE_uv / max_primary_dE2000 / passed

**当前不含嵌入图表**——xy 图只在 PDF / HTML 里。要看图请打开 `report.html`。

## `report.pdf`

QuestPDF 生成，A4 横向布局：

| 区域 | 内容 |
|---|---|
| 封面 | 计划名 + 探针 SN + 开始时间 |
| CIE 1931 xy 图 | SkiaSharp 绘制：连续色彩场（每个 0.8% 栅格按 sRGB 等价值填色）+ spectral locus + 460-620nm 波长刻度 + 每个 target 实线轮廓（R/G/B primary 渐变填充）+ 同色暗 30% 虚线（实测）+ 独立配色（sRGB=蓝 / DCI-P3=绿 / AdobeRGB=橙 / Rec.2020=红）|
| Gamma 曲线 | γ / a / R² / 黑场 / 白场 / 样本数 + log-log SVG 图（γ=1.8/2.4 参考线 + 测量点）|
| 色域分析表 | target / coverage / Δu'v' (W) / max dE2000 / R/G/B dE2000 / Result |
| 样本表 | patch / x / y / Lv / Tcp |

## `report.html`（推荐先看这个）

单文件自包含（约 17 KB），**无 CDN / 无外部请求**——双击或邮件附件都能打开。

可交互元素：

| 区域 | 行为 |
|---|---|
| 工具栏按钮（每个 target 一个） | 点击切换该 target 在色度图上的可见性；按钮内嵌 RGB micro-dots + cov% + PASS/FAIL 徽章 |
| 色域分析表行 | 点击 → 高亮该行 + 自动开启被关掉的图层 |
| 样本行 hover | 在 SVG 上画橙色圆环 + 文字标记 `(id, x.xxx, y.yyy)` |
| 样本行 click | 下方详情面板展开：cx / cy / Lv / Tcp / Δu'v / Probe SN / triX / triY / triZ |
| 顶部"Coverage (target vs measured)" 提示 | 帮理解透 / 虚线关系 |

### 配色约定

| Target | 颜色 | 实线 / 虚线 |
|---|---|---|
| sRGB | `#1f77b4`（蓝） | 实线 = 目标；虚线（6/4 dash）= 实测 |
| DCI-P3 | `#2ca02c`（绿） | 同上 |
| AdobeRGB | `#ff7f0e`（橙） | 同上 |
| Rec.2020 | `#d62728`（红） | 同上 |

测量值用同色 30% 加深，让同一 hue 的目标/实测两层清晰区分。

### CIE 1931 色度图细节

- **x 轴**：0.0 – 0.8（每 0.1 一格，与所有 CIE 1931 xy 数据范围一致）
- **y 轴**：0.0 – 0.9（每 0.1 一格）
- **每个网格格** ≈ 70×70 px（HTML/PDF 同步），0.1 chromaticity 单位在两轴上同尺寸
- **连续色彩场**：每格按 (x, y) → XYZ(Y=1) → linear sRGB → gamma 计算
- **波长刻度**：460 / 480 / 500 / 520 / 540 / 560 / 580 / 600 / 620 nm（向外指向，文字随位置自动选 left/right/center）
- **spectral locus**（外边界）：用 Catmull-Rom → 二次贝塞尔曲线渲染，C1 连续
- **line of purples**（底边）：实线紫红色（`#5a2a6e`）

### Gamma 曲线细节

- **x 轴（log）**：0.01 / 0.1 / 1.0 三档
- **y 轴（linear）**：0.00 / 0.25 / 0.50 / 0.75 / 1.00
- **理想参考线**：γ=1.8、γ=2.4 虚线
- **测量点**：蓝色实心圆（半径 3.5px，白边）
- **R² 解读**：≥ 0.99 算 PASS，0.95-0.99 OK，< 0.95 FAIL

### HTML 报告的健壮性

- 没有 NaN/Infinity（用 `San()` 在 DTO 阶段把非有限值替换为 `null`，前端用 `—` 显示）
- 没有外部依赖（CSS / JS / SVG 全部 inline）
- 没有 popup（XAML 错误吞 stderr 后只显示无窗口——CLI/HTML 不受影响）
- 没有登录、没有授权
- IE 不支持（用现代 CSS `flex` / `grid`）

## 字段对照

| 报告位置 | 字段 | 含义 | 详细 |
|---|---|---|---|
| CSV / XLSX / HTML | `coverage` | 实测三原色面积 / 目标面积，0..1 | [color-science.md#coverage](color-science.md#coverage) |
| CSV / XLSX / HTML | `max_dE2000` | R/G/B 中 ΔE2000 最大值 | [color-science.md#dE2000](color-science.md#dE2000) |
| CSV / XLSX / HTML | `delta_uv_W` | 实测白点到 D65 的 u'v' 距离 | [color-science.md#duvw](color-science.md#duvw) |
| HTML（Gamma 卡） | `gamma` / `scaleA` / `RSquared` | Y=a·V^γ 三参数 + 拟合优度 | [color-science.md#gamma](color-science.md#gamma) |
| HTML（样本详情） | `triX/Y/Z` | 原始 CIE XYZ 三刺激值 | — |

## 报告存证建议

1. **记录当时屏幕的色温预设值**——`Δu'v' (W)` 是和 D65 比的，色温不同不可横向比
2. **关掉显示器侧的所有"色温自动"模式**（EyeCare / 护眼 / 暖色）——测的是"屏幕当前模式"而不是"屏幕的物理色准"
3. **同时保留原色测量数据**——只看 HTML 的色域图容易丢失数值证据，CSV 是 ground truth
