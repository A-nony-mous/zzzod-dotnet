using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Application.DailySignIn;

/// <summary>
/// 每日签到代理执行流程：按配置转发到具体的签到子应用。
/// </summary>
public sealed class DailySignInOperation : ZOperation
{
	private readonly DailySignInConfig _config;

	private readonly int _instanceIndex;

	private readonly string _groupId;

	private CancellationToken _cancellationToken;

	/// <summary>
	/// 初始化每日签到代理执行流程。
	/// </summary>
	public DailySignInOperation(ZContext context, DailySignInConfig config, int instanceIndex, string groupId)
		: base(context, "每日签到")
	{
		_config = config;
		_instanceIndex = instanceIndex;
		_groupId = groupId;
	}

	/// <inheritdoc />
	protected override Task OnInitializeAsync(CancellationToken cancellationToken)
	{
		_cancellationToken = cancellationToken;
		return Task.CompletedTask;
	}

	/// <summary>
	/// 运行子应用：读取配置选中的签到子应用并代理执行。成功时不透传子应用状态，
	/// 失败时透传子应用的失败状态。
	/// </summary>
	[OperationNode("运行子应用", IsStartNode = true)]
	public async Task<OperationRoundResult> RunSubApp()
	{
		string subAppId = _config.SelectedSign;
		if (string.IsNullOrWhiteSpace(subAppId))
		{
			return RoundFail("未选择子应用");
		}
		if (!ZContext.RunContext.IsAppRegistered(subAppId))
		{
			return RoundFail("未找到应用 " + subAppId);
		}
		IApplication application = ZContext.RunContext.GetApplication(subAppId, _instanceIndex, _groupId);
		OperationResult result = await application.ExecuteAsync(_cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		return result.IsSuccess ? RoundSuccess() : RoundFail(result.Status);
	}
}
