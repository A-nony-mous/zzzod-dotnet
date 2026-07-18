using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Ocr;
using OneDragon.Core.Runtime;
using OneDragon.Core.Screen;
using OneDragon.Core.Template;
using OpenCvSharp;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.AppHost.Devtools;

public sealed class ZzzImageAnalysisService : IZzzImageAnalysisService
{
	private sealed class PipelineStepDocument
	{
		public string? Step { get; set; }

		public Dictionary<string, object?>? Params { get; set; }
	}

	private static readonly IDeserializer Deserializer = new DeserializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance).IgnoreUnmatchedProperties().Build();

	private static readonly ISerializer Serializer = new SerializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance).DisableAliases().Build();

	private static readonly IReadOnlyList<ImageAnalysisStepDefinition> Definitions = CreateDefinitions();

	private readonly ZzzRunRoot _runRoot;

	private readonly ZzzRuntimeManager _runtime;

	private string PipelineDirectory => Path.Combine(_runRoot.Path, "assets", "image_analysis_pipelines");

	public ZzzImageAnalysisService(ZzzRunRoot runRoot, ZzzRuntimeManager runtime)
	{
		_runRoot = runRoot;
		_runtime = runtime;
		Directory.CreateDirectory(PipelineDirectory);
	}

	public IReadOnlyList<string> GetPipelineNames()
	{
		return (from name in Directory.EnumerateFiles(PipelineDirectory, "*.yml", SearchOption.TopDirectoryOnly).Select(Path.GetFileNameWithoutExtension)
			where !string.IsNullOrWhiteSpace(name)
			select name).Cast<string>().OrderBy<string, string>((string name) => name, StringComparer.Ordinal).ToArray();
	}

	public IReadOnlyList<ImageAnalysisStepDefinition> GetAvailableSteps()
	{
		return Definitions;
	}

	public IReadOnlyList<string> GetTemplateNames()
	{
		using TemplateLoader templateLoader = new TemplateLoader(new OneDragonEnvironment(_runRoot.Path));
		return (from template in templateLoader.GetAllTemplateInfoFromDisk()
			select template.SubDir + "/" + template.TemplateId).OrderBy<string, string>((string name) => name, StringComparer.Ordinal).ToArray();
	}

	public IReadOnlyList<string> GetScreenNames()
	{
		ScreenContext screenContext = CreateScreenContext();
		return screenContext.ScreenInfoList.Select((ScreenInfo screen) => screen.ScreenName).ToArray();
	}

	public IReadOnlyList<string> GetAreaNames(string screenName)
	{
		ScreenContext screenContext = CreateScreenContext();
		ScreenInfo value;
		return screenContext.ScreenInfoMap.TryGetValue(screenName, out value) ? value.AreaList.Select((ScreenArea area) => area.AreaName).ToArray() : Array.Empty<string>();
	}

	public ImageAnalysisPipeline LoadPipeline(string name)
	{
		string pipelinePath = GetPipelinePath(name);
		if (!File.Exists(pipelinePath))
		{
			throw new FileNotFoundException("流水线不存在：" + name, pipelinePath);
		}
		List<PipelineStepDocument> list = Deserializer.Deserialize<List<PipelineStepDocument>>(File.ReadAllText(pipelinePath));
		return new ImageAnalysisPipeline((list ?? new List<PipelineStepDocument>()).Select(ToStep).ToArray());
	}

	public void SavePipeline(string name, ImageAnalysisPipeline pipeline)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name, "name");
		ArgumentNullException.ThrowIfNull(pipeline, "pipeline");
		File.WriteAllText(GetPipelinePath(name), Serializer.Serialize(pipeline.Steps.Select(ToDocument).ToArray()));
	}

	public void RenamePipeline(string oldName, string newName)
	{
		string pipelinePath = GetPipelinePath(oldName);
		if (!File.Exists(pipelinePath))
		{
			throw new FileNotFoundException("流水线不存在：" + oldName, pipelinePath);
		}
		File.Move(pipelinePath, GetPipelinePath(newName), overwrite: false);
	}

	public void DeletePipeline(string name)
	{
		string pipelinePath = GetPipelinePath(name);
		if (File.Exists(pipelinePath))
		{
			File.Delete(pipelinePath);
		}
	}

	public ImageAnalysisExecutionResult Execute(ImageAnalysisPipeline pipeline, byte[] imageBytes)
	{
		ArgumentNullException.ThrowIfNull(pipeline, "pipeline");
		using Mat mat = Cv2.ImDecode(imageBytes, ImreadModes.Color);
		if (mat.Empty())
		{
			throw new InvalidDataException("图片解码失败。");
		}
		using Mat mat2 = mat.Clone();
		Mat mask = null;
		List<OpenCvSharp.Point[]> contours = new List<OpenCvSharp.Point[]>();
		List<string> list = new List<string>();
		List<ImageAnalysisStepTiming> list2 = new List<ImageAnalysisStepTiming>();
		int offsetX = 0;
		int offsetY = 0;
		Stopwatch stopwatch = Stopwatch.StartNew();
		ZContext runtime = _runtime.EnsureContext();
		try
		{
			foreach (ImageAnalysisStep step in pipeline.Steps)
			{
				Stopwatch stopwatch2 = Stopwatch.StartNew();
				ExecuteStep(step, runtime, mat2, ref mask, ref contours, list, ref offsetX, ref offsetY);
				stopwatch2.Stop();
				list2.Add(new ImageAnalysisStepTiming(step.Name, stopwatch2.Elapsed.TotalMilliseconds));
			}
			stopwatch.Stop();
			return new ImageAnalysisExecutionResult(mat2.ImEncode(), mask?.ImEncode(), list, list2, stopwatch.Elapsed.TotalMilliseconds);
		}
		finally
		{
			mask?.Dispose();
		}
	}

	public ImageAnalysisColorChannels GetColorChannels(byte[] imageBytes)
	{
		using Mat mat = Cv2.ImDecode(imageBytes, ImreadModes.Color);
		if (mat.Empty())
		{
			throw new InvalidDataException("图片解码失败。");
		}
		return new ImageAnalysisColorChannels(new ImageAnalysisColorSpace[4]
		{
			CreateColorSpace("RGB", mat, ColorConversionCodes.BGR2RGB, new string[3] { "R", "G", "B" }),
			CreateColorSpace("HSV", mat, ColorConversionCodes.BGR2HSV, new string[3] { "H", "S", "V" }),
			CreateColorSpace("YUV", mat, ColorConversionCodes.BGR2YUV, new string[3] { "Y", "U", "V" }),
			CreateColorSpace("LAB", mat, ColorConversionCodes.BGR2Lab, new string[3] { "L", "A", "B" })
		});
	}

	private static void ExecuteStep(ImageAnalysisStep step, ZContext runtime, Mat display, ref Mat? mask, ref List<OpenCvSharp.Point[]> contours, List<string> results, ref int offsetX, ref int offsetY)
	{
		switch (step.Name)
		{
		case "按区域裁剪":
		{
			string text2 = String(step, "screen_name");
			string text3 = String(step, "area_name");
			ScreenArea area = runtime.ScreenContext.GetArea(text2, text3);
			if (area == null)
			{
				throw new InvalidOperationException("未找到画面区域：" + text2 + "/" + text3);
			}
			CropInPlace(display, area.X1, area.Y1, area.Width, area.Height);
			offsetX += area.X1;
			offsetY += area.Y1;
			Replace(ref mask, null);
			DisposeContours(ref contours);
			results.Add($"已执行 按区域裁剪，区域：({area.X1}, {area.Y1}, {area.X2}, {area.Y2})，当前总偏移：({offsetX}, {offsetY})");
			break;
		}
		case "按模板裁剪":
		{
			(string, string) tuple = SplitTemplate(String(step, "template_name"));
			string item = tuple.Item1;
			string item2 = tuple.Item2;
			TemplateInfo template = runtime.TemplateLoader.GetTemplate(item, item2);
			if (template == null)
			{
				throw new InvalidOperationException("无法加载模板：" + item + "/" + item2);
			}
			OneDragon.Core.Abstractions.Geometry.Rect value = template.GetTemplateRectByPoint() ?? throw new InvalidOperationException("模板缺少裁剪区域：" + item + "/" + item2);
			CropInPlace(display, value.X1, value.Y1, value.Width, value.Height);
			offsetX += value.X1;
			offsetY += value.Y1;
			Replace(ref mask, null);
			DisposeContours(ref contours);
			results.Add($"已执行 按模板裁剪，区域：{value}，当前总偏移：({offsetX}, {offsetY})");
			break;
		}
		case "环形裁剪":
		{
			int x = display.Width / 2 + Int(step, "center_x_offset");
			int y = display.Height / 2 + Int(step, "center_y_offset");
			int radius = Math.Max(1, Math.Min(display.Width, display.Height) / 2 - Int(step, "outer_radius_reduction"));
			int num = Math.Max(0, Int(step, "inner_radius"));
			using Mat mat4 = (Mat)Mat.Zeros(display.Size(), MatType.CV_8UC1);
			Cv2.Circle(mat4, new OpenCvSharp.Point(x, y), radius, Scalar.White, -1);
			if (num > 0)
			{
				Cv2.Circle(mat4, new OpenCvSharp.Point(x, y), num, Scalar.Black, -1);
			}
			int num2 = Int(step, "notch_radius");
			if (num2 > 0)
			{
				Cv2.Circle(mat4, new OpenCvSharp.Point(Int(step, "notch_x"), Int(step, "notch_y")), num2, Scalar.Black, -1);
			}
			display.SetTo(Scalar.Black, ~mat4);
			Replace(ref mask, mat4.Clone());
			results.Add("已执行环形裁剪");
			break;
		}
		case "灰度化":
		{
			using Mat mat2 = new Mat();
			Cv2.CvtColor(display, mat2, ColorConversionCodes.BGR2GRAY);
			Replace(ref mask, mat2.Clone());
			Cv2.CvtColor(mat2, display, ColorConversionCodes.GRAY2BGR);
			results.Add("图像已转换为灰度");
			break;
		}
		case "直方图均衡化":
		{
			EnsureMask(mask, step.Name);
			using Mat mat = new Mat();
			Cv2.EqualizeHist(mask, mat);
			Replace(ref mask, mat.Clone());
			Cv2.CvtColor(mat, display, ColorConversionCodes.GRAY2BGR);
			results.Add("已应用直方图均衡化");
			break;
		}
		case "二值化":
		{
			EnsureMask(mask, step.Name);
			string text = String(step, "method", "OTSU");
			Mat mat3 = new Mat();
			if (text == "OTSU")
			{
				Cv2.Threshold(mask, mat3, 0.0, 255.0, ThresholdTypes.Otsu);
			}
			else if (text == "ADAPTIVE_GAUSSIAN" || text == "ADAPTIVE_MEAN")
			{
				int blockSize = Math.Max(3, Int(step, "adaptive_block_size", 11) | 1);
				Cv2.AdaptiveThreshold(mask, mat3, 255.0, (text == "ADAPTIVE_GAUSSIAN") ? AdaptiveThresholdTypes.GaussianC : AdaptiveThresholdTypes.MeanC, ThresholdTypes.Binary, blockSize, Int(step, "adaptive_c", 2));
			}
			else
			{
				Cv2.Threshold(mask, mat3, Int(step, "threshold_value", 127), 255.0, ThresholdTypes.Binary);
			}
			Replace(ref mask, mat3);
			Cv2.CvtColor(mask, display, ColorConversionCodes.GRAY2BGR);
			results.Add("已应用二值化（方法：" + text + "）");
			break;
		}
		case "RGB 范围过滤":
		case "HSV 范围过滤":
		{
			bool flag = step.Name.StartsWith("HSV", StringComparison.Ordinal);
			string text6 = (flag ? "hsv" : "rgb");
			int[] array = Tuple(step, text6 + "_color");
			int[] array2 = Tuple(step, text6 + "_diff");
			using Mat mat8 = new Mat();
			Cv2.CvtColor(display, mat8, flag ? ColorConversionCodes.BGR2HSV : ColorConversionCodes.BGR2RGB);
			Scalar lowerb = new Scalar(Math.Max(0, array[0] - array2[0]), Math.Max(0, array[1] - array2[1]), Math.Max(0, array[2] - array2[2]));
			Scalar upperb = new Scalar(Math.Min(flag ? 179 : 255, array[0] + array2[0]), Math.Min(255, array[1] + array2[1]), Math.Min(255, array[2] + array2[2]));
			Mat mat9 = new Mat();
			Cv2.InRange(mat8, lowerb, upperb, mat9);
			Replace(ref mask, mat9);
			using Mat mat10 = new Mat();
			Cv2.BitwiseAnd(display, display, mat10, mask);
			mat10.CopyTo(display);
			results.Add("已应用" + text6.ToUpperInvariant() + "范围过滤");
			break;
		}
		case "腐蚀":
		case "膨胀":
		case "形态学":
		{
			EnsureMask(mask, step.Name);
			int num3 = Math.Max(1, Int(step, "kernel_size", 3));
			using Mat mat5 = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(num3, num3));
			Mat mat6 = new Mat();
			if (step.Name == "腐蚀")
			{
				InputArray src = mask;
				OutputArray dst = mat6;
				InputArray element = mat5;
				int iterations = Math.Max(1, Int(step, "iterations", 1));
				Cv2.Erode(src, dst, element, null, iterations);
			}
			else if (step.Name == "膨胀")
			{
				InputArray src2 = mask;
				OutputArray dst2 = mat6;
				InputArray element2 = mat5;
				int iterations = Math.Max(1, Int(step, "iterations", 1));
				Cv2.Dilate(src2, dst2, element2, null, iterations);
			}
			else
			{
				InputArray src3 = mask;
				OutputArray dst3 = mat6;
				string text5 = String(step, "op", "开运算");
				if (1 == 0)
				{
				}
				MorphTypes op = text5 switch
				{
					"闭运算" => MorphTypes.Close, 
					"梯度" => MorphTypes.Gradient, 
					"顶帽" => MorphTypes.TopHat, 
					"黑帽" => MorphTypes.BlackHat, 
					_ => MorphTypes.Open, 
				};
				if (1 == 0)
				{
				}
				Cv2.MorphologyEx(src3, dst3, op, mat5);
			}
			Replace(ref mask, mat6);
			using Mat mat7 = new Mat();
			Cv2.BitwiseAnd(display, display, mat7, mask);
			mat7.CopyTo(display);
			results.Add("已执行" + step.Name);
			break;
		}
		case "查找轮廓":
		{
			EnsureMask(mask, step.Name);
			DisposeContours(ref contours);
			string text4 = String(step, "mode", "EXTERNAL");
			if (1 == 0)
			{
			}
			RetrievalModes retrievalModes = text4 switch
			{
				"LIST" => RetrievalModes.List, 
				"CCOMP" => RetrievalModes.CComp, 
				"TREE" => RetrievalModes.Tree, 
				_ => RetrievalModes.External, 
			};
			if (1 == 0)
			{
			}
			RetrievalModes mode = retrievalModes;
			ContourApproximationModes method = ((String(step, "method", "SIMPLE") == "NONE") ? ContourApproximationModes.ApproxNone : ContourApproximationModes.ApproxSimple);
			Cv2.FindContours(mask, out OpenCvSharp.Point[][] contours2, out HierarchyIndex[] _, mode, method);
			contours = contours2.ToList();
			if (Bool(step, "draw_contours", fallback: true))
			{
				Cv2.DrawContours(display, contours2, -1, new Scalar(0.0, 255.0, 0.0), 2);
			}
			results.Add($"找到 {contours.Count} 个轮廓");
			break;
		}
		case "按面积过滤":
			FilterContours(step, display, ref contours, results, (OpenCvSharp.Point[] c) => Cv2.ContourArea(c), "面积", "min_area", "max_area");
			break;
		case "按周长过滤":
			FilterContours(step, display, ref contours, results, (OpenCvSharp.Point[] c) => Cv2.ArcLength(c, Bool(step, "closed", fallback: true)), "周长", "min_length", "max_length");
			break;
		case "按半径过滤":
			FilterContours(step, display, ref contours, results, delegate(OpenCvSharp.Point[] c)
			{
				Cv2.MinEnclosingCircle(c, out var _, out var radius2);
				return radius2;
			}, "半径", "min_radius", "max_radius");
			break;
		case "按长宽比过滤":
			FilterContours(step, display, ref contours, results, delegate(OpenCvSharp.Point[] c)
			{
				OpenCvSharp.Rect rect = Cv2.BoundingRect(c);
				return (rect.Height == 0) ? 0.0 : ((double)rect.Width / (double)rect.Height);
			}, "长宽比", "min_ratio", "max_ratio");
			break;
		case "按质心距离过滤":
			FilterByCentroidDistance(step, display, ref contours, results);
			break;
		case "轮廓属性分析":
			AnalyzeContours(step, display, contours, results, offsetX, offsetY);
			break;
		case "OCR识别":
		{
			IReadOnlyList<OcrMatchResult> ocrResultList = runtime.OcrService.GetOcrResultListWithoutOverlayVision(display);
			results.Add($"OCR 识别到 {ocrResultList.Count} 个文本项：");
			{
				foreach (OcrMatchResult item3 in ocrResultList)
				{
					results.Add($"  - '{item3.Text}'（置信度：{item3.Confidence:F2}）at {item3.Rect}");
					if (Bool(step, "draw_text_box", fallback: true))
					{
						Cv2.Rectangle(display, new OpenCvSharp.Rect(item3.Rect.X1, item3.Rect.Y1, item3.Rect.Width, item3.Rect.Height), new Scalar(255.0, 0.0, 255.0), 2);
					}
				}
				break;
			}
		}
		case "模板匹配":
			ExecuteTemplateMatch(step, runtime, display, results);
			break;
		case "形状匹配":
			throw new InvalidOperationException("形状匹配需要 assets/image_analysis_templates 中的真实轮廓模板。");
		default:
			throw new InvalidOperationException("未知流水线步骤：" + step.Name);
		}
	}

	private static void FilterContours(ImageAnalysisStep step, Mat display, ref List<OpenCvSharp.Point[]> contours, List<string> results, Func<OpenCvSharp.Point[], double> selector, string label, string minKey, string maxKey)
	{
		double min = Double(step, minKey);
		double max = Double(step, maxKey, double.MaxValue);
		List<OpenCvSharp.Point[]> list = contours.Where(delegate(OpenCvSharp.Point[] c)
		{
			double num = selector(c);
			return num >= min && num <= max;
		}).ToList();
		contours = list;
		if (Bool(step, "draw_contours", fallback: true))
		{
			Cv2.DrawContours(display, contours, -1, new Scalar(0.0, 255.0, 0.0), 2);
		}
		results.Add($"按{label}过滤后剩余 {contours.Count} 个轮廓");
	}

	private static void FilterByCentroidDistance(ImageAnalysisStep step, Mat display, ref List<OpenCvSharp.Point[]> contours, List<string> results)
	{
		if (contours.Count < 2)
		{
			return;
		}
		double maxDistance = Double(step, "max_distance", 50.0);
		List<Point2d> centers = contours.Select(delegate(OpenCvSharp.Point[] c)
		{
			Moments moments = Cv2.Moments(c);
			return new Point2d(moments.M10 / Math.Max(moments.M00, 1.0), moments.M01 / Math.Max(moments.M00, 1.0));
		}).ToList();
		List<OpenCvSharp.Point[]> list = contours.Where((OpenCvSharp.Point[] _, int i) => centers.Where((Point2d point2d, int j) => i != j).Any((Point2d other) => centers[i].DistanceTo(other) <= maxDistance)).ToList();
		contours = list;
		if (Bool(step, "draw_contours", fallback: true))
		{
			Cv2.DrawContours(display, contours, -1, new Scalar(0.0, 255.0, 0.0), 2);
		}
		results.Add($"按质心距离过滤后剩余 {contours.Count} 个轮廓");
	}

	private static void AnalyzeContours(ImageAnalysisStep step, Mat display, List<OpenCvSharp.Point[]> contours, List<string> results, int offsetX, int offsetY)
	{
		foreach (var item3 in contours.Select((OpenCvSharp.Point[] value, int index) => (value: value, index: index)))
		{
			OpenCvSharp.Point[] item = item3.value;
			int item2 = item3.index;
			OpenCvSharp.Rect rect = Cv2.BoundingRect(item);
			results.Add($"轮廓 {item2}：({rect.X + offsetX}, {rect.Y + offsetY}, {rect.Width}, {rect.Height})，面积 {Cv2.ContourArea(item):F2}");
			if (Bool(step, "show_bounding_box", fallback: true))
			{
				Cv2.Rectangle(display, rect, new Scalar(0.0, 255.0, 0.0), 2);
			}
		}
	}

	private static void ExecuteTemplateMatch(ImageAnalysisStep step, ZContext runtime, Mat display, List<string> results)
	{
		(string, string) tuple = SplitTemplate(String(step, "template_name"));
		string item = tuple.Item1;
		string item2 = tuple.Item2;
		TemplateInfo template = runtime.TemplateLoader.GetTemplate(item, item2);
		if (template?.Raw == null)
		{
			throw new InvalidOperationException("无法加载图像模板：" + item + "/" + item2);
		}
		using Mat mat = new Mat();
		Cv2.MatchTemplate(display, template.Raw, mat, TemplateMatchModes.CCoeffNormed);
		Cv2.MinMaxLoc(mat, out var _, out var maxVal, out var _, out var maxLoc);
		double num = Double(step, "threshold", 0.8);
		if (maxVal >= num)
		{
			Cv2.Rectangle(display, new OpenCvSharp.Rect(maxLoc.X, maxLoc.Y, template.Raw.Width, template.Raw.Height), new Scalar(0.0, 255.0, 255.0), 2);
			results.Add($"找到匹配，置信度 {maxVal:F4} at {maxLoc}");
		}
		else
		{
			results.Add($"未找到足够置信度的匹配（最高 {maxVal:F4}）");
		}
	}

	private static ImageAnalysisColorSpace CreateColorSpace(string name, Mat source, ColorConversionCodes conversion, string[] names)
	{
		using Mat mat = new Mat();
		Cv2.CvtColor(source, mat, conversion);
		Mat[] array = Cv2.Split(mat);
		try
		{
			return new ImageAnalysisColorSpace(name, array.Select((Mat channel, int i) => new ImageAnalysisChannel(names[i], channel.ImEncode())).ToArray());
		}
		finally
		{
			Mat[] array2 = array;
			foreach (Mat mat2 in array2)
			{
				mat2.Dispose();
			}
		}
	}

	private ScreenContext CreateScreenContext()
	{
		ScreenContext screenContext = new ScreenContext(new OneDragonEnvironment(_runRoot.Path));
		screenContext.Reload();
		return screenContext;
	}

	private string GetPipelinePath(string name)
	{
		string fileName = Path.GetFileName(name.Trim());
		if (string.IsNullOrWhiteSpace(fileName) || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
		{
			throw new ArgumentException("流水线名称无效。", "name");
		}
		return Path.Combine(PipelineDirectory, fileName + ".yml");
	}

	private static ImageAnalysisStep ToStep(PipelineStepDocument document)
	{
		return new ImageAnalysisStep(document.Step ?? string.Empty, document.Params ?? new Dictionary<string, object>());
	}

	private static PipelineStepDocument ToDocument(ImageAnalysisStep step)
	{
		return new PipelineStepDocument
		{
			Step = step.Name,
			Params = step.Parameters
		};
	}

	private static void CropInPlace(Mat image, int x, int y, int width, int height)
	{
		OpenCvSharp.Rect roi = new OpenCvSharp.Rect(Math.Clamp(x, 0, image.Width), Math.Clamp(y, 0, image.Height), Math.Clamp(width, 1, image.Width - Math.Clamp(x, 0, image.Width)), Math.Clamp(height, 1, image.Height - Math.Clamp(y, 0, image.Height)));
		using Mat mat = new Mat(image, roi);
		using Mat mat2 = mat.Clone();
		mat2.CopyTo(image);
	}

	private static void Replace(ref Mat? target, Mat? replacement)
	{
		target?.Dispose();
		target = replacement;
	}

	private static void DisposeContours(ref List<OpenCvSharp.Point[]> contours)
	{
		contours = new List<OpenCvSharp.Point[]>();
	}

	private static void EnsureMask(Mat? mask, string step)
	{
		if (mask == null)
		{
			throw new InvalidOperationException(step + " 需要前置掩码步骤。");
		}
	}

	private static (string, string) SplitTemplate(string value)
	{
		string[] array = value.Split('/', 2);
		if (array.Length != 2)
		{
			throw new InvalidOperationException("模板名称无效：" + value);
		}
		return (array[0], array[1]);
	}

	private static string String(ImageAnalysisStep step, string key, string fallback = "")
	{
		object value;
		return step.Parameters.TryGetValue(key, out value) ? (Convert.ToString(value) ?? fallback) : fallback;
	}

	private static int Int(ImageAnalysisStep step, string key, int fallback = 0)
	{
		object value;
		return step.Parameters.TryGetValue(key, out value) ? Convert.ToInt32(value) : fallback;
	}

	private static double Double(ImageAnalysisStep step, string key, double fallback = 0.0)
	{
		object value;
		return step.Parameters.TryGetValue(key, out value) ? Convert.ToDouble(value) : fallback;
	}

	private static bool Bool(ImageAnalysisStep step, string key, bool fallback = false)
	{
		object value;
		return step.Parameters.TryGetValue(key, out value) ? Convert.ToBoolean(value) : fallback;
	}

	private static int[] Tuple(ImageAnalysisStep step, string key)
	{
		if (!step.Parameters.TryGetValue(key, out object value) || value == null)
		{
			return new int[3];
		}
		if (value is IEnumerable<object> source)
		{
			return source.Select(Convert.ToInt32).Take(3).Concat(new int[3])
				.Take(3)
				.ToArray();
		}
		return new int[3];
	}

	private static IReadOnlyList<ImageAnalysisStepDefinition> CreateDefinitions()
	{
		return new ImageAnalysisStepDefinition[21]
		{
			Def("按区域裁剪", "按画面配置中的区域裁剪图像。", Screen("screen_name", "画面"), Area("area_name", "区域", "screen_name")),
			Def("按模板裁剪", "按真实模板配置区域裁剪图像。", Template("template_name", "模板"), BoolDef("enable_match", "启用匹配", value: false), DoubleDef("match_threshold", "匹配阈值", 0.8, 0.0, 1.0)),
			Def("环形裁剪", "保留指定的环形区域。", IntDef("center_x_offset", "中心X偏移", 0, -9999.0, 9999.0), IntDef("center_y_offset", "中心Y偏移", 0, -9999.0, 9999.0), IntDef("outer_radius_reduction", "外半径缩减", 0, 0.0, 9999.0), IntDef("inner_radius", "内半径", 0, 0.0, 9999.0), IntDef("notch_x", "缺口X", 0, 0.0, 9999.0), IntDef("notch_y", "缺口Y", 0, 0.0, 9999.0), IntDef("notch_radius", "缺口半径", 0, 0.0, 9999.0)),
			Def("灰度化", "将彩色图像转换为灰度图像。"),
			Def("直方图均衡化", "增强灰度图像的全局对比度。"),
			Def("二值化", "将灰度图像转换为二值图像。", Choice("method", "二值化算法", "OTSU", "BINARY", "OTSU", "ADAPTIVE_GAUSSIAN", "ADAPTIVE_MEAN"), IntDef("threshold_value", "固定阈值", 127, 0.0, 255.0), IntDef("adaptive_block_size", "自适应-块大小", 11, 3.0, 99.0), IntDef("adaptive_c", "自适应-常量C", 2, -50.0, 50.0)),
			Def("RGB 范围过滤", "按 RGB 中心值和差值生成掩码。", TupleDef("rgb_color", "RGB中心值"), TupleDef("rgb_diff", "RGB差值")),
			Def("HSV 范围过滤", "按 HSV 中心值和差值生成掩码。", TupleDef("hsv_color", "HSV中心值"), TupleDef("hsv_diff", "HSV差值")),
			Def("腐蚀", "腐蚀二值掩码。", IntDef("kernel_size", "核大小", 3, 1.0, 21.0), IntDef("iterations", "迭代次数", 1, 1.0, 20.0)),
			Def("膨胀", "膨胀二值掩码。", IntDef("kernel_size", "核大小", 3, 1.0, 21.0), IntDef("iterations", "迭代次数", 1, 1.0, 20.0)),
			Def("形态学", "执行开运算、闭运算等形态学操作。", Choice("op", "操作类型", "开运算", "开运算", "闭运算", "梯度", "顶帽", "黑帽"), IntDef("kernel_size", "核大小", 3, 1.0, 21.0)),
			Def("查找轮廓", "从二值掩码查找轮廓。", Choice("mode", "轮廓检索模式", "EXTERNAL", "EXTERNAL", "LIST", "CCOMP", "TREE"), Choice("method", "轮廓逼近方法", "SIMPLE", "SIMPLE", "NONE"), BoolDef("draw_contours", "绘制轮廓", value: true)),
			Def("按面积过滤", "按面积范围过滤轮廓。", DoubleDef("min_area", "最小面积", 0.0, 0.0, 999999.0), DoubleDef("max_area", "最大面积", 999999.0, 0.0, 999999.0), BoolDef("draw_contours", "绘制轮廓", value: true)),
			Def("按周长过滤", "按周长范围过滤轮廓。", BoolDef("closed", "闭合轮廓", value: true), DoubleDef("min_length", "最小周长", 0.0, 0.0, 999999.0), DoubleDef("max_length", "最大周长", 999999.0, 0.0, 999999.0), BoolDef("draw_contours", "绘制轮廓", value: true)),
			Def("按半径过滤", "按最小外接圆半径过滤轮廓。", DoubleDef("min_radius", "最小半径", 0.0, 0.0, 999999.0), DoubleDef("max_radius", "最大半径", 999999.0, 0.0, 999999.0), BoolDef("draw_contours", "绘制轮廓", value: true)),
			Def("按长宽比过滤", "按外接矩形长宽比过滤轮廓。", DoubleDef("min_ratio", "最小长宽比", 0.0, 0.0, 100.0), DoubleDef("max_ratio", "最大长宽比", 100.0, 0.0, 100.0), BoolDef("draw_contours", "绘制轮廓", value: true)),
			Def("按质心距离过滤", "保留质心距离满足条件的轮廓。", DoubleDef("max_distance", "最大距离", 50.0, 0.0, 999999.0), BoolDef("draw_contours", "绘制轮廓", value: true)),
			Def("轮廓属性分析", "输出轮廓边界和面积。", BoolDef("show_bounding_box", "显示边界框", value: true), BoolDef("show_center", "显示中心", value: true)),
			Def("形状匹配", "使用真实轮廓模板比较形状。", Template("template_name", "模板轮廓名称"), DoubleDef("max_dissimilarity", "最大差异度", 0.5, 0.0, 10.0), BoolDef("draw_contours", "绘制匹配轮廓", value: true)),
			Def("模板匹配", "在当前图像中搜索真实模板图像。", Template("template_name", "模板图像名称"), DoubleDef("threshold", "匹配置信度", 0.8, 0.0, 1.0), Choice("method", "匹配算法", "TM_CCOEFF_NORMED", "TM_CCOEFF_NORMED", "TM_CCORR_NORMED", "TM_SQDIFF_NORMED")),
			Def("OCR识别", "使用当前生产 OCR 运行时识别图像。", BoolDef("draw_text_box", "绘制识别结果", value: true))
		};
	}

	private static ImageAnalysisStepDefinition Def(string name, string description, params ImageAnalysisParameterDefinition[] parameters)
	{
		return new ImageAnalysisStepDefinition(name, description, parameters);
	}

	private static ImageAnalysisParameterDefinition IntDef(string name, string label, int value, double min, double max)
	{
		return new ImageAnalysisParameterDefinition(name, label, ImageAnalysisParameterKind.Integer, value, min, max);
	}

	private static ImageAnalysisParameterDefinition DoubleDef(string name, string label, double value, double min, double max)
	{
		return new ImageAnalysisParameterDefinition(name, label, ImageAnalysisParameterKind.Double, value, min, max);
	}

	private static ImageAnalysisParameterDefinition BoolDef(string name, string label, bool value)
	{
		return new ImageAnalysisParameterDefinition(name, label, ImageAnalysisParameterKind.Boolean, value);
	}

	private static ImageAnalysisParameterDefinition Choice(string name, string label, string value, params string[] options)
	{
		return new ImageAnalysisParameterDefinition(name, label, ImageAnalysisParameterKind.Choice, value, 0.0, 0.0, options);
	}

	private static ImageAnalysisParameterDefinition Template(string name, string label)
	{
		return new ImageAnalysisParameterDefinition(name, label, ImageAnalysisParameterKind.Template, "");
	}

	private static ImageAnalysisParameterDefinition Screen(string name, string label)
	{
		return new ImageAnalysisParameterDefinition(name, label, ImageAnalysisParameterKind.Screen, "");
	}

	private static ImageAnalysisParameterDefinition Area(string name, string label, string parent)
	{
		return new ImageAnalysisParameterDefinition(name, label, ImageAnalysisParameterKind.Area, "", 0.0, 0.0, null, parent);
	}

	private static ImageAnalysisParameterDefinition TupleDef(string name, string label)
	{
		return new ImageAnalysisParameterDefinition(name, label, ImageAnalysisParameterKind.IntegerTuple, new int[3], 0.0, 255.0);
	}
}
