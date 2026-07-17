using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.SuibianTemple;

/// <summary>
/// 随便观入口服务。
/// </summary>
public interface ISuibianTempleOperationServices
{
	/// <summary>是否在随便观入口。</summary>
	bool IsInTempleEntry(ZContext context, Mat? screen);

	/// <summary>传送到随便观。</summary>
	Task<OperationResult> TransportAsync(ZContext context);

	/// <summary>前往入口。</summary>
	OperationResult GoToTempleEntry(ZContext context, Mat? screen, SuibianTempleConfig config);

	/// <summary>处理自动托管。</summary>
	Task<OperationResult> HandleAutoManageAsync(ZContext context, SuibianTempleConfig config);

	/// <summary>处理游历小队。</summary>
	Task<OperationResult> HandleAdventureSquadAsync(ZContext context, SuibianTempleConfig config, bool claim, bool dispatch);

	/// <summary>处理饮茶仙。</summary>
	Task<OperationResult> HandleYumChaSinAsync(ZContext context, SuibianTempleConfig config);

	/// <summary>处理制造坊。</summary>
	Task<OperationResult> HandleCraftAsync(ZContext context, SuibianTempleConfig config);

	/// <summary>处理售卖铺。</summary>
	Task<OperationResult> HandleSalesStallAsync(ZContext context, SuibianTempleConfig config);

	/// <summary>处理好物铺。</summary>
	Task<OperationResult> HandleGoodGoodsAsync(ZContext context, SuibianTempleConfig config);

	/// <summary>处理邦巢。</summary>
	Task<OperationResult> HandleBooBoxAsync(ZContext context, SuibianTempleConfig config);

	/// <summary>处理德丰大押。</summary>
	Task<OperationResult> HandlePawnshopAsync(ZContext context, SuibianTempleConfig config);

	/// <summary>返回大世界。</summary>
	Task<OperationResult> BackToNormalWorldAsync(ZContext context);
}
