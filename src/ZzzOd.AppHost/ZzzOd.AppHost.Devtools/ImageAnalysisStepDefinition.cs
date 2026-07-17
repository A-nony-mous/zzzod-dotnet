using System.Collections.Generic;

namespace ZzzOd.AppHost.Devtools;

public sealed record ImageAnalysisStepDefinition(string Name, string Description, IReadOnlyList<ImageAnalysisParameterDefinition> Parameters);
