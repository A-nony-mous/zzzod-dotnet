using System;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.IntelBoard;

/// <summary>
/// 情报板流程服务。
/// </summary>
public interface IIntelBoardOperationServices
{
	/// <summary>加载迷失之地检测模型。</summary>
	bool LoadLostVoidDetectorModel(ZContext context);

	/// <summary>返回录像店。</summary>
	Task<OperationResult> BackToVideoStoreAsync(ZContext context);

	/// <summary>打开情报板入口。</summary>
	Task<OperationResult> OpenBoardAsync(ZContext context, Mat? screen);

	/// <summary>点击情报板。</summary>
	Task<OperationResult> ClickBoardAsync(ZContext context, Mat? screen);

	/// <summary>刷新委托。</summary>
	Task<OperationResult> RefreshCommissionAsync(ZContext context, Mat? screen);

	/// <summary>打开筛选。</summary>
	Task<OperationResult> OpenFilterAsync(ZContext context, Mat? screen);

	/// <summary>重置筛选。</summary>
	Task<OperationResult> ResetFilterAsync(ZContext context, Mat? screen);

	/// <summary>选择委托类型。</summary>
	Task<OperationResult> SelectCommissionTypeAsync(ZContext context, IntelBoardCommissionType commissionType, Mat? screen);

	/// <summary>关闭筛选。</summary>
	Task<OperationResult> CloseFilterAsync(ZContext context);

	/// <summary>寻找可接取委托。</summary>
	Task<IntelBoardCommissionType?> FindCommissionAsync(ZContext context, Mat? screen);

	/// <summary>翻页委托列表。</summary>
	Task ScrollCommissionListAsync(ZContext context);

	/// <summary>接取委托。</summary>
	Task<OperationResult> AcceptCommissionAsync(ZContext context, Mat? screen);

	/// <summary>下一步。</summary>
	Task<OperationResult> NextStepAsync(ZContext context, Mat? screen);

	/// <summary>确认接取失败。</summary>
	Task<OperationResult> ConfirmAcceptFailedAsync(ZContext context, Mat? screen);

	/// <summary>选择预备编队。</summary>
	Task<OperationResult> ChooseTeamAsync(ZContext context, int teamIndex);

	/// <summary>点击出战。</summary>
	Task<OperationResult> DeployAsync(ZContext context, Mat? screen);

	/// <summary>确认委托代行中弹窗。</summary>
	Task<OperationResult> ConfirmCommissionAgentAsync(ZContext context, Mat? screen);

	/// <summary>加载自动战斗。</summary>
	void InitAutoBattle(ZContext context, IntelBoardConfig config);

	/// <summary>检查战斗画面。</summary>
	OperationResult CheckBattleScreenReady(ZContext context, Mat? screen);

	/// <summary>战斗前移动。</summary>
	Task<OperationResult> PreBattleMoveAsync(ZContext context, IntelBoardCommissionType? commissionType);

	/// <summary>开始自动战斗。</summary>
	void StartAutoBattle(ZContext context);

	/// <summary>运行战斗。</summary>
	Task<OperationResult> RunBattleAsync(ZContext context, Mat? screen, DateTimeOffset? screenshotTimeUtc);

	/// <summary>检查是否回到列表。</summary>
	Task<OperationResult> CheckBackToListAsync(ZContext context, Mat? screen);

	/// <summary>点击结算按钮。</summary>
	Task<OperationResult> ClickSettlementButtonAsync(ZContext context, Mat? screen);

	/// <summary>读取情报板进度。</summary>
	Task<OperationResult> ReadProgressAsync(ZContext context, Mat? screen);
}
