using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.RedemptionCode;

/// <summary>
/// 兑换码应用 factory。
/// </summary>
public sealed class RedemptionCodeFactory : ZApplicationFactory
{
	/// <summary>
	/// 初始化 factory。
	/// </summary>
	public RedemptionCodeFactory(ZContext context)
		: base(context, new ZApplicationFactoryMetadata("redemption_code", "兑换码", "one_dragon", NeedNotify: true))
	{
	}

	/// <inheritdoc />
	public override IApplication CreateApplication(int instanceIndex, string groupId)
	{
		RedemptionCodeConfig config = (RedemptionCodeConfig)GetConfig(instanceIndex, groupId);
		RedemptionCodeRunRecord runRecord = (RedemptionCodeRunRecord)GetRunRecord(instanceIndex);
		return new RedemptionCodeApp(base.Context, config, runRecord);
	}

	/// <inheritdoc />
	public override IApplicationConfig GetConfig(int instanceIndex, string groupId)
	{
		return RedemptionCodeConfig.Load(base.Context.Environment, instanceIndex, groupId);
	}

	/// <inheritdoc />
	public override IApplicationRunRecord GetRunRecord(int instanceIndex)
	{
		return RedemptionCodeRunRecord.Load(base.Context.Environment, instanceIndex, base.Context.GameAccountConfig.GameRefreshHourOffset);
	}
}
