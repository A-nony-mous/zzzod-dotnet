using System;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.HollowZero.WitheredDomain;

/// <summary>
/// 默认枯萎之都流程。
/// </summary>
public sealed class OperationWitheredDomainAppFlow : IWitheredDomainAppFlow
{
	private readonly IWitheredDomainRunner _runner;

	private readonly IWitheredDomainAppActions _actions;

	/// <summary>
	/// 初始化流程。
	/// </summary>
	public OperationWitheredDomainAppFlow(IWitheredDomainRunner? runner = null, IWitheredDomainAppActions? actions = null)
	{
		_runner = runner ?? new HollowRunnerWitheredDomainRunner();
		_actions = actions ?? new DefaultWitheredDomainAppActions();
	}

	/// <inheritdoc />
	public async Task<OperationResult> RunAsync(ZContext context, WitheredDomainConfig config, WitheredDomainRunRecord runRecord, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		context.WitheredDomain.InitBeforeRun(config.ChallengeConfig);
		(string, string) tuple = SplitMissionName(config.MissionName);
		string missionTypeName = tuple.Item1;
		string missionName = tuple.Item2;
		int level = 1;
		int phase = 1;
		OperationResult firstScreen = await _actions.CheckFirstScreenAsync(context, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (!firstScreen.IsSuccess)
		{
			return firstScreen;
		}
		bool isInHollow = string.Equals(firstScreen.Status, "在空洞内", StringComparison.Ordinal);
		if (isInHollow)
		{
			level = -1;
			phase = -1;
		}
		else if (!string.Equals(firstScreen.Status, "零号空洞-入口", StringComparison.Ordinal))
		{
			OperationResult transport = await _actions.TransportToEntryAsync(context, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (!transport.IsSuccess)
			{
				return transport;
			}
		}
		while (true)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (isInHollow)
			{
				OperationResult hollow = await AutoRunAsync(context, config, runRecord, missionTypeName, missionName, level, phase, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				if (!hollow.IsSuccess)
				{
					return hollow;
				}
			}
			OperationResult waitEntry = await _actions.WaitEntryLoadingAsync(context, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (!waitEntry.IsSuccess)
			{
				return waitEntry;
			}
			OperationResult chooseType = await _actions.ChooseMissionTypeAsync(context, runRecord, missionTypeName, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (!chooseType.IsSuccess)
			{
				return chooseType;
			}
			if (string.Equals(chooseType.Status, "已完成基本次数", StringComparison.Ordinal))
			{
				return await new WitheredDomainFinishOperation(context, _actions).ExecuteAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			if (!string.Equals(chooseType.Status, "下一步", StringComparison.Ordinal))
			{
				OperationResult chooseMission = await _actions.ChooseMissionAsync(context, missionName, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				if (!chooseMission.IsSuccess)
				{
					return chooseMission;
				}
			}
			// "下一步" Operation 内部已按 WAIT/RETRY 自环，直到出现"出战"/"继续-确认"成功态
			// 或重试耗尽失败，这里只需单次调用，不再由调用方套外层重试循环。
			cancellationToken.ThrowIfCancellationRequested();
			OperationResult next = await _actions.ClickNextAsync(context, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (!next.IsSuccess)
			{
				return next;
			}
			if (string.Equals(next.Status, "出战", StringComparison.Ordinal))
			{
				OperationResult deploy = await _actions.DeployAsync(context, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				if (!deploy.IsSuccess)
				{
					return deploy;
				}
			}
			else if (string.Equals(next.Status, "继续-确认", StringComparison.Ordinal))
			{
				level = -1;
				phase = -1;
			}
			isInHollow = true;
		}
	}

	private async Task<OperationResult> AutoRunAsync(ZContext context, WitheredDomainConfig config, WitheredDomainRunRecord runRecord, string missionTypeName, string missionName, int level, int phase, CancellationToken cancellationToken)
	{
		try
		{
			context.WitheredDomain.InitBeforeHollowStart(missionTypeName, missionName, level, phase);
		}
		catch
		{
			return new OperationResult(IsSuccess: false, "模型加载失败 请重新下载模型");
		}
		return await _runner.RunAsync(context, config, runRecord, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private static (string MissionTypeName, string MissionName) SplitMissionName(string missionName)
	{
		if (string.IsNullOrWhiteSpace(missionName))
		{
			return (MissionTypeName: "旧都列车", MissionName: "旧都列车-内部");
		}
		int num = missionName.IndexOf('-', StringComparison.Ordinal);
		return (num < 0) ? (MissionTypeName: missionName, MissionName: missionName) : (MissionTypeName: missionName.Substring(0, num), MissionName: missionName);
	}
}
