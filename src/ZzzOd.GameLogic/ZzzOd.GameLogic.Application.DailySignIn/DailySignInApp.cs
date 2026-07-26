using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.DailySignIn;

/// <summary>
/// 每日签到应用：代理运行用户所选的具体每日签到应用（吼吼饼铺、卦象集录、刮刮卡）。
/// </summary>
public sealed class DailySignInApp : ZApplication
{
	private readonly DailySignInConfig _config;

	private readonly int _instanceIndex;

	private readonly string _groupId;

	private readonly IDailySignInFlow _flow;

	/// <summary>
	/// 初始化每日签到应用。
	/// </summary>
	/// <param name="context">运行上下文。</param>
	/// <param name="instanceIndex">账号实例索引。</param>
	/// <param name="groupId">应用组 id。</param>
	/// <param name="config">签到配置；为空时从磁盘加载。</param>
	/// <param name="runRecord">运行记录。</param>
	/// <param name="flow">代理执行流程；为空时使用默认 Operation 流程。</param>
	public DailySignInApp(ZContext context, int instanceIndex, string groupId, DailySignInConfig? config = null, DailySignInRunRecord? runRecord = null, IDailySignInFlow? flow = null)
		: base(context, "daily_signin", runRecord, "每日签到")
	{
		_instanceIndex = instanceIndex;
		_groupId = groupId;
		_config = config ?? DailySignInConfig.Load(context.Environment, instanceIndex, groupId);
		_flow = flow ?? new OperationDailySignInFlow();
	}

	/// <inheritdoc />
	protected override Task<OperationResult> ExecuteCoreAsync(CancellationToken cancellationToken)
	{
		return _flow.RunAsync(base.Context, _config, _instanceIndex, _groupId, cancellationToken);
	}
}
