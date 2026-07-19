using System;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.GameData;

namespace ZzzOd.GameLogic.Operations.Compendium;

/// <summary>
/// 通过快捷手册传送到指定训练入口。
/// </summary>
public sealed class TransportByCompendium : ZOperation
{
	private sealed class GotoCompendiumTabOperation : ZOperation
	{
		private readonly string _tabName;

		public GotoCompendiumTabOperation(ZContext context, string tabName)
			: base(context, "快捷手册 " + tabName)
		{
			_tabName = tabName;
		}

		[OperationNode("快捷手册", IsStartNode = true)]
		private OperationRoundResult GotoTab()
		{
			return RoundByGotoScreen(null, "快捷手册-" + _tabName);
		}
	}

	private readonly Func<ZContext, Task<OperationResult>> _backToWorldAsync;

	private readonly Func<ZContext, string, Task<OperationResult>> _gotoCompendiumTabAsync;

	private readonly Func<ZContext, string, Task<OperationResult>> _chooseCategoryAsync;

	private readonly Func<ZContext, CompendiumMissionType, Task<OperationResult>> _chooseMissionTypeAsync;

	/// <summary>目标页签。</summary>
	public string TabName { get; }

	/// <summary>目标分类。</summary>
	public string CategoryName { get; }

	/// <summary>目标副本类型。</summary>
	public string? MissionTypeName { get; }

	/// <summary>
	/// 初始化快捷手册传送操作。
	/// </summary>
	public TransportByCompendium(ZContext context, string tabName, string categoryName, string? missionTypeName = null, Func<ZContext, Task<OperationResult>>? backToWorldAsync = null, Func<ZContext, Task<OperationResult>>? openCompendiumAsync = null, Func<ZContext, string, Task<OperationResult>>? chooseTabAsync = null, Func<ZContext, string, Task<OperationResult>>? chooseCategoryAsync = null, Func<ZContext, CompendiumMissionType, Task<OperationResult>>? chooseMissionTypeAsync = null, Func<ZContext, string, Task<OperationResult>>? gotoCompendiumTabAsync = null)
		: base(context, $"传送 快捷手册 {tabName}-{categoryName}-{missionTypeName ?? string.Empty}")
	{
		TabName = tabName;
		CategoryName = categoryName;
		MissionTypeName = ((missionTypeName == "自定义模板") ? "基础材料" : missionTypeName);
		_backToWorldAsync = backToWorldAsync ?? new Func<ZContext, Task<OperationResult>>(DefaultBackToWorldAsync);
		_gotoCompendiumTabAsync = gotoCompendiumTabAsync ?? BuildGotoCompendiumTabAsync(openCompendiumAsync, chooseTabAsync);
		_chooseCategoryAsync = chooseCategoryAsync ?? new Func<ZContext, string, Task<OperationResult>>(DefaultChooseCategoryAsync);
		_chooseMissionTypeAsync = chooseMissionTypeAsync ?? new Func<ZContext, CompendiumMissionType, Task<OperationResult>>(DefaultChooseMissionTypeAsync);
	}

	[OperationNode("返回大世界", IsStartNode = true)]
	private async Task<OperationRoundResult> BackToWorld()
	{
		string targetScreen = "快捷手册-" + TabName;
		string? currentScreen = CheckAndUpdateCurrentScreen(base.LastScreenshot);
		if (string.Equals(currentScreen, targetScreen, StringComparison.Ordinal) || (currentScreen != null && base.ZContext.ScreenContext.GetScreenRoute(currentScreen, targetScreen)?.CanGo == true))
		{
			return RoundSuccess();
		}
		return RoundByOperationResult(await _backToWorldAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false));
	}

	[NodeFrom("返回大世界")]
	[OperationNode("快捷手册")]
	private async Task<OperationRoundResult> ChooseTab()
	{
		return RoundByOperationResult(await _gotoCompendiumTabAsync(base.ZContext, TabName).ConfigureAwait(continueOnCapturedContext: false));
	}

	[NodeFrom("快捷手册")]
	[OperationNode("选择分类")]
	private async Task<OperationRoundResult> ChooseCategory()
	{
		return RoundByOperationResult(await _chooseCategoryAsync(base.ZContext, CategoryName).ConfigureAwait(continueOnCapturedContext: false));
	}

	[NodeFrom("选择分类")]
	[OperationNode("选择副本分类")]
	private async Task<OperationRoundResult> ChooseMissionType()
	{
		CompendiumMissionType missionType = base.ZContext.CompendiumService.GetMissionTypeData(TabName, CategoryName, MissionTypeName ?? string.Empty);
		if (missionType == null)
		{
			return RoundSuccess("无需选择副本");
		}
		return RoundByOperationResult(await _chooseMissionTypeAsync(base.ZContext, missionType).ConfigureAwait(continueOnCapturedContext: false));
	}

	private static Task<OperationResult> DefaultBackToWorldAsync(ZContext context)
	{
		BackToNormalWorld backToNormalWorld = new BackToNormalWorld(context, ensureNormalWorld: true);
		return backToNormalWorld.ExecuteAsync();
	}

	private static Func<ZContext, string, Task<OperationResult>> BuildGotoCompendiumTabAsync(Func<ZContext, Task<OperationResult>>? openCompendiumAsync, Func<ZContext, string, Task<OperationResult>>? chooseTabAsync)
	{
		if (openCompendiumAsync == null && chooseTabAsync == null)
		{
			return DefaultGotoCompendiumTabAsync;
		}
		return async delegate(ZContext context, string tabName)
		{
			OperationResult operationResult = ((openCompendiumAsync != null) ? (await openCompendiumAsync(context).ConfigureAwait(continueOnCapturedContext: false)) : (await DefaultGotoCompendiumTabAsync(context, tabName).ConfigureAwait(continueOnCapturedContext: false)));
			OperationResult openResult = operationResult;
			if (!openResult.IsSuccess)
			{
				return openResult;
			}
			return (chooseTabAsync == null) ? openResult : (await chooseTabAsync(context, tabName).ConfigureAwait(continueOnCapturedContext: false));
		};
	}

	private static async Task<OperationResult> DefaultGotoCompendiumTabAsync(ZContext context, string tabName)
	{
		GotoCompendiumTabOperation operation = new GotoCompendiumTabOperation(context, tabName);
		return await operation.ExecuteAsync().ConfigureAwait(continueOnCapturedContext: false);
	}

	private static Task<OperationResult> DefaultChooseCategoryAsync(ZContext context, string categoryName)
	{
		CompendiumChooseCategory compendiumChooseCategory = new CompendiumChooseCategory(context, categoryName);
		return compendiumChooseCategory.ExecuteAsync();
	}

	private static Task<OperationResult> DefaultChooseMissionTypeAsync(ZContext context, CompendiumMissionType missionType)
	{
		CompendiumChooseMissionType compendiumChooseMissionType = new CompendiumChooseMissionType(context, missionType);
		return compendiumChooseMissionType.ExecuteAsync();
	}
}
