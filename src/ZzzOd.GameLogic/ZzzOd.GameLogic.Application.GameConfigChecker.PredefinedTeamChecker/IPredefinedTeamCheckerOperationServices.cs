using System.Collections.Generic;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Matcher;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.GameConfigChecker.PredefinedTeamChecker;

/// <summary>
/// 预备编队角色识别 Operation 依赖服务。
/// </summary>
public interface IPredefinedTeamCheckerOperationServices
{
	/// <summary>
	/// 前往菜单。
	/// </summary>
	Task<OperationResult> GotoMenuAsync(ZContext context);

	/// <summary>
	/// 返回大世界。
	/// </summary>
	Task<OperationResult> BackToNormalWorldAsync(ZContext context);

	/// <summary>
	/// 运行 OCR。
	/// </summary>
	IReadOnlyDictionary<string, MatchResultList> RunOcr(ZContext context, Mat screen);

	/// <summary>
	/// 匹配预备编队代理人头像。
	/// </summary>
	IReadOnlyList<MatchResult> MatchTeamAgentTemplate(ZContext context, Mat screen, OneDragon.Core.Abstractions.Geometry.Rect avatarRect);
}
