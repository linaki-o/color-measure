# 报告文件

每次运行（`--out <dir>`）默认生成 4 个文件。同一目录可放多份历史报告，HTML 不依赖外部资源，PDF/XLSX/CSV 是只读快照。

## samples.csv

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

## summary.xlsx

ClosedXML 写出，两个 sheet：

- `samples`：与 samples.csv 一致（patch_id / x / y / Lv / Tcp / duv）
- `gamut`：target / coverage / white_dE_uv / max_primary_dE2000 / passed

**当前不含嵌入图表**——xy 图只在 PDF / HTML 里。要看图请打开 `report.html`。

## report.pdf

QuestPDF 生成，A4 横向布局：

| 区域 | 内容 |
|---|---|
| 封面 | 计划名 + 探针 SN + 开始时间 |
| CIE 1931 xy 图 | SkiaSharp 绘制：spectral locus 灰线 + 每个 target 实线+浅填充 + 同色虚线实测 + 独立配色（sRGB=蓝 / DCI-P3=绿 / AdobeRGB=橙 / Rec.2020=红） |
| 图例（右上） | 每行：target 实色块 + measured 虚线块 + name + cov% + PASS/FAIL |
| 色域分析表 | target / coverage / Δu'v'(W) / max dE2000 / R/G/B dE2000 / Result |
| 样本表 | patch / x / y / Lv / Tcp |

## report.html

单文件自包含（约 17 KB），**无 CDN / 无网络请求**，双击或邮件附件都能打开。原生 SVG + 原生 JS。

### 交互

| 元素 | 行为 |
|---|---|
| 工具栏按钮（每个 target 一个） | 点击切换该 target 在色度图上的可见性；按钮内嵌 PASS/FAIL 徽章 |
| 色域分析表行 | 点击 → 高亮该行 + 自动开启被关掉的图层 |
| 样本行 hover | 在 SVG 上画橙色圆环 + 文字标记 `(id, x.xxx, y.yyy)` |
| 样本行 click | 在下方详情面板展开：cx / cy / Lv / Tcp / Δu'v / Probe SN / triX / triY / triZ |
| 顶部"Coverage (target vs measured)" 提示 | 帮助用户理解实线 vs 虚线 |

### 配色约定

| Target | 颜色 | 实线 / 虚线 |
|---|---|---|
| sRGB | #1f77b4（蓝） | 实线 = 目标；虚线（6/4 dash）= 实测 |
| DCI-P3 | #2ca02c（绿） | 同上 |
| AdobeRGB | #ff7f0e（橙） | 同上 |
| Rec.2020 | #d62728（红） | 同上 |

测量值使用的色比目标值深 30%，让同一 hue 的两层清晰区分。
