using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OpenCvSharp;
using ZzzOd.GameLogic.Application.Notify;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.AppHost.Notifications;

/// <summary>
/// 将 AppHost 的生产推送服务接入 GameLogic 的应用运行时。
/// </summary>
internal sealed class AppHostPushNotificationAdapter : IPushNotificationService
{
	private readonly IZzzPushNotificationService _service;

	public AppHostPushNotificationAdapter(IZzzPushNotificationService service)
	{
		_service = service;
	}

	public async Task<OperationResult> PushAsync(ZContext context, string title, string content, Mat? image, CancellationToken cancellationToken)
	{
		ZzzPushTestResult result = await _service.SendTestAsync(null, title, content, cancellationToken, image).ConfigureAwait(continueOnCapturedContext: false);
		return new OperationResult(result.Success, result.Message);
	}
}
