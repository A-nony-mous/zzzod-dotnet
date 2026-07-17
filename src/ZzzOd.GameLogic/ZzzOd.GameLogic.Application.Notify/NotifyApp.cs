using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.Notify;

/// <summary>
/// 一条龙完成通知应用。
/// </summary>
public sealed class NotifyApp : ZApplication
{
	private readonly INotifyAppFlow _flow;

	/// <summary>
	/// 初始化通知应用。
	/// </summary>
	public NotifyApp(ZContext context, NotifyRunRecord? runRecord = null, INotifyAppFlow? flow = null, bool needCheckGameWindow = true)
		: base(context, "notify", runRecord, "通知", 1, null, needCheckGameWindow)
	{
		_flow = flow ?? new DefaultNotifyAppFlow();
	}

	/// <inheritdoc />
	protected override async Task<OperationResult> ExecuteCoreAsync(CancellationToken cancellationToken)
	{
		using Mat screenshot = base.Context.Controller?.Screenshot().Screen;
		return await _flow.RunAsync(base.Context, screenshot, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}
}
