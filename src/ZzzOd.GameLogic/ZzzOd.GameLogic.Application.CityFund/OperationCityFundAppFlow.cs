using System;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.CityFund;

/// <summary>
/// 默认丽都城募 Operation 流程。
/// </summary>
public sealed class OperationCityFundAppFlow : ICityFundAppFlow
{
	private readonly Func<ZContext, CancellationToken, Task<OperationResult>> _executeOperationAsync;

	/// <summary>
	/// 初始化默认流程。
	/// </summary>
	public OperationCityFundAppFlow(Func<ZContext, CancellationToken, Task<OperationResult>>? executeOperationAsync = null)
	{
		_executeOperationAsync = executeOperationAsync ?? new Func<ZContext, CancellationToken, Task<OperationResult>>(ExecuteDefaultOperationAsync);
	}

	/// <inheritdoc />
	public Task<OperationResult> RunAsync(ZContext context, CancellationToken cancellationToken)
	{
		return _executeOperationAsync(context, cancellationToken);
	}

	private static Task<OperationResult> ExecuteDefaultOperationAsync(ZContext context, CancellationToken cancellationToken)
	{
		CityFundOperation cityFundOperation = new CityFundOperation(context);
		return cityFundOperation.ExecuteAsync(cancellationToken);
	}
}
