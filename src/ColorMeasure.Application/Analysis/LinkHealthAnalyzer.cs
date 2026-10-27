using ColorMeasure.Domain;
using ColorMeasure.Domain.Entities;

namespace ColorMeasure.Application.Analysis;

/// <summary>
/// Automated signal-chain health check.
///
/// The GPU palette driver sends 8-bit sRGB (BT.709) SDR signals. When the OS or the
/// display is in HDR mode, that signal passes through an SDR→HDR conversion
/// (colour management + gamut mapping + tone mapping) before the panel ever sees it.
/// That conversion is neither chromaticity-preserving nor gamma-preserving, so the
/// numbers in the report stop representing the panel's native colorimetry.
///
/// This analyzer looks for the measurable fingerprints of that conversion:
///
///  1. <b>Saturation compression</b> — gamut mapping pulls the primaries *toward* the
///     white point. This is the strongest HDR signature, and it is direction-aware:
///     primaries pushed *away* from white (a genuinely wide-gamut display) is a real
///     property of the display, not a link artifact, so it is not flagged.
///  2. <b>Midtone gamma off 2.2</b> — a rewritten EOTF (PQ / tone mapping) flattens
///     the midtones relative to the sRGB power law.
///  3. <b>Black-level lift</b> — PQ code value 0 is not 0 nits, so the black point
///     floats above the floor.
///  4. <b>Gamma fit R²</b> — a clean power law fits with R² > 0.99.
///  5. <b>White point Δu'v'</b> — reported for context (a colour-temperature issue,
///     independent of the link type).
///
/// Thresholds were calibrated against real measurements taken on the same panel in
/// both SDR and HDR modes; see docs/color-science.md#link-health.
/// </summary>
public static class LinkHealthAnalyzer
{
    // S1 black-level lift (cd/m^2). A clean SDR panel sits at 0.04-0.07.
    public const double BlackWarnCdM2 = 0.5;
    public const double BlackCriticalCdM2 = 1.5;

    // S2 core midtone gamma deviation from the sRGB reference of 2.2.
    public const double MidtoneGammaWarn = 0.15;
    public const double MidtoneGammaCritical = 0.45;

    // S3 saturation compression: mean pull of the three primaries toward white (percent
    // of the sRGB->white path). Only counted when all three primaries move the same way.
    public const double SaturationCompressionWarnPct = 2.5;
    public const double SaturationCompressionCriticalPct = 5.0;

    // S4 gamma fit quality.
    public const double GammaR2Warn = 0.99;
    public const double GammaR2Critical = 0.95;

    // S5 white point deviation from D65 in the CIE 1976 UCS.
    public const double WhitePointWarn = 0.012;
    public const double WhitePointCritical = 0.025;

    /// <summary>The sRGB reference gamma used for the midtone comparison.</summary>
    public const double ReferenceGamma = 2.2;

    public static LinkHealthReport Analyze(
        IReadOnlyList<ColorSample> samples,
        IReadOnlyList<GamutReport> gamutReports,
        GammaReport? gamma,
        DisplayState declaredState)
    {
        var signals = new List<LinkSignal>();

        // -- S1: black-level lift -------------------------------------------------
        if (gamma is { } g && double.IsFinite(g.BlackLevelCdM2))
        {
            double black = g.BlackLevelCdM2;
            var sev = black > BlackCriticalCdM2 ? LinkSignalSeverity.Critical
                     : black > BlackWarnCdM2 ? LinkSignalSeverity.Warning
                     : LinkSignalSeverity.Info;
            signals.Add(new LinkSignal(
                "black-lift", "黑场抬升",
                $"黑场实测 {black:F3} cd/m²（干净 SDR 通常 0.02-0.08）。" +
                "PQ 码值 0 不对应 0 nit，HDR 链路会抬起黑位。",
                black, BlackWarnCdM2, sev));
        }

        // -- S2: core midtone gamma ----------------------------------------------
        if (gamma is { MidtoneGamma: { } mid } && double.IsFinite(mid))
        {
            double dev = Math.Abs(mid - ReferenceGamma);
            var sev = dev > MidtoneGammaCritical ? LinkSignalSeverity.Critical
                     : dev > MidtoneGammaWarn ? LinkSignalSeverity.Warning
                     : LinkSignalSeverity.Info;
            signals.Add(new LinkSignal(
                "midtone-gamma", "核心区 Gamma 偏离 2.2",
                $"核心区 (V 0.25-0.75) 拟合 γ = {mid:F3}，偏离 sRGB 参考 {ReferenceGamma:F1} 达 {dev:F3}。" +
                "EOTF 被改写（PQ / 色调映射）会压平中间调。",
                dev, MidtoneGammaWarn, sev));
        }

        // -- S3: saturation compression (direction-aware) ---------------------------
        var (meanPullPct, allSameDirection, primaryDetail) = MeasureSaturationPull(gamutReports);
        if (meanPullPct is { } pull)
        {
            var sev = LinkSignalSeverity.Info;
            if (allSameDirection && pull > 0)
            {
                sev = pull > SaturationCompressionCriticalPct ? LinkSignalSeverity.Critical
                     : pull > SaturationCompressionWarnPct ? LinkSignalSeverity.Warning
                     : LinkSignalSeverity.Info;
            }
            signals.Add(new LinkSignal(
                "saturation-compression", "原色饱和度压缩",
                primaryDetail +
                (allSameDirection && pull > 0
                    ? " 三个原色同向朝白点移动 —— gamut mapping 的典型特征。"
                    : " 未出现同向压缩（若为负值说明显示器原生色域比 sRGB 宽，属正常特性）。"),
                pull, SaturationCompressionWarnPct, sev));
        }

        // -- S4: gamma fit quality -------------------------------------------------
        if (gamma is { } gg && double.IsFinite(gg.RSquared))
        {
            double r2 = gg.RSquared;
            var sev = r2 < GammaR2Critical ? LinkSignalSeverity.Critical
                     : r2 < GammaR2Warn ? LinkSignalSeverity.Warning
                     : LinkSignalSeverity.Info;
            signals.Add(new LinkSignal(
                "gamma-fit-r2", "Gamma 拟合优度",
                $"全量程 log-log 拟合 R² = {r2:F4}（纯 power law 应 > 0.99）。",
                r2, GammaR2Warn, sev));
        }

        // -- S5: white point ------------------------------------------------------
        if (gamutReports.Count > 0)
        {
            double duv = gamutReports.Max(r => r.WhiteDeltaUv);
            if (double.IsFinite(duv))
            {
                var sev = duv > WhitePointCritical ? LinkSignalSeverity.Critical
                         : duv > WhitePointWarn ? LinkSignalSeverity.Warning
                         : LinkSignalSeverity.Info;
                signals.Add(new LinkSignal(
                    "white-point", "白点偏离 D65",
                    $"Δu'v'(W) = {duv:F4}（D65 = u'0.1978 v'0.4683）。" +
                    "偏大通常是色温没设到 6500K，属于色温问题而非链路问题。",
                    duv, WhitePointWarn, sev));
            }
        }

        var verdict = Decide(signals, hasGamma: gamma is not null, hasGamut: gamutReports.Count > 0);
        var summary = Summarize(verdict, signals, declaredState);

        return new LinkHealthReport(verdict, summary, declaredState, signals);
    }

    /// <summary>
    /// For each primary, the fraction of the sRGB→white path that the measured primary
    /// has moved along. Positive = toward white (compression), negative = away from white
    /// (expansion, i.e. a genuinely wider display). Returns the mean in percent, whether
    /// all three moved the same way, and a human-readable breakdown.
    /// </summary>
    private static (double? MeanPullPct, bool AllSameDirection, string Detail)
        MeasureSaturationPull(IReadOnlyList<GamutReport> reports)
    {
        // Prefer the sRGB report (the usual reference), else the first one.
        var report = reports.FirstOrDefault(r => string.Equals(r.Target.Name, "sRGB", StringComparison.OrdinalIgnoreCase))
                  ?? reports.FirstOrDefault();
        if (report is null) return (null, false, "");

        var srgb = ColorSpaceCatalog.Srgb;
        var pairs = new (string Name, ChromaticityPoint Measured, ChromaticityPoint Standard)[]
        {
            ("R", report.MeasuredRed,   srgb.Red),
            ("G", report.MeasuredGreen, srgb.Green),
            ("B", report.MeasuredBlue,  srgb.Blue),
        };
        var white = report.MeasuredWhite;

        var fracs = new List<double>();
        var parts = new List<string>();
        foreach (var (name, measured, standard) in pairs)
        {
            double toWx = white.X - standard.X;
            double toWy = white.Y - standard.Y;
            double len2 = toWx * toWx + toWy * toWy;
            if (len2 <= 0) continue;
            double dx = measured.X - standard.X;
            double dy = measured.Y - standard.Y;
            double f = (dx * toWx + dy * toWy) / len2;
            fracs.Add(f);
            parts.Add($"{name}={f * 100:F2}%");
        }
        if (fracs.Count < 3) return (null, false, string.Join("  ", parts));

        var mean = fracs.Average();
        var allPos = fracs.All(f => f > 0);
        var allNeg = fracs.All(f => f < 0);
        var same = allPos || allNeg;
        return (mean * 100, same, string.Join("  ", parts) + $"  平均={mean * 100:F2}%");
    }

    private static LinkVerdict Decide(IReadOnlyList<LinkSignal> signals, bool hasGamma, bool hasGamut)
    {
        if (!hasGamma || !hasGamut) return LinkVerdict.Unknown;

        int critical = signals.Count(s => s.Severity == LinkSignalSeverity.Critical);
        int warning = signals.Count(s => s.Severity == LinkSignalSeverity.Warning);

        if (critical >= 1) return LinkVerdict.NonSdrLikely;
        if (warning >= 2) return LinkVerdict.NonSdrLikely;
        if (warning == 1) return LinkVerdict.Suspicious;
        return LinkVerdict.SdrLikely;
    }

    private static string Summarize(LinkVerdict verdict, IReadOnlyList<LinkSignal> signals, DisplayState declared)
    {
        var critical = signals.Where(s => s.Severity == LinkSignalSeverity.Critical).Select(s => s.Title).ToList();
        var warning = signals.Where(s => s.Severity == LinkSignalSeverity.Warning).Select(s => s.Title).ToList();

        string core = verdict switch
        {
            LinkVerdict.SdrLikely =>
                "信号链路各项指标均符合纯 SDR 直通特征，本次数据可作为显示器原生色彩特性的参考。",
            LinkVerdict.Suspicious =>
                $"有 {warning.Count} 项指标偏离 SDR 基准（{string.Join("、", warning)}），" +
                "可能是显示器处于非 sRGB 模式，也可能信号链路经过了转换，请结合实际设置判断。",
            LinkVerdict.NonSdrLikely =>
                $"有 {critical.Count + warning.Count} 项指标偏离 SDR 基准" +
                $"（严重：{(critical.Count > 0 ? string.Join("、", critical) : "无")}；警告：{(warning.Count > 0 ? string.Join("、", warning) : "无")}），" +
                "本次测得的色域/亮度特性很可能不代表面板的原生值（典型原因：Windows HDR 或显示器 OSD 的 HDR 模式开启，" +
                "导致送出的 SDR 信号被 SDR→HDR 转换改写）。",
            _ => "数据不足以判断信号链路状态（需要至少一条灰阶 ramp 和一组原色测量）。",
        };

        if (declared == DisplayState.Sdr && verdict == LinkVerdict.NonSdrLikely)
            core += "  ⚠️ 配方声明为 SDR，但实测特征指向非 SDR 链路 —— 声明与实测矛盾，请优先排查。";

        if (declared == DisplayState.Hdr && verdict == LinkVerdict.SdrLikely)
            core += "  注意：配方声明为 HDR，但实测特征接近纯 SDR，请确认 Windows HDR 与显示器 OSD 的实际开关状态。";

        return core;
    }
}
