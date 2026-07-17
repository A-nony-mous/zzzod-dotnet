using Avalonia.Media;
using OpenCvSharp;

namespace ZzzOd.Gui.Services.Home;

internal static class ZzzHomeThemeColorExtractor
{
    private const int MaximumDimension = 200;
    private const double MinimumSaturation = 0.05;
    private const double MinimumValue = 0.1;
    private const double ThemeSaturation = 0.6;
    private const double ThemeValue = 0.7;

    internal static bool TryExtract(string path, bool video, out Color color)
    {
        using Mat source = video ? ReadFirstVideoFrame(path) : Cv2.ImRead(path, ImreadModes.Color);
        return TryExtract(source, out color);
    }

    internal static bool TryExtract(Mat source, out Color color)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Empty() || source.Width <= 0 || source.Height <= 0)
        {
            color = default;
            return false;
        }

        double scale = Math.Min(1d, MaximumDimension / (double)Math.Max(source.Width, source.Height));
        using Mat sample = new();
        if (scale < 1d)
        {
            Cv2.Resize(
                source,
                sample,
                new OpenCvSharp.Size(
                    Math.Max(1, (int)(source.Width * scale)),
                    Math.Max(1, (int)(source.Height * scale))));
        }
        else
        {
            source.CopyTo(sample);
        }

        using Mat hsv = new();
        Cv2.CvtColor(sample, hsv, ColorConversionCodes.BGR2HSV);
        double sumCos = 0d;
        double sumSin = 0d;
        for (int y = 0; y < hsv.Rows; y++)
        {
            for (int x = 0; x < hsv.Cols; x++)
            {
                Vec3b pixel = hsv.At<Vec3b>(y, x);
                double saturation = pixel.Item1 / 255d;
                double value = pixel.Item2 / 255d;
                if (saturation <= MinimumSaturation || value <= MinimumValue)
                {
                    continue;
                }

                double weight = saturation * value;
                double angle = pixel.Item0 * (Math.PI / 90d);
                sumCos += Math.Cos(angle) * weight;
                sumSin += Math.Sin(angle) * weight;
            }
        }

        if (Math.Sqrt(sumCos * sumCos + sumSin * sumSin) < 1e-6)
        {
            color = default;
            return false;
        }

        double hue = Math.Atan2(sumSin, sumCos) * (180d / Math.PI);
        if (hue < 0d)
        {
            hue += 360d;
        }

        color = FromHsv(hue, ThemeSaturation, ThemeValue);
        return true;
    }

    private static Mat ReadFirstVideoFrame(string path)
    {
        using VideoCapture capture = new(path);
        Mat frame = new();
        capture.Read(frame);
        return frame;
    }

    private static Color FromHsv(double hue, double saturation, double value)
    {
        double chroma = value * saturation;
        double sector = (hue % 360d) / 60d;
        double secondary = chroma * (1d - Math.Abs(sector % 2d - 1d));
        (double red, double green, double blue) = sector switch
        {
            < 1d => (chroma, secondary, 0d),
            < 2d => (secondary, chroma, 0d),
            < 3d => (0d, chroma, secondary),
            < 4d => (0d, secondary, chroma),
            < 5d => (secondary, 0d, chroma),
            _ => (chroma, 0d, secondary),
        };
        double match = value - chroma;
        return Color.FromRgb(ToByte(red + match), ToByte(green + match), ToByte(blue + match));
    }

    private static byte ToByte(double component) =>
        (byte)Math.Clamp((int)Math.Round(component * 255d, MidpointRounding.AwayFromZero), 0, 255);
}

