using OpenCvSharp;
using Xunit;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Platform;

public sealed class VisionRuntimeSmokeTests
{
	[Fact]
	public void OpenCvRuntime_CreatesMatAndCountsPixels()
	{
		OpenCvTestRuntime.RequireAvailable();
		using Mat mat = new Mat(10, 10, MatType.CV_8UC1, Scalar.All(255.0));
		Assert.Equal(100, Cv2.CountNonZero(mat));
	}

	[Fact]
	public void OpenCvRuntime_RunsMinimalTemplateMatching()
	{
		OpenCvTestRuntime.RequireAvailable();
		using Mat mat = new Mat(20, 20, MatType.CV_8UC1, Scalar.Black);
		Cv2.Rectangle(mat, new Rect(8, 7, 4, 5), Scalar.White, -1);
		using Mat mat2 = new Mat(mat, new Rect(8, 7, 4, 5)).Clone();
		using Mat mat3 = new Mat();
		Cv2.MatchTemplate(mat, mat2, mat3, TemplateMatchModes.SqDiffNormed);
		Cv2.MinMaxLoc(mat3, out var minVal, out var _, out var minLoc, out var _);
		Assert.Equal(0.0, minVal, 6);
		Assert.Equal(new Point(8, 7), minLoc);
	}
}
