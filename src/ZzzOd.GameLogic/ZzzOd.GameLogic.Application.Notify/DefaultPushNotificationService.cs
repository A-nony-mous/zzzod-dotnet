using System;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.Notify;

/// <summary>
/// 默认推送通知服务。
/// </summary>
public sealed class DefaultPushNotificationService : IPushNotificationService
{
	/// <inheritdoc />
	public Task<OperationResult> PushAsync(ZContext context, string title, string content, Mat? image, CancellationToken cancellationToken)
	{
		context.Logger.Warning("未配置第三方推送通道，通知未发送。{NewLine}{Title}{NewLine}{Content}", Environment.NewLine, title, content);
		return Task.FromResult(new OperationResult(IsSuccess: false, "未配置第三方推送通道"));
	}
}
