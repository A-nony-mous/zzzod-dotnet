using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Events;
using OneDragon.Core.Ocr;
using OneDragon.Core.Template;
using OneDragon.Core.Utils;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.GameData;

namespace ZzzOd.GameLogic.AutoBattle;

public sealed class TargetStateChecker : IAutoBattleTargetStateChecker
{
	private readonly ZContext? _ctx;

	public TargetStateChecker(ZContext? ctx = null)
	{
		_ctx = ctx;
	}

	public IReadOnlyList<TargetStateCheckResult> RunTask(object? screen, DetectionTask task)
	{
		if (screen is TargetStateCheckFrame frame)
		{
			return RunTask(frame, task);
		}
		if (screen is Mat screen2 && _ctx != null)
		{
			long timestamp = Stopwatch.GetTimestamp();
			try
			{
				TargetStateCheckFrame targetStateCheckFrame = BuildFrameFromMat(screen2, task);
				try
				{
					// 自动战斗要求 1 秒软超时：构建 frame 累计耗时超过该阈值时标记为超时，
					// 交由 InterpretResult 按各状态定义的语义降级为清除/未命中，而不是继续使用过期画面得出的结果。
					targetStateCheckFrame.IsTimedOut = Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds > 1000.0;
					return RunTask(targetStateCheckFrame, task);
				}
				finally
				{
					targetStateCheckFrame.MaskImage?.Dispose();
				}
			}
			finally
			{
				_ctx.EventBus.Publish(PerformanceMetricEventIds.Sample, new PerformanceMetricEventPayload(new PerformanceMetricSample("cv_pipeline_ms", Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds, "ms", DateTimeOffset.UtcNow, 20.0, new Dictionary<string, object> { ["pipeline"] = task.PipelineName })));
			}
		}
		return RunTask(new TargetStateCheckFrame
		{
			IsSuccess = false
		}, task);
	}

	public IReadOnlyList<TargetStateCheckResult> RunTask(TargetStateCheckFrame frame, DetectionTask task)
	{
		List<TargetStateCheckResult> list = new List<TargetStateCheckResult>();
		foreach (TargetStateDef stateDefinition in task.StateDefinitions)
		{
			list.Add(InterpretResult(frame, stateDefinition));
		}
		return list;
	}

	public TargetStateCheckResult InterpretResult(TargetStateCheckFrame? frame, TargetStateDef stateDef)
	{
		if (frame == null || !frame.IsSuccess)
		{
			return MissOrClear(stateDef);
		}
		if (frame.IsTimedOut)
		{
			return TargetStateCheckResult.Clear(stateDef.StateName);
		}
		TargetCheckWay checkWay = stateDef.CheckWay;
		if (1 == 0)
		{
		}
		TargetStateCheckResult result = checkWay switch
		{
			TargetCheckWay.ContourCountInRange => CheckContourCount(frame, stateDef), 
			TargetCheckWay.OcrResultAsNumber => CheckOcrAsNumber(frame, stateDef), 
			TargetCheckWay.OcrTextContains => CheckOcrTextContains(frame, stateDef), 
			TargetCheckWay.OcrTextSimilarity => CheckOcrTextSimilarity(frame, stateDef), 
			TargetCheckWay.MapContourLengthToPercent => CheckMapContourLengthToPercent(frame, stateDef), 
			_ => TargetStateCheckResult.Miss(stateDef.StateName), 
		};
		if (1 == 0)
		{
		}
		return result;
	}

	private static TargetStateCheckResult CheckContourCount(TargetStateCheckFrame frame, TargetStateDef stateDef)
	{
		int count = frame.Contours.Count;
		int num = GetInt(stateDef.CheckParams, "min_count", 0);
		int num2 = GetInt(stateDef.CheckParams, "max_count", 999);
		return (num <= count && count <= num2) ? TargetStateCheckResult.Hit(stateDef.StateName) : MissOrClear(stateDef);
	}

	private static TargetStateCheckResult CheckOcrAsNumber(TargetStateCheckFrame frame, TargetStateDef stateDef)
	{
		Match match = Regex.Match(frame.OcrText, "\\d+");
		if (!match.Success)
		{
			return MissOrClear(stateDef);
		}
		int result;
		return int.TryParse(match.Value, out result) ? TargetStateCheckResult.HitValue(stateDef.StateName, result) : MissOrClear(stateDef);
	}

	private static TargetStateCheckResult CheckOcrTextContains(TargetStateCheckFrame frame, TargetStateDef stateDef)
	{
		string ocrText = frame.OcrText;
		if (string.IsNullOrEmpty(ocrText))
		{
			return MissOrClear(stateDef);
		}
		IReadOnlyList<string> stringList = GetStringList(stateDef.CheckParams, "contains");
		IReadOnlyList<string> stringList2 = GetStringList(stateDef.CheckParams, "exclude");
		string text = GetString(stateDef.CheckParams, "mode", "any");
		bool flag = GetBool(stateDef.CheckParams, "case_sensitive", fallback: false);
		string processedText = (flag ? ocrText : ocrText.ToLowerInvariant());
		IEnumerable<string> enumerable;
		if (!flag)
		{
			enumerable = stringList.Select((string item) => item.ToLowerInvariant());
		}
		else
		{
			IEnumerable<string> enumerable2 = stringList;
			enumerable = enumerable2;
		}
		IEnumerable<string> source = enumerable;
		IEnumerable<string> enumerable3;
		if (!flag)
		{
			enumerable3 = stringList2.Select((string item) => item.ToLowerInvariant());
		}
		else
		{
			IEnumerable<string> enumerable2 = stringList2;
			enumerable3 = enumerable2;
		}
		IEnumerable<string> source2 = enumerable3;
		if (source2.Any((string ex) => processedText.Contains(ex, StringComparison.Ordinal)))
		{
			return MissOrClear(stateDef);
		}
		return ((text == "all") ? source.All((string item) => processedText.Contains(item, StringComparison.Ordinal)) : source.Any((string item) => processedText.Contains(item, StringComparison.Ordinal))) ? TargetStateCheckResult.Hit(stateDef.StateName) : MissOrClear(stateDef);
	}

	private static TargetStateCheckResult CheckOcrTextSimilarity(TargetStateCheckFrame frame, TargetStateDef stateDef)
	{
		if (string.IsNullOrEmpty(frame.OcrText))
		{
			return MissOrClear(stateDef);
		}
		IReadOnlyList<string> stringList = GetStringList(stateDef.CheckParams, "expected_texts");
		double threshold = GetDouble(stateDef.CheckParams, "threshold", 0.5);
		string item = StringUtils.FindBestMatchBySimilarity(frame.OcrText, stringList, threshold).Match;
		return (item != null) ? TargetStateCheckResult.Hit(stateDef.StateName) : MissOrClear(stateDef);
	}

	private static TargetStateCheckResult CheckMapContourLengthToPercent(TargetStateCheckFrame frame, TargetStateDef stateDef)
	{
		if (frame.Contours.Count == 0 || frame.MaskImage == null || frame.MaskImage.Empty() || frame.MaskImage.Width == 0)
		{
			return MissOrClear(stateDef);
		}
		int num = GetInt(stateDef.CheckParams, "contour_index", 0);
		if (frame.Contours.Count <= num)
		{
			return MissOrClear(stateDef);
		}
		double num2 = (double)Math.Min(Cv2.BoundingRect(frame.Contours[num]).Width, frame.MaskImage.Width) / (double)frame.MaskImage.Width;
		return TargetStateCheckResult.HitValue(stateDef.StateName, (int)(num2 * 100.0));
	}

	private static TargetStateCheckResult MissOrClear(TargetStateDef stateDef)
	{
		return stateDef.ClearOnMiss ? TargetStateCheckResult.Clear(stateDef.StateName) : TargetStateCheckResult.Miss(stateDef.StateName);
	}

	private TargetStateCheckFrame BuildFrameFromMat(Mat screen, DetectionTask task)
	{
		string pipelineName = task.PipelineName;
		if (1 == 0)
		{
		}
		TargetStateCheckFrame result = pipelineName switch
		{
			"lock-far" => BuildLockFarFrame(screen), 
			"ocr-abnormal" => BuildOcrAbnormalFrame(screen), 
			"boss_stun_line" => BuildBossStunLineFrame(screen), 
			_ => new TargetStateCheckFrame
			{
				IsSuccess = false
			}, 
		};
		if (1 == 0)
		{
		}
		return result;
	}

	private TargetStateCheckFrame BuildLockFarFrame(Mat screen)
	{
		Mat mat = CropByTemplate(screen, "target_lock");
		if (mat == null)
		{
			return new TargetStateCheckFrame
			{
				IsSuccess = false
			};
		}
		using Mat image = mat;
		using Mat source = FilterByHsv(image, new int[3] { 20, 200, 255 }, new int[3] { 5, 55, 100 });
		using Mat source2 = Morph(source, erode: true, 3, 1);
		using Mat mat2 = Morph(source2, erode: false, 3, 1);
		Cv2.FindContours(mat2, out OpenCvSharp.Point[][] contours, out HierarchyIndex[] _, RetrievalModes.List, ContourApproximationModes.ApproxSimple);
		OpenCvSharp.Point[][] contours2 = contours.Where(delegate(OpenCvSharp.Point[] contour)
		{
			double num = Cv2.ContourArea(contour);
			if ((num < 100.0 || num > 1500.0) ? true : false)
			{
				return false;
			}
			OpenCvSharp.Rect rect = Cv2.BoundingRect(contour);
			if (rect.Height == 0)
			{
				return false;
			}
			double num2 = (double)rect.Width / (double)rect.Height;
			return num2 >= 0.8 && num2 <= 1.1;
		}).ToArray();
		contours2 = FilterByCentroidDistance(contours2, 1.0);
		return new TargetStateCheckFrame
		{
			Contours = contours2
		};
	}

	private TargetStateCheckFrame BuildOcrAbnormalFrame(Mat screen)
	{
		Mat mat = CropByTemplate(screen, "locked_target_info", out OneDragon.Core.Abstractions.Geometry.Rect cropRect);
		if (mat == null)
		{
			return new TargetStateCheckFrame
			{
				IsSuccess = false
			};
		}
		using Mat mat2 = mat;
		long timestamp = Stopwatch.GetTimestamp();
		_ctx.Logger.Information("自动战斗目标OCR开始: Pipeline=ocr-abnormal, Width={Width}, Height={Height}", mat2.Width, mat2.Height);
		string text = string.Concat(from result in _ctx.OcrService.GetOcrResultListForCrop(
			mat2,
			screen.Width,
			screen.Height,
			cropRect.X1,
			cropRect.Y1)
			select result.Text);
		_ctx.Logger.Information("自动战斗目标OCR结束: Pipeline=ocr-abnormal, ElapsedMilliseconds={ElapsedMilliseconds:F2}, Text={Text}", Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds, text);
		return new TargetStateCheckFrame
		{
			OcrText = text
		};
	}

	private TargetStateCheckFrame BuildBossStunLineFrame(Mat screen)
	{
		Mat mat = CropByTemplate(screen, "boss_stun_line");
		if (mat == null)
		{
			return new TargetStateCheckFrame
			{
				IsSuccess = false
			};
		}
		using Mat image = mat;
		Mat mat2 = FilterByHsv(image, new int[3] { 26, 255, 255 }, new int[3] { 5, 5, 5 });
		Cv2.FindContours(mat2, out OpenCvSharp.Point[][] contours, out HierarchyIndex[] _, RetrievalModes.CComp, ContourApproximationModes.ApproxSimple);
		return new TargetStateCheckFrame
		{
			Contours = contours,
			MaskImage = mat2
		};
	}

	private Mat? CropByTemplate(Mat screen, string templateId)
	{
		return CropByTemplate(screen, templateId, out _);
	}

	private Mat? CropByTemplate(Mat screen, string templateId, out OneDragon.Core.Abstractions.Geometry.Rect cropRect)
	{
		cropRect = default(OneDragon.Core.Abstractions.Geometry.Rect);
		TemplateInfo template = _ctx.TemplateLoader.GetTemplate("target_state", templateId);
		if (template != null)
		{
			OneDragon.Core.Abstractions.Geometry.Rect? templateRectByPoint = template.GetTemplateRectByPoint();
			OneDragon.Core.Abstractions.Geometry.Rect valueOrDefault = default(OneDragon.Core.Abstractions.Geometry.Rect);
			int num;
			if (templateRectByPoint.HasValue)
			{
				valueOrDefault = templateRectByPoint.GetValueOrDefault();
				num = 1;
			}
			else
			{
				num = 0;
			}
			if (num != 0)
			{
				if (valueOrDefault.X1 < 0 || valueOrDefault.Y1 < 0 || valueOrDefault.X2 > screen.Width || valueOrDefault.Y2 > screen.Height || valueOrDefault.Width <= 0 || valueOrDefault.Height <= 0)
				{
					return null;
				}
				cropRect = valueOrDefault;
				return CvImageUtils.Crop(screen, valueOrDefault);
			}
		}
		return null;
	}

	private static Mat FilterByHsv(Mat image, IReadOnlyList<int> hsvColor, IReadOnlyList<int> hsvDiff)
	{
		using Mat mat = new Mat();
		Cv2.CvtColor(image, mat, ColorConversionCodes.BGR2HSV);
		Mat mat2 = new Mat(image.Rows, image.Cols, MatType.CV_8UC1, Scalar.Black);
		for (int i = 0; i < mat.Rows; i++)
		{
			for (int j = 0; j < mat.Cols; j++)
			{
				Vec3b vec3b = mat.At<Vec3b>(i, j);
				if (HueInCircularRange(vec3b.Item0, hsvColor[0], hsvDiff[0]) && InRange((int)vec3b.Item1, hsvColor[1] - hsvDiff[1], hsvColor[1] + hsvDiff[1]) && InRange((int)vec3b.Item2, hsvColor[2] - hsvDiff[2], hsvColor[2] + hsvDiff[2]))
				{
					mat2.Set(i, j, byte.MaxValue);
				}
			}
		}
		return mat2;
	}

	private static Mat Morph(Mat source, bool erode, int kernelSize, int iterations)
	{
		Mat mat = new Mat();
		using Mat mat2 = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(kernelSize, kernelSize));
		if (erode)
		{
			InputArray src = source;
			OutputArray dst = mat;
			InputArray element = mat2;
			int iterations2 = iterations;
			Cv2.Erode(src, dst, element, null, iterations2);
		}
		else
		{
			InputArray src2 = source;
			OutputArray dst2 = mat;
			InputArray element2 = mat2;
			int iterations2 = iterations;
			Cv2.Dilate(src2, dst2, element2, null, iterations2);
		}
		return mat;
	}

	private static OpenCvSharp.Point[][] FilterByCentroidDistance(IReadOnlyList<OpenCvSharp.Point[]> contours, double maxDistance)
	{
		if (contours.Count <= 1)
		{
			return contours.ToArray();
		}
		List<(OpenCvSharp.Point[] Contour, Point2d Center)> centers = new List<(OpenCvSharp.Point[], Point2d)>();
		foreach (OpenCvSharp.Point[] contour in contours)
		{
			Moments moments = Cv2.Moments(contour);
			if (!(Math.Abs(moments.M00) < double.Epsilon))
			{
				centers.Add((contour, new Point2d(moments.M10 / moments.M00, moments.M01 / moments.M00)));
			}
		}
		return (from item in centers
			where centers.Any<(OpenCvSharp.Point[], Point2d)>(((OpenCvSharp.Point[] Contour, Point2d Center) other) => item.Contour != other.Contour && Distance(item.Center, other.Center) <= maxDistance)
			select item.Contour).ToArray();
	}

	private static double Distance(Point2d left, Point2d right)
	{
		double num = left.X - right.X;
		double num2 = left.Y - right.Y;
		return Math.Sqrt(num * num + num2 * num2);
	}

	private static bool InRange(double value, double low, double high)
	{
		return value >= Math.Max(0.0, low) && value <= Math.Min(255.0, high);
	}

	private static bool HueInCircularRange(byte hue, int center, int diff)
	{
		int num = (center % 180 + 180) % 180;
		int num2 = Math.Abs(hue - num);
		num2 = Math.Min(num2, 180 - num2);
		return num2 <= diff;
	}

	private static int GetInt(IReadOnlyDictionary<string, object> values, string key, int fallback)
	{
		if (!values.TryGetValue(key, out object value))
		{
			return fallback;
		}
		if (1 == 0)
		{
		}
		int result = (int)((value is int num) ? num : ((value is long num2) ? num2 : ((value is double num3) ? ((int)num3) : ((!(value is float num4)) ? fallback : ((int)num4)))));
		if (1 == 0)
		{
		}
		return result;
	}

	private static double GetDouble(IReadOnlyDictionary<string, object> values, string key, double fallback)
	{
		if (!values.TryGetValue(key, out object value))
		{
			return fallback;
		}
		if (1 == 0)
		{
		}
		double result = ((value is double num) ? num : ((value is float num2) ? ((double)num2) : ((value is int num3) ? ((double)num3) : ((!(value is long num4)) ? fallback : ((double)num4)))));
		if (1 == 0)
		{
		}
		return result;
	}

	private static string GetString(IReadOnlyDictionary<string, object> values, string key, string fallback)
	{
		object value;
		return (values.TryGetValue(key, out value) && value is string text) ? text : fallback;
	}

	private static bool GetBool(IReadOnlyDictionary<string, object> values, string key, bool fallback)
	{
		object value;
		return (values.TryGetValue(key, out value) && value is bool flag) ? flag : fallback;
	}

	private static IReadOnlyList<string> GetStringList(IReadOnlyDictionary<string, object> values, string key)
	{
		if (!values.TryGetValue(key, out object value))
		{
			return Array.Empty<string>();
		}
		if (1 == 0)
		{
		}
		string[] result = ((value is string text) ? new string[1] { text } : ((value is string[] array) ? array : ((value is IEnumerable<string> source) ? source.ToArray() : ((!(value is object[] source2)) ? Array.Empty<string>() : source2.OfType<string>().ToArray()))));
		if (1 == 0)
		{
		}
		return result;
	}
}
