namespace Aurora.Shared;

internal static class WindowSizing
{
    public static (int Width, int Height) Fit(int widthDip, int heightDip, uint dpi, int workWidth, int workHeight)
    {
        double scale = (dpi == 0 ? 96 : dpi) / 96d;
        int margin = (int)Math.Round(32 * scale);
        int width = Math.Max(1, (int)Math.Min(workWidth - margin, Math.Round(widthDip * scale)));
        int height = Math.Max(1, (int)Math.Min(workHeight - margin, Math.Round(heightDip * scale)));
        return (width, height);
    }
}
