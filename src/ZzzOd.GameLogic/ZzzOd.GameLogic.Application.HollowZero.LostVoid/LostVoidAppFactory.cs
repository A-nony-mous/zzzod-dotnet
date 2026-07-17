using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

/// <summary>
/// 迷失之地应用 factory。
/// </summary>
public sealed class LostVoidAppFactory : ZApplicationFactory
{
	/// <summary>
	/// 初始化迷失之地 factory。
	/// </summary>
	public LostVoidAppFactory(ZContext context)
		: base(context, new ZApplicationFactoryMetadata("lost_void", "迷失之地", "one_dragon", NeedNotify: true))
	{
	}

	/// <inheritdoc />
	public override IApplication CreateApplication(int instanceIndex, string groupId)
	{
		LostVoidConfig config = (LostVoidConfig)GetConfig(instanceIndex, groupId);
		return new LostVoidApp(base.Context, config, (LostVoidRunRecord)GetRunRecord(instanceIndex));
	}

	/// <inheritdoc />
	public override IApplicationConfig GetConfig(int instanceIndex, string groupId)
	{
		return LostVoidConfig.Load(base.Context.Environment, instanceIndex, groupId);
	}

	/// <inheritdoc />
	public override IApplicationRunRecord GetRunRecord(int instanceIndex)
	{
		LostVoidConfig config = (LostVoidConfig)GetConfig(instanceIndex, "one_dragon");
		return LostVoidRunRecord.Load(base.Context.Environment, config, instanceIndex, base.Context.GameAccountConfig.GameRefreshHourOffset);
	}
}
