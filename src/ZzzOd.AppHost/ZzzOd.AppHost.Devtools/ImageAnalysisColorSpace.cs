using System.Collections.Generic;

namespace ZzzOd.AppHost.Devtools;

public sealed record ImageAnalysisColorSpace(string Name, IReadOnlyList<ImageAnalysisChannel> Channels);
