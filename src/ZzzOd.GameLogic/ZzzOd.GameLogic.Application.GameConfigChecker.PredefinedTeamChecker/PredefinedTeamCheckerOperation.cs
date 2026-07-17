using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Matcher;
using OneDragon.Core.Utils;
using OpenCvSharp;
using Serilog;
using ZzzOd.GameLogic.Config;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.GameData;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Application.GameConfigChecker.PredefinedTeamChecker;

/// <summary>
/// 预备编队角色识别 Operation。
/// </summary>
public sealed class PredefinedTeamCheckerOperation : ZOperation
{
	private const int MaxScrollTimes = 4;

	private readonly IPredefinedTeamCheckerOperationServices _services;

	private readonly TimeSpan _retryDelay;

	private readonly TimeSpan _successDelay;

	private int _scrollTimes;

	/// <summary>
	/// 已下滑次数。
	/// </summary>
	public int ScrollTimes => _scrollTimes;

	/// <summary>
	/// 初始化预备编队角色识别 Operation。
	/// </summary>
	public PredefinedTeamCheckerOperation(ZContext context, IPredefinedTeamCheckerOperationServices? services = null, TimeSpan? retryDelay = null, TimeSpan? successDelay = null)
		: base(context, "预备编队角色识别")
	{
		_services = services ?? new DefaultPredefinedTeamCheckerOperationServices();
		_retryDelay = retryDelay ?? TimeSpan.FromSeconds(1L);
		_successDelay = successDelay ?? TimeSpan.FromSeconds(2L);
	}

	/// <inheritdoc />
	protected override Task OnInitializeAsync(CancellationToken cancellationToken)
	{
		_scrollTimes = 0;
		return Task.CompletedTask;
	}

	/// <summary>
	/// 前往菜单画面。
	/// </summary>
	[OperationNode("前往菜单画面", IsStartNode = true)]
	public async Task<OperationRoundResult> GotoMenu()
	{
		return RoundByOperationResult(await _services.GotoMenuAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false));
	}

	/// <summary>
	/// 前往更多功能画面。
	/// </summary>
	[NodeFrom("前往菜单画面")]
	[OperationNode("前往更多功能画面")]
	public OperationRoundResult GotoMenuMore()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? retryDelay = _retryDelay;
		return RoundByGotoScreen(lastScreenshot, "菜单-更多功能", null, null, retryDelay);
	}

	/// <summary>
	/// 点击预备编队。
	/// </summary>
	[NodeFrom("前往更多功能画面")]
	[OperationNode("点击预备编队")]
	public OperationRoundResult ClickPredefinedTeam()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? successDelay = _successDelay;
		TimeSpan? retryDelay = _retryDelay;
		IReadOnlyList<(string, string)> untilNotFindAll = new (string, string)[] { ("菜单-更多功能", "按钮-兑换码") };
		return RoundByFindAndClickArea(lastScreenshot, "菜单-更多功能", "按钮-预备编队", null, successDelay, retryDelay, cropFirst: true, centerX: false, null, untilNotFindAll);
	}

	/// <summary>
	/// 识别编队角色。
	/// </summary>
	[NodeFrom("点击预备编队")]
	[OperationNode("识别编队角色")]
	public OperationRoundResult CheckTeamMembers()
	{
		UpdateTeamMembers(base.LastScreenshot);
		if (_scrollTimes >= 4)
		{
			return RoundSuccess();
		}
		if (base.ZContext.Controller == null)
		{
			return RoundRetry("未获取控制器", null, _retryDelay);
		}
		OneDragon.Core.Abstractions.Geometry.Point point = new OneDragon.Core.Abstractions.Geometry.Point(base.ZContext.Controller.StandardWidth / 2, base.ZContext.Controller.StandardHeight / 2);
		OneDragon.Core.Abstractions.Geometry.Point end = point + new OneDragon.Core.Abstractions.Geometry.Point(0, -500);
		base.ZContext.Controller.DragTo(end, point);
		_scrollTimes++;
		return RoundWait("继续识别", null, _retryDelay);
	}

	/// <summary>
	/// 更新队伍成员。
	/// </summary>
	public void UpdateTeamMembers(Mat? screen)
	{
		if (screen == null)
		{
			return;
		}
		IReadOnlyDictionary<string, MatchResultList> readOnlyDictionary = _services.RunOcr(base.ZContext, screen);
		string[] targetWords = base.ZContext.TeamConfig.TeamList.Select((PredefinedTeamInfo team) => team.Name).ToArray();
		foreach (KeyValuePair<string, MatchResultList> item in readOnlyDictionary)
		{
			item.Deconstruct(out var key, out var value);
			string word = key;
			MatchResultList matchResultList = value;
			MatchResult max = matchResultList.Max;
			if (max == null)
			{
				continue;
			}
			int? num = StringUtils.FindBestMatchByDifflib(word, targetWords);
			if (!num.HasValue)
			{
				continue;
			}
			PredefinedTeamInfo predefinedTeamInfo = base.ZContext.TeamConfig.TeamList[num.Value];
			OneDragon.Core.Abstractions.Geometry.Rect avatarRect = new OneDragon.Core.Abstractions.Geometry.Rect(max.LeftTop.X - 10, max.LeftTop.Y, max.LeftTop.X + 800, max.LeftTop.Y + 250);
			IReadOnlyList<MatchResult> readOnlyList = _services.MatchTeamAgentTemplate(base.ZContext, screen, avatarRect);
			if (readOnlyList.Count == 0)
			{
				continue;
			}
			List<Agent> list = (from match in FilterOverlappedAgentMatches(readOnlyList)
				select match.Data).OfType<Agent>().ToList();
			if (list.Count != 0)
			{
				Log.Information("编队名称: {TeamName} 识别代理人: {Agents}", predefinedTeamInfo.Name, list.Select((Agent agent) => agent.AgentName).ToArray());
				base.ZContext.UpdateTeamMembers(predefinedTeamInfo.Name, list.Select((Agent agent) => agent.AgentId).ToList());
			}
		}
	}

	/// <summary>
	/// 成功后返回。
	/// </summary>
	[NodeFrom("识别编队角色")]
	[OperationNode("成功后返回")]
	public async Task<OperationRoundResult> BackAtLast()
	{
		return RoundByOperationResult(await _services.BackToNormalWorldAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false));
	}

	private static IReadOnlyList<MatchResult> FilterOverlappedAgentMatches(IReadOnlyList<MatchResult> agentMatches)
	{
		List<MatchResult> list = new List<MatchResult>();
		foreach (MatchResult item in agentMatches.OrderBy((MatchResult match) => match.LeftTop.X))
		{
			if (list.Count == 0)
			{
				list.Add(item);
				continue;
			}
			MatchResult matchResult = list[list.Count - 1];
			if (CalUtils.CalculateOverlapPercent(item.Rect, matchResult.Rect) < 0.7)
			{
				list.Add(item);
			}
			else if (item.Confidence > matchResult.Confidence)
			{
				list[list.Count - 1] = item;
			}
		}
		return list;
	}
}
