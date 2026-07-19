using System;
using System.Linq;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Application.IntelBoard;

/// <summary>
/// 情报板应用主流程。
/// </summary>
public sealed class IntelBoardOperation : ZOperation
{
	/// <summary>本周期已完成。</summary>
	public const string StatusCurrentPeriodComplete = "本周期已完成";

	/// <summary>未筛选。</summary>
	public const string StatusNotFiltered = "未筛选";

	/// <summary>无委托。</summary>
	public const string StatusNoCommission = "无委托";

	/// <summary>接取失败。</summary>
	public const string StatusAcceptFailed = "接取失败";

	/// <summary>未选择代理人。</summary>
	public const string StatusNoAgentSelected = "未选择代理人";

	/// <summary>自动战斗中。</summary>
	public const string StatusAutoBattleRunning = "自动战斗中";

	private readonly IntelBoardConfig _config;

	private readonly IntelBoardRunRecord _runRecord;

	private readonly IIntelBoardOperationServices _services;

	private int _scrollTimes;

	private bool _hasFiltered;

	private IntelBoardCommissionType? _currentCommissionType;

	/// <summary>
	/// 当前委托类型。
	/// </summary>
	public IntelBoardCommissionType? CurrentCommissionType => _currentCommissionType;

	/// <summary>
	/// 当前是否处于 BaselineParity `战斗中` 节点。
	/// </summary>
	public bool IsAutoBattleNodeActive => string.Equals(base.CurrentNode.Name, "战斗中", StringComparison.Ordinal);

	/// <summary>
	/// 初始化情报板流程。
	/// </summary>
	public IntelBoardOperation(ZContext context, IntelBoardConfig config, IntelBoardRunRecord runRecord, IIntelBoardOperationServices? services = null)
		: base(context, "情报板", 1)
	{
		_config = config;
		_runRecord = runRecord;
		_services = services ?? new DefaultIntelBoardOperationServices();
	}

	/// <summary>
	/// 仅在战斗节点恢复自动战斗。
	/// </summary>
	public void ResumeAutoBattle()
	{
		if (IsAutoBattleNodeActive)
		{
			base.ZContext.AutoBattleContext.ResumeAutoBattle();
		}
	}

	/// <summary>
	/// 解析情报板进度 OCR 文本。
	/// </summary>
	public static bool TryParseProgress(string? text, out int current)
	{
		current = 0;
		if (string.IsNullOrWhiteSpace(text))
		{
			return false;
		}
		string source = text.Replace('／', '/');
		string text2 = new string(source.Where((char ch) => char.IsDigit(ch) || ch == '/').ToArray());
		if (!text2.Contains('/', StringComparison.Ordinal))
		{
			return false;
		}
		string s = text2.Split('/')[0];
		return int.TryParse(s, out current);
	}

	/// <summary>
	/// 返回录像店。
	/// </summary>
	[OperationNode("返回录像店", IsStartNode = true)]
	public async Task<OperationRoundResult> BackToWorld()
	{
		return RoundByOperationResult(await _services.BackToVideoStoreAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false));
	}

	/// <summary>
	/// 打开情报板。
	/// </summary>
	[NodeFrom("返回录像店")]
	[OperationNode("打开情报板")]
	public async Task<OperationRoundResult> OpenBoard()
	{
		if (_config.ExpGrindMode ? _runRecord.ExpComplete : _runRecord.ProgressComplete)
		{
			return RoundSuccess("本周期已完成");
		}
		return RoundByPythonClickResult(await _services.OpenBoardAsync(base.ZContext, base.LastScreenshot).ConfigureAwait(continueOnCapturedContext: false), TimeSpan.FromSeconds(1L));
	}

	/// <summary>
	/// 点击情报板。
	/// </summary>
	[NodeFrom("打开情报板")]
	[OperationNode("点击情报板")]
	public async Task<OperationRoundResult> ClickBoard()
	{
		return RoundByOperationResult(await _services.ClickBoardAsync(base.ZContext, base.LastScreenshot).ConfigureAwait(continueOnCapturedContext: false), null, retryOnFail: true, TimeSpan.FromSeconds(1L));
	}

	/// <summary>
	/// 刷新委托。
	/// </summary>
	[NodeFrom("检查进度", Success = false)]
	[NodeFrom("接取委托", Success = false)]
	[OperationNode("刷新委托")]
	public async Task<OperationRoundResult> RefreshCommission()
	{
		_scrollTimes = 0;
		if (!_hasFiltered)
		{
			return RoundSuccess("未筛选");
		}
		return RoundByPythonClickResult(await _services.RefreshCommissionAsync(base.ZContext, base.LastScreenshot).ConfigureAwait(continueOnCapturedContext: false), TimeSpan.FromSeconds(1L));
	}

	/// <summary>
	/// 打开筛选。
	/// </summary>
	[NodeFrom("刷新委托", Status = "未筛选")]
	[OperationNode("打开筛选", NodeMaxRetryTimes = 60)]
	public async Task<OperationRoundResult> OpenFilter()
	{
		OperationResult result = await _services.OpenFilterAsync(base.ZContext, base.LastScreenshot).ConfigureAwait(continueOnCapturedContext: false);
		if (!result.IsSuccess && string.Equals(result.Status, "未找到筛选按钮", StringComparison.Ordinal))
		{
			return RoundRetry(result.Status, null, TimeSpan.FromSeconds(1L));
		}
		return RoundByOperationResult(result, null, retryOnFail: true, TimeSpan.FromMilliseconds(500L));
	}

	/// <summary>
	/// 重置筛选。
	/// </summary>
	[NodeFrom("打开筛选")]
	[OperationNode("重置筛选")]
	public async Task<OperationRoundResult> ResetFilter()
	{
		return RoundByOperationResult(await _services.ResetFilterAsync(base.ZContext, base.LastScreenshot).ConfigureAwait(continueOnCapturedContext: false), null, retryOnFail: true, TimeSpan.FromMilliseconds(500L));
	}

	/// <summary>
	/// 选择恶名狩猎。
	/// </summary>
	[NodeFrom("重置筛选")]
	[OperationNode("选择恶名狩猎")]
	public async Task<OperationRoundResult> SelectNotoriousHunt()
	{
		return RoundByOperationResult(await _services.SelectCommissionTypeAsync(base.ZContext, IntelBoardCommissionType.NotoriousHunt, base.LastScreenshot).ConfigureAwait(continueOnCapturedContext: false), null, retryOnFail: true, TimeSpan.FromMilliseconds(500L));
	}

	/// <summary>
	/// 选择专业挑战室。
	/// </summary>
	[NodeFrom("选择恶名狩猎")]
	[OperationNode("选择专业挑战室")]
	public async Task<OperationRoundResult> SelectExpertChallenge()
	{
		return RoundByOperationResult(await _services.SelectCommissionTypeAsync(base.ZContext, IntelBoardCommissionType.ExpertChallenge, base.LastScreenshot).ConfigureAwait(continueOnCapturedContext: false), null, retryOnFail: true, TimeSpan.FromMilliseconds(500L));
	}

	/// <summary>
	/// 关闭筛选。
	/// </summary>
	[NodeFrom("选择专业挑战室")]
	[OperationNode("关闭筛选")]
	public async Task<OperationRoundResult> CloseFilter()
	{
		_hasFiltered = true;
		OperationResult result = await _services.CloseFilterAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false);
		if (!result.IsSuccess)
		{
			return IsMissingArea(result.Status) ? RoundFail(result.Status) : RoundRetry(result.Status);
		}
		return RoundSuccess(result.Status, result.Data, TimeSpan.FromSeconds(1L));
	}

	/// <summary>
	/// 寻找委托。
	/// </summary>
	[NodeFrom("刷新委托")]
	[NodeFrom("关闭筛选")]
	[NodeFrom("寻找委托", Status = "翻页")]
	[NodeFrom("接取失败")]
	[OperationNode("寻找委托")]
	public async Task<OperationRoundResult> FindCommission()
	{
		IntelBoardCommissionType? commissionType = await _services.FindCommissionAsync(base.ZContext, base.LastScreenshot).ConfigureAwait(continueOnCapturedContext: false);
		if (commissionType.HasValue)
		{
			_currentCommissionType = commissionType.Value;
			return RoundSuccess();
		}
		if (_scrollTimes >= 5)
		{
			return RoundSuccess("无委托");
		}
		_scrollTimes++;
		base.ZContext.Logger.Information("情报板未找到可接取委托，翻页 {ScrollTimes}/5", _scrollTimes);
		await _services.ScrollCommissionListAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false);
		return RoundWait("翻页", null, TimeSpan.FromSeconds(1L));
	}

	/// <summary>
	/// 接取委托。
	/// </summary>
	[NodeFrom("寻找委托")]
	[OperationNode("接取委托")]
	public async Task<OperationRoundResult> AcceptCommission()
	{
		OperationResult result = await _services.AcceptCommissionAsync(base.ZContext, base.LastScreenshot).ConfigureAwait(continueOnCapturedContext: false);
		if (result.IsSuccess && (string.Equals(result.Status, "接取委托", StringComparison.Ordinal) || string.Equals(result.Status, "前往", StringComparison.Ordinal)))
		{
			return RoundWait(result.Status, null, TimeSpan.FromMilliseconds(500L));
		}
		return RoundByOperationResult(result, null, retryOnFail: true, TimeSpan.FromMilliseconds(500L));
	}

	/// <summary>
	/// 下一步。
	/// </summary>
	[NodeFrom("接取委托")]
	[OperationNode("下一步")]
	public async Task<OperationRoundResult> NextStep()
	{
		OperationResult result = await _services.NextStepAsync(base.ZContext, base.LastScreenshot).ConfigureAwait(continueOnCapturedContext: false);
		if (result.IsSuccess && (string.Equals(result.Status, "下一步", StringComparison.Ordinal) || string.Equals(result.Status, "无报酬模式", StringComparison.Ordinal)))
		{
			return RoundWait(result.Status, null, TimeSpan.FromSeconds(1L));
		}
		if (result.IsSuccess && (string.Equals(result.Status, "预备编队", StringComparison.Ordinal) || string.Equals(result.Status, "接取失败", StringComparison.Ordinal)))
		{
			return RoundSuccess(result.Status, result.Data);
		}
		return RoundByOperationResult(result, null, retryOnFail: true, TimeSpan.FromSeconds(1L));
	}

	/// <summary>
	/// 接取失败。
	/// </summary>
	[NodeFrom("下一步", Status = "接取失败")]
	[OperationNode("接取失败")]
	public async Task<OperationRoundResult> AcceptFailed()
	{
		return RoundByOperationResult(await _services.ConfirmAcceptFailedAsync(base.ZContext, base.LastScreenshot).ConfigureAwait(continueOnCapturedContext: false), null, retryOnFail: true);
	}

	/// <summary>
	/// 选择预备编队。
	/// </summary>
	[NodeFrom("下一步")]
	[OperationNode("选择预备编队")]
	public async Task<OperationRoundResult> ChoosePredefinedTeam()
	{
		if (_config.PredefinedTeamIndex == -1)
		{
			return RoundSuccess("无需选择预备编队");
		}
		return RoundByOperationResult(await _services.ChooseTeamAsync(base.ZContext, _config.PredefinedTeamIndex).ConfigureAwait(continueOnCapturedContext: false));
	}

	/// <summary>
	/// 点击出战。
	/// </summary>
	[NodeFrom("选择预备编队")]
	[NodeFrom("选择任意预备编队")]
	[OperationNode("点击出战")]
	public async Task<OperationRoundResult> ClickDeploy()
	{
		return RoundByOperationResult(await _services.DeployAsync(base.ZContext, base.LastScreenshot).ConfigureAwait(continueOnCapturedContext: false), null, retryOnFail: true, TimeSpan.FromSeconds(1L));
	}

	/// <summary>
	/// 委托代行中弹窗。
	/// </summary>
	[NodeFrom("点击出战")]
	[OperationNode("委托代行中弹窗")]
	public async Task<OperationRoundResult> ClickCommissionAgent()
	{
		OperationResult result = await _services.ConfirmCommissionAgentAsync(base.ZContext, base.LastScreenshot).ConfigureAwait(continueOnCapturedContext: false);
		return result.IsSuccess && string.Equals(result.Status, "未选择代理人", StringComparison.Ordinal) ? RoundSuccess("未选择代理人", result.Data, TimeSpan.FromSeconds(1L)) : RoundByOperationResult(result, null, retryOnFail: true);
	}

	/// <summary>
	/// 选择首个预备编队后重新出战。
	/// </summary>
	[NodeFrom("委托代行中弹窗", Status = "未选择代理人")]
	[OperationNode("选择任意预备编队")]
	public async Task<OperationRoundResult> ChooseAnyPredefinedTeam()
	{
		ZzzOd.GameLogic.Config.PredefinedTeamInfo fallbackTeam = base.ZContext.TeamConfig.TeamList.FirstOrDefault();
		if (fallbackTeam == null)
		{
			return RoundFail("没有可用预备编队");
		}
		base.ZContext.Logger.Warning("情报板出战未选择代理人，使用预备编队 {TeamName} 重新出战", fallbackTeam.Name);
		return RoundByOperationResult(await _services.ChooseTeamAsync(base.ZContext, fallbackTeam.Idx).ConfigureAwait(continueOnCapturedContext: false));
	}

	/// <summary>
	/// 加载自动战斗指令。
	/// </summary>
	[NodeFrom("委托代行中弹窗")]
	[OperationNode("加载自动战斗指令")]
	public OperationRoundResult InitAutoBattle()
	{
		_services.InitAutoBattle(base.ZContext, _config);
		return RoundSuccess();
	}

	/// <summary>
	/// 等待战斗画面加载。
	/// </summary>
	[NodeFrom("加载自动战斗指令")]
	[OperationNode("等待战斗画面加载", NodeMaxRetryTimes = 60)]
	public OperationRoundResult WaitBattleScreen()
	{
		OperationResult operationResult = _services.CheckBattleScreenReady(base.ZContext, base.LastScreenshot);
		return operationResult.IsSuccess ? RoundSuccess(operationResult.Status, operationResult.Data) : RoundRetry(operationResult.Status, operationResult.Data, TimeSpan.FromSeconds(1L));
	}

	/// <summary>
	/// 战斗前移动。
	/// </summary>
	[NodeFrom("等待战斗画面加载")]
	[OperationNode("战斗前移动")]
	public async Task<OperationRoundResult> PreBattleMove()
	{
		return RoundByOperationResult(await _services.PreBattleMoveAsync(base.ZContext, _currentCommissionType).ConfigureAwait(continueOnCapturedContext: false));
	}

	/// <summary>
	/// 开始自动战斗。
	/// </summary>
	[NodeFrom("战斗前移动")]
	[OperationNode("开始自动战斗")]
	public OperationRoundResult StartAutoBattle()
	{
		_services.StartAutoBattle(base.ZContext);
		return RoundSuccess();
	}

	/// <summary>
	/// 战斗中。
	/// </summary>
	[NodeFrom("开始自动战斗")]
	[OperationNode("战斗中", Mute = true, TimeoutSeconds = 600.0)]
	public async Task<OperationRoundResult> AutoBattle()
	{
		OperationResult result = await _services.RunBattleAsync(base.ZContext, base.LastScreenshot, base.LastScreenshotTimeUtc).ConfigureAwait(continueOnCapturedContext: false);
		if (!result.IsSuccess && string.Equals(result.Status, "自动战斗中", StringComparison.Ordinal))
		{
			return RoundWait(null, null, TimeSpan.FromSeconds(base.ZContext.BattleAssistantConfig.ScreenshotInterval));
		}
		return RoundByOperationResult(result);
	}

	/// <summary>
	/// 检查回到委托列表。
	/// </summary>
	[NodeFrom("战斗中")]
	[NodeFrom("点击结算按钮")]
	[OperationNode("检查回到委托列表")]
	public async Task<OperationRoundResult> CheckBackToList()
	{
		OperationResult result = await _services.CheckBackToListAsync(base.ZContext, base.LastScreenshot).ConfigureAwait(continueOnCapturedContext: false);
		if (result.IsSuccess)
		{
			IntelBoardCommissionType? currentCommissionType = _currentCommissionType;
			if (currentCommissionType.HasValue)
			{
				_runRecord.AddCommission(_currentCommissionType.Value);
			}
			_currentCommissionType = null;
			return RoundSuccess("结算完成");
		}
		return RoundFail(result.Status ?? "未回到列表");
	}

	/// <summary>
	/// 点击结算按钮。
	/// </summary>
	[NodeFrom("检查回到委托列表", Success = false)]
	[OperationNode("点击结算按钮", NodeMaxRetryTimes = 60)]
	public async Task<OperationRoundResult> ClickSettlementButton()
	{
		return RoundByOperationResult(await _services.ClickSettlementButtonAsync(base.ZContext, base.LastScreenshot).ConfigureAwait(continueOnCapturedContext: false), null, retryOnFail: true, TimeSpan.FromSeconds(1L));
	}

	/// <summary>
	/// 检查进度。
	/// </summary>
	[NodeFrom("检查回到委托列表")]
	[NodeFrom("点击情报板")]
	[OperationNode("检查进度")]
	public async Task<OperationRoundResult> CheckProgress()
	{
		if (_config.ExpGrindMode && _runRecord.ExpComplete)
		{
			return RoundSuccess("完成");
		}
		OperationResult progressResult = await _services.ReadProgressAsync(base.ZContext, base.LastScreenshot).ConfigureAwait(continueOnCapturedContext: false);
		if (!progressResult.IsSuccess)
		{
			return RoundFail(progressResult.Status ?? "读取进度失败");
		}
		object data = progressResult.Data;
		int progress = default(int);
		int num;
		if (data is int)
		{
			progress = (int)data;
			num = 1;
		}
		else
		{
			num = 0;
		}
		if (num == 0)
		{
			return RoundFail("进度数据无效");
		}
		if (!_config.ExpGrindMode && progress >= 1000)
		{
			_runRecord.MarkProgressComplete();
			return RoundSuccess("完成");
		}
		if (_config.ExpGrindMode && _runRecord.NotoriousHuntCount == 0 && _runRecord.ExpertChallengeCount == 0 && _runRecord.BaseExp == 0 && progress > 0)
		{
			_runRecord.UpdateBaseExp((progress + 69) / 70 * 250);
			if (_runRecord.ExpComplete)
			{
				return RoundSuccess("完成");
			}
		}
		return RoundFail("继续");
	}

	/// <summary>
	/// 结束处理。
	/// </summary>
	[NodeFrom("打开情报板", Status = "本周期已完成")]
	[NodeFrom("检查进度")]
	[NodeFrom("寻找委托", Status = "无委托")]
	[OperationNodeNotify(OperationNodeNotifyTiming.CurrentDone, Detail = true)]
	[OperationNode("结束处理")]
	public OperationRoundResult FinishProcessing()
	{
		string status = $"完成 恶名狩猎: {_runRecord.NotoriousHuntCount}, 专业挑战室: {_runRecord.ExpertChallengeCount}, 累计经验: {_runRecord.TotalExp}";
		return RoundSuccess(status);
	}

	private OperationRoundResult RoundByPythonClickResult(OperationResult result, TimeSpan delay)
	{
		return (!result.IsSuccess && IsMissingArea(result.Status)) ? RoundFail(result.Status, result.Data) : RoundByOperationResult(result, null, retryOnFail: true, delay);
	}

	private static bool IsMissingArea(string? status)
	{
		return status?.StartsWith("区域未配置 ", StringComparison.Ordinal) ?? false;
	}
}
