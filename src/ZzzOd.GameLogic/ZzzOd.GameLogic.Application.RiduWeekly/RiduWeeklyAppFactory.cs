using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.RiduWeekly;

/// <summary>
/// 丽都周纪应用 factory。
/// </summary>
public sealed class RiduWeeklyAppFactory : ZApplicationFactory
{
	/// <summary>
	/// 初始化 factory。
	/// </summary>
	public RiduWeeklyAppFactory(ZContext context)
		: base(context, new ZApplicationFactoryMetadata("ridu_weekly", "丽都周纪 (领奖励)", "one_dragon", NeedNotify: true))
	{
	}

	/// <inheritdoc />
	public override IApplication CreateApplication(int instanceIndex, string groupId)
	{
		return new RiduWeeklyApp(base.Context, (ZApplicationRunRecord)GetRunRecord(instanceIndex));
	}

	/// <inheritdoc />
	public override IApplicationRunRecord GetRunRecord(int instanceIndex)
	{
		return RiduWeeklyRunRecord.Load(base.Context.Environment, instanceIndex, base.Context.GameAccountConfig.GameRefreshHourOffset);
	}
}
