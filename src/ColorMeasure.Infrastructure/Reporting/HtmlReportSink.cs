using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ColorMeasure.Domain.Abstractions;
using ColorMeasure.Domain.Entities;

namespace ColorMeasure.Infrastructure.Reporting;

/// <summary>
/// Emits a single-file interactive HTML report. No CDN, no network: the CIE xy diagram is
/// drawn from inline SVG by vanilla JS, and run data is baked into the page as JSON so the
/// file is fully portable. Interactivity:
///  - Toggle buttons show/hide each target's target+measured triangles on the diagram.
///  - Hovering a sample row plants a marker on the diagram at that sample's (x, y).
///  - Clicking a sample row expands a detail panel with XYZ/Lv/Tcp.
/// </summary>
public sealed class HtmlReportSink : IReportSink
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public Task WriteAsync(MeasurementRun run, string outputDirectory, CancellationToken ct)
    {
        Directory.CreateDirectory(outputDirectory);
        var path = Path.Combine(outputDirectory, "report.html");
        var html = Render(run);
        File.WriteAllText(path, html, Encoding.UTF8);
        return Task.CompletedTask;
    }

    private static string Render(MeasurementRun run)
    {
        var data = new ReportData
        {
            probeSerial = run.ProbeSerialNo,
            planName = run.PlanName,
            startedAt = run.StartedAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss"),
            finishedAt = run.FinishedAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss"),
            samples = run.Samples.Select(s => new SampleData
            {
                id = s.PatchId,
                x = San(s.Chromaticity.X),
                y = San(s.Chromaticity.Y),
                lv = San(s.Lv),
                triX = San(s.X),
                triY = San(s.Y),
                triZ = San(s.Z),
                duv = San(s.DuV),
                tcp = San(s.Tcp),
                measuredAt = s.MeasuredAt.UtcDateTime.ToString("HH:mm:ss"),
            }).ToList(),
            gamuts = run.GamutReports.Select(g => new GamutData
            {
                name = g.Target.Name,
                coverage = San(g.CoverageRatio),
                whiteDeltaUv = San(g.WhiteDeltaUv),
                maxDeltaE2000 = San(g.MaxPrimaryDeltaE2000),
                passed = g.Passed,
                primaryDeltaEs = g.PrimaryDeltaEs.Select(p => new PrimaryDelta { channel = p.Channel, dE = San(p.DeltaE2000) }).ToList(),
                target = new TrianglePoints
                {
                    r = Pt(g.Target.Red), g = Pt(g.Target.Green),
                    b = Pt(g.Target.Blue), w = Pt(g.Target.White),
                },
                measured = new TrianglePoints
                {
                    r = Pt(g.MeasuredRed), g = Pt(g.MeasuredGreen),
                    b = Pt(g.MeasuredBlue), w = Pt(g.MeasuredWhite),
                },
            }).ToList(),
        };
        var json = JsonSerializer.Serialize(data, JsonOpts);

        return $$"""
<!doctype html>
<html lang="zh-CN">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>ColorMeasure 报告 — {{HtmlEncode(run.PlanName)}}</title>
<style>
:root {
  --bg: #fafafa;
  --panel: #ffffff;
  --border: #e1e4e8;
  --text: #1f2328;
  --muted: #57606a;
  --accent: #0969da;
  --pass: #1a7f37;
  --fail: #cf222e;
  --hover: #f6f8fa;
  --row-stripe: #f9f9f9;
}
* { box-sizing: border-box; }
html, body { margin: 0; padding: 0; }
body {
  font: 14px/1.5 -apple-system, "Segoe UI", "PingFang SC", "Microsoft YaHei", sans-serif;
  background: var(--bg);
  color: var(--text);
  padding: 24px;
}
header {
  max-width: 1180px; margin: 0 auto 18px auto;
}
h1 { margin: 0 0 6px 0; font-size: 22px; }
.meta { color: var(--muted); font-size: 12px; }
.card {
  max-width: 1180px; margin: 0 auto 18px auto;
  background: var(--panel);
  border: 1px solid var(--border);
  border-radius: 8px;
  padding: 16px 20px;
}
h2 { margin: 0 0 12px 0; font-size: 16px; border-bottom: 1px solid var(--border); padding-bottom: 8px; }
.toolbar {
  display: flex; flex-wrap: wrap; gap: 8px; margin-bottom: 10px;
  align-items: center;
}
.toolbar .hint { font-size: 12px; color: var(--muted); margin-right: auto; }
.toggle {
  cursor: pointer;
  display: inline-flex; align-items: center; gap: 6px;
  padding: 4px 10px;
  border: 1.5px solid var(--c, #888);
  background: var(--bg);
  color: var(--text);
  border-radius: 16px;
  font-size: 12px;
  user-select: none;
  transition: opacity .15s ease;
}
.toggle.off { opacity: .35; }
.toggle .dot {
  width: 10px; height: 10px; border-radius: 50%; background: var(--c);
}
.toggle .vbadge {
  font-size: 10px; font-weight: 600;
  padding: 1px 6px; border-radius: 8px;
  color: white;
}
.toggle .vbadge.pass { background: var(--pass); }
.toggle .vbadge.fail { background: var(--fail); }
svg#diagram {
  width: 100%; height: auto; display: block;
  background: white;
  border: 1px solid var(--border);
  border-radius: 4px;
}
.legend-row {
  display: flex; flex-wrap: wrap; gap: 18px;
  font-size: 12px; color: var(--muted);
  margin-top: 8px;
}
.legend-row .item { display: inline-flex; align-items: center; gap: 6px; }
.swatch-line { display: inline-block; width: 22px; height: 3px; vertical-align: middle; }
.swatch-line.solid { background: #444; }
.swatch-line.dashed {
  background: repeating-linear-gradient(to right, #444 0 4px, transparent 4px 8px);
}
table { width: 100%; border-collapse: collapse; font-size: 13px; }
th, td { padding: 8px 10px; text-align: right; border-bottom: 1px solid var(--border); }
th:first-child, td:first-child { text-align: left; }
th { background: var(--row-stripe); font-weight: 600; color: var(--muted); }
tbody tr:hover { background: var(--hover); cursor: pointer; }
tbody tr.highlight { background: #fff8c5; }
td.pass { color: var(--pass); font-weight: 600; }
td.fail { color: var(--fail); font-weight: 600; }
.patch-chip {
  display: inline-block; width: 10px; height: 10px;
  border-radius: 2px; margin-right: 6px; vertical-align: middle;
  border: 1px solid rgba(0,0,0,0.15);
}
.detail-panel {
  margin-top: 12px;
  padding: 12px;
  background: var(--hover);
  border: 1px solid var(--border);
  border-radius: 4px;
  display: none;
  font-family: ui-monospace, Consolas, "Cascadia Mono", monospace;
  font-size: 12px;
  white-space: pre-wrap;
}
.detail-panel.show { display: block; }
.detail-panel .kv { display: grid; grid-template-columns: 130px 1fr 130px 1fr; gap: 4px 12px; }
.empty { color: var(--muted); font-style: italic; padding: 8px 0; }
.spectral-label { font-size: 9px; fill: var(--muted); }
.axis-label { font-size: 10px; fill: var(--muted); }
</style>
</head>
<body>
<header>
  <h1>ColorMeasure 报告</h1>
  <div class="meta">
    <span>计划: <b>{{HtmlEncode(run.PlanName)}}</b></span> ·
    <span>探针: {{HtmlEncode(run.ProbeSerialNo)}}</span> ·
    <span>样本: {{run.Samples.Count}}</span> ·
    <span>开始: {{run.StartedAt.LocalDateTime:yyyy-MM-dd HH:mm:ss}}</span>
  </div>
</header>

<section class="card">
  <h2>CIE 1931 色度图</h2>
  <div class="toolbar">
    <span class="hint">点击切换图层 · hover 样本行会在图上标点</span>
  </div>
  <svg id="diagram" viewBox="0 0 960 720" preserveAspectRatio="xMidYMid meet"></svg>
  <div class="legend-row">
    <span class="item"><span class="swatch-line solid"></span> 目标色域</span>
    <span class="item"><span class="swatch-line dashed"></span> 实测色域</span>
  </div>
</section>

  <section class="card">
    <h2>色域分析</h2>
    <table id="gamut-table">
      <thead>
        <tr>
          <th>Target</th>
          <th>Coverage</th>
          <th>Δu'v' (W)</th>
          <th>Max ΔE2000</th>
          <th>R dE2000</th>
          <th>G dE2000</th>
          <th>B dE2000</th>
          <th>Result</th>
        </tr>
      </thead>
      <tbody id="gamut-rows"></tbody>
    </table>
  </section>

  <section class="card">
    <h2>样本</h2>
    <table id="samples-table">
      <thead>
        <tr>
          <th>Patch</th>
          <th>cx</th>
          <th>cy</th>
          <th>Lv (cd/m²)</th>
          <th>X</th>
          <th>Y</th>
          <th>Z</th>
          <th>Tcp (K)</th>
          <th>Δu'v</th>
          <th>探针</th>
          <th>时刻</th>
        </tr>
      </thead>
      <tbody id="sample-rows"></tbody>
    </table>
    <div id="sample-detail" class="detail-panel"></div>
  </section>

<script>
const RUN = {{json}};

const palette = [
  '#1f77b4', '#2ca02c', '#ff7f0e', '#d62728',
  '#9467bd', '#8c564b', '#17becf', '#7f7f7f',
];
const MAX_X = 0.8, MAX_Y = 0.9;
const svgNS = 'http://www.w3.org/2000/svg';

function svg(name, attrs, parent) {
  const el = document.createElementNS(svgNS, name);
  for (const [k, v] of Object.entries(attrs)) el.setAttribute(k, v);
  (parent || document.getElementById('diagram')).appendChild(el);
  return el;
}

function toPixel(x, y, plot) {
  return {
    x: plot.x + (x / MAX_X) * plot.width,
    y: plot.y + (1 - y / MAX_Y) * plot.height,
  };
}

function buildDiagram() {
  const diagram = document.getElementById('diagram');
  diagram.innerHTML = '';
  const plot = { x: 50, y: 30, width: 880, height: 640 };

  // Grid
  const grid = svg('g', { stroke: '#e6e6e6', 'stroke-width': 1 }, diagram);
  for (let x = 0; x <= 0.8 + 1e-9; x += 0.1) {
    const p = toPixel(x, 0, plot);
    svg('line', { x1: p.x, y1: plot.y, x2: p.x, y2: plot.y + plot.height }, grid);
  }
  for (let y = 0; y <= 0.9 + 1e-9; y += 0.1) {
    const p = toPixel(0, y, plot);
    svg('line', { x1: plot.x, y1: p.y, x2: plot.x + plot.width, y2: p.y }, grid);
  }

  // Spectral locus with wavelength tags + purple limit. Last entry has nm == 0.
  const locus = [
    [0.1741,0.0050,380],[0.1738,0.0049,390],[0.1733,0.0048,400],[0.1726,0.0048,410],
    [0.1714,0.0051,420],[0.1703,0.0058,430],[0.1689,0.0069,440],[0.1669,0.0086,450],
    [0.1644,0.0109,460],[0.1611,0.0138,470],[0.1566,0.0177,480],[0.1510,0.0227,490],
    [0.1440,0.0297,500],[0.1355,0.0389,510],[0.1241,0.0578,520],[0.1096,0.0868,530],
    [0.0913,0.1327,540],[0.0687,0.2007,550],[0.0454,0.2950,560],[0.0235,0.4127,570],
    [0.0082,0.5384,580],[0.0039,0.6548,590],[0.0139,0.7502,600],[0.0389,0.8120,610],
    [0.0743,0.8338,620],[0.1142,0.8262,630],[0.1547,0.8059,640],[0.1929,0.7816,650],
    [0.2296,0.7543,660],[0.2658,0.7243,670],[0.3016,0.6923,680],[0.3373,0.6589,690],
    [0.3731,0.6245,700],[0.4087,0.5896,710],[0.4441,0.5547,720],[0.4788,0.5202,730],
    [0.5125,0.4866,740],[0.5448,0.4544,750],[0.5755,0.4242,760],[0.6042,0.3965,770],
    [0.7347,0.2653,0], // purple limit
  ];

  // Pre-compute pixel positions and the smooth-path 'd' string once; reuse for
  // fan fill + the outline on top.
  const points = locus.map(p => toPixel(p[0], p[1], plot));

  // Fan wedges meeting at midpoints → the colored fill has a C1 outer boundary.
  const wp0 = toPixel(0.3127, 0.3290, plot);
  const fanGroup = svg('g', { id: 'horseshoe-fan' }, diagram);
  for (let i = 0; i < locus.length - 1; i++) {
    const w2 = locus[i + 1][2];
    const fill = w2 === 0 ? '#780082' : wavelengthToColor((locus[i][2] + w2) / 2);
    const mx = (points[i].x + points[i + 1].x) / 2;
    const my = (points[i].y + points[i + 1].y) / 2;
    const tri = document.createElementNS(svgNS, 'polygon');
    tri.setAttribute('points',
      `${wp0.x.toFixed(1)},${wp0.y.toFixed(1)} ` +
      `${points[i].x.toFixed(1)},${points[i].y.toFixed(1)} ` +
      `${mx.toFixed(1)},${my.toFixed(1)}`);
    tri.setAttribute('fill', fill);
    tri.setAttribute('stroke', 'none');
    fanGroup.appendChild(tri);
  }

  // Line of purples: dashed gray edge from 380nm (locus[0]) to purple limit (locus[last]).
  svg('line', { x1: points[0].x, y1: points[0].y, x2: points[points.length-1].x, y2: points[points.length-1].y,
    stroke: '#222', 'stroke-width': 1, 'stroke-dasharray': '5,3' }, diagram);

  // Smooth spectral-locus outline: Catmull-Rom → quadratic Bezier through midpoints.
  // Pass a C1 curve through every consecutive midpoint using each original vertex
  // as the control point. No visible polygon facets at any zoom level.
  let d = `M ${points[0].x.toFixed(1)},${points[0].y.toFixed(1)}`;
  for (let i = 1; i < points.length - 2; i++) {
    const mx = (points[i].x + points[i + 1].x) / 2;
    const my = (points[i].y + points[i + 1].y) / 2;
    d += ` Q ${points[i].x.toFixed(1)},${points[i].y.toFixed(1)} ${mx.toFixed(1)},${my.toFixed(1)}`;
  }
  d += ` L ${points[points.length - 2].x.toFixed(1)},${points[points.length - 2].y.toFixed(1)}`;
  const locusOutline = document.createElementNS(svgNS, 'path');
  locusOutline.setAttribute('d', d);
  locusOutline.setAttribute('fill', 'none');
  locusOutline.setAttribute('stroke', '#222');
  locusOutline.setAttribute('stroke-width', '1.5');
  diagram.appendChild(locusOutline);

  // Tick labels at 480 / 580 / 680 for orientation.
  for (const [x, y, nm] of locus) {
    if (nm !== 480 && nm !== 580 && nm !== 680) continue;
    const p = toPixel(x, y, plot);
    const t = svg('text', { x: p.x + 5, y: p.y - 4, 'font-size': 9, fill: '#222' }, diagram);
    t.textContent = nm;
  }

  // Axis labels
  [0, 0.4, 0.8].forEach(x => {
    const p = toPixel(x, 0, plot);
    const t = svg('text', { x: p.x, y: plot.y + plot.height + 16, 'text-anchor': 'middle', class: 'axis-label' }, diagram);
    t.textContent = x.toFixed(1);
  });
  const xLabel = svg('text', { x: plot.x + plot.width - 50, y: plot.y + plot.height + 32, class: 'axis-label' }, diagram);
  xLabel.textContent = 'CIE 1931 x';

  // Build <defs> with one linear gradient per gamut. Each gradient runs along the
  // gamut's R→B edge with three stops at the projected positions of R, G, B
  // (G's stop is computed by projecting G onto the R–B line). Colors are the
  // primary channel colors. Result: each target triangle fills with the actual
  // R/G/B color content of that color space — sRGB looks red/green/blue, P3
  // looks slightly different, etc. This is how rtings / notebookcheck render
  // chromaticity diagrams.
  const defs = svg('defs', {}, diagram);
  RUN.gamuts.forEach((g, i) => {
    const grad = document.createElementNS(svgNS, 'linearGradient');
    const id = `grad-${cssEscape(g.name)}`;
    grad.setAttribute('id', id);
    grad.setAttribute('gradientUnits', 'userSpaceOnUse');

    const rPx = toPixel(g.target.r.x, g.target.r.y, plot);
    const gPx = toPixel(g.target.g.x, g.target.g.y, plot);
    const bPx = toPixel(g.target.b.x, g.target.b.y, plot);

    // Gradient axis = R → B; G is encoded as a stop along this axis at its
    // projection, giving a proper barycentric-ish fill (red toward R, green
    // toward G, blue toward B) without a full mesh gradient.
    const dx = bPx.x - rPx.x, dy = bPx.y - rPx.y;
    const lenSq = dx * dx + dy * dy;
    const projG = lenSq > 0 ? ((gPx.x - rPx.x) * dx + (gPx.y - rPx.y) * dy) / lenSq : 0.5;

    grad.setAttribute('x1', rPx.x.toFixed(1));
    grad.setAttribute('y1', rPx.y.toFixed(1));
    grad.setAttribute('x2', bPx.x.toFixed(1));
    grad.setAttribute('y2', bPx.y.toFixed(1));

    const stops = [
      { offset: 0,                              color: '#ff2222' },
      { offset: Math.max(0.01, Math.min(0.99, projG)), color: '#22cc22' },
      { offset: 1,                              color: '#2244ff' },
    ];
    for (const s of stops) {
      const stop = document.createElementNS(svgNS, 'stop');
      stop.setAttribute('offset', (s.offset * 100).toFixed(2) + '%');
      stop.setAttribute('stop-color', s.color);
      stop.setAttribute('stop-opacity', '0.45');
      grad.appendChild(stop);
    }
    defs.appendChild(grad);
  });

  // Gamuts: one group per target so we can toggle.
  RUN.gamuts.forEach((g, i) => {
    const color = palette[i % palette.length];
    const gGroup = svg('g', { 'data-target': g.name }, diagram);

    const measuredColor = darken(color, 0.3);
    const targetPts = [g.target.r, g.target.g, g.target.b].map(p => toPixel(p.x, p.y, plot));
    const measuredPts = [g.measured.r, g.measured.g, g.measured.b].map(p => toPixel(p.x, p.y, plot));

    // Target triangle: fill with the R/G/B primary gradient for that gamut; outline
    // keeps the palette stroke so the legend can still identify which is which.
    const targetTri = svg('polygon', {
      fill: `url(#grad-${cssEscape(g.name)})`,
      stroke: color, 'stroke-width': 2.2, opacity: '0.85',
    }, gGroup);
    targetTri.setAttribute('points', targetPts.map(p => `${p.x.toFixed(1)},${p.y.toFixed(1)}`).join(' '));

    // Measured triangle: dashed outline in the palette-muted color, no fill.
    const measuredTri = svg('polygon', {
      fill: 'none', stroke: measuredColor, 'stroke-width': 1.8, 'stroke-dasharray': '6,4',
    }, gGroup);
    measuredTri.setAttribute('points', measuredPts.map(p => `${p.x.toFixed(1)},${p.y.toFixed(1)}`).join(' '));

    // Primary vertex markers (R red / G green / B blue) on the target triangle, so
    // "where is this gamut's red primary" is answerable at a glance.
    const primaryColors = ['#ff2222', '#22cc22', '#2244ff'];
    [g.target.r, g.target.g, g.target.b].forEach((pt, j) => {
      const px = toPixel(pt.x, pt.y, plot);
      svg('circle', {
        cx: px.x, cy: px.y, r: 4.5,
        fill: primaryColors[j], stroke: '#fff', 'stroke-width': 1.5,
      }, gGroup);
    });

    // Whichever white point is finite: small dot.
    if (Number.isFinite(g.measured.w.x) && Number.isFinite(g.measured.w.y)) {
      const wp = toPixel(g.measured.w.x, g.measured.w.y, plot);
      svg('circle', { cx: wp.x, cy: wp.y, r: 4, fill: measuredColor, stroke: '#fff', 'stroke-width': 1 }, gGroup);
    }
  });

  // Border
  svg('rect', {
    x: plot.x, y: plot.y, width: plot.width, height: plot.height,
    fill: 'none', stroke: '#333', 'stroke-width': 1
  }, diagram);

  // Sample-marker group (managed by hover handlers)
  const markerGroup = svg('g', { id: 'sample-markers' }, diagram);

  return { plot, markerGroup };
}

function withAlpha(hex, alpha) {
  const r = parseInt(hex.slice(1, 3), 16);
  const g = parseInt(hex.slice(3, 5), 16);
  const b = parseInt(hex.slice(5, 7), 16);
  return `rgba(${r},${g},${b},${alpha})`;
}
function darken(hex, amount) {
  const r = Math.max(0, Math.round(parseInt(hex.slice(1, 3), 16) * (1 - amount)));
  const g = Math.max(0, Math.round(parseInt(hex.slice(3, 5), 16) * (1 - amount)));
  const b = Math.max(0, Math.round(parseInt(hex.slice(5, 7), 16) * (1 - amount)));
  return '#' + [r, g, b].map(n => n.toString(16).padStart(2, '0')).join('');
}
function wavelengthToColor(nm) {
  let n = Math.max(380, Math.min(770, nm));
  let r, g, b, factor = 1.0;
  if (n < 440)      { r = -(n - 440) / 60; g = 0;          b = 1; }
  else if (n < 490) { r = 0;              g = (n - 440) / 50; b = 1; }
  else if (n < 510) { r = 0;              g = 1;          b = -(n - 510) / 20; }
  else if (n < 580) { r = (n - 510) / 70; g = 1;          b = 0; }
  else if (n < 645) { r = 1;              g = -(n - 645) / 65; b = 0; }
  else              { r = 1;              g = 0;          b = 0; }
  if (n < 420) factor = 0.3 + 0.7 * (n - 380) / 40;
  else if (n > 700) factor = 0.3 + 0.7 * (770 - n) / 70;
  r *= factor; g *= factor; b *= factor;
  const clamp = v => Math.max(0, Math.min(255, Math.round(v * 255)));
  return `rgb(${clamp(r)},${clamp(g)},${clamp(b)})`;
}

function buildToolbar(plot) {
  const toolbar = document.querySelector('.toolbar');
  RUN.gamuts.forEach((g, i) => {
    const color = palette[i % palette.length];
    const btn = document.createElement('button');
    btn.className = 'toggle';
    btn.style.setProperty('--c', color);
    btn.dataset.target = g.name;
    btn.innerHTML = `<span class="dot"></span><b>${g.name}</b>`
      + `<span class="cov">cov ${g.coverage == null ? '—' : (g.coverage*100).toFixed(1) + '%'}</span>`
      + `<span class="vbadge ${g.passed ? 'pass' : 'fail'}">${g.passed ? 'PASS' : 'FAIL'}</span>`;
    btn.addEventListener('click', () => toggleGamut(g.name, btn));
    toolbar.appendChild(btn);
  });
}

function toggleGamut(name, btn) {
  btn.classList.toggle('off');
  const visible = !btn.classList.contains('off');
  document.querySelectorAll(`#diagram g[data-target="${cssEscape(name)}"]`)
    .forEach(el => el.style.display = visible ? '' : 'none');
  recomputeSampleMarkers();
}

function cssEscape(s) { return s.replace(/[^a-zA-Z0-9_-]/g, c => '\\' + c); }

function buildGamutTable() {
  const tbody = document.getElementById('gamut-rows');
  if (RUN.gamuts.length === 0) {
    tbody.innerHTML = '<tr><td colspan="8" class="empty">没有可用的色域分析结果</td></tr>';
    return;
  }
  RUN.gamuts.forEach((g, i) => {
    const color = palette[i % palette.length];
    const find = ch => (g.primaryDeltaEs.find(p => p.channel === ch) || { dE: null }).dE;
    const tr = document.createElement('tr');
    tr.dataset.target = g.name;
    tr.innerHTML =
      `<td><span class="patch-chip" style="background:${color}"></span><b>${g.name}</b></td>`
      + `<td>${g.coverage == null ? '—' : (g.coverage*100).toFixed(1) + '%'}</td>`
      + `<td>${num(g.whiteDeltaUv)}</td>`
      + `<td>${num(g.maxDeltaE2000)}</td>`
      + `<td>${num(find('R'))}</td>`
      + `<td>${num(find('G'))}</td>`
      + `<td>${num(find('B'))}</td>`
      + `<td class="${g.passed ? 'pass' : 'fail'}">${g.passed ? 'PASS' : 'FAIL'}</td>`;
    tr.addEventListener('click', () => {
      document.querySelectorAll('#gamut-rows tr').forEach(r => r.classList.remove('highlight'));
      tr.classList.add('highlight');
      const btn = document.querySelector(`.toggle[data-target="${cssEscape(g.name)}"]`);
      if (btn && btn.classList.contains('off')) { toggleGamut(g.name, btn); }
    });
    tbody.appendChild(tr);
  });
}

function buildSamplesTable(plot, markerGroup) {
  const tbody = document.getElementById('sample-rows');
  if (RUN.samples.length === 0) {
    tbody.innerHTML = '<tr><td colspan="11" class="empty">没有采集到任何样本</td></tr>';
    return;
  }
  RUN.samples.forEach(s => {
    const tr = document.createElement('tr');
    tr.innerHTML =
      `<td><b>${s.id}</b></td>`
      + `<td>${num(s.x)}</td>`
      + `<td>${num(s.y)}</td>`
      + `<td>${num(s.lv, 2)}</td>`
      + `<td>${num(s.triX, 4)}</td>`
      + `<td>${num(s.triY, 4)}</td>`
      + `<td>${num(s.triZ, 4)}</td>`
      + `<td>${num(s.tcp, 0)}</td>`
      + `<td>${num(s.duv, 5)}</td>`
      + `<td>${RUN.probeSerial}</td>`
      + `<td>${s.measuredAt}</td>`;
    tr.addEventListener('mouseenter', () => plantMarker(s, markerGroup, plot));
    tr.addEventListener('mouseleave', () => clearMarker(markerGroup));
    tr.addEventListener('click', () => showSampleDetail(s));
    tbody.appendChild(tr);
  });
}

let activeMarkers = [];
function plantMarker(s, group, plot) {
  clearMarker(group);
  if (!Number.isFinite(s.x) || !Number.isFinite(s.y)) return;
  const p = toPixel(s.x, s.y, plot);
  const ring = svg('circle', { cx: p.x, cy: p.y, r: 10, fill: 'none', stroke: '#ff8c00', 'stroke-width': 2.5 }, group);
  const dot = svg('circle', { cx: p.x, cy: p.y, r: 4, fill: '#ff8c00' }, group);
  const label = svg('text', { x: p.x + 14, y: p.y - 10, fill: '#ff8c00', 'font-size': 13, 'font-weight': 600 }, group);
  label.textContent = `${s.id}  (${s.x.toFixed(4)}, ${s.y.toFixed(4)})`;
  activeMarkers.push(ring, dot, label);
}
function clearMarker(group) {
  activeMarkers.forEach(m => m.remove());
  activeMarkers = [];
}
function recomputeSampleMarkers() {
  // Toggling doesn't directly affect markers, but kept for symmetry / future use.
}

function showSampleDetail(s) {
  const panel = document.getElementById('sample-detail');
  panel.classList.add('show');
  panel.innerHTML =
    `<b>${s.id}</b>  ·  measured at ${s.measuredAt}\n`
    + `<div class="kv">`
    + `<span>Chromaticity x</span><span>${num(s.x, 6)}</span>`
    + `<span>Chromaticity y</span><span>${num(s.y, 6)}</span>`
    + `<span>Lv (cd/m²)</span><span>${num(s.lv, 4)}</span>`
    + `<span>Tcp (K)</span><span>${num(s.tcp, 2)}</span>`
    + `<span>Δu'v</span><span>${num(s.duv, 6)}</span>`
    + `<span>Probe SN</span><span>${RUN.probeSerial}</span>`
    + `<span>Tristimulus X</span><span>${num(s.triX, 4)}</span>`
    + `<span>Tristimulus Y</span><span>${num(s.triY, 4)}</span>`
    + `<span>Tristimulus Z</span><span>${num(s.triZ, 4)}</span>`
    + `</div>`;
}

function num(v, digits = 4) {
  if (!Number.isFinite(v)) return '—';
  return v.toFixed(digits);
}

(function init() {
  const { plot, markerGroup } = buildDiagram();
  buildToolbar(plot);
  buildGamutTable();
  buildSamplesTable(plot, markerGroup);
})();
</script>
</body>
</html>
""";
    }

    private static XYPoint Pt(ChromaticityPoint p) => new() { x = p.X, y = p.Y };

    /// <summary>
    /// Sanitize a non-finite value to <c>null</c> so the JSON stays valid. Non-finite values
    /// come from degenerate inputs (e.g. zero-sum XYZ → NaN xy, or division by zero in ΔE2000
    /// that produces ±Infinity). The HTML page renders these as <c>—</c>.
    /// </summary>
    private static double? San(double v) => double.IsFinite(v) ? v : null;

    private static string HtmlEncode(string s) => System.Net.WebUtility.HtmlEncode(s);

    public sealed class ReportData
    {
        public string probeSerial { get; set; } = "";
        public string planName { get; set; } = "";
        public string startedAt { get; set; } = "";
        public string finishedAt { get; set; } = "";
        public List<SampleData> samples { get; set; } = new();
        public List<GamutData> gamuts { get; set; } = new();
    }

    public sealed class SampleData
    {
        public string id { get; set; } = "";
        public double? x { get; set; }
        public double? y { get; set; }
        public double? lv { get; set; }
        public double? triX { get; set; }
        public double? triY { get; set; }
        public double? triZ { get; set; }
        public double? duv { get; set; }
        public double? tcp { get; set; }
        public string measuredAt { get; set; } = "";
    }

    public sealed class GamutData
    {
        public string name { get; set; } = "";
        public double? coverage { get; set; }
        public double? whiteDeltaUv { get; set; }
        public double? maxDeltaE2000 { get; set; }
        public bool passed { get; set; }
        public List<PrimaryDelta> primaryDeltaEs { get; set; } = new();
        public TrianglePoints target { get; set; } = new();
        public TrianglePoints measured { get; set; } = new();
    }

    public sealed class PrimaryDelta
    {
        public string channel { get; set; } = "";
        public double? dE { get; set; }
    }

    public sealed class TrianglePoints
    {
        public XYPoint r { get; set; } = new();
        public XYPoint g { get; set; } = new();
        public XYPoint b { get; set; } = new();
        public XYPoint w { get; set; } = new();
    }

    public sealed class XYPoint
    {
        public double? x { get; set; }
        public double? y { get; set; }
    }
}
