using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.DriveDiscDismantle;

/// <summary>
/// 驱动盘拆解应用流程。
/// </summary>
public interface IDriveDiscDismantleAppFlow
{
	/// <summary>
	/// 运行驱动盘拆解。
	/// </summary>
	Task<OperationResult> RunAsync(ZContext context, DriveDiscDismantleConfig config, DriveDiscDismantleRunRecord runRecord, CancellationToken cancellationToken);
}
