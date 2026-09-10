namespace ColorMeasure.Domain.Entities;

/// <summary>
/// A reference RGB patch displayed to the device under test (sRGB framebuffer coordinates).
/// </summary>
public readonly record struct PatchSpec(string Id, byte R, byte G, byte B, string? Label = null)
{
    public static PatchSpec FromHex(string id, string hex, string? label = null)
    {
        if (hex.Length == 7 && hex[0] == '#') hex = hex[1..];
        if (hex.Length != 6) throw new ArgumentException($"Invalid hex color length: {hex}", nameof(hex));
        try
        {
            var r = Convert.ToByte(hex.Substring(0, 2), 16);
            var g = Convert.ToByte(hex.Substring(2, 2), 16);
            var b = Convert.ToByte(hex.Substring(4, 2), 16);
            return new PatchSpec(id, r, g, b, label);
        }
        catch (FormatException ex)
        {
            throw new ArgumentException($"Invalid hex color: {hex}", nameof(hex), ex);
        }
    }
}
