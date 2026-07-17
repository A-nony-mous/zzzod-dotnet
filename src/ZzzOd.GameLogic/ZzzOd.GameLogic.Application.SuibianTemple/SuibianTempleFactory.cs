using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.SuibianTemple;

/// <summary>
/// 随便观应用 factory。
/// </summary>
public sealed class SuibianTempleFactory : ZApplicationFactory
{
	/// <summary>
	/// 初始化随便观 factory。
	/// </summary>
	public SuibianTempleFactory(ZContext context)
		: base(context, new ZApplicationFactoryMetadata("suibian_temple", "随便观", "one_dragon", NeedNotify: true))
	{
	}

	/// <inheritdoc />
	public override IApplication CreateApplication(int instanceIndex, string groupId)
	{
		SuibianTempleConfig config = (SuibianTempleConfig)GetConfig(instanceIndex, groupId);
		return new SuibianTempleApp(base.Context, config, (SuibianTempleRunRecord)GetRunRecord(instanceIndex));
	}

	/// <inheritdoc />
	public override IApplicationConfig GetConfig(int instanceIndex, string groupId)
	{
		return SuibianTempleConfig.Load(base.Context.Environment, instanceIndex, groupId);
	}

	/// <inheritdoc />
	public override IApplicationRunRecord GetRunRecord(int instanceIndex)
	{
		return SuibianTempleRunRecord.Load(base.Context.Environment, instanceIndex, base.Context.GameAccountConfig.GameRefreshHourOffset);
	}
}
