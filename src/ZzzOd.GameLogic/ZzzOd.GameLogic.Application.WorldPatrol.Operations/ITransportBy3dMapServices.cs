using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.WorldPatrol.Operations;

/// <summary>
/// 3D 地图传送流程服务。
/// </summary>
public interface ITransportBy3dMapServices
{
	/// <summary>识别当前画面。</summary>
	string? CheckCurrentScreen(ZContext context, Mat? screen, IReadOnlyList<string> screenNameList);

	/// <summary>返回大世界。</summary>
	Task<OperationResult> BackToNormalWorldAsync(ZContext context, CancellationToken cancellationToken);

	/// <summary>打开 3D 地图。</summary>
	bool Open3dMap(ZContext context, Mat? screen);

	/// <summary>选择区域。</summary>
	OperationResult ChooseArea(ZContext context, Mat? screen, string areaName, WorldPatrolArea targetArea);

	/// <summary>展开子区域。</summary>
	OperationResult ExpandSubArea(ZContext context, Mat? screen);

	/// <summary>选择子区域。</summary>
	OperationResult ChooseSubArea(ZContext context, Mat? screen, string areaName);

	/// <summary>打开筛选。</summary>
	OperationResult OpenFilter(ZContext context, Mat? screen);

	/// <summary>选择筛选项。</summary>
	OperationResult ChooseFilter(ZContext context, Mat? screen, string targetWord);

	/// <summary>关闭筛选。</summary>
	OperationResult CloseFilter(ZContext context, Mat? screen);

	/// <summary>拖到最小缩放。</summary>
	OperationResult ClickMiniScale(ZContext context);

	/// <summary>初始化传送点搜索。</summary>
	OperationResult InitTransportPointSearch(ZContext context, WorldPatrolArea targetArea, string targetTransportName);

	/// <summary>搜索传送点。</summary>
	OperationResult SearchTransportPoint(ZContext context, Mat? screen, string targetTransportName, CancellationToken cancellationToken);

	/// <summary>关闭区域信息弹窗。</summary>
	void CloseAreaInfoPopup(ZContext context, Mat? screen);

	/// <summary>点击前往。</summary>
	OperationResult ClickGo(ZContext context, Mat? screen);

	/// <summary>等待传送后返回大世界。</summary>
	Task<OperationResult> WaitNormalWorldAfterTransportAsync(ZContext context, WorldPatrolArea targetArea, CancellationToken cancellationToken);
}
