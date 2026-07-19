using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Controller;

namespace ZzzOd.GameLogic.Application.OneDragonApp;

/// <summary>
/// 一条龙自然完成后的 Windows 平台动作。
/// </summary>
public interface IZOneDragonCompletionPlatform
{
	/// <summary>
	/// 请求关闭游戏并等待窗口关闭。
	/// </summary>
	Task<OperationResult> CloseGameAsync(ControllerBase? controller, CancellationToken cancellationToken);

	/// <summary>
	/// 请求 Windows 关机。
	/// </summary>
	Task<OperationResult> ShutdownAsync(CancellationToken cancellationToken);
}
