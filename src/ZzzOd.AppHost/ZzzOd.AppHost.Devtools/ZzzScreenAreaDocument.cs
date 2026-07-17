using System.Collections.Generic;

namespace ZzzOd.AppHost.Devtools;

public sealed record ZzzScreenAreaDocument(string AreaName, bool IdMark, int X1, int Y1, int X2, int Y2, string Text, double LcsPercent, string TemplateSubDir, string TemplateId, double TemplateMatchThreshold, IReadOnlyList<IReadOnlyList<int>>? ColorRange, IReadOnlyList<string> GotoList, string? GamepadKey);
