using System.Collections.Generic;

namespace ZzzOd.AppHost.Devtools;

public sealed record ImageAnalysisStep(string Name, Dictionary<string, object?> Parameters);
