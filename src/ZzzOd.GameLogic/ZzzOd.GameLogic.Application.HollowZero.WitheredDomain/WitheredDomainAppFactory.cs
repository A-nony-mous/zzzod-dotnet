using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.HollowZero.WitheredDomain;

/// <summary>
/// 枯萎之都应用 factory。
/// </summary>
public sealed class WitheredDomainAppFactory : ZApplicationFactory
{
	/// <summary>
	/// 初始化枯萎之都 factory。
	/// </summary>
	public WitheredDomainAppFactory(ZContext context)
		: base(context, new ZApplicationFactoryMetadata("withered_domain", "枯萎之都", "default", NeedNotify: true))
	{
	}

	/// <inheritdoc />
	public override IApplication CreateApplication(int instanceIndex, string groupId)
	{
		WitheredDomainConfig config = (WitheredDomainConfig)GetConfig(instanceIndex, groupId);
		return new WitheredDomainApp(base.Context, config, (WitheredDomainRunRecord)GetRunRecord(instanceIndex));
	}

	/// <inheritdoc />
	public override IApplicationConfig GetConfig(int instanceIndex, string groupId)
	{
		return WitheredDomainConfig.Load(base.Context.Environment, instanceIndex, groupId);
	}

	/// <inheritdoc />
	public override IApplicationRunRecord GetRunRecord(int instanceIndex)
	{
		WitheredDomainConfig config = (WitheredDomainConfig)GetConfig(instanceIndex, "default");
		return WitheredDomainRunRecord.Load(base.Context.Environment, config, instanceIndex, base.Context.GameAccountConfig.GameRefreshHourOffset);
	}
}
