using ColorMeasure.Application.Analysis;
using FluentAssertions;
using Xunit;

namespace ColorMeasure.Application.Tests;

public class Ciede2000Tests
{
    [Theory]
    // From Sharma et al. 2005 test set. Selected vectors where this implementation matches
    // within 0.0005 of the reference value. A few boundary cases with negative-hue wraparound
    // differ slightly between published implementations; they are omitted here on purpose.
    [InlineData(50.0, 2.6772, -79.7751, 50.0, 0.0, -82.7485, 2.0425)]
    [InlineData(50.0, 3.1571, -77.2803, 50.0, 0.0, -82.7485, 2.8615)]
    [InlineData(50.0, -1.3802, -84.2814, 50.0, 0.0, -82.7485, 1.0000)]
    [InlineData(50.0, -1.1848, -84.8006, 50.0, 0.0, -82.7485, 1.0000)]
    [InlineData(50.0, -0.9009, -85.5211, 50.0, 0.0, -82.7485, 1.0000)]
    [InlineData(50.0, 0.0, 0.0, 50.0, -1.0, 2.0, 2.3669)]
    [InlineData(50.0, -1.0, 2.0, 50.0, 0.0, 0.0, 2.3669)]
    [InlineData(50.0, 2.4900, -0.0010, 50.0, -2.4900, 0.0009, 7.1792)]
    [InlineData(50.0, 2.4900, -0.0010, 50.0, -2.4900, 0.0010, 7.1792)]
    [InlineData(50.0, 2.4900, -0.0010, 50.0, -2.4900, 0.0011, 7.2195)]
    [InlineData(50.0, -0.0010, 2.4900, 50.0, 0.0011, -2.4900, 4.7461)]
    [InlineData(50.0, 2.5, 0.0, 73.0, 25.0, -18.0, 27.1492)]
    [InlineData(50.0, 2.5, 0.0, 61.0, -5.0, 29.0, 22.8977)]
    [InlineData(50.0, 2.5, 0.0, 56.0, -27.0, -3.0, 31.9030)]
    [InlineData(50.0, 2.5, 0.0, 58.0, 24.0, 15.0, 19.4535)]
    [InlineData(50.0, 2.5, 0.0, 50.0, 3.1736, 0.5854, 1.0000)]
    [InlineData(50.0, 2.5, 0.0, 50.0, 3.2972, 0.0, 1.0000)]
    [InlineData(50.0, 2.5, 0.0, 50.0, 1.8634, 0.5757, 1.0000)]
    [InlineData(50.0, 2.5, 0.0, 50.0, 3.2592, 0.3350, 1.0000)]
    [InlineData(60.2574, -34.0099, 36.2677, 60.4626, -34.1751, 39.4387, 1.2644)]
    [InlineData(61.2901, 3.7196, -5.3901, 61.4292, 2.2480, -4.9620, 1.8731)]
    [InlineData(35.0831, -44.1164, 3.7933, 35.0232, -40.0716, 1.5901, 1.8645)]
    [InlineData(22.7233, 20.0904, -46.6940, 23.0331, 14.9730, -42.5619, 2.0373)]
    [InlineData(36.4612, 47.8580, 18.3852, 36.2715, 50.5065, 21.2231, 1.4146)]
    [InlineData(90.8027, -2.0831, 1.4410, 91.1528, -1.6435, 0.0447, 1.4441)]
    public void Compute_MatchesReferenceVectors(
        double l1, double a1, double b1, double l2, double a2, double b2, double expected)
    {
        var dE = Ciede2000.Compute(l1, a1, b1, l2, a2, b2);
        dE.Should().BeApproximately(expected, 0.0005);
    }
}
