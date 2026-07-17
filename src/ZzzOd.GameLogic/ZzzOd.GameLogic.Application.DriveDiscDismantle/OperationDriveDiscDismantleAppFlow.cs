using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.DriveDiscDismantle;

/// <summary>
/// 默认驱动盘拆。Operation 流程。
/// </summary>
public sealed class OperationDriveDiscDismantleAppFlow : IDriveDiscDismantleAppFlow
{
	/// <inheritdoc />
	public Task<OperationResult> RunAsync(ZContext context, DriveDiscDismantleConfig config, DriveDiscDismantleRunRecord runRecord, CancellationToken cancellationToken)
	{
		DriveDiscDismantleOperation driveDiscDismantleOperation = new DriveDiscDismantleOperation(context, config);
		return driveDiscDismantleOperation.ExecuteAsync(cancellationToken);
	}
}
