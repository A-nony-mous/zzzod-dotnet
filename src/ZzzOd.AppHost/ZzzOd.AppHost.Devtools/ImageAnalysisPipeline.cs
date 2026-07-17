using System.Collections.Generic;

namespace ZzzOd.AppHost.Devtools;

public sealed record ImageAnalysisPipeline(IReadOnlyList<ImageAnalysisStep> Steps);
