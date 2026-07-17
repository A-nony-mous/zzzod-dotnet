using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Application.DriveDiscDismantle;

/// <summary>
/// 默认驱动盘拆解流程服务。
/// </summary>
public sealed class DefaultDriveDiscDismantleOperationServices : IDriveDiscDismantleOperationServices
{
	/// <inheritdoc />
	public Task<OperationResult> BackToNormalWorldAsync(ZContext context)
	{
		return new BackToNormalWorld(context).ExecuteAsync();
	}
}
