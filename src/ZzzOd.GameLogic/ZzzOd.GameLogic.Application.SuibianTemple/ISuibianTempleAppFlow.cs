using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.SuibianTemple;

/// <summary>
/// 随便观应用流程。
/// </summary>
public interface ISuibianTempleAppFlow
{
	Task<OperationResult> RunAsync(ZContext context, SuibianTempleConfig config, CancellationToken cancellationToken);
}
