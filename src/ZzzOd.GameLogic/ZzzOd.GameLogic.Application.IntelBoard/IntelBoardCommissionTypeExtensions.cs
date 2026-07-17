using System;

namespace ZzzOd.GameLogic.Application.IntelBoard;

/// <summary>
/// 情报板委托类型扩展。
/// </summary>
public static class IntelBoardCommissionTypeExtensions
{
	/// <summary>
	/// 转换为 BaselineParity 配置和 OCR 使用的中文值。
	/// </summary>
	public static string ToDisplayName(this IntelBoardCommissionType commissionType)
	{
		if (1 == 0)
		{
		}
		string result = commissionType switch
		{
			IntelBoardCommissionType.ExpertChallenge => "专业挑战室", 
			IntelBoardCommissionType.NotoriousHunt => "恶名狩猎", 
			_ => string.Empty, 
		};
		if (1 == 0)
		{
		}
		return result;
	}

	/// <summary>
	/// 从 OCR 文本解析委托类型。
	/// </summary>
	public static bool TryParseDisplayName(string? value, out IntelBoardCommissionType commissionType)
	{
		commissionType = IntelBoardCommissionType.ExpertChallenge;
		if (string.Equals(value, "专业挑战室", StringComparison.Ordinal))
		{
			commissionType = IntelBoardCommissionType.ExpertChallenge;
			return true;
		}
		if (string.Equals(value, "恶名狩猎", StringComparison.Ordinal))
		{
			commissionType = IntelBoardCommissionType.NotoriousHunt;
			return true;
		}
		return false;
	}
}
