using System.Collections.Generic;
using ZzzOd.GameLogic.Config;

namespace ZzzOd.AppHost.Backend;

internal sealed record ZzzOneDragonAppMergeResult(IReadOnlyList<OneDragonApplicationConfigItem> AllApps, IReadOnlyList<OneDragonApplicationConfigItem> VisibleApps, bool Changed);
