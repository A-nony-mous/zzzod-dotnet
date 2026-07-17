using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.Devtools.OperationDebug;

/// <summary>
/// 指令调试应用。
/// </summary>
public sealed class OperationDebugApp : ZApplication
{
	private readonly OperationDebugService _service;

	/// <summary>
	/// 应用配置。
	/// </summary>
	public OperationDebugConfig Config { get; }

	/// <summary>
	/// 初始化应用。
	/// </summary>
	public OperationDebugApp(ZContext context, OperationDebugConfig config, ZApplicationRunRecord? runRecord = null, OperationDebugService? service = null)
		: base(context, "operation_debug", runRecord, "指令调试")
	{
		Config = config;
		_service = service ?? new OperationDebugService(config, new OperationDebugTemplateLoader(context.Environment), new AutoBattleOperationDebugAtomicOpFactory(context.AutoBattleContext), new ZContextOperationDebugControllerModeSwitcher(context));
	}

	/// <inheritdoc />
	public override Task OnStopAsync(CancellationToken cancellationToken)
	{
		_service.Dispose();
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	protected override async Task<OperationResult> ExecuteCoreAsync(CancellationToken cancellationToken)
	{
		OperationDebugOperation operation = new OperationDebugOperation(base.Context, _service);
		using (cancellationToken.Register(_service.Stop))
		{
			try
			{
				OperationResult result = await operation.ExecuteAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				cancellationToken.ThrowIfCancellationRequested();
				return result;
			}
			finally
			{
				_service.Dispose();
			}
		}
	}
}
