using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.ShiyuDefense;

/// <summary>
/// 式舆防卫战应用 factory。
/// </summary>
public sealed class ShiyuDefenseAppFactory : ZApplicationFactory
{
	/// <summary>
	/// 初始化 factory。
	/// </summary>
	public ShiyuDefenseAppFactory(ZContext context)
		: base(context, new ZApplicationFactoryMetadata("shiyu_defense", "式舆防卫战", "one_dragon", NeedNotify: true))
	{
	}

	/// <inheritdoc />
	public override IApplication CreateApplication(int instanceIndex, string groupId)
	{
		ShiyuDefenseConfig config = (ShiyuDefenseConfig)GetConfig(instanceIndex, groupId);
		return new ShiyuDefenseApp(base.Context, config, (ShiyuDefenseRunRecord)GetRunRecord(instanceIndex));
	}

	/// <inheritdoc />
	public override IApplicationConfig GetConfig(int instanceIndex, string groupId)
	{
		return ShiyuDefenseConfig.Load(base.Context.Environment, instanceIndex, groupId);
	}

	/// <inheritdoc />
	public override IApplicationRunRecord GetRunRecord(int instanceIndex)
	{
		ShiyuDefenseConfig config = ShiyuDefenseConfig.Load(base.Context.Environment, instanceIndex, "one_dragon");
		return ShiyuDefenseRunRecord.Load(base.Context.Environment, instanceIndex, config, base.Context.GameAccountConfig.GameRefreshHourOffset);
	}
}
