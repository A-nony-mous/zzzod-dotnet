using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

/// <summary>
/// 迷失之地应用。
/// </summary>
public sealed class LostVoidApp : ZApplication
{
	public const string StatusEnoughTimes = "完成通关次数";

	public const string StatusAgain = "继续挑战";

	public const string StatusAgainMatrix = "继续挑战-矩阵行动";

	private readonly LostVoidConfig _config;

	private readonly LostVoidRunRecord _runRecord;

	private readonly ILostVoidAppFlow _flow;

	/// <summary>
	/// 初始化迷失之地应用。
	/// </summary>
	public LostVoidApp(ZContext context, LostVoidConfig? config = null, LostVoidRunRecord? runRecord = null, ILostVoidAppFlow? flow = null)
		: base(context, "lost_void", runRecord, "迷失之地")
	{
		_config = config ?? LostVoidConfig.Load(context.Environment, context.RunContext.CurrentInstanceIndex.GetValueOrDefault(), "one_dragon");
		_runRecord = runRecord ?? LostVoidRunRecord.Load(context.Environment, _config, context.RunContext.CurrentInstanceIndex.GetValueOrDefault(), context.GameAccountConfig.GameRefreshHourOffset);
		_flow = flow ?? new OperationLostVoidAppFlow();
	}

	/// <inheritdoc />
	protected override Task<OperationResult> ExecuteCoreAsync(CancellationToken cancellationToken)
	{
		return _flow.RunAsync(base.Context, _config, _runRecord, cancellationToken);
	}

	/// <summary>暂停当前运行层并释放自动战斗输入。</summary>
	public override Task OnPauseAsync(CancellationToken cancellationToken)
	{
		if (_flow is ILostVoidAppLifecycle lostVoidAppLifecycle)
		{
			lostVoidAppLifecycle.Pause(base.Context);
		}
		return Task.CompletedTask;
	}

	/// <summary>恢复当前运行层的前台窗口和战斗状态。</summary>
	public override Task OnResumeAsync(CancellationToken cancellationToken)
	{
		if (_flow is ILostVoidAppLifecycle lostVoidAppLifecycle)
		{
			lostVoidAppLifecycle.Resume(base.Context);
		}
		return base.OnResumeAsync(cancellationToken);
	}

	/// <summary>停止当前运行层。</summary>
	public override Task OnStopAsync(CancellationToken cancellationToken)
	{
		if (_flow is ILostVoidAppLifecycle lostVoidAppLifecycle)
		{
			lostVoidAppLifecycle.Stop(base.Context);
		}
		return Task.CompletedTask;
	}
}
