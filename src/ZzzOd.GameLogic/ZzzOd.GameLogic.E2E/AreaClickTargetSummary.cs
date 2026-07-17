namespace ZzzOd.GameLogic.E2E;

/// <summary>
/// 区域点击目标明细。
/// </summary>
public sealed class AreaClickTargetSummary
{
	/// <summary>画面名称。</summary>
	public string ScreenName { get; set; } = string.Empty;

	/// <summary>区域名称。</summary>
	public string AreaName { get; set; } = string.Empty;

	/// <summary>区域类型。</summary>
	public string AreaKind { get; set; } = string.Empty;

	/// <summary>点击点 X。</summary>
	public int? ClickX { get; set; }

	/// <summary>点击点 Y。</summary>
	public int? ClickY { get; set; }

	/// <summary>是否按住 Alt。</summary>
	public bool PcAlt { get; set; }

	/// <summary>手柄动作名。</summary>
	public string? GamepadAction { get; set; }

	/// <summary>模板置信度。</summary>
	public double? MatchConfidence { get; set; }

	/// <summary>失败原因。</summary>
	public string? FailureReason { get; set; }
}
