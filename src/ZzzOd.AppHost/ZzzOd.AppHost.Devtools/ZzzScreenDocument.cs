using System.Collections.Generic;

namespace ZzzOd.AppHost.Devtools;

public sealed record ZzzScreenDocument(string OldScreenId, string ScreenId, string ScreenName, string AppId, bool PcAlt, IReadOnlyList<ZzzScreenAreaDocument> Areas);
