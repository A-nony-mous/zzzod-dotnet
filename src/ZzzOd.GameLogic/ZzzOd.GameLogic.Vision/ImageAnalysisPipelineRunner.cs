using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using OneDragon.Core.Runtime;
using OneDragon.Core.Screen;
using OpenCvSharp;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Vision;

/// <summary>
/// 执行 assets/image_analysis_pipelines 中的运行时图像分析流水线。
/// </summary>
public sealed class ImageAnalysisPipelineRunner
{
	private sealed class ImageAnalysisPipelineStep
	{
		public string Step { get; init; } = string.Empty;

		public Dictionary<string, object?> Params { get; init; } = new Dictionary<string, object>(StringComparer.Ordinal);
	}

	private static readonly IDeserializer Deserializer = new DeserializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance).IgnoreUnmatchedProperties().Build();

	/// <summary>
	/// 执行指定流水线并返回最终轮廓和绝对坐标。
	/// </summary>
	public ImageAnalysisPipelineRunResult Run(ZContext context, string pipelineName, Mat screen)
	{
		ArgumentNullException.ThrowIfNull(context, "context");
		ArgumentException.ThrowIfNullOrWhiteSpace(pipelineName, "pipelineName");
		ArgumentNullException.ThrowIfNull(screen, "screen");
		string pipelinePath = GetPipelinePath(context.Environment, pipelineName);
		if (!File.Exists(pipelinePath))
		{
			return ImageAnalysisPipelineRunResult.Fail("图像分析流水线缺失 " + pipelinePath, pipelinePath);
		}
		IReadOnlyList<ImageAnalysisPipelineStep> readOnlyList;
		try
		{
			readOnlyList = Deserializer.Deserialize<List<ImageAnalysisPipelineStep>>(File.ReadAllText(pipelinePath));
		}
		catch (Exception ex)
		{
			return ImageAnalysisPipelineRunResult.Fail("图像分析流水线读取失败 " + pipelinePath + ": " + ex.Message, pipelinePath);
		}
		if (readOnlyList == null || readOnlyList.Count == 0)
		{
			return ImageAnalysisPipelineRunResult.Fail("图像分析流水线为空 " + pipelinePath, pipelinePath);
		}
		Mat mat = screen.Clone();
		Mat mat2 = null;
		Point[][] contours = Array.Empty<Point[]>();
		int offsetX = 0;
		int offsetY = 0;
		try
		{
			foreach (ImageAnalysisPipelineStep item in readOnlyList)
			{
				switch (item.Step)
				{
				case "按区域裁剪":
				{
					string text2 = String(item, "screen_name");
					string text3 = String(item, "area_name");
					OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea(text2, text3);
					if (area == null)
					{
						return ImageAnalysisPipelineRunResult.Fail($"图像分析区域缺失 {text2}/{text3}，流水线 {pipelinePath}", pipelinePath);
					}
					int num = Math.Clamp(area.X1, 0, mat.Width);
					int num2 = Math.Clamp(area.Y1, 0, mat.Height);
					int num3 = Math.Clamp(area.X2, 0, mat.Width);
					int num4 = Math.Clamp(area.Y2, 0, mat.Height);
					if (num3 <= num || num4 <= num2)
					{
						return ImageAnalysisPipelineRunResult.Fail($"图像分析区域越界 {text2}/{text3}，流水线 {pipelinePath}", pipelinePath);
					}
					using (Mat mat3 = new Mat(mat, new Rect(num, num2, num3 - num, num4 - num2)))
					{
						Mat mat4 = mat3.Clone();
						mat.Dispose();
						mat = mat4;
						mat2?.Dispose();
						mat2 = null;
						contours = Array.Empty<Point[]>();
						offsetX += num;
						offsetY += num2;
					}
					break;
				}
				case "HSV 范围过滤":
				{
					int[] array = IntTuple(item, "hsv_color");
					int[] array2 = IntTuple(item, "hsv_diff");
					using (Mat mat7 = new Mat())
					{
						Cv2.CvtColor(mat, mat7, ColorConversionCodes.BGR2HSV);
						Mat mat8 = new Mat();
						Cv2.InRange(mat7, new Scalar(Math.Clamp(array[0] - array2[0], 0, 179), Math.Clamp(array[1] - array2[1], 0, 255), Math.Clamp(array[2] - array2[2], 0, 255)), new Scalar(Math.Clamp(array[0] + array2[0], 0, 179), Math.Clamp(array[1] + array2[1], 0, 255), Math.Clamp(array[2] + array2[2], 0, 255)), mat8);
						mat2?.Dispose();
						mat2 = mat8;
					}
					break;
				}
				case "腐蚀":
				case "膨胀":
				{
					if (mat2 == null)
					{
						return ImageAnalysisPipelineRunResult.Fail("图像分析步骤 " + item.Step + " 缺少前置 mask，流水线 " + pipelinePath, pipelinePath);
					}
					int num5 = Math.Max(1, Int(item, "kernel_size", 3));
					int num6 = Math.Max(1, Int(item, "iterations", 1));
					using (Mat mat5 = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(num5, num5)))
					{
						Mat mat6 = new Mat();
						if (item.Step == "腐蚀")
						{
							InputArray src = mat2;
							OutputArray dst = mat6;
							InputArray element = mat5;
							int iterations = num6;
							Cv2.Erode(src, dst, element, null, iterations);
						}
						else
						{
							InputArray src2 = mat2;
							OutputArray dst2 = mat6;
							InputArray element2 = mat5;
							int iterations = num6;
							Cv2.Dilate(src2, dst2, element2, null, iterations);
						}
						mat2.Dispose();
						mat2 = mat6;
					}
					break;
				}
				case "查找轮廓":
				{
					if (mat2 == null)
					{
						return ImageAnalysisPipelineRunResult.Fail("图像分析步骤 查找轮廓 缺少前置 mask，流水线 " + pipelinePath, pipelinePath);
					}
					string text = String(item, "mode", "EXTERNAL");
					if (1 == 0)
					{
					}
					RetrievalModes retrievalModes = text switch
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
					ContourApproximationModes method = ((String(item, "method", "SIMPLE") == "NONE") ? ContourApproximationModes.ApproxNone : ContourApproximationModes.ApproxSimple);
					Cv2.FindContours(mat2, out contours, out HierarchyIndex[] _, mode, method);
					break;
				}
				case "按面积过滤":
				{
					double min = Double(item, "min_area", 0.0);
					double max = Double(item, "max_area", double.MaxValue);
					contours = contours.Where((Point[] contour) => Cv2.ContourArea(contour) >= min && Cv2.ContourArea(contour) <= max).ToArray();
					break;
				}
				default:
					return ImageAnalysisPipelineRunResult.Fail("图像分析步骤未支持 " + item.Step + "，流水线 " + pipelinePath, pipelinePath);
				}
			}
			IReadOnlyList<ImageAnalysisContour> contours2 = contours.Select(delegate(Point[] contour)
			{
				Rect rect = Cv2.BoundingRect(contour);
				return new ImageAnalysisContour(contour, new Rect(rect.X + offsetX, rect.Y + offsetY, rect.Width, rect.Height), Cv2.ContourArea(contour));
			}).ToArray();
			return ImageAnalysisPipelineRunResult.Success(pipelinePath, contours2);
		}
		catch (Exception ex2)
		{
			return ImageAnalysisPipelineRunResult.Fail("图像分析流水线执行失败 " + pipelinePath + ": " + ex2.Message, pipelinePath);
		}
		finally
		{
			mat2?.Dispose();
			mat.Dispose();
		}
	}

	/// <summary>
	/// 获取流水线资产的运行时路径。
	/// </summary>
	public static string GetPipelinePath(OneDragonEnvironment environment, string pipelineName)
	{
		return environment.GetResourcePath("assets", "image_analysis_pipelines", pipelineName + ".yml");
	}

	private static string String(ImageAnalysisPipelineStep step, string key, string? defaultValue = null)
	{
		if (step.Params.TryGetValue(key, out object value) && value != null)
		{
			return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
		}
		if (defaultValue != null)
		{
			return defaultValue;
		}
		throw new InvalidDataException("流水线步骤 " + step.Step + " 缺少参数 " + key);
	}

	private static int Int(ImageAnalysisPipelineStep step, string key, int defaultValue)
	{
		if (!step.Params.TryGetValue(key, out object value) || value == null)
		{
			return defaultValue;
		}
		return Convert.ToInt32(value, CultureInfo.InvariantCulture);
	}

	private static double Double(ImageAnalysisPipelineStep step, string key, double defaultValue)
	{
		if (!step.Params.TryGetValue(key, out object value) || value == null)
		{
			return defaultValue;
		}
		return Convert.ToDouble(value, CultureInfo.InvariantCulture);
	}

	private static int[] IntTuple(ImageAnalysisPipelineStep step, string key)
	{
		if (!step.Params.TryGetValue(key, out object value) || !(value is IEnumerable<object> source))
		{
			throw new InvalidDataException("流水线步骤 " + step.Step + " 缺少数组参数 " + key);
		}
		int[] array = source.Select((object item) => Convert.ToInt32(item, CultureInfo.InvariantCulture)).ToArray();
		if (array.Length != 3)
		{
			throw new InvalidDataException($"流水线步骤 {step.Step} 参数 {key} 必须包含 3 个值");
		}
		return array;
	}
}
