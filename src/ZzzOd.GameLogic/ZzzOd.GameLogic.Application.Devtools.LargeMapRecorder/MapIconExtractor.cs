using System;
using System.Collections.Generic;
using System.IO;
using OneDragon.Core.Runtime;
using OneDragon.Core.Template;
using OneDragon.Core.Utils;
using OpenCvSharp;

namespace ZzzOd.GameLogic.Application.Devtools.LargeMapRecorder;

/// <summary>
/// 地图图标模板裁剪工具。
/// </summary>
public sealed class MapIconExtractor
{
	private readonly OneDragonEnvironment _environment;

	/// <summary>
	/// 初始化裁剪工具。
	/// </summary>
	public MapIconExtractor(OneDragonEnvironment environment)
	{
		_environment = environment;
	}

	public MapIconExtractionResult Extract(Mat raw, IReadOnlyList<(Scalar Lower, Scalar Upper)> colorRanges)
	{
		ArgumentNullException.ThrowIfNull(raw, "raw");
		if (colorRanges.Count == 0)
		{
			throw new ArgumentException("颜色范围不能为空。", "colorRanges");
		}
		using Mat mat = new Mat(raw.Rows, raw.Cols, MatType.CV_8UC1, Scalar.Black);
		foreach (var (lowerb, upperb) in colorRanges)
		{
			using Mat mat2 = new Mat();
			Cv2.InRange(raw, RgbToBgr(lowerb), RgbToBgr(upperb), mat2);
			Cv2.BitwiseOr(mat, mat2, mat);
		}
		Rect? rect = FindNonZeroBounds(mat);
		if (!rect.HasValue)
		{
			throw new InvalidOperationException("没有找到符合颜色范围的图标像素。");
		}
		int x = rect.Value.X;
		int y = rect.Value.Y;
		int num = rect.Value.Width - 1;
		int num2 = rect.Value.Height - 1;
		Mat mat3 = new Mat(num2 + 2, num + 2, raw.Type(), Scalar.Black);
		Mat mat4 = new Mat(num2 + 2, num + 2, MatType.CV_8UC1, Scalar.Black);
		if (num > 0 && num2 > 0)
		{
			using Mat mat5 = new Mat(raw, new Rect(x, y, num, num2));
			using Mat mat6 = new Mat(mat, new Rect(x, y, num, num2));
			using Mat m = new Mat(mat3, new Rect(1, 1, num, num2));
			using Mat m2 = new Mat(mat4, new Rect(1, 1, num, num2));
			mat5.CopyTo(m);
			mat6.CopyTo(m2);
		}
		return new MapIconExtractionResult(mat3, mat4);
	}

	private static Scalar RgbToBgr(Scalar color) => new Scalar(color.Val2, color.Val1, color.Val0, color.Val3);

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

	public MapIconExtractionResult ExtractAndSave(string iconId, Mat raw, IReadOnlyList<(Scalar Lower, Scalar Upper)> colorRanges)
	{
		MapIconExtractionResult mapIconExtractionResult = Extract(raw, colorRanges);
		string resourcePath = _environment.GetResourcePath("assets", "template", "map", iconId);
		CvImageUtils.SaveImage(mapIconExtractionResult.Raw, Path.Combine(resourcePath, "raw.png"));
		CvImageUtils.SaveImage(mapIconExtractionResult.Mask, Path.Combine(resourcePath, "mask.png"));
		return mapIconExtractionResult;
	}

	public MapIconExtractionResult ExtractAndSave(string iconId, IReadOnlyList<(Scalar Lower, Scalar Upper)> colorRanges)
	{
		using TemplateLoader templateLoader = new TemplateLoader(_environment);
		TemplateInfo template = templateLoader.GetTemplate("map", iconId);
		if (template?.Raw == null)
		{
			throw new FileNotFoundException("地图图标模板 " + iconId + " 缺少 raw.png。", iconId);
		}
		return ExtractAndSave(iconId, template.Raw, colorRanges);
	}
}
