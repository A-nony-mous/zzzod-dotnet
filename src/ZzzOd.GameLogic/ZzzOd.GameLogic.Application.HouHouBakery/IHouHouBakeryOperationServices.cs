using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.HouHouBakery;

/// <summary>
/// 吼吼饼铺流程服务。
/// </summary>
public interface IHouHouBakeryOperationServices
{
	/// <summary>传送到吼吼饼铺。</summary>
	Task<OperationResult> TransportAsync(ZContext context);

	/// <summary>交互。</summary>
	OperationResult Interact(ZContext context);

	/// <summary>识别目标 OCR 文本。</summary>
	Task<bool> RecognizeTextAsync(ZContext context, Mat? screen, string targetText);

	/// <summary>点击 OCR 文本。</summary>
	Task<OperationResult> ClickTextAsync(ZContext context, Mat? screen, string targetText);

	/// <summary>点击屏幕中心。</summary>
	OperationResult ClickCenter(ZContext context);

	/// <summary>点击盲盒区域。</summary>
	Task<OperationResult> ClickBlindBoxAsync(ZContext context);

	/// <summary>返回大世界。</summary>
	Task<OperationResult> BackToNormalWorldAsync(ZContext context);
}
