using System.Globalization;

namespace ZzzOd.AppHost.Backend;

/// <summary>
/// 战斗状态显示行。
/// </summary>
/// <param name="StateName">状态名称。</param>
/// <param name="TriggerTime">最近触发时间。</param>
/// <param name="TriggerSeconds">距最近触发的秒数。</param>
/// <param name="Value">状态值。</param>
/// <param name="Revision">状态记录版本。</param>
public sealed record ZzzBattleAssistantStateDto(string StateName, double TriggerTime, double TriggerSeconds, int? Value, long Revision)
{
	/// <summary>按 BaselineParity 表格格式显示四位小数。</summary>
	public string TriggerSecondsText => TriggerSeconds.ToString("F4", CultureInfo.InvariantCulture);

	/// <summary>空值显示为空字符串。</summary>
	public string ValueText => Value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
}
