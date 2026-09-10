using CASDK2;
using ColorMeasure.Domain;
using ColorMeasure.Domain.Abstractions;
using ColorMeasure.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ColorMeasure.Infrastructure.CaSdk;

/// <summary>
/// Concrete <see cref="ICA200Probe"/> backed by the managed CA-SDK2 wrapper (CASDK2Net.dll).
/// SDK calls are serialized on a single semaphore — the native SDK is not thread-safe and a
/// probe is exclusive hardware.
/// </summary>
public sealed class Ca200ProbeAdapter : ICA200Probe
{
    private readonly ILogger<Ca200ProbeAdapter> _log;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private CASDK2Ca200? _ca200;
    private CASDK2Cas? _cas;
    private CASDK2Ca? _ca;
    private CASDK2Probe? _probe;
    private string _serialNo = "unknown";

    public Ca200ProbeAdapter(ILogger<Ca200ProbeAdapter> log) => _log = log;

    public Task<Result<IReadOnlyList<DeviceInfo>>> DiscoverAsync(CancellationToken ct)
    {
        try
        {
            CASDK2DeviceData[] devices = null!;
            int rc = CASDK2Discovery.SearchAllUSBDevices(ref devices);
            if (rc != 0) return Task.FromResult(Translate<IReadOnlyList<DeviceInfo>>(rc, "Discovery"));

            var list = new List<DeviceInfo>();
            if (devices is not null)
            {
                foreach (var d in devices)
                {
                    list.Add(new DeviceInfo(
                        SerialNo: new string(d.strSerialNo).TrimEnd('\0'),
                        ModelName: d.iProbeType.ToString(),
                        ConnectionString: new string(d.strTarget).TrimEnd('\0')));
                }
            }
            int destroyRc = CASDK2Discovery.DestroyListOfDevices(ref devices);
            if (destroyRc != 0) _log.LogWarning("DestroyListOfDevices returned {Rc}", destroyRc);
            return Task.FromResult(Result<IReadOnlyList<DeviceInfo>>.Ok(list));
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "USB discovery failed");
            return Task.FromResult(Result<IReadOnlyList<DeviceInfo>>.Fail(new Error("SDK_EXCEPTION", ex.Message)));
        }
    }

    public async Task<Result<Unit>> ConnectAsync(ConnectionProfile profile, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            _ca200 ??= new CASDK2Ca200();
            int rc = _ca200.AutoConnect();
            if (rc != 0) return Translate<Unit>(rc, "AutoConnect");

            _cas = new CASDK2Cas();
            rc = _ca200.get_Cas(ref _cas);
            if (rc != 0) return Translate<Unit>(rc, "get_Cas");

            int count = 0;
            rc = _cas.get_Count(ref count);
            if (rc != 0) return Translate<Unit>(rc, "Cas.get_Count");
            if (count == 0) return Result<Unit>.Fail(Error.ProbeNotConnected);

            _ca = new CASDK2Ca();
            rc = _cas.get_Item(1, ref _ca);
            if (rc != 0) return Translate<Unit>(rc, "Cas.get_Item");

            _probe = new CASDK2Probe();
            rc = _ca.get_SingleProbe(ref _probe);
            if (rc != 0) return Translate<Unit>(rc, "Ca.get_SingleProbe");

            int rcSync = _probe.put_SyncMode(3, 60.0); // SYNC_UNIV
            if (rcSync != 0) _log.LogWarning("put_SyncMode returned {Rc}", rcSync);

            int rcAvg = _probe.put_AveragingMode(0); // AVRG_SLOW
            if (rcAvg != 0) _log.LogWarning("put_AveragingMode returned {Rc}", rcAvg);

            string sn = "";
            int rcSn = _probe.get_SerialNO(ref sn);
            if (rcSn == 0) _serialNo = sn.TrimEnd('\0');
            return Result<Unit>.Ok(new Unit());
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Connect failed");
            return Result<Unit>.Fail(new Error("SDK_EXCEPTION", ex.Message));
        }
        finally { _gate.Release(); }
    }

    public async Task<Result<Unit>> ZeroCalAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            EnsureConnected();
            int rc = _ca!.CalZero();
            return rc == 0 ? Result<Unit>.Ok(new Unit()) : Translate<Unit>(rc, "CalZero");
        }
        catch (Exception ex) { return Result<Unit>.Fail(new Error("SDK_EXCEPTION", ex.Message)); }
        finally { _gate.Release(); }
    }

    public async Task<Result<ColorSample>> MeasureAsync(MeasureOptions options, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            EnsureConnected();

            int avgMode = options.AverageCount <= 1 ? 1 : 0; // FAST vs SLOW
            int rc = _probe!.put_AveragingMode(avgMode);
            if (rc != 0) _log.LogWarning("put_AveragingMode returned {Rc}", rc);

            int displayMode = MapDisplayMode(options.DisplayMode);
            rc = _probe.put_DisplayMode(displayMode);
            if (rc != 0) _log.LogWarning("put_DisplayMode returned {Rc}", rc);

            for (int i = 0; i < Math.Max(1, options.AverageCount); i++)
            {
                rc = _ca!.Measure();
                if (rc != 0) return Translate<ColorSample>(rc, "Measure");
            }

            double x = 0, y = 0, z = 0, sx = 0, sy = 0, lv = 0, duv = 0, tcp = 0;
            if (_probe.get_X(ref x) != 0 ||
                _probe.get_Y(ref y) != 0 ||
                _probe.get_Z(ref z) != 0 ||
                _probe.get_sx(ref sx) != 0 ||
                _probe.get_sy(ref sy) != 0 ||
                _probe.get_Lv(ref lv) != 0 ||
                _probe.get_duv(ref duv) != 0 ||
                _probe.get_T(ref tcp) != 0)
            {
                return Result<ColorSample>.Fail(new Error("SDK_READ", "Failed to read probe values"));
            }

            if (double.IsInfinity(x) || double.IsInfinity(y) || double.IsInfinity(z))
                return Result<ColorSample>.Fail(new Error("SDK_INFINITY", "Probe returned infinity (out of range?)"));

            return Result<ColorSample>.Ok(new ColorSample(
                PatchId: options.PatchId,
                Chromaticity: ChromaticityPoint.FromXYZ(x, y, z),
                Lv: lv,
                X: x, Y: y, Z: z,
                DuV: duv,
                Tcp: tcp,
                ProbeSerialNo: _serialNo,
                MeasuredAt: DateTimeOffset.UtcNow));
        }
        catch (Exception ex) { return Result<ColorSample>.Fail(new Error("SDK_EXCEPTION", ex.Message)); }
        finally { _gate.Release(); }
    }

    public async Task<Result<ProbeInfo>> GetInfoAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            EnsureConnected();
            string sn = _serialNo;
            string fw = "";
            int rc = _probe!.get_CAVersion(ref fw);
            if (rc != 0) _log.LogWarning("get_CAVersion returned {Rc}", rc);
            return Result<ProbeInfo>.Ok(new ProbeInfo(sn.TrimEnd('\0'), "CA-410", fw.TrimEnd('\0')));
        }
        catch (Exception ex) { return Result<ProbeInfo>.Fail(new Error("SDK_EXCEPTION", ex.Message)); }
        finally { _gate.Release(); }
    }

    public async Task<Result<Unit>> DisconnectAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_ca200 is null) return Result<Unit>.Ok(new Unit());
            int rc = _ca200.AutoDisconnect();
            return rc == 0 ? Result<Unit>.Ok(new Unit()) : Translate<Unit>(rc, "AutoDisconnect");
        }
        catch (Exception ex) { return Result<Unit>.Fail(new Error("SDK_EXCEPTION", ex.Message)); }
        finally
        {
            _probe?.Dispose(); _ca?.Dispose(); _cas?.Dispose(); _ca200?.Dispose();
            _probe = null; _ca = null; _cas = null; _ca200 = null;
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync(CancellationToken.None);
        _gate.Dispose();
    }

    private void EnsureConnected()
    {
        if (_probe is null || _ca is null) throw new InvalidOperationException("Probe is not connected");
    }

    private static int MapDisplayMode(string mode) => mode.ToUpperInvariant() switch
    {
        "LXY" or "LVXY" => 0,        // DSP_LXY
        "DUV" => 1,                  // DSP_DUV
        "XYZ" => 7,                  // DSP_XYZC
        "T_DUV" or "TDUV" => 1,
        _ => 0,
    };

    private Result<T> Translate<T>(int rc, string op)
    {
        string msg = "";
        try
        {
            int mrc = GlobalFunctions.CASDK2_GetLocalizedErrorMsgFromErrorCode(2, rc, ref msg); // 2 = LANG_CH_SIMP
            if (mrc != 0 || string.IsNullOrEmpty(msg))
                msg = $"CA-SDK2 error code {rc}";
        }
        catch { msg = $"CA-SDK2 error code {rc}"; }
        _log.LogError("SDK {Op} failed: rc={Rc} msg={Msg}", op, rc, msg);
        return Result<T>.Fail(Error.FromSdk(rc, $"{op}: {msg}"));
    }
}
