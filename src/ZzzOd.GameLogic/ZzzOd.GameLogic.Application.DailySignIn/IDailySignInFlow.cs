using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.DailySignIn;

/// <summary>
/// 每日签到流程。
/// </summary>
public interface IDailySignInFlow
{
	/// <summary>
	/// 运行每日签到：代理执行配置中选定的签到子应用。
	/// </summary>
	Task<OperationResult> RunAsync(ZContext context, DailySignInConfig config, int instanceIndex, string groupId, CancellationToken cancellationToken);
}
