using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Application.GameConfigChecker.MouseSensitivityChecker;

/// <summary>
/// 鼠标灵敏度检测 Operation。
/// </summary>
public sealed class MouseSensitivityCheckerOperation : ZOperation
{
	/// <summary>鼠标模式转向距离。</summary>
	public const int DefaultTurnDistance = 500;

	/// <summary>手柄测试推动时长。</summary>
	public const double DefaultGamepadTestDurationSeconds = 0.3;

	private readonly IMouseSensitivityCheckerOperationServices _services;

	private readonly int _turnDistance;

	private readonly double _gamepadTestDurationSeconds;

	private int _angleCheckTimes;

	private double _lastAngle;

	private readonly List<double> _angleDiffList = new List<double>();

	/// <summary>
	/// 角度偏移列表。
	/// </summary>
	public IReadOnlyList<double> AngleDiffList => _angleDiffList;

	/// <summary>
	/// 初始化鼠标灵敏度检测 Operation。
	/// </summary>
	public MouseSensitivityCheckerOperation(ZContext context, IMouseSensitivityCheckerOperationServices? services = null, int turnDistance = 500, double gamepadTestDurationSeconds = 0.3)
		: base(context, "鼠标灵敏度检测")
	{
		_services = services ?? new DefaultMouseSensitivityCheckerOperationServices();
		_turnDistance = turnDistance;
		_gamepadTestDurationSeconds = gamepadTestDurationSeconds;
	}

	/// <summary>
	/// 返回大世界。
	/// </summary>
	[OperationNode("返回大世界", IsStartNode = true)]
	public async Task<OperationRoundResult> BackAtFirst()
	{
		return RoundByOperationResult(await _services.BackToNormalWorldAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false));
	}

	/// <summary>
	/// 传送。
	/// </summary>
	[NodeFrom("返回大世界")]
	[OperationNode("传送")]
	public async Task<OperationRoundResult> Transport()
	{
		return RoundByOperationResult(await _services.TransportToVideoStoreAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false));
	}

	/// <summary>
	/// 转向检测。
	/// </summary>
	[NodeFrom("传送")]
	[OperationNode("转向检测")]
	public OperationRoundResult Check()
	{
		bool flag = _services.IsGamepadMode(base.ZContext);
		if (flag && (double)Math.Abs(base.ZContext.GameConfig.TurnDx) < 1E-06)
		{
			return RoundFail("手柄灵敏度检测需先完成鼠标灵敏度检测 (turn_dx)");
		}
		double? num = _services.ReadViewAngle(base.ZContext);
		if (!num.HasValue)
		{
			return RoundFail("识别朝向失败");
		}
		if (_angleCheckTimes > 0)
		{
			double item = NormalizeAngleDiff(num.Value - _lastAngle);
			_angleDiffList.Add(item);
		}
		_angleCheckTimes++;
		if (_angleCheckTimes >= 10)
		{
			return RoundSuccess();
		}
		_lastAngle = num.Value;
		if (flag)
		{
			_services.TurnGamepad(base.ZContext, _gamepadTestDurationSeconds);
		}
		else
		{
			_services.TurnByDistance(base.ZContext, _turnDistance);
		}
		return RoundWait("转向继续下一轮识别", null, TimeSpan.FromSeconds(2L));
	}

	/// <summary>
	/// 结果统计。
	/// </summary>
	[NodeFrom("转向检测")]
	[OperationNode("结果统计")]
	public OperationRoundResult Calculate()
	{
		double num = ((_angleDiffList.Count == 0) ? 0.0 : _angleDiffList.Average());
		if (Math.Abs(num) < 1E-06)
		{
			return RoundFail("平均角度差过小，检测结果不可靠");
		}
		if (_services.IsGamepadMode(base.ZContext))
		{
			double num2 = Math.Abs((double)base.ZContext.GameConfig.TurnDx * num) / _gamepadTestDurationSeconds;
			base.ZContext.GameConfig.GamepadTurnSpeed = (float)num2;
			_services.UpdateGamepadTurnSpeed(base.ZContext, num2);
		}
		else
		{
			double num3 = (double)_turnDistance / num;
			base.ZContext.GameConfig.TurnDx = (float)num3;
			_services.UpdateTurnDx(base.ZContext, num3);
		}
		return RoundSuccess("完成检测");
	}

	/// <summary>
	/// 归一化角度差。
	/// </summary>
	public static double NormalizeAngleDiff(double angleDiff)
	{
		return (angleDiff > 180.0) ? (angleDiff - 360.0) : angleDiff;
	}
}
