using System;
using System.Collections.Generic;
using OneDragon.Core.Ocr;

namespace ZzzOd.GameLogic.Vision;

/// <summary>
/// 图像分析流水线的执行结果。
/// </summary>
public sealed record ImageAnalysisPipelineRunResult(
	bool IsSuccess,
	string Status,
	string PipelinePath,
	IReadOnlyList<ImageAnalysisContour> Contours,
	IReadOnlyList<OcrMatchResult> OcrResults)
{
	/// <summary>构造成功结果。</summary>
	public static ImageAnalysisPipelineRunResult Success(
		string path,
		IReadOnlyList<ImageAnalysisContour> contours,
		IReadOnlyList<OcrMatchResult>? ocrResults = null)
	{
		return new ImageAnalysisPipelineRunResult(IsSuccess: true, string.Empty, path, contours, ocrResults ?? Array.Empty<OcrMatchResult>());
	}

	/// <summary>构造失败结果。</summary>
	public static ImageAnalysisPipelineRunResult Fail(string status, string path)
	{
		return new ImageAnalysisPipelineRunResult(IsSuccess: false, status, path, Array.Empty<ImageAnalysisContour>(), Array.Empty<OcrMatchResult>());
	}
}
