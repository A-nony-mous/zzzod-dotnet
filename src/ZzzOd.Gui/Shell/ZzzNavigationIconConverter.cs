using System.Globalization;
using Avalonia.Data.Converters;

namespace ZzzOd.Gui.Shell;

public sealed class ZzzNavigationIconConverter : IMultiValueConverter
{
    public static ZzzNavigationIconConverter Instance { get; } = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 3 || values[0] is not string regular || values[1] is not string selected)
        {
            return null;
        }

        return values[2] is true ? selected : regular;
    }
}
