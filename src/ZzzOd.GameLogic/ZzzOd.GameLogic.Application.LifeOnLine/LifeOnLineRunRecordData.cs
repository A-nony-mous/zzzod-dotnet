using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Application.LifeOnLine;

/// <summary>
/// 生命热线运行记录 YAML 数据。
/// </summary>
public sealed class LifeOnLineRunRecordData
{
	/// <summary>当日完成次数。</summary>
	[YamlMember(Alias = "daily_run_times", ApplyNamingConventions = false)]
	public int DailyRunTimes { get; set; }
}
