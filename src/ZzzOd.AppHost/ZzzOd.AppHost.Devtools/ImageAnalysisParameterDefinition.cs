using System.Collections.Generic;

namespace ZzzOd.AppHost.Devtools;

public sealed record ImageAnalysisParameterDefinition(string Name, string Label, ImageAnalysisParameterKind Kind, object? DefaultValue, double Minimum = 0.0, double Maximum = 0.0, IReadOnlyList<string>? Options = null, string? Parent = null, string? ToolTip = null);
