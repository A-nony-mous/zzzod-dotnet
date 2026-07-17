using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.RandomPlay;

/// <summary>
/// 录像店营业应用 factory。
/// </summary>
public sealed class RandomPlayAppFactory : ZApplicationFactory
{
	/// <summary>
	/// 初始化 factory。
	/// </summary>
	public RandomPlayAppFactory(ZContext context)
		: base(context, new ZApplicationFactoryMetadata("random_play", "录像店营业", "one_dragon", NeedNotify: true))
	{
	}

	/// <inheritdoc />
	public override IApplication CreateApplication(int instanceIndex, string groupId)
	{
		RandomPlayConfig config = (RandomPlayConfig)GetConfig(instanceIndex, groupId);
		return new RandomPlayApp(base.Context, config, (RandomPlayRunRecord)GetRunRecord(instanceIndex));
	}

	/// <inheritdoc />
	public override IApplicationConfig GetConfig(int instanceIndex, string groupId)
	{
		return RandomPlayConfig.Load(base.Context.Environment, instanceIndex, groupId);
	}

	/// <inheritdoc />
	public override IApplicationRunRecord GetRunRecord(int instanceIndex)
	{
		return RandomPlayRunRecord.Load(base.Context.Environment, instanceIndex, base.Context.GameAccountConfig.GameRefreshHourOffset);
	}
}
