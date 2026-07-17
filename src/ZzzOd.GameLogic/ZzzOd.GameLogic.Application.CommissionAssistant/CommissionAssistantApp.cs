using System;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Application.Devtools.ScreenshotHelper;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.CommissionAssistant;

/// <summary>
/// 委托助手应用。
/// </summary>
public sealed class CommissionAssistantApp : ZApplication
{
	private readonly CommissionAssistantConfig _config;

	private readonly CommissionAssistantRuntimeState _state;

	private readonly ICommissionAssistantAppFlow _flow;

	private IDisposable? _keyboardSubscription;

	/// <summary>
	/// 当前运行态。
	/// </summary>
	public CommissionAssistantRuntimeState State => _state;

	/// <summary>
	/// 初始化委托助手应用。
	/// </summary>
	public CommissionAssistantApp(ZContext context, CommissionAssistantConfig? config = null, CommissionAssistantRuntimeState? state = null, ICommissionAssistantAppFlow? flow = null)
		: base(context, "commission_assistant", null, "委托助手")
	{
		_config = config ?? CommissionAssistantConfig.Load(context.Environment, context.RunContext.CurrentInstanceIndex.GetValueOrDefault(), "one_dragon");
		_state = state ?? new CommissionAssistantRuntimeState();
		_flow = flow ?? new OperationCommissionAssistantAppFlow();
	}

	/// <summary>
	/// 处理按键事件。
	/// </summary>
	public void HandleKeyPress(string key)
	{
		_state.HandleKeyPress(key, _config);
	}

	/// <inheritdoc />
	public override Task OnPauseAsync(CancellationToken cancellationToken)
	{
		UnsubscribeKeyboard();
		_flow.Pause(base.Context, _state);
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public override async Task OnResumeAsync(CancellationToken cancellationToken)
	{
		await base.OnResumeAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		SubscribeKeyboard();
		_flow.Resume(base.Context, _state);
	}

	/// <inheritdoc />
	public override Task OnStopAsync(CancellationToken cancellationToken)
	{
		UnsubscribeKeyboard();
		_flow.Stop(base.Context, _state);
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	protected override async Task<OperationResult> ExecuteCoreAsync(CancellationToken cancellationToken)
	{
		SubscribeKeyboard();
		try
		{
			return await _flow.RunAsync(base.Context, _config, _state, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		finally
		{
			UnsubscribeKeyboard();
		}
	}

	private void SubscribeKeyboard()
	{
		if (_keyboardSubscription == null)
		{
			_keyboardSubscription = ScreenshotHelperGlobalInputSource.Subscribe(HandleGlobalKeyPress);
		}
	}

	private bool HandleGlobalKeyPress(string key)
	{
		if (!base.Context.RunContext.IsContextRunning)
		{
			return false;
		}
		HandleKeyPress(key);
		return true;
	}

	private void UnsubscribeKeyboard()
	{
		_keyboardSubscription?.Dispose();
		_keyboardSubscription = null;
	}
}
