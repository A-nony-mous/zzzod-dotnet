using System.Collections.Generic;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Matcher;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Application.GameConfigChecker.PredefinedTeamChecker;

/// <summary>
/// 预备编队角色识别默认 Operation 依赖服务。
/// </summary>
public sealed class DefaultPredefinedTeamCheckerOperationServices : IPredefinedTeamCheckerOperationServices
{
	/// <inheritdoc />
	public async Task<OperationResult> GotoMenuAsync(ZContext context)
	{
		GotoMenu operation = new GotoMenu(context);
		return await operation.ExecuteAsync().ConfigureAwait(continueOnCapturedContext: false);
	}

	/// <inheritdoc />
	public async Task<OperationResult> BackToNormalWorldAsync(ZContext context)
	{
		BackToNormalWorld operation = new BackToNormalWorld(context);
		return await operation.ExecuteAsync().ConfigureAwait(continueOnCapturedContext: false);
	}

	/// <inheritdoc />
	public IReadOnlyDictionary<string, MatchResultList> RunOcr(ZContext context, Mat screen)
	{
		return context.OcrService.GetOcrResultMap(screen);
	}

	/// <inheritdoc />
	public IReadOnlyList<MatchResult> MatchTeamAgentTemplate(ZContext context, Mat screen, OneDragon.Core.Abstractions.Geometry.Rect avatarRect)
	{
		return AgentTemplateMatcher.MatchTeamAgentTemplate(context, screen, avatarRect);
	}
}
