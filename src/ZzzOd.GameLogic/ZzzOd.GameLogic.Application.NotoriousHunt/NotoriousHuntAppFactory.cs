using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.NotoriousHunt;

/// <summary>
/// 恶名狩猎应用 factory。
/// </summary>
public sealed class NotoriousHuntAppFactory : ZApplicationFactory
{
	/// <summary>
	/// 初始化 factory。
	/// </summary>
	public NotoriousHuntAppFactory(ZContext context)
		: base(context, new ZApplicationFactoryMetadata("notorious_hunt", "恶名狩猎", "one_dragon", NeedNotify: true))
	{
	}

	/// <inheritdoc />
	public override IApplication CreateApplication(int instanceIndex, string groupId)
	{
		NotoriousHuntConfig config = (NotoriousHuntConfig)GetConfig(instanceIndex, groupId);
		return new NotoriousHuntApp(base.Context, config, (NotoriousHuntRunRecord)GetRunRecord(instanceIndex));
	}

	/// <inheritdoc />
	public override IApplicationConfig GetConfig(int instanceIndex, string groupId)
	{
		return NotoriousHuntConfig.Load(base.Context.Environment, instanceIndex, groupId);
	}

	/// <inheritdoc />
	public override IApplicationRunRecord GetRunRecord(int instanceIndex)
	{
		NotoriousHuntConfig config = NotoriousHuntConfig.Load(base.Context.Environment, instanceIndex, "one_dragon");
		return NotoriousHuntRunRecord.Load(base.Context.Environment, instanceIndex, config, base.Context.GameAccountConfig.GameRefreshHourOffset);
	}
}
