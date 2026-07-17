using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.CityFund;

/// <summary>
/// 丽都城募应用。
/// </summary>
public sealed class CityFundApp : ZApplication
{
	private readonly ICityFundAppFlow _flow;

	/// <summary>
	/// 初始化丽都城募应用。
	/// </summary>
	public CityFundApp(ZContext context, ZApplicationRunRecord? runRecord = null, ICityFundAppFlow? flow = null)
		: base(context, "city_fund", runRecord, "丽都城募")
	{
		_flow = flow ?? new OperationCityFundAppFlow();
	}

	/// <inheritdoc />
	protected override async Task<OperationResult> ExecuteCoreAsync(CancellationToken cancellationToken)
	{
		base.Context.ScreenContext.EnterScope("city_fund");
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
