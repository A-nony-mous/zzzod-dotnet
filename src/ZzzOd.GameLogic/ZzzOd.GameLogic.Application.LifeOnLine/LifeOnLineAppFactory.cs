using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.LifeOnLine;

/// <summary>
/// 生命热线应用 factory。
/// </summary>
public sealed class LifeOnLineAppFactory : ZApplicationFactory
{
	/// <summary>
	/// 初始化 factory。
	/// </summary>
	public LifeOnLineAppFactory(ZContext context)
		: base(context, new ZApplicationFactoryMetadata("life_on_line", "真·拿命验收", "one_dragon", NeedNotify: true))
	{
	}

	/// <inheritdoc />
	public override IApplication CreateApplication(int instanceIndex, string groupId)
	{
		LifeOnLineConfig config = (LifeOnLineConfig)GetConfig(instanceIndex, groupId);
		return new LifeOnLineApp(base.Context, config, (LifeOnLineRunRecord)GetRunRecord(instanceIndex));
	}

	/// <inheritdoc />
	public override IApplicationConfig GetConfig(int instanceIndex, string groupId)
	{
		return LifeOnLineConfig.Load(base.Context.Environment, instanceIndex, groupId);
	}

	/// <inheritdoc />
	public override IApplicationRunRecord GetRunRecord(int instanceIndex)
	{
		LifeOnLineConfig config = LifeOnLineConfig.Load(base.Context.Environment, instanceIndex, "one_dragon");
		return LifeOnLineRunRecord.Load(base.Context.Environment, instanceIndex, config, base.Context.GameAccountConfig.GameRefreshHourOffset);
	}
}
