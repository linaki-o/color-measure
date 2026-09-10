namespace ColorMeasure.Application.Analysis;

/// <summary>
/// CIEDE2000 color difference. Reference: Sharma, Wu &amp; Dalal, 2005.
/// Inputs are CIE Lab (L in 0..100, a/b unbounded).
/// </summary>
public static class Ciede2000
{
    public static double Compute(double l1, double a1, double b1, double l2, double a2, double b2)
    {
        double kL = 1, kC = 1, kH = 1;

        double c1 = Math.Sqrt(a1 * a1 + b1 * b1);
        double c2 = Math.Sqrt(a2 * a2 + b2 * b2);
        double cBar = (c1 + c2) / 2.0;

        double g = 0.5 * (1 - Math.Sqrt(Math.Pow(cBar, 7) / (Math.Pow(cBar, 7) + Math.Pow(25.0, 7))));
        double a1p = (1 + g) * a1;
        double a2p = (1 + g) * a2;

        double c1p = Math.Sqrt(a1p * a1p + b1 * b1);
        double c2p = Math.Sqrt(a2p * a2p + b2 * b2);

        double h1p = Math.Atan2(b1, a1p); if (h1p < 0) h1p += 2 * Math.PI;
        double h2p = Math.Atan2(b2, a2p); if (h2p < 0) h2p += 2 * Math.PI;

        double dLp = l2 - l1;
        double dCp = c2p - c1p;

        double dhp;
        if (c1p * c2p == 0) dhp = 0;
        else if (Math.Abs(h2p - h1p) <= Math.PI) dhp = h2p - h1p;
        else if (h2p - h1p > Math.PI) dhp = h2p - h1p - 2 * Math.PI;
        else dhp = h2p - h1p + 2 * Math.PI;

        double dHp = 2 * Math.Sqrt(c1p * c2p) * Math.Sin(dhp / 2.0);

        double lBar = (l1 + l2) / 2.0;
        double cBarP = (c1p + c2p) / 2.0;

        double hBarP;
        if (c1p * c2p == 0) hBarP = h1p + h2p;
        else if (Math.Abs(h1p - h2p) <= Math.PI) hBarP = (h1p + h2p) / 2.0;
        else if (h1p + h2p < 2 * Math.PI) hBarP = (h1p + h2p + 2 * Math.PI) / 2.0;
        else hBarP = (h1p + h2p - 2 * Math.PI) / 2.0;

        double t = 1
            - 0.17 * Math.Cos(hBarP - Deg2Rad(30))
            + 0.24 * Math.Cos(2 * hBarP)
            + 0.32 * Math.Cos(3 * hBarP + Deg2Rad(6))
            - 0.20 * Math.Cos(4 * hBarP - Deg2Rad(63));

        double dTheta = Deg2Rad(30) * Math.Exp(-Math.Pow((hBarP - Deg2Rad(275)) / Deg2Rad(25), 2));
        double rC = 2 * Math.Sqrt(Math.Pow(cBarP, 7) / (Math.Pow(cBarP, 7) + Math.Pow(25.0, 7)));
        double sH = 1 + 0.015 * cBarP * t;
        double sL = 1 + (0.015 * Math.Pow(lBar - 50, 2)) / Math.Sqrt(20 + Math.Pow(lBar - 50, 2));
        double sC = 1 + 0.045 * cBarP;
        double rT = -Math.Sin(2 * dTheta) * rC;

        double term1 = dLp / (kL * sL);
        double term2 = dCp / (kC * sC);
        double term3 = dHp / (kH * sH);
        return Math.Sqrt(term1 * term1 + term2 * term2 + term3 * term3 + rT * term2 * term3);
    }

    private static double Deg2Rad(double deg) => deg * Math.PI / 180.0;
}
