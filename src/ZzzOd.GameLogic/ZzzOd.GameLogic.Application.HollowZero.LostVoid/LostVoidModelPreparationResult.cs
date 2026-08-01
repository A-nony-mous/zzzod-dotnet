using System;

namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

/// <summary>
/// 迷失之地模型准备结果。
/// </summary>
public sealed record LostVoidModelPreparationResult(
	bool IsSuccess,
	string Stage,
	string ModelPath,
	string? ErrorMessage = null,
	Exception? Exception = null)
{
	/// <summary>模型准备成功。</summary>
	public static LostVoidModelPreparationResult Success(string modelPath)
	{
		return new LostVoidModelPreparationResult(true, "完成", modelPath);
	}

	/// <summary>模型准备失败。</summary>
	public static LostVoidModelPreparationResult Failure(string stage, string modelPath, string errorMessage, Exception? exception = null)
	{
		return new LostVoidModelPreparationResult(false, stage, modelPath, errorMessage, exception);
	}

	/// <summary>供应用结果展示的失败信息。</summary>
	public string ToFailureStatus()
	{
		return $"模型准备失败[{Stage}]: {ErrorMessage ?? "未知错误"}; ModelPath={ModelPath}";
	}
}
