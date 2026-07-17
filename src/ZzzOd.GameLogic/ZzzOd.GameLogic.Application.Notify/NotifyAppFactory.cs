using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.Notify;

/// <summary>
/// 通知应用 factory。
/// </summary>
public sealed class NotifyAppFactory : ZApplicationFactory
{
	/// <summary>
	/// 初始化通知应用 factory。
	/// </summary>
	public NotifyAppFactory(ZContext context)
		: base(context, new ZApplicationFactoryMetadata("notify", "通知", "one_dragon"))
	{
	}

	/// <inheritdoc />
	public override IApplication CreateApplication(int instanceIndex, string groupId)
	{
		return new NotifyApp(base.Context, (NotifyRunRecord)GetRunRecord(instanceIndex));
	}

	/// <inheritdoc />
	public override IApplicationRunRecord GetRunRecord(int instanceIndex)
	{
		return NotifyRunRecord.Load(base.Context.Environment, instanceIndex, base.Context.GameAccountConfig.GameRefreshHourOffset);
	}
}
