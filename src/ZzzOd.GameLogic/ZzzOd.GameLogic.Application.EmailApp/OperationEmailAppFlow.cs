using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.EmailApp;

/// <summary>
/// 默认邮件 Operation 流程。
/// </summary>
public sealed class OperationEmailAppFlow : IEmailAppFlow
{
	/// <inheritdoc />
	public Task<OperationResult> RunAsync(ZContext context, CancellationToken cancellationToken)
	{
		EmailOperation emailOperation = new EmailOperation(context);
		return emailOperation.ExecuteAsync(cancellationToken);
	}
}
