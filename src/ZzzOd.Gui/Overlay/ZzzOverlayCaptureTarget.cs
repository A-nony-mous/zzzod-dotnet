using Avalonia;
using Avalonia.Controls;

namespace ZzzOd.Gui.Overlay;

internal sealed record ZzzOverlayCaptureTarget(string Id, Window Window, PixelPoint Position, PixelSize Size);
