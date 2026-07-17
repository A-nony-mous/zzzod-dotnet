using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.ScratchCard;

/// <summary>
/// 刮刮卡应用 factory。
/// </summary>
public sealed class ScratchCardFactory : ZApplicationFactory
{
	/// <summary>
	/// 初始化 factory。
	/// </summary>
	public ScratchCardFactory(ZContext context)
		: base(context, new ZApplicationFactoryMetadata("scratch_card", "刮刮卡", "one_dragon", NeedNotify: true))
	{
	}

	/// <inheritdoc />
	public override IApplication CreateApplication(int instanceIndex, string groupId)
	{
		return new ScratchCardApp(base.Context, (ZApplicationRunRecord)GetRunRecord(instanceIndex));
	}

	/// <inheritdoc />
	public override IApplicationRunRecord GetRunRecord(int instanceIndex)
	{
		return ScratchCardRunRecord.Load(base.Context.Environment, instanceIndex, base.Context.GameAccountConfig.GameRefreshHourOffset);
	}
}
