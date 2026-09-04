namespace WinPieGestures.WinUI.Input;

internal static class RadialSelectionMath
{
    public static int QuantizeMain(double angle, int sectorCount)
    {
        int count = sectorCount is 4 or 8 or 12 ? sectorCount : 8;
        double step = Math.Tau / count;
        int index = (int)Math.Round((angle + Math.PI / 2) / step, MidpointRounding.AwayFromZero);
        return ((index % count) + count) % count;
    }

    public static int QuantizeSub(double angle, int mainIndex, int mainCount, int subCount)
    {
        if (mainIndex < 0 || mainIndex >= mainCount || subCount <= 0)
        {
            return -1;
        }

        double mainStep = Math.Tau / mainCount;
        double parentCenter = -Math.PI / 2 + mainIndex * mainStep;
        double parentStart = parentCenter - mainStep / 2;
        double relative = NormalizeSigned(angle - parentStart);
        if (relative < 0)
        {
            relative += Math.Tau;
        }

        // The main sector is already locked by QuantizeMain. Clamp tiny floating-point
        // excursions at the boundary rather than accidentally selecting an adjacent child.
        relative = Math.Clamp(relative, 0, Math.Max(0, mainStep - double.Epsilon));
        int index = (int)Math.Floor(relative / (mainStep / subCount));
        return Math.Clamp(index, 0, subCount - 1);
    }

    private static double NormalizeSigned(double value)
    {
        while (value <= -Math.PI) value += Math.Tau;
        while (value > Math.PI) value -= Math.Tau;
        return value;
    }
}
