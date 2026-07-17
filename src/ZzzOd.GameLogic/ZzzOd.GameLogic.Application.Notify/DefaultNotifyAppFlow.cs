using System;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Configuration;
using OpenCvSharp;
using ZzzOd.GameLogic.Config;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.Notify;

/// <summary>
/// 默认通知流程。
/// </summary>
public sealed class DefaultNotifyAppFlow : INotifyAppFlow
{
	private readonly NotifyMessageFormatter _formatter;

	private readonly IPushNotificationService? _pushService;

	private readonly Func<DateTimeOffset> _now;

	private readonly Func<ZContext, string> _titleProvider;

	private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;

	/// <summary>
	/// 初始化默认通知流程。
	/// </summary>
	public DefaultNotifyAppFlow(NotifyMessageFormatter? formatter = null, IPushNotificationService? pushService = null, Func<DateTimeOffset>? now = null, Func<ZContext, string>? titleProvider = null, Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
	{
		_formatter = formatter ?? new NotifyMessageFormatter();
		_pushService = pushService;
		_now = now ?? ((Func<DateTimeOffset>)(() => DateTimeOffset.Now));
		_titleProvider = titleProvider ?? new Func<ZContext, string>(GetConfiguredTitle);
		_delayAsync = delayAsync ?? new Func<TimeSpan, CancellationToken, Task>(Task.Delay);
	}

	/// <inheritdoc />
	public async Task<OperationResult> RunAsync(ZContext context, Mat? screenshot, CancellationToken cancellationToken)
	{
		NotifyMessage message = _formatter.Format(context, _now());
		OperationResult result = await (_pushService ?? context.PushNotificationService).PushAsync(context, _titleProvider(context), message.Content, screenshot, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (!result.IsSuccess)
		{
			context.Logger.Warning("通知推送返回失败，但 NotifyApp 结果按 Python 语义只由失败指令决定。Status={Status}", result.Status);
		}
		await _delayAsync(TimeSpan.FromSeconds(5L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		return (!message.HasFailure) ? new OperationResult(IsSuccess: true, "通知已发送", message.Content) : new OperationResult(IsSuccess: false, "存在失败指令", message.Content);
	}

	private static string GetConfiguredTitle(ZContext context)
	{
		int valueOrDefault = context.RunContext.CurrentInstanceIndex.GetValueOrDefault();
		YamlConfig<NotifyConfig> yamlConfig = new YamlConfig<NotifyConfig>(context.Environment, "notify", null, valueOrDefault);
		return yamlConfig.Current.Title;
	}
}
