using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.SuibianTemple;

/// <summary>
/// 随便观应用。
/// </summary>
public sealed class SuibianTempleApp : ZApplication
{
	private readonly SuibianTempleConfig _config;

	private readonly ISuibianTempleAppFlow _flow;

	/// <summary>
	/// 初始化随便观应用。
	/// </summary>
	public SuibianTempleApp(ZContext context, SuibianTempleConfig? config = null, SuibianTempleRunRecord? runRecord = null, ISuibianTempleAppFlow? flow = null)
		: base(context, "suibian_temple", runRecord, "随便观")
	{
		_config = config ?? SuibianTempleConfig.Load(context.Environment, context.RunContext.CurrentInstanceIndex.GetValueOrDefault(), "one_dragon");
		_flow = flow ?? new OperationSuibianTempleAppFlow();
	}

	/// <inheritdoc />
	protected override Task<OperationResult> ExecuteCoreAsync(CancellationToken cancellationToken)
	{
		return _flow.RunAsync(base.Context, _config, cancellationToken);
	}
}
