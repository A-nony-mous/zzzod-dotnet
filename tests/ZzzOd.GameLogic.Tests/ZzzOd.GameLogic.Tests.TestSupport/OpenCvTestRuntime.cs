using System;
using OpenCvSharp;

namespace ZzzOd.GameLogic.Tests.TestSupport;

internal static class OpenCvTestRuntime
{
	public static void RequireAvailable()
	{
		try
		{
			using Mat mat = new Mat(1, 1, MatType.CV_8UC1, Scalar.All(1.0));
			if (mat.Empty())
			{
				throw new InvalidOperationException("OpenCV runtime created an empty smoke-test Mat.");
			}
		}
		catch (DllNotFoundException innerException)
		{
			throw new InvalidOperationException("OpenCV native runtime is required for Windows vision tests.", innerException);
		}
		catch (TypeInitializationException ex) when (ex.InnerException is DllNotFoundException)
		{
			throw new InvalidOperationException("OpenCV native runtime is required for Windows vision tests.", ex);
		}
	}
}
