using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.Notify;

/// <summary>
/// 推送通知服务。
/// </summary>
public interface IPushNotificationService
{
	/// <summary>
	/// 推送通知到第三方通道。
	/// </summary>
	Task<OperationResult> PushAsync(ZContext context, string title, string content, Mat? image, CancellationToken cancellationToken);
}
