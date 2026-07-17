using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.GameConfigChecker.MouseSensitivityChecker;

/// <summary>
/// 鼠标灵敏度检测应用。
/// </summary>
public sealed class MouseSensitivityCheckerApp : ZApplication
{
	private readonly IMouseSensitivityCheckerFlow _flow;

	/// <summary>
	/// 初始化鼠标灵敏度检测应用。
	/// </summary>
	public MouseSensitivityCheckerApp(ZContext context, ZApplicationRunRecord? runRecord = null, IMouseSensitivityCheckerFlow? flow = null)
		: base(context, "mouse_sensitivity_checker", runRecord, "鼠标灵敏度检测")
	{
		_flow = flow ?? new OperationMouseSensitivityCheckerFlow();
	}

	/// <inheritdoc />
	protected override Task<OperationResult> ExecuteCoreAsync(CancellationToken cancellationToken)
	{
		return _flow.RunAsync(base.Context, cancellationToken);
	}
}
