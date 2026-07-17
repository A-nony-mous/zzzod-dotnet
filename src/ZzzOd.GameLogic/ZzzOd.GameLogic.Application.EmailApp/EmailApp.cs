using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.EmailApp;

/// <summary>
/// 邮件应用。
/// </summary>
public sealed class EmailApp : ZApplication
{
	private readonly IEmailAppFlow _flow;

	/// <summary>
	/// 初始化邮件应用。
	/// </summary>
	public EmailApp(ZContext context, ZApplicationRunRecord? runRecord = null, IEmailAppFlow? flow = null)
		: base(context, "email", runRecord, "邮件")
	{
		_flow = flow ?? new OperationEmailAppFlow();
	}

	/// <inheritdoc />
	protected override async Task<OperationResult> ExecuteCoreAsync(CancellationToken cancellationToken)
	{
		base.Context.ScreenContext.EnterScope("email");
		try
		{
			return await _flow.RunAsync(base.Context, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		finally
		{
			base.Context.ScreenContext.ExitScope();
		}
	}
}
