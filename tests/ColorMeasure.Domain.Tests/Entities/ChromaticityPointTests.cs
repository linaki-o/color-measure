using Xunit;
using ColorMeasure.Domain.Entities;
using FluentAssertions;

namespace ColorMeasure.Domain.Tests;

public class PatchSpecTests
{
    [Fact]
    public void FromHex_ParsesStandardHex()
    {
        var p = PatchSpec.FromHex("red", "#FF0000");
        p.R.Should().Be(255);
        p.G.Should().Be(0);
        p.B.Should().Be(0);
    }

    [Fact]
    public void FromHex_AcceptsNoHash()
    {
        var p = PatchSpec.FromHex("g", "00FF00");
        p.G.Should().Be(255);
    }

    [Theory]
    [InlineData("")]
    [InlineData("#FFF")]
    [InlineData("#GGGGGG")]
    public void FromHex_RejectsInvalid(string hex)
    {
        var act = () => PatchSpec.FromHex("x", hex);
        act.Should().Throw<ArgumentException>();
    }
}

public class ChromaticityPointTests
{
    [Fact]
    public void FromXYZ_D65White_RoundTrips()
    {
        // D65 in XYZ (Y=1) -> xy should be (0.3127, 0.3290)
        var xy = ChromaticityPoint.FromXYZ(0.95047, 1.0, 1.08883);
        xy.X.Should().BeApproximately(0.3127, 1e-3);
        xy.Y.Should().BeApproximately(0.3290, 1e-3);
    }

    [Fact]
    public void FromXYZ_ZeroSum_ReturnsNaN()
    {
        var xy = ChromaticityPoint.FromXYZ(0, 0, 0);
        double.IsNaN(xy.X).Should().BeTrue();
        double.IsNaN(xy.Y).Should().BeTrue();
    }

    [Fact]
    public void ToUvPrime_D65_MatchesKnown()
    {
        // u' = 4x/(-2x+12y+3), v' = 9y/(-2x+12y+3)
        // For D65 (0.3127, 0.3290): u' approx 0.19783, v' approx 0.46832
        var (u, v) = ChromaticityPoint.D65.ToUvPrime1976();
        u.Should().BeApproximately(0.19783, 1e-4);
        v.Should().BeApproximately(0.46832, 1e-4);
    }
}
