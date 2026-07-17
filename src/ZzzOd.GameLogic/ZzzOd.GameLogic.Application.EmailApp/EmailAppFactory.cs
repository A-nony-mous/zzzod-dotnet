using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.EmailApp;

/// <summary>
/// 邮件应用 factory。
/// </summary>
public sealed class EmailAppFactory : ZApplicationFactory
{
	/// <summary>
	/// 初始化 factory。
	/// </summary>
	public EmailAppFactory(ZContext context)
		: base(context, new ZApplicationFactoryMetadata("email", "邮件", "one_dragon", NeedNotify: true))
	{
	}

	/// <inheritdoc />
	public override IApplication CreateApplication(int instanceIndex, string groupId)
	{
		return new EmailApp(base.Context, (ZApplicationRunRecord)GetRunRecord(instanceIndex));
	}

	/// <inheritdoc />
	public override IApplicationRunRecord GetRunRecord(int instanceIndex)
	{
		return EmailRunRecord.Load(base.Context.Environment, instanceIndex, base.Context.GameAccountConfig.GameRefreshHourOffset);
	}
}
