using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.DailySignIn;

/// <summary>
/// 每日签到应用 factory。
/// </summary>
public sealed class DailySignInFactory : ZApplicationFactory
{
	/// <summary>
	/// 初始化 factory。
	/// </summary>
	public DailySignInFactory(ZContext context)
		: base(context, new ZApplicationFactoryMetadata("daily_signin", "每日签到", "one_dragon", NeedNotify: true))
	{
	}

	/// <inheritdoc />
	public override IApplication CreateApplication(int instanceIndex, string groupId)
	{
		DailySignInConfig config = (DailySignInConfig)GetConfig(instanceIndex, groupId);
		return new DailySignInApp(base.Context, instanceIndex, groupId, config, (DailySignInRunRecord)GetRunRecord(instanceIndex));
	}

	/// <inheritdoc />
	public override IApplicationConfig GetConfig(int instanceIndex, string groupId)
	{
		return DailySignInConfig.Load(base.Context.Environment, instanceIndex, groupId);
	}

	/// <inheritdoc />
	public override IApplicationRunRecord GetRunRecord(int instanceIndex)
	{
		return DailySignInRunRecord.Load(base.Context.Environment, instanceIndex, base.Context.GameAccountConfig.GameRefreshHourOffset);
	}
}
