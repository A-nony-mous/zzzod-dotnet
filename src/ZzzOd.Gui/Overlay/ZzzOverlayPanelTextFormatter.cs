using System.Globalization;
using ZzzOd.AppHost.Overlay;

namespace ZzzOd.Gui.Overlay;

internal static class ZzzOverlayPanelTextFormatter
{
    private const string EmptyValue = "/";
    private static readonly string[] CoreMetricOrder = ["ocr_ms", "yolo_ms", "cv_pipeline_ms", "operation_round_ms", "overlay_refresh_ms"];

    public static string Format(string panelId, ZzzOverlaySnapshotDto snapshot, ZzzOverlayGuiSettings settings, DateTimeOffset? now = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(panelId);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(settings);
        return panelId switch
        {
            "log" => FormatLogs(snapshot.Logs, settings.LogMaxLines, settings.LogFadeSeconds, now),
            "state" => FormatState(snapshot.State),
            "battle" => FormatBattle(snapshot.State?.AutoBattle),
            "decision" => FormatDecisions(snapshot.Decisions),
            "timeline" => FormatTimeline(snapshot.Timeline),
            "performance" => FormatPerformance(snapshot.Performance, settings.PerformanceMetrics, now),
            _ => string.Empty,
        };
    }

    internal static string FormatLogs(
        IReadOnlyList<ZzzOverlayLogEntryDto> logs,
        int maxLines,
        int fadeSeconds,
        DateTimeOffset? now = null)
    {
        DateTimeOffset current = now ?? DateTimeOffset.UtcNow;
        return string.Join(
            Environment.NewLine,
            logs
                .Where(item => current - item.Timestamp <= TimeSpan.FromSeconds(Math.Max(3, fadeSeconds)))
                .OrderBy(item => item.Timestamp)
                .TakeLast(Math.Max(20, maxLines))
                .Select(item => $"[{item.Timestamp.ToLocalTime():HH:mm:ss}] [{item.Level}]" +
                    (string.IsNullOrWhiteSpace(item.Category) ? string.Empty : $" [{item.Category}]") +
                    $" {item.Message}" +
                    (string.IsNullOrWhiteSpace(item.Exception) ? string.Empty : Environment.NewLine + item.Exception)));
    }

    internal static string FormatBattle(ZzzOverlayAutoBattleStateDto? autoBattle)
    {
        bool running = autoBattle?.IsRunning == true;
        string trigger = running ? Fallback(autoBattle!.CurrentTrigger) : EmptyValue;
        string expression = running ? Fallback(autoBattle!.CurrentExpression) : EmptyValue;
        string duration = running && autoBattle!.CurrentDurationSeconds is double seconds
            ? seconds.ToString("0.0", CultureInfo.InvariantCulture) + "s"
            : EmptyValue;
        List<string> rows =
        [
            $"[触发器] {trigger}",
            $"[条件集] {expression}",
            $"[持续] {duration}",
        ];

        IReadOnlyList<ZzzOverlayBattleStateRowDto> stateRows = running
            ? autoBattle!.StateRows ?? []
            : [];
        if (stateRows.Count > 0)
        {
            rows.Add(string.Empty);
            rows.AddRange(stateRows.Select(row =>
                $"{row.StateName} {row.SecondsSinceTrigger.ToString("0.0", CultureInfo.InvariantCulture)}" +
                (row.Value.HasValue ? $" {row.Value.Value.ToString(CultureInfo.InvariantCulture)}" : string.Empty)));
        }

        return string.Join(Environment.NewLine, rows);
    }

    private static string Fallback(string? value) =>
        string.IsNullOrWhiteSpace(value) ? EmptyValue : value;

    internal static string FormatState(ZzzOverlayRunStateDto? state)
    {
        if (state is null)
        {
            return string.Empty;
        }

        List<string> rows = [];
        Add(rows, "RunState", state.RunState);
        Add(rows, "CurrentAppId", state.CurrentAppId);
        Add(rows, "CurrentApp", state.CurrentApp);
        Add(rows, "CurrentNode", state.CurrentNode);
        Add(rows, "PreviousNode", state.PreviousNode);
        if (state.NodeRetry.HasValue)
        {
            Add(rows, "NodeRetry", state.NodeRetry.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        Add(rows, "CurrentGroupId", state.CurrentGroupId);
        if (state.CurrentInstanceIndex.HasValue)
        {
            Add(rows, "CurrentInstanceIndex", state.CurrentInstanceIndex.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        if (state.AutoBattle is ZzzOverlayAutoBattleStateDto autoBattle)
        {
            Add(rows, "AutoBattle", autoBattle.IsRunning ? "RUNNING" : "STOP");
            Add(rows, "FrontAgent", autoBattle.FrontAgentName);
            if (autoBattle.FrontSpecialReady.HasValue)
            {
                Add(rows, "FrontSpecial", autoBattle.FrontSpecialReady.Value ? "Y" : "N");
            }

            if (autoBattle.FrontUltimateReady.HasValue)
            {
                Add(rows, "FrontUltimate", autoBattle.FrontUltimateReady.Value ? "Y" : "N");
            }

            Add(rows, "Dodge", autoBattle.LatestDodgeState);
            if (autoBattle.ChainReady.HasValue)
            {
                Add(rows, "Chain", autoBattle.ChainReady.Value ? "READY" : "-");
            }

            Add(rows, "QuickAssist", autoBattle.LatestQuickAssistAgent);
            if (autoBattle.DistanceMeters.HasValue)
            {
                Add(rows, "Distance", autoBattle.DistanceMeters.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "m");
            }
        }

        return string.Join(Environment.NewLine, rows);
    }

    internal static string FormatDecisions(IReadOnlyList<ZzzOverlayDecisionDto> decisions) =>
        string.Join(
            Environment.NewLine,
            decisions
                .OrderBy(item => item.CreatedAt)
                .TakeLast(24)
                .Select(item => $"[{item.CreatedAt.ToLocalTime():HH:mm:ss}] [{item.Source}] {item.Trigger} => {item.Expression} / {item.Operation} [{item.Status}]"));

    internal static string FormatTimeline(IReadOnlyList<ZzzOverlayTimelineItemDto> timeline) =>
        string.Join(
            Environment.NewLine,
            timeline
                .OrderBy(item => item.CreatedAt)
                .TakeLast(28)
                .Select(item => $"[{item.CreatedAt.ToLocalTime():HH:mm:ss}] [{item.Level}] [{item.Category}] {item.Title} {item.Detail}".TrimEnd()));

    internal static string FormatPerformance(
        IReadOnlyList<ZzzOverlayPerformanceSampleDto> samples,
        IReadOnlyDictionary<string, bool> enabledMetricMap,
        DateTimeOffset? now = null)
    {
        DateTimeOffset current = now ?? DateTimeOffset.UtcNow;
        Dictionary<string, ZzzOverlayPerformanceSampleDto> latest = samples
            .GroupBy(item => item.Metric, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.CreatedAt).Last(), StringComparer.Ordinal);
        Dictionary<string, int> coreOrder = CoreMetricOrder
            .Select((metric, index) => (metric, index))
            .ToDictionary(item => item.metric, item => item.index, StringComparer.Ordinal);
        return string.Join(
            Environment.NewLine,
            latest.Values
                .Where(item => !enabledMetricMap.TryGetValue(item.Metric, out bool enabled) || enabled)
                .OrderBy(item => coreOrder.TryGetValue(item.Metric, out int index) ? index : int.MaxValue)
                .ThenBy(item => item.Metric, StringComparer.Ordinal)
                .Select(item => $"{item.Metric}: {item.Value:F2} {item.Unit} ({Math.Max(0, (int)(current - item.CreatedAt).TotalMilliseconds)}ms ago)"));
    }

    private static void Add(List<string> rows, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            rows.Add($"{key}: {value}");
        }
    }
}
