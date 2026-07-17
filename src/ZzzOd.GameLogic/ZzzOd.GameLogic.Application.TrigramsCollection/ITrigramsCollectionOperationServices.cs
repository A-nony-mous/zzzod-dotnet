using System.Collections.Generic;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.TrigramsCollection;

/// <summary>
/// 卦象集录流程服务。
/// </summary>
public interface ITrigramsCollectionOperationServices
{
	/// <summary>传送到阿朔。</summary>
	Task<OperationResult> TransportAsync(ZContext context);

	/// <summary>交互。</summary>
	OperationResult Interact(ZContext context);

	/// <summary>按优先级读取 OCR 文本。</summary>
	Task<TrigramOcrMatch?> ReadPriorityTextAsync(ZContext context, Mat? screen, IReadOnlyList<string> priorityWords);

	/// <summary>点击获取卦象区域。</summary>
	Task<OperationResult> ClickGetTrigramAsync(ZContext context);

	/// <summary>拖拽获取卦象。</summary>
	void DragForTrigram(ZContext context);

	/// <summary>点击确认。</summary>
	Task<OperationResult> ClickConfirmAsync(ZContext context, OneDragon.Core.Abstractions.Geometry.Point? center);

	/// <summary>返回大世界。</summary>
	Task<OperationResult> BackToNormalWorldAsync(ZContext context);
}
