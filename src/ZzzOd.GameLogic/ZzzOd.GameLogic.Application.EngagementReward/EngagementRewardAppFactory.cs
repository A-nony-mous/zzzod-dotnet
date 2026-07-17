using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.EngagementReward;

/// <summary>
/// 活跃度奖励应用 factory。
/// </summary>
public sealed class EngagementRewardAppFactory : ZApplicationFactory
{
	/// <summary>
	/// 初始化 factory。
	/// </summary>
	public EngagementRewardAppFactory(ZContext context)
		: base(context, new ZApplicationFactoryMetadata("engagement_reward", "活跃度奖励", "one_dragon", NeedNotify: true))
	{
	}

	/// <inheritdoc />
	public override IApplication CreateApplication(int instanceIndex, string groupId)
	{
		return new EngagementRewardApp(base.Context, (ZApplicationRunRecord)GetRunRecord(instanceIndex));
	}

	/// <inheritdoc />
	public override IApplicationRunRecord GetRunRecord(int instanceIndex)
	{
		return EngagementRewardRunRecord.Load(base.Context.Environment, instanceIndex, base.Context.GameAccountConfig.GameRefreshHourOffset);
	}
}
