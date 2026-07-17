using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.DriveDiscDismantle;

/// <summary>
/// 驱动盘拆解应用 factory。
/// </summary>
public sealed class DriveDiscDismantleAppFactory : ZApplicationFactory
{
	/// <summary>
	/// 初始化 factory。
	/// </summary>
	public DriveDiscDismantleAppFactory(ZContext context)
		: base(context, new ZApplicationFactoryMetadata("drive_disc_dismantle", "驱动盘拆解", "one_dragon", NeedNotify: true))
	{
	}

	/// <inheritdoc />
	public override IApplication CreateApplication(int instanceIndex, string groupId)
	{
		DriveDiscDismantleConfig config = (DriveDiscDismantleConfig)GetConfig(instanceIndex, groupId);
		return new DriveDiscDismantleApp(base.Context, config, (DriveDiscDismantleRunRecord)GetRunRecord(instanceIndex));
	}

	/// <inheritdoc />
	public override IApplicationConfig GetConfig(int instanceIndex, string groupId)
	{
		return DriveDiscDismantleConfig.Load(base.Context.Environment, instanceIndex, groupId);
	}

	/// <inheritdoc />
	public override IApplicationRunRecord GetRunRecord(int instanceIndex)
	{
		return DriveDiscDismantleRunRecord.Load(base.Context.Environment, instanceIndex, base.Context.GameAccountConfig.GameRefreshHourOffset);
	}
}
