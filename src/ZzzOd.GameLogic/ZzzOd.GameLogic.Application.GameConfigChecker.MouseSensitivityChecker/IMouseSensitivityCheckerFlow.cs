using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.GameConfigChecker.MouseSensitivityChecker;

/// <summary>
/// 鼠标灵敏度检测流程。
/// </summary>
public interface IMouseSensitivityCheckerFlow
{
	/// <summary>
	/// 运行鼠标灵敏度检测。
	/// </summary>
	Task<OperationResult> RunAsync(ZContext context, CancellationToken cancellationToken);
}
