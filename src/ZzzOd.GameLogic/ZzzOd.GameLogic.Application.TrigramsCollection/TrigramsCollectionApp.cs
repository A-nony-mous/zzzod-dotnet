using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.TrigramsCollection;

/// <summary>
/// 卦象集录应用。
/// </summary>
public sealed class TrigramsCollectionApp : ZApplication
{
	private readonly ITrigramsCollectionFlow _flow;

	/// <summary>
	/// 初始化卦象集录应用。
	/// </summary>
	public TrigramsCollectionApp(ZContext context, TrigramsCollectionRunRecord? runRecord = null, ITrigramsCollectionFlow? flow = null)
		: base(context, "trigrams_collection", runRecord, "卦象集录")
	{
		_flow = flow ?? new OperationTrigramsCollectionFlow();
	}

	/// <inheritdoc />
	protected override async Task<OperationResult> ExecuteCoreAsync(CancellationToken cancellationToken)
	{
		base.Context.ScreenContext.EnterScope("trigrams_collection");
		try
		{
			return await _flow.RunAsync(base.Context, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		finally
		{
			base.Context.ScreenContext.ExitScope();
		}
	}
}
