using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.EmailApp;

/// <summary>
/// 邮件应用流程。
/// </summary>
public interface IEmailAppFlow
{
	/// <summary>
	/// 运行邮件领取流程。
	/// </summary>
	Task<OperationResult> RunAsync(ZContext context, CancellationToken cancellationToken);
}
