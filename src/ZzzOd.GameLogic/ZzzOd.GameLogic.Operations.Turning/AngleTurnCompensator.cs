using System;
using OneDragon.Core.Utils;
using ZzzOd.GameLogic.Controller;

namespace ZzzOd.GameLogic.Operations.Turning;

/// <summary>
/// 运行期角度转向补偿会话。
/// </summary>
public sealed class AngleTurnCompensator
{
	private const double AngleEpsilon = 1E-06;

	private const double MinScale = 0.5;

	private const double MaxScale = 2.0;

	private const double MaxScaleChange = 0.1;

	private const double MinAngleForReverseUnfold = 150.0;

	private readonly Action<double> _turnByAngleDiff;

	/// <summary>
	/// 当前补偿比例。
	/// </summary>
	public double Scale { get; private set; } = 1.0;

	/// <summary>
	/// 上一轮转向来源角度。
	/// </summary>
	public double? LastSourceAngle { get; private set; }

	/// <summary>
	/// 上一轮实际下发角度。
	/// </summary>
	public double? LastEffectiveAngleDiff { get; private set; }

	/// <summary>
	/// 使用控制器创建补偿会话。
	/// </summary>
	public AngleTurnCompensator(ZPcController controller)
		: this(delegate(double angleDiff)
		{
			controller.TurnByAngleDiff((float)angleDiff);
		})
	{
	}

	/// <summary>
	/// 使用可记录动作创建补偿会话。
	/// </summary>
	public AngleTurnCompensator(Action<double> turnByAngleDiff)
	{
		_turnByAngleDiff = turnByAngleDiff;
	}

	/// <summary>
	/// 清空比例和上一轮样本。
	/// </summary>
	public void Reset()
	{
		Scale = 1.0;
		ClearPendingSample();
	}

	/// <summary>
	/// 清空上一轮尚未学习的样本。
	/// </summary>
	public void ClearPendingSample()
	{
		LastSourceAngle = null;
		LastEffectiveAngleDiff = null;
	}

	/// <summary>
	/// 根据转向前后的实际角度变化更新补偿比例。
	/// </summary>
	public void Learn(double sourceAngle, double effectiveAngleDiff, double currentAngle)
	{
		if (!(Math.Abs(effectiveAngleDiff) <= 1E-06))
		{
			double num = ObservedAngleChange(sourceAngle, effectiveAngleDiff, currentAngle);
			if (!(Math.Abs(num) <= 1E-06) && !(num * effectiveAngleDiff <= 0.0))
			{
				double val = effectiveAngleDiff / num - Scale;
				double num2 = Math.Max(-0.1, Math.Min(val, 0.1));
				Scale = Math.Max(0.5, Math.Min(Scale + num2, 2.0));
			}
		}
	}

	/// <summary>
	/// 使用当前朝向学习上一轮样本，再下发本轮转向。
	/// </summary>
	public double TurnFromAngle(double sourceAngle, double angleDiff)
	{
		if (LastSourceAngle.HasValue && LastEffectiveAngleDiff.HasValue)
		{
			Learn(LastSourceAngle.Value, LastEffectiveAngleDiff.Value, sourceAngle);
		}
		LastSourceAngle = sourceAngle;
		LastEffectiveAngleDiff = Turn(angleDiff);
		return LastEffectiveAngleDiff.Value;
	}

	/// <summary>
	/// 按当前补偿比例下发转向。
	/// </summary>
	public double Turn(double angleDiff, double? maxAbsAngleDiff = null)
	{
		double num = angleDiff * Scale;
		if (maxAbsAngleDiff.HasValue)
		{
			num = Math.Max(0.0 - maxAbsAngleDiff.Value, Math.Min(num, maxAbsAngleDiff.Value));
		}
		_turnByAngleDiff(num);
		return num;
	}

	private static double ObservedAngleChange(double sourceAngle, double effectiveAngleDiff, double currentAngle)
	{
		double num = CalUtils.AngleDelta(sourceAngle, currentAngle);
		if (num * effectiveAngleDiff >= 0.0)
		{
			return num;
		}
		if (Math.Abs(effectiveAngleDiff) < 150.0 || Math.Abs(num) < 150.0)
		{
			return num;
		}
		return (effectiveAngleDiff > 0.0) ? (num + 360.0) : (num - 360.0);
	}
}
