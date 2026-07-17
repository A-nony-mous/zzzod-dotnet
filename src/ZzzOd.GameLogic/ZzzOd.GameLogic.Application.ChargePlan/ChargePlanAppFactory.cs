using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.ChargePlan;

/// <summary>
/// 电量计划应用 factory。
/// </summary>
public sealed class ChargePlanAppFactory : ZApplicationFactory
{
	/// <summary>
	/// 初始化 factory。
	/// </summary>
	public ChargePlanAppFactory(ZContext context)
		: base(context, new ZApplicationFactoryMetadata("charge_plan", "体力刷本", "one_dragon", NeedNotify: true))
	{
	}

	/// <inheritdoc />
	public override IApplication CreateApplication(int instanceIndex, string groupId)
	{
		return new ChargePlanApp(base.Context, (ChargePlanConfig)GetConfig(instanceIndex, groupId), (ChargePlanRunRecord)GetRunRecord(instanceIndex));
	}

	/// <inheritdoc />
	public override IApplicationConfig GetConfig(int instanceIndex, string groupId)
	{
		return ChargePlanConfig.Load(base.Context.Environment, instanceIndex, groupId);
	}

	/// <inheritdoc />
	public override IApplicationRunRecord GetRunRecord(int instanceIndex)
	{
		return ChargePlanRunRecord.Load(base.Context.Environment, instanceIndex, base.Context.GameAccountConfig.GameRefreshHourOffset);
	}
}
