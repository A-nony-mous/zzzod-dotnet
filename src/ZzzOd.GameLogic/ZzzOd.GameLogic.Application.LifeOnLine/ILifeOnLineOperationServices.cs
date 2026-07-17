using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.LifeOnLine;

/// <summary>
/// 生命热线流程服务。
/// </summary>
public interface ILifeOnLineOperationServices
{
	/// <summary>传送到 HDD。</summary>
	Task<OperationResult> TransportToHddAsync(ZContext context);

	/// <summary>等待大世界。</summary>
	Task<OperationResult> WaitNormalWorldAsync(ZContext context);

	/// <summary>HDD 街区是否可见。</summary>
	bool IsHddStreetVisible(ZContext context, Mat? screen);

	/// <summary>执行交互。</summary>
	void Interact(ZContext context);

	/// <summary>进入副本。</summary>
	Task<OperationResult> EnterMissionAsync(ZContext context, int predefinedTeamIndex);

	/// <summary>战斗画面是否就绪。</summary>
	bool IsBattleScreenReady(ZContext context, Mat? screen);

	/// <summary>执行按键脚本。</summary>
	Task<OperationResult> RunKeySimAsync(ZContext context);

	/// <summary>对话人是否可见。</summary>
	bool IsDialogPersonVisible(ZContext context, Mat? screen);

	/// <summary>战斗完成按钮是否可见。</summary>
	bool IsBattleResultCompleteVisible(ZContext context, Mat? screen);

	/// <summary>点击第一个对话选项。</summary>
	string? ClickFirstDialogOption(ZContext context, Mat? screen);

	/// <summary>点击菜单返回。</summary>
	OperationResult ClickMenuBack(ZContext context);

	/// <summary>点击战斗结果完成。</summary>
	OperationResult ClickBattleResultComplete(ZContext context, Mat? screen);

	/// <summary>单次检查大世界。</summary>
	Task<OperationResult> WaitNormalWorldOnceAsync(ZContext context);

	/// <summary>点击 HDD 空白处。</summary>
	OperationResult ClickHddBlank(ZContext context);

	/// <summary>返回大世界。</summary>
	Task<OperationResult> BackToWorldAsync(ZContext context);

	/// <summary>退出战斗按钮是否可见。</summary>
	bool IsExitBattleVisible(ZContext context, Mat? screen);

	/// <summary>点击战斗菜单。</summary>
	OperationResult ClickBattleMenu(ZContext context);

	/// <summary>点击退出战斗。</summary>
	OperationResult ClickExitBattle(ZContext context, Mat? screen);

	/// <summary>点击退出战斗确认。</summary>
	OperationResult ClickExitBattleConfirm(ZContext context, Mat? screen);
}
