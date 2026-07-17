using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.HouHouBakery;

/// <summary>
/// 吼吼饼铺应用。
/// </summary>
public sealed class HouHouBakeryApp : ZApplication
{
	private readonly IHouHouBakeryFlow _flow;

	/// <summary>
	/// 初始化吼吼饼铺应用。
	/// </summary>
	public HouHouBakeryApp(ZContext context, HouHouBakeryRunRecord? runRecord = null, IHouHouBakeryFlow? flow = null)
		: base(context, "hou_hou_bakery", runRecord, "吼吼饼铺")
	{
		_flow = flow ?? new OperationHouHouBakeryFlow();
	}

	/// <inheritdoc />
	protected override async Task<OperationResult> ExecuteCoreAsync(CancellationToken cancellationToken)
	{
		base.Context.ScreenContext.EnterScope("hou_hou_bakery");
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
