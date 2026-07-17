using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.Notify;

/// <summary>
/// 通知应用流程。
/// </summary>
public interface INotifyAppFlow
{
	/// <summary>
	/// 运行通知流程。
	/// </summary>
	Task<OperationResult> RunAsync(ZContext context, Mat? screenshot, CancellationToken cancellationToken);
}
