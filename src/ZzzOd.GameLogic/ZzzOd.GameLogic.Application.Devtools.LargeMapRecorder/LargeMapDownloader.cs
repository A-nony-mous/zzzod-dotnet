using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Runtime;
using OneDragon.Core.Utils;
using OpenCvSharp;

namespace ZzzOd.GameLogic.Application.Devtools.LargeMapRecorder;

/// <summary>
/// 大地图图片下载器。
/// </summary>
public sealed class LargeMapDownloader
{
	private readonly OneDragonEnvironment _environment;

	private readonly IRemoteMapImageClient _imageClient;

	/// <summary>
	/// 已知米游社地图 URL。
	/// </summary>
	public static IReadOnlyDictionary<string, string> ImageUrlMap { get; } = new Dictionary<string, string>(StringComparer.Ordinal) { ["HKC_ZYZZQ_DLDC"] = "https://act-upload.mihoyo.com/nap-obc-indep/2025/06/26/76099754/c3474736e3ff2b10ea0a46dee0604f89_2135290461906531157.png" };

	/// <summary>
	/// 初始化下载器。
	/// </summary>
	public LargeMapDownloader(OneDragonEnvironment environment, IRemoteMapImageClient? imageClient = null)
	{
		_environment = environment;
		_imageClient = imageClient ?? new HttpRemoteMapImageClient();
	}

	/// <summary>
	/// 获取区域地图图片，优先读取 .debug 缓存。
	/// </summary>
	public async Task<Mat> GetAreaMapImageAsync(string areaId, int resize = 120, CancellationToken cancellationToken = default(CancellationToken))
	{
		string filePath = GetAreaMapImagePath(areaId, resize);
		Mat cached = CvImageUtils.ReadImage(filePath);
		if (cached != null)
		{
			return cached;
		}
		if (!ImageUrlMap.TryGetValue(areaId, out string imageUrl))
		{
			throw new KeyNotFoundException("未知大地图区域 " + areaId);
		}
		Mat image = await GetMapImageAsync(imageUrl, resize, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		CvImageUtils.SaveImage(image, filePath);
		return image;
	}

	/// <summary>
	/// 下载并处理远程地图图片。
	/// </summary>
	public async Task<Mat> GetMapImageAsync(string imageUrl, int resize = 120, CancellationToken cancellationToken = default(CancellationToken))
	{
		string url = $"{imageUrl}?x-oss-process=image/resize,p_{resize}";
		using Mat encoded = Mat.FromArray(await _imageClient.GetBytesAsync(url, cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
		Mat decoded = Cv2.ImDecode(encoded, ImreadModes.Color);
		if (decoded.Empty())
		{
			decoded.Dispose();
			throw new InvalidOperationException("远程地图图片解码失败。");
		}
		return AddRoadMargin(decoded);
	}

	/// <summary>
	/// 缓存文件路径。
	/// </summary>
	public string GetAreaMapImagePath(string areaId, int resize)
	{
		return _environment.GetPathUnderWorkDir(".debug", "mys_large_map", areaId, $"{resize}.png");
	}

	private static Mat AddRoadMargin(Mat image)
	{
		using Mat mat = new Mat();
		Cv2.InRange(image, new Scalar(0.0, 0.0, 0.0), new Scalar(30.0, 30.0, 30.0), mat);
		Rect? rect = FindNonZeroBounds(mat);
		if (!rect.HasValue)
		{
			return image;
		}
		int x = rect.Value.X;
		int y = rect.Value.Y;
		int num = rect.Value.X + rect.Value.Width - 1;
		int num2 = rect.Value.Y + rect.Value.Height - 1;
		int num3 = Math.Max(0, 200 - y);
		int num4 = Math.Max(0, 200 - (image.Rows - 1 - num2));
		int num5 = Math.Max(0, 200 - x);
		int num6 = Math.Max(0, 200 - (image.Cols - 1 - num));
		if (num3 == 0 && num4 == 0 && num5 == 0 && num6 == 0)
		{
			return image;
		}
		Mat mat2 = new Mat();
		Cv2.CopyMakeBorder(image, mat2, num3, num4, num5, num6, BorderTypes.Constant, new Scalar(210.0, 210.0, 210.0));
		image.Dispose();
		return mat2;
	}

	private static Rect? FindNonZeroBounds(Mat mask)
	{
		int num = int.MaxValue;
		int num2 = int.MaxValue;
		int num3 = int.MinValue;
		int num4 = int.MinValue;
		for (int i = 0; i < mask.Rows; i++)
		{
			for (int j = 0; j < mask.Cols; j++)
			{
				if (mask.At<byte>(i, j) != 0)
				{
					num = Math.Min(num, j);
					num2 = Math.Min(num2, i);
					num3 = Math.Max(num3, j);
					num4 = Math.Max(num4, i);
				}
			}
		}
		return (num == int.MaxValue) ? ((Rect?)null) : new Rect?(new Rect(num, num2, num3 - num + 1, num4 - num2 + 1));
	}
}
