using System.Collections.Generic;

namespace ZzzOd.AppHost.Devtools;

public sealed record ImageAnalysisExecutionResult(byte[] DisplayImage, byte[]? MaskImage, IReadOnlyList<string> AnalysisResults, IReadOnlyList<ImageAnalysisStepTiming> StepTimings, double TotalMilliseconds);
