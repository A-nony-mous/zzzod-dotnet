using System;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Screen;
using OpenCvSharp;
using ZzzOd.GameLogic.Application.SuibianTemple.Operations;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Application.SuibianTemple;

/// <summary>
/// 默认随便观入口服务。
/// </summary>
public sealed class DefaultSuibianTempleOperationServices : ISuibianTempleOperationServices
{
	/// <inheritdoc />
	public bool IsInTempleEntry(ZContext context, Mat? screen)
	{
		return screen != null && string.Equals(ScreenUtils.GetMatchScreenName(context, screen, new string[] { "随便观-入口" }), "随便观-入口", StringComparison.Ordinal);
	}

	/// <inheritdoc />
	public Task<OperationResult> TransportAsync(ZContext context)
	{
		return new Transport(context, "澄辉坪", "随便观", waitAtLast: false).ExecuteAsync();
	}

	/// <inheritdoc />
	public OperationResult GoToTempleEntry(ZContext context, Mat? screen, SuibianTempleConfig config)
	{
		return new SuibianTempleEntryNavigation(context, config).ExecuteAsync().GetAwaiter().GetResult();
	}

	/// <inheritdoc />
	public Task<OperationResult> HandleAutoManageAsync(ZContext context, SuibianTempleConfig config)
	{
		return new SuibianTempleAutoManage(context, config).ExecuteAsync();
	}

	/// <inheritdoc />
	public Task<OperationResult> HandleAdventureSquadAsync(ZContext context, SuibianTempleConfig config, bool claim, bool dispatch)
	{
		return new SuibianTempleAdventureSquad(context, config, claim, dispatch).ExecuteAsync();
	}

	/// <inheritdoc />
	public Task<OperationResult> HandleYumChaSinAsync(ZContext context, SuibianTempleConfig config)
	{
		return new SuibianTempleYumChaSin(context, config).ExecuteAsync();
	}

	/// <inheritdoc />
	public Task<OperationResult> HandleCraftAsync(ZContext context, SuibianTempleConfig config)
	{
		return new SuibianTempleCraft(context, config).ExecuteAsync();
	}

	/// <inheritdoc />
	public Task<OperationResult> HandleSalesStallAsync(ZContext context, SuibianTempleConfig config)
	{
		return new SuibianTempleSalesStall(context, config).ExecuteAsync();
	}

	/// <inheritdoc />
	public Task<OperationResult> HandleGoodGoodsAsync(ZContext context, SuibianTempleConfig config)
	{
		return new SuibianTempleGoodGoods(context, config).ExecuteAsync();
	}

	/// <inheritdoc />
	public Task<OperationResult> HandleBooBoxAsync(ZContext context, SuibianTempleConfig config)
	{
		return new SuibianTempleBooBox(context, config).ExecuteAsync();
	}

	/// <inheritdoc />
	public Task<OperationResult> HandlePawnshopAsync(ZContext context, SuibianTempleConfig config)
	{
		return new SuibianTemplePawnshop(context, config).ExecuteAsync();
	}

	/// <inheritdoc />
	public Task<OperationResult> BackToNormalWorldAsync(ZContext context)
	{
		return new BackToNormalWorld(context).ExecuteAsync();
	}
}
