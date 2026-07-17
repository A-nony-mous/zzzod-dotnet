using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.DriveDiscDismantle;

/// <summary>
/// 驱动盘拆解应用。
/// </summary>
public sealed class DriveDiscDismantleApp : ZApplication
{
	private readonly DriveDiscDismantleConfig _config;

	private readonly DriveDiscDismantleRunRecord _runRecord;

	private readonly IDriveDiscDismantleAppFlow _flow;

	/// <summary>
	/// 初始化驱动盘拆解应用。
	/// </summary>
	public DriveDiscDismantleApp(ZContext context, DriveDiscDismantleConfig? config = null, DriveDiscDismantleRunRecord? runRecord = null, IDriveDiscDismantleAppFlow? flow = null)
		: base(context, "drive_disc_dismantle", runRecord, "驱动盘拆解")
	{
		_config = config ?? DriveDiscDismantleConfig.Load(context.Environment, context.RunContext.CurrentInstanceIndex.GetValueOrDefault(), "one_dragon");
		_runRecord = runRecord ?? new DriveDiscDismantleRunRecord(context.GameAccountConfig.GameRefreshHourOffset);
		_flow = flow ?? new OperationDriveDiscDismantleAppFlow();
	}

	/// <inheritdoc />
	protected override async Task<OperationResult> ExecuteCoreAsync(CancellationToken cancellationToken)
	{
		base.Context.ScreenContext.EnterScope("drive_disc_dismantle");
		try
		{
			return await _flow.RunAsync(base.Context, _config, _runRecord, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		finally
		{
			base.Context.ScreenContext.ExitScope();
		}
	}
}
