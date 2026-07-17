using System;
using System.Threading;
using ZzzOd.GameLogic.AutoBattle;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.AppHost.Backend;

/// <summary>
/// 在进程内桥接自动战斗指令加载事件，不进入 API 事件流。
/// </summary>
public sealed class ZzzBattleAssistantRuntimeSource : IDisposable
{
	private readonly Lock _lock = new Lock();

	private ZContext? _context;

	private IDisposable? _operationLoadedSubscription;

	/// <summary>
	/// 指令加载完成事件。
	/// </summary>
	public event Action? OperationLoaded;

	/// <summary>
	/// 绑定当前运行上下文。
	/// </summary>
	/// <param name="context">ZZZ 运行上下文。</param>
	public void Attach(ZContext context)
	{
		ArgumentNullException.ThrowIfNull(context, "context");
		using (_lock.EnterScope())
		{
			if (_context != context)
			{
				_operationLoadedSubscription?.Dispose();
				_context = context;
				_operationLoadedSubscription = context.EventBus.Subscribe<AutoBattleOperator>("指令已加载", delegate
				{
					this.OperationLoaded?.Invoke();
				}, this);
			}
		}
	}

	/// <inheritdoc />
	public void Dispose()
	{
		using (_lock.EnterScope())
		{
			_operationLoadedSubscription?.Dispose();
			_operationLoadedSubscription = null;
			_context = null;
			this.OperationLoaded = null;
		}
	}
}
