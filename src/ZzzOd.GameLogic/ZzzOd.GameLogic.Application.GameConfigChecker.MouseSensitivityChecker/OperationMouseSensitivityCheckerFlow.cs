using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.GameConfigChecker.MouseSensitivityChecker;

/// <summary>
/// 默认鼠标灵敏度检。Operation 流程。
/// </summary>
public sealed class OperationMouseSensitivityCheckerFlow : IMouseSensitivityCheckerFlow
{
	/// <inheritdoc />
	public Task<OperationResult> RunAsync(ZContext context, CancellationToken cancellationToken)
	{
		MouseSensitivityCheckerOperation mouseSensitivityCheckerOperation = new MouseSensitivityCheckerOperation(context);
		return mouseSensitivityCheckerOperation.ExecuteAsync(cancellationToken);
	}
}
