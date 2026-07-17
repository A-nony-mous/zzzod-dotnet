using System;
using OpenCvSharp;

namespace ZzzOd.GameLogic.Application.Devtools.LargeMapRecorder;

/// <summary>
/// 图标提取结果。
/// </summary>
public sealed record MapIconExtractionResult(Mat Raw, Mat Mask) : IDisposable
{
	/// <inheritdoc />
	public void Dispose()
	{
		Raw.Dispose();
		Mask.Dispose();
	}
}
