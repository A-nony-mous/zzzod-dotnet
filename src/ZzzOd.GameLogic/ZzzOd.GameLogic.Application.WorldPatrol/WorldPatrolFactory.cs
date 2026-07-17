using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.WorldPatrol;

/// <summary>
/// 锄大地应用 factory。
/// </summary>
public sealed class WorldPatrolFactory : ZApplicationFactory
{
	/// <summary>
	/// 初始化锄大地 factory。
	/// </summary>
	public WorldPatrolFactory(ZContext context)
		: base(context, new ZApplicationFactoryMetadata("world_patrol", "锄大地", "default", NeedNotify: true))
	{
	}

	/// <inheritdoc />
	public override IApplication CreateApplication(int instanceIndex, string groupId)
	{
		WorldPatrolConfig config = (WorldPatrolConfig)GetConfig(instanceIndex, groupId);
		return new WorldPatrolApp(base.Context, config, (WorldPatrolRunRecord)GetRunRecord(instanceIndex));
	}

	/// <inheritdoc />
	public override IApplicationConfig GetConfig(int instanceIndex, string groupId)
	{
		return WorldPatrolConfig.Load(base.Context.Environment, instanceIndex, groupId);
	}

	/// <inheritdoc />
	public override IApplicationRunRecord GetRunRecord(int instanceIndex)
	{
		return WorldPatrolRunRecord.Load(base.Context.Environment, instanceIndex, base.Context.GameAccountConfig.GameRefreshHourOffset);
	}
}
