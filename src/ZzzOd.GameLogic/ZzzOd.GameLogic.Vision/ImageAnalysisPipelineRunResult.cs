using System;
using System.Collections.Generic;

namespace ZzzOd.GameLogic.Vision;

/// <summary>
/// 图像分析流水线的执行结果。
/// </summary>
public sealed record ImageAnalysisPipelineRunResult(bool IsSuccess, string Status, string PipelinePath, IReadOnlyList<ImageAnalysisContour> Contours)
{
	/// <summary>构造成功结果。</summary>
	public static ImageAnalysisPipelineRunResult Success(string path, IReadOnlyList<ImageAnalysisContour> contours)
	{
		return new ImageAnalysisPipelineRunResult(IsSuccess: true, string.Empty, path, contours);
	}

	/// <summary>构造失败结果。</summary>
	public static ImageAnalysisPipelineRunResult Fail(string status, string path)
	{
		return new ImageAnalysisPipelineRunResult(IsSuccess: false, status, path, Array.Empty<ImageAnalysisContour>());
	}
}
