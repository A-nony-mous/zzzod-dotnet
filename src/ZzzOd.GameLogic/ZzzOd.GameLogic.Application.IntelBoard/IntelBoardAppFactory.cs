using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.IntelBoard;

/// <summary>
/// 情报板应用 factory。
/// </summary>
public sealed class IntelBoardAppFactory : ZApplicationFactory
{
	/// <summary>
	/// 初始化 factory。
	/// </summary>
	public IntelBoardAppFactory(ZContext context)
		: base(context, new ZApplicationFactoryMetadata("intel_board", "情报板", "one_dragon", NeedNotify: true))
	{
	}

	/// <inheritdoc />
	public override IApplication CreateApplication(int instanceIndex, string groupId)
	{
		IntelBoardConfig config = (IntelBoardConfig)GetConfig(instanceIndex, groupId);
		return new IntelBoardApp(base.Context, config, (IntelBoardRunRecord)GetRunRecord(instanceIndex));
	}

	/// <inheritdoc />
	public override IApplicationConfig GetConfig(int instanceIndex, string groupId)
	{
		return IntelBoardConfig.Load(base.Context.Environment, instanceIndex, groupId);
	}

	/// <inheritdoc />
	public override IApplicationRunRecord GetRunRecord(int instanceIndex)
	{
		IntelBoardConfig config = IntelBoardConfig.Load(base.Context.Environment, instanceIndex, "one_dragon");
		return IntelBoardRunRecord.Load(base.Context.Environment, instanceIndex, config, base.Context.GameAccountConfig.GameRefreshHourOffset);
	}
}
