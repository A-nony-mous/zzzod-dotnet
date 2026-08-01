using System;
using System.Collections.Generic;
using System.Linq;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Template;
using OneDragon.Core.Utils;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.GameData;

namespace ZzzOd.GameLogic.AutoBattle;

public static class AgentStateChecker
{
	public static string GetTemplateId(AgentStateDef stateDef, int? total = null, int? pos = null)
	{
		if (!total.HasValue || !pos.HasValue)
		{
			return stateDef.TemplateId;
		}
		return (total == 2 && pos == 2) ? $"{stateDef.TemplateId}_{total}_{pos}" : $"{stateDef.TemplateId}_3_{pos}";
	}

	public static int CheckStateValue(Mat image, AgentStateDef stateDef, Mat? mask = null)
	{
		AgentStateDef agentStateDef = ResolveStateDef(stateDef);
		AgentStateCheckWay checkWay = agentStateDef.CheckWay;
		if (1 == 0)
		{
		}
		int result = checkWay switch
		{
			AgentStateCheckWay.COLOR_RANGE_CONNECT => CountByColorRange(image, agentStateDef, mask), 
			AgentStateCheckWay.COLOR_RANGE_EXIST => ExistsByColorRange(image, agentStateDef, mask), 
			AgentStateCheckWay.BACKGROUND_GRAY_RANGE_LENGTH => LengthByBackgroundGray(image, agentStateDef), 
			AgentStateCheckWay.FOREGROUND_GRAY_RANGE_LENGTH => LengthByForegroundGray(image, agentStateDef), 
			AgentStateCheckWay.FOREGROUND_COLOR_RANGE_LENGTH => LengthByForegroundColor(image, agentStateDef), 
			AgentStateCheckWay.COLOR_CHANNEL_MAX_RANGE_EXIST => ExistsByColorChannelMaxRange(image, agentStateDef, mask), 
			AgentStateCheckWay.COLOR_CHANNEL_EQUAL_RANGE_CONNECT => ExistsByColorChannelEqualRange(image, agentStateDef, mask), 
			_ => 0, 
		};
		if (1 == 0)
		{
		}
		return result;
	}

	public static int CheckStateValue(ZContext ctx, Mat screen, AgentStateDef stateDef, int? total = null, int? pos = null)
	{
		AgentStateDef agentStateDef = ResolveStateDef(stateDef, total, pos);
		TemplateInfo template = ctx.TemplateLoader.GetTemplate("agent_state", GetTemplateId(agentStateDef, total, pos));
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
				using Mat mat = CvImageUtils.Crop(screen, valueOrDefault);
				Mat mask = template.Mask;
				AgentStateCheckWay checkWay = agentStateDef.CheckWay;
				if (1 == 0)
				{
				}
				int result = checkWay switch
				{
					AgentStateCheckWay.TEMPLATE_FOUND => (template.Raw != null) ? CheckTemplateFound(mat, template.Raw, agentStateDef.TemplateThreshold ?? 0.8, mask) : 0, 
					AgentStateCheckWay.TEMPLATE_NOT_FOUND => (template.Raw != null) ? CheckTemplateNotFound(mat, template.Raw, agentStateDef.TemplateThreshold ?? 0.8, mask) : 0, 
					_ => CheckStateValue(mat, agentStateDef, mask), 
				};
				if (1 == 0)
				{
				}
				return result;
			}
		}
		return 0;
	}

	public static Mat FilterByColor(Mat image, AgentStateDef stateDef, string colorMode = "auto")
	{
		bool flag = colorMode == "hsv" || (colorMode == "auto" && stateDef.HsvColor != null && stateDef.HsvColorDiff != null);
		bool flag2 = colorMode == "rgb" || (colorMode == "auto" && !flag && stateDef.LowerColor != null && stateDef.UpperColor != null);
		if (flag && stateDef.HsvColor != null && stateDef.HsvColorDiff != null)
		{
			return FilterByHsv(image, stateDef.HsvColor, stateDef.HsvColorDiff);
		}
		if (flag2 && stateDef.LowerColor != null && stateDef.UpperColor != null)
		{
			return FilterByRgb(image, stateDef.LowerColor, stateDef.UpperColor);
		}
		return new Mat(image.Rows, image.Cols, MatType.CV_8UC1, Scalar.White);
	}

	public static int CountByColorRange(Mat image, AgentStateDef stateDef, Mat? mask = null)
	{
		using Mat image2 = ApplyMask(image, mask);
		using Mat mask2 = FilterByColor(image2, stateDef);
		using Mat mask3 = Dilate(mask2, 2);
		return CountConnectedComponents(mask3, stateDef.ConnectCnt);
	}

	public static int ExistsByColorRange(Mat image, AgentStateDef stateDef, Mat? mask = null)
	{
		return (CountByColorRange(image, stateDef, mask) > 0) ? 1 : 0;
	}

	public static int LengthByBackgroundGray(Mat image, AgentStateDef stateDef)
	{
		double[] array = ColumnGrayMeans(image);
		int num = array.Length;
		if (num == 0)
		{
			return 0;
		}
		var (num2, num3) = FindBounds(array, stateDef.LowerColor, stateDef.UpperColor, match: true);
		var (num4, num5) = FindBounds(array, stateDef.LowerColor, stateDef.UpperColor, match: false);
		int num7;
		if (num2 < num4)
		{
			int num6 = ClampCount(num3 - num2 + 1, num);
			num7 = num - num6;
		}
		else
		{
			num7 = ClampCount(num5 - num4 + 1, num);
		}
		return (int)((double)num7 * 100.0 / (double)num);
	}

	public static int LengthByForegroundGray(Mat image, AgentStateDef stateDef)
	{
		double[] array = ColumnGrayMeans(image);
		if (stateDef.SplitColorRange != null && stateDef.SplitColorRange.Count >= 2)
		{
			array = array.Where((double value) => value < (double)stateDef.SplitColorRange[0] || value > (double)stateDef.SplitColorRange[1]).ToArray();
		}
		int num = array.Length;
		if (num == 0)
		{
			return 0;
		}
		(int Left, int Right) tuple = FindBounds(array, stateDef.LowerColor, stateDef.UpperColor, match: true);
		int item = tuple.Left;
		int item2 = tuple.Right;
		int num2 = ClampCount(item2 - item + 1, num);
		return (int)((double)(num2 * stateDef.MaxLength) / (double)num);
	}

	public static int LengthByForegroundColor(Mat image, AgentStateDef stateDef)
	{
		using Mat mat = FilterByColor(image, stateDef);
		using Mat mat2 = new Mat();
		Cv2.FindNonZero(mat, mat2);
		int count = 0;
		if (!mat2.Empty())
		{
			count = Cv2.BoundingRect(mat2).Width;
		}
		int width = image.Width;
		if (width == 0)
		{
			return 0;
		}
		count = ClampCount(count, width);
		return (int)((double)(count * stateDef.MaxLength) / (double)width);
	}

	public static int CountByColorChannelMaxRange(Mat image, AgentStateDef stateDef, Mat? mask = null)
	{
		using Mat image2 = ApplyMask(image, mask);
		using Mat mask2 = CreateMaxChannelMask(image2, stateDef.LowerColor, stateDef.UpperColor);
		using Mat mask3 = Dilate(mask2, 2);
		return CountConnectedComponents(mask3, stateDef.ConnectCnt);
	}

	public static int ExistsByColorChannelMaxRange(Mat image, AgentStateDef stateDef, Mat? mask = null)
	{
		return (CountByColorChannelMaxRange(image, stateDef, mask) > 0) ? 1 : 0;
	}

	public static int CountByColorChannelEqualRange(Mat image, AgentStateDef stateDef, Mat? mask = null)
	{
		using Mat mat = ApplyMask(image, mask);
		// 对齐 参考实现的三通道相等判定：用通道比较代替逐像素访问
		Mat[] channels = Cv2.Split(mat);
		int num;
		try
		{
			using Mat equal01 = new Mat();
			using Mat equal12 = new Mat();
			using Mat equalAll = new Mat();
			Cv2.Compare(channels[0], channels[1], equal01, CmpType.EQ);
			Cv2.Compare(channels[1], channels[2], equal12, CmpType.EQ);
			Cv2.BitwiseAnd(equal01, equal12, equalAll);
			num = Cv2.CountNonZero(equalAll);
		}
		finally
		{
			foreach (Mat channel in channels)
			{
				channel.Dispose();
			}
		}
		return (num >= stateDef.ConnectCnt) ? 1 : 0;
	}

	public static int ExistsByColorChannelEqualRange(Mat image, AgentStateDef stateDef, Mat? mask = null)
	{
		return CountByColorChannelEqualRange(image, stateDef, mask);
	}

	public static int CheckTemplateFound(Mat source, Mat template, double threshold = 0.8, Mat? mask = null)
	{
		if (source.Empty() || template.Empty() || source.Width < template.Width || source.Height < template.Height)
		{
			return 0;
		}
		using Mat mat = new Mat();
		if (mask != null && !mask.Empty())
		{
			Cv2.MatchTemplate(source, template, mat, TemplateMatchModes.CCoeffNormed, mask);
		}
		else
		{
			Cv2.MatchTemplate(source, template, mat, TemplateMatchModes.CCoeffNormed);
		}
		// 对齐 参考实现 cv2_utils.match_template(TM_CCOEFF_NORMED)：带 mask 匹配可能产生 NaN，
		// 参考实现侧 result >= threshold 对 NaN 为假、对 +inf 为真，先替换 NaN 再取最大值。
		Cv2.PatchNaNs(mat, -1.0);
		Cv2.MinMaxLoc((InputArray)mat, out double _, out double maxVal);
		return (maxVal >= threshold) ? 1 : 0;
	}

	public static int CheckTemplateNotFound(Mat source, Mat template, double threshold = 0.8, Mat? mask = null)
	{
		return (CheckTemplateFound(source, template, threshold, mask) != 1) ? 1 : 0;
	}

	private static Mat FilterByRgb(Mat image, IReadOnlyList<int> lower, IReadOnlyList<int> upper)
	{
		// 对齐 参考实现 cv2.inRange 的向量化实现；单元素上下界表示三通道共用同一区间
		bool perChannel = lower.Count != 1 && upper.Count != 1;
		Scalar lowerBound = perChannel
			? new Scalar(ClampLow(lower[2]), ClampLow(lower[1]), ClampLow(lower[0]))
			: new Scalar(ClampLow(lower[0]), ClampLow(lower[0]), ClampLow(lower[0]));
		Scalar upperBound = perChannel
			? new Scalar(ClampHigh(upper[2]), ClampHigh(upper[1]), ClampHigh(upper[0]))
			: new Scalar(ClampHigh(upper[0]), ClampHigh(upper[0]), ClampHigh(upper[0]));
		Mat mat = new Mat();
		Cv2.InRange(image, lowerBound, upperBound, mat);
		return mat;
	}

	private static Mat FilterByHsv(Mat image, IReadOnlyList<int> hsvColor, IReadOnlyList<int> hsvDiff)
	{
		using Mat mat = new Mat();
		Cv2.CvtColor(image, mat, ColorConversionCodes.BGR2HSV);
		// 色相是环形量：中心 ± 容差跨越 0/180 时要拆成两段区间并取并集
		int hueCenter = (hsvColor[0] % 180 + 180) % 180;
		int hueDiff = hsvDiff[0];
		double saturationLow = ClampLow(hsvColor[1] - hsvDiff[1]);
		double saturationHigh = ClampHigh(hsvColor[1] + hsvDiff[1]);
		double valueLow = ClampLow(hsvColor[2] - hsvDiff[2]);
		double valueHigh = ClampHigh(hsvColor[2] + hsvDiff[2]);
		Mat mat2;
		if (hueDiff >= 90)
		{
			mat2 = new Mat();
			Cv2.InRange(mat, new Scalar(0.0, saturationLow, valueLow), new Scalar(179.0, saturationHigh, valueHigh), mat2);
			return mat2;
		}
		int hueLow = hueCenter - hueDiff;
		int hueHigh = hueCenter + hueDiff;
		if (hueLow >= 0 && hueHigh <= 179)
		{
			mat2 = new Mat();
			Cv2.InRange(mat, new Scalar(hueLow, saturationLow, valueLow), new Scalar(hueHigh, saturationHigh, valueHigh), mat2);
			return mat2;
		}
		int wrappedLow = (hueLow + 180) % 180;
		int wrappedHigh = hueHigh % 180;
		using Mat lowerPart = new Mat();
		using Mat upperPart = new Mat();
		Cv2.InRange(mat, new Scalar(wrappedLow, saturationLow, valueLow), new Scalar(179.0, saturationHigh, valueHigh), lowerPart);
		Cv2.InRange(mat, new Scalar(0.0, saturationLow, valueLow), new Scalar(wrappedHigh, saturationHigh, valueHigh), upperPart);
		mat2 = new Mat();
		Cv2.BitwiseOr(lowerPart, upperPart, mat2);
		return mat2;
	}

	private static double ClampLow(double low)
	{
		return Math.Max(0.0, low);
	}

	private static double ClampHigh(double high)
	{
		return Math.Min(255.0, high);
	}

	private static Mat ApplyMask(Mat image, Mat? mask)
	{
		if (mask == null || mask.Empty())
		{
			return image.Clone();
		}
		Mat mat = new Mat();
		Cv2.BitwiseAnd(image, image, mat, mask);
		return mat;
	}

	private static Mat Dilate(Mat mask, int iterations)
	{
		Mat mat = new Mat();
		using Mat mat2 = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
		Cv2.Dilate(mask, mat, mat2, null, iterations);
		return mat;
	}

	private static int CountConnectedComponents(Mat mask, int minArea)
	{
		using Mat mat = new Mat();
		using Mat mat2 = new Mat();
		using Mat mat3 = new Mat();
		int num = Cv2.ConnectedComponentsWithStats(mask, mat, mat2, mat3);
		int num2 = 0;
		for (int i = 1; i < num; i++)
		{
			int num3 = mat2.At<int>(i, 4);
			if (num3 >= minArea)
			{
				num2++;
			}
		}
		return num2;
	}

	private static double[] ColumnGrayMeans(Mat image)
	{
		if (image.Empty())
		{
			return Array.Empty<double>();
		}
		using Mat mat = new Mat();
		Cv2.CvtColor(image, mat, ColorConversionCodes.RGB2GRAY);
		// 对齐 参考实现 numpy 的按列求均值：用 Reduce 一次算完，避免逐像素访问
		using Mat columnSums = new Mat();
		Cv2.Reduce(mat, columnSums, ReduceDimension.Row, ReduceTypes.Avg, MatType.CV_64F);
		double[] array = new double[mat.Cols];
		for (int i = 0; i < mat.Cols; i++)
		{
			array[i] = columnSums.At<double>(0, i);
		}
		return array;
	}

	private static (int Left, int Right) FindBounds(IReadOnlyList<double> values, IReadOnlyList<int>? lower, IReadOnlyList<int>? upper, bool match)
	{
		int count = values.Count;
		int num = count + 1;
		int num2 = 0;
		int num3 = lower?[0] ?? 0;
		int num4 = upper?[0] ?? 255;
		for (int i = 0; i < values.Count; i++)
		{
			bool flag = InRange(values[i], num3, num4);
			if (flag == match)
			{
				num = Math.Min(num, i);
				num2 = Math.Max(num2, i);
			}
		}
		return (Left: num, Right: num2);
	}

	private static Mat CreateMaxChannelMask(Mat image, IReadOnlyList<int>? lower, IReadOnlyList<int>? upper)
	{
		int num = lower?[0] ?? 0;
		int num2 = upper?[0] ?? 255;
		// 对齐 参考实现 np.max(axis=0) + cv2.inRange：先取三通道逐像素最大值，再做区间判定
		Mat[] channels = Cv2.Split(image);
		try
		{
			using Mat maxChannel = new Mat();
			Cv2.Max(channels[0], channels[1], maxChannel);
			Cv2.Max(maxChannel, channels[2], maxChannel);
			Mat mat = new Mat();
			Cv2.InRange(maxChannel, ClampLow(num), ClampHigh(num2), mat);
			return mat;
		}
		finally
		{
			foreach (Mat channel in channels)
			{
				channel.Dispose();
			}
		}
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

	private static int ClampCount(int count, int totalCount)
	{
		return Math.Clamp(count, 0, totalCount);
	}

	public static AgentStateDef ResolveStateDef(AgentStateDef stateDef, int? total = null, int? pos = null)
	{
		if (stateDef == CommonAgentStateEnum.ENERGY_31.Value)
		{
			string stateName = stateDef.StateName;
			IReadOnlyList<int> lowerColor = new int[] { 90 };
			IReadOnlyList<int> upperColor = new int[] { 255 };
			IReadOnlyList<int> splitColorRange = new int[2] { 0, 30 };
			return new AgentStateDef(stateName, AgentStateCheckWay.FOREGROUND_GRAY_RANGE_LENGTH, "energy_3_1", lowerColor, upperColor, null, null, null, splitColorRange, 120);
		}
		if (stateDef == CommonAgentStateEnum.ENERGY_32.Value)
		{
			string stateName2 = stateDef.StateName;
			IReadOnlyList<int> lowerColor2 = new int[] { 90 };
			IReadOnlyList<int> upperColor2 = new int[] { 255 };
			IReadOnlyList<int> splitColorRange = new int[2] { 0, 30 };
			return new AgentStateDef(stateName2, AgentStateCheckWay.FOREGROUND_GRAY_RANGE_LENGTH, "energy_3_2", lowerColor2, upperColor2, null, null, null, splitColorRange, 120);
		}
		if (stateDef == CommonAgentStateEnum.ENERGY_33.Value)
		{
			string stateName3 = stateDef.StateName;
			IReadOnlyList<int> lowerColor3 = new int[] { 90 };
			IReadOnlyList<int> upperColor3 = new int[] { 255 };
			IReadOnlyList<int> splitColorRange = new int[2] { 0, 30 };
			return new AgentStateDef(stateName3, AgentStateCheckWay.FOREGROUND_GRAY_RANGE_LENGTH, "energy_3_3", lowerColor3, upperColor3, null, null, null, splitColorRange, 120);
		}
		if (stateDef == CommonAgentStateEnum.ENERGY_21.Value)
		{
			string stateName4 = stateDef.StateName;
			IReadOnlyList<int> lowerColor4 = new int[] { 90 };
			IReadOnlyList<int> upperColor4 = new int[] { 255 };
			IReadOnlyList<int> splitColorRange = new int[2] { 0, 30 };
			return new AgentStateDef(stateName4, AgentStateCheckWay.FOREGROUND_GRAY_RANGE_LENGTH, "energy_2_1", lowerColor4, upperColor4, null, null, null, splitColorRange, 120);
		}
		if (stateDef == CommonAgentStateEnum.ENERGY_22.Value)
		{
			string stateName5 = stateDef.StateName;
			IReadOnlyList<int> lowerColor5 = new int[] { 90 };
			IReadOnlyList<int> upperColor5 = new int[] { 255 };
			IReadOnlyList<int> splitColorRange = new int[2] { 0, 30 };
			return new AgentStateDef(stateName5, AgentStateCheckWay.FOREGROUND_GRAY_RANGE_LENGTH, "energy_2_2", lowerColor5, upperColor5, null, null, null, splitColorRange, 120);
		}
		if (stateDef == CommonAgentStateEnum.SPECIAL_31.Value || stateDef == CommonAgentStateEnum.SPECIAL_21.Value)
		{
			return new AgentStateDef(stateDef.StateName, AgentStateCheckWay.COLOR_RANGE_CONNECT, "special_3_1", null, null, new int[3] { 0, 0, 255 }, new int[3] { 90, 255, 50 }, 200);
		}
		if (stateDef == CommonAgentStateEnum.SPECIAL_32.Value)
		{
			return new AgentStateDef(stateDef.StateName, AgentStateCheckWay.COLOR_CHANNEL_MAX_RANGE_EXIST, "energy_3_2", new int[] { 150 }, new int[] { 255 }, null, null, 10, null, 100, 0);
		}
		if (stateDef == CommonAgentStateEnum.SPECIAL_33.Value)
		{
			return new AgentStateDef(stateDef.StateName, AgentStateCheckWay.COLOR_CHANNEL_MAX_RANGE_EXIST, "energy_3_3", new int[] { 150 }, new int[] { 255 }, null, null, 10, null, 100, 0);
		}
		if (stateDef == CommonAgentStateEnum.SPECIAL_22.Value)
		{
			return new AgentStateDef(stateDef.StateName, AgentStateCheckWay.COLOR_CHANNEL_MAX_RANGE_EXIST, "energy_2_2", new int[] { 150 }, new int[] { 255 }, null, null, 10, null, 100, 0);
		}
		if (stateDef == CommonAgentStateEnum.ULTIMATE_31.Value || stateDef == CommonAgentStateEnum.ULTIMATE_21.Value)
		{
			return new AgentStateDef(stateDef.StateName, AgentStateCheckWay.COLOR_RANGE_CONNECT, "ultimate_3_1", null, null, new int[3] { 0, 0, 255 }, new int[3] { 90, 255, 50 }, 1000);
		}
		if (stateDef == CommonAgentStateEnum.ULTIMATE_32.Value)
		{
			return new AgentStateDef(stateDef.StateName, AgentStateCheckWay.COLOR_RANGE_EXIST, "ultimate_3_2", new int[3] { 250, 150, 20 }, new int[3] { 255, 255, 70 }, null, null, 5, null, 100, 0);
		}
		if (stateDef == CommonAgentStateEnum.ULTIMATE_33.Value)
		{
			return new AgentStateDef(stateDef.StateName, AgentStateCheckWay.COLOR_RANGE_EXIST, "ultimate_3_3", new int[3] { 250, 150, 20 }, new int[3] { 255, 255, 70 }, null, null, 5, null, 100, 0);
		}
		if (stateDef == CommonAgentStateEnum.ULTIMATE_22.Value)
		{
			return new AgentStateDef(stateDef.StateName, AgentStateCheckWay.COLOR_RANGE_EXIST, "ultimate_2_2", new int[3] { 250, 150, 20 }, new int[3] { 255, 255, 70 }, null, null, 5, null, 100, 0);
		}
		if (stateDef == CommonAgentStateEnum.LIFE_DEDUCTION_31.Value)
		{
			string stateName6 = stateDef.StateName;
			IReadOnlyList<int> lowerColor6 = new int[3] { 140, 30, 30 };
			IReadOnlyList<int> upperColor6 = new int[3] { 160, 50, 50 };
			int? minValueTriggerState = 1;
			return new AgentStateDef(stateName6, AgentStateCheckWay.FOREGROUND_COLOR_RANGE_LENGTH, "life_deduction_3_1", lowerColor6, upperColor6, null, null, null, null, 100, minValueTriggerState);
		}
		if (stateDef == CommonAgentStateEnum.LIFE_DEDUCTION_21.Value)
		{
			string stateName7 = stateDef.StateName;
			IReadOnlyList<int> lowerColor7 = new int[3] { 140, 30, 30 };
			IReadOnlyList<int> upperColor7 = new int[3] { 160, 50, 50 };
			int? minValueTriggerState = 1;
			return new AgentStateDef(stateName7, AgentStateCheckWay.FOREGROUND_COLOR_RANGE_LENGTH, "life_deduction_2_1", lowerColor7, upperColor7, null, null, null, null, 100, minValueTriggerState);
		}
		if (stateDef == CommonAgentStateEnum.GUARD_BREAK.Value)
		{
			return new AgentStateDef(stateDef.StateName, AgentStateCheckWay.COLOR_CHANNEL_EQUAL_RANGE_CONNECT, "guard_break", new int[] { 0 }, new int[] { 255 }, null, null, 10000, null, 100, 0);
		}
		if (stateDef == CommonAgentStateEnum.SWITCH_BAN.Value)
		{
			return new AgentStateDef(stateDef.StateName, AgentStateCheckWay.COLOR_RANGE_EXIST, "switch_ban", null, null, new int[3] { 45, 35, 85 }, new int[3] { 45, 50, 93 }, 2000, null, 100, 0);
		}
		return ResolveNamedAgentState(stateDef, total, pos);
	}

	private static AgentStateDef ResolveNamedAgentState(AgentStateDef stateDef, int? total, int? pos)
	{
		string stateName = stateDef.StateName;
		if (1 == 0)
		{
		}
		AgentStateDef result;
		if (stateName == "叶瞬光-常态")
		{
			string stateName2 = stateDef.StateName;
			double? templateThreshold = 0.7;
			result = new AgentStateDef(stateName2, AgentStateCheckWay.TEMPLATE_FOUND, "yeshunguang_normal", null, null, null, null, null, null, 100, null, templateThreshold);
		}
		else
		{
			result = ResolveKnownColorAgentState(stateDef, total, pos);
		}
		if (1 == 0)
		{
		}
		return result;
	}

	private static AgentStateDef ResolveKnownColorAgentState(AgentStateDef stateDef, int? total, int? pos)
	{
		string stateName = stateDef.StateName;
		if (1 == 0)
		{
		}
		AgentStateDef result = stateName switch
		{
			"艾莲-急冻充能" => new AgentStateDef(stateDef.StateName, AgentStateCheckWay.COLOR_RANGE_CONNECT, "ellen", new int[3] { 200, 245, 250 }, new int[3] { 255, 255, 255 }, null, null, 2), 
			"格莉丝-电能" => new AgentStateDef(stateDef.StateName, AgentStateCheckWay.COLOR_RANGE_CONNECT, "grace", null, null, new int[3] { 0, 255, 255 }, new int[3] { 20, 255, 50 }, 2), 
			"苍角-涡流" => new AgentStateDef(stateDef.StateName, AgentStateCheckWay.COLOR_RANGE_CONNECT, "soukaku", new int[3] { 0, 220, 220 }, new int[3] { 175, 255, 255 }, null, null, 15), 
			"朱鸢-子弹数" => new AgentStateDef(stateDef.StateName, AgentStateCheckWay.COLOR_RANGE_CONNECT, "zhu_yuan", new int[3] { 240, 60, 0 }, new int[3] { 255, 180, 20 }, null, null, 5), 
			"青衣-电压" => new AgentStateDef(stateDef.StateName, AgentStateCheckWay.BACKGROUND_GRAY_RANGE_LENGTH, "qingyi", new int[] { 0 }, new int[] { 70 }), 
			"简-萨霍夫跳" => new AgentStateDef(stateDef.StateName, AgentStateCheckWay.COLOR_RANGE_EXIST, "jane_attack", new int[3] { 100, 20, 20 }, new int[3] { 255, 255, 255 }, null, null, 20), 
			"简-狂热心流" => new AgentStateDef(stateDef.StateName, AgentStateCheckWay.FOREGROUND_COLOR_RANGE_LENGTH, "jane_red", new int[3] { 200, 20, 20 }, new int[3] { 255, 255, 255 }, null, null, 10), 
			"赛斯-意气" => new AgentStateDef(stateDef.StateName, AgentStateCheckWay.BACKGROUND_GRAY_RANGE_LENGTH, "seth_lowell", new int[] { 0 }, new int[] { 10 }), 
			"柏妮思-燃点" => new AgentStateDef(stateDef.StateName, AgentStateCheckWay.FOREGROUND_COLOR_RANGE_LENGTH, "burnice_white", null, null, new int[3] { 0, 255, 255 }, new int[3] { 90, 200, 100 }), 
			"莱特-士气" => new AgentStateDef(stateDef.StateName, AgentStateCheckWay.BACKGROUND_GRAY_RANGE_LENGTH, "lighter", new int[] { 0 }, new int[] { 50 }), 
			"雅-落霜" => new AgentStateDef(stateDef.StateName, AgentStateCheckWay.COLOR_RANGE_CONNECT, "hoshimi_miyabi", null, null, new int[3] { 90, 255, 255 }, new int[3] { 60, 255, 50 }, 5), 
			"伊芙琳-燎火" => new AgentStateDef(stateDef.StateName, AgentStateCheckWay.BACKGROUND_GRAY_RANGE_LENGTH, "evelyn_chevalier_1", new int[] { 0 }, new int[] { 30 }), 
			"伊芙琳-燎索点" => new AgentStateDef(stateDef.StateName, AgentStateCheckWay.COLOR_RANGE_CONNECT, "evelyn_chevalier_2", new int[3] { 70, 70, 70 }, new int[3] { 255, 255, 255 }, null, null, 5), 
			"波可娜-猎步" => new AgentStateDef(stateDef.StateName, AgentStateCheckWay.COLOR_RANGE_CONNECT, "pulchra_hunter", new int[3] { 200, 120, 30 }, new int[3] { 255, 255, 255 }, null, null, 1), 
			"扳机-绝意" => new AgentStateDef(stateDef.StateName, AgentStateCheckWay.FOREGROUND_COLOR_RANGE_LENGTH, "trigger", new int[3] { 0, 50, 0 }, new int[3] { 255, 255, 255 }), 
			"薇薇安-飞羽" => new AgentStateDef(stateDef.StateName, AgentStateCheckWay.COLOR_RANGE_CONNECT, "vivian_master_1", new int[3] { 150, 110, 170 }, new int[3] { 255, 255, 255 }, null, null, 5), 
			"薇薇安-护羽" => new AgentStateDef(stateDef.StateName, AgentStateCheckWay.COLOR_RANGE_CONNECT, "vivian_master_2", new int[3] { 170, 170, 200 }, new int[3] { 255, 255, 255 }, null, null, 5), 
			"仪玄-玄墨值" => new AgentStateDef(stateDef.StateName, AgentStateCheckWay.COLOR_RANGE_CONNECT, "yixuan_auric_Ink", null, null, new int[3] { 20, 127, 255 }, new int[3] { 15, 128, 50 }, 10, null, 100, 0), 
			"仪玄-术法值全满" => new AgentStateDef(stateDef.StateName, AgentStateCheckWay.COLOR_RANGE_EXIST, "yixuan_technique", null, null, new int[3] { 30, 0, 245 }, new int[3] { 15, 245, 255 }, 10, null, 100, 0), 
			"仪玄-术法值" => new AgentStateDef(stateDef.StateName, AgentStateCheckWay.FOREGROUND_COLOR_RANGE_LENGTH, "yixuan_technique", null, null, new int[3] { 0, 255, 255 }, new int[3] { 90, 220, 200 }, null, null, 120), 
			"威风" => new AgentStateDef(stateDef.StateName, AgentStateCheckWay.FOREGROUND_COLOR_RANGE_LENGTH, "ju_fufu", null, null, new int[3] { 248, 164, 67 }, new int[3] { 100, 100, 30 }, null, null, 200), 
			"柚叶-甜度点" => new AgentStateDef(stateDef.StateName, AgentStateCheckWay.COLOR_RANGE_CONNECT, "yuzuha", new int[3] { 221, 107, 113 }, new int[3] { 255, 255, 255 }, null, null, 6), 
			"爱丽丝-剑仪" => new AgentStateDef(stateDef.StateName, AgentStateCheckWay.FOREGROUND_COLOR_RANGE_LENGTH, "alice", null, null, new int[3] { 90, 255, 0 }, new int[3] { 89, 254, 255 }, null, null, 300), 
			"席德-钢能" => new AgentStateDef(stateDef.StateName, AgentStateCheckWay.FOREGROUND_COLOR_RANGE_LENGTH, "seed", null, null, new int[3] { 90, 255, 255 }, new int[3] { 89, 55, 55 }, null, null, 150), 
			"奥菲丝-蓄炎" => new AgentStateDef(stateDef.StateName, AgentStateCheckWay.FOREGROUND_COLOR_RANGE_LENGTH, "orphie", null, null, new int[3] { 90, 255, 255 }, new int[3] { 89, 255, 55 }, null, null, 125), 
			"卢西娅-梦境值" => new AgentStateDef(stateDef.StateName, AgentStateCheckWay.FOREGROUND_COLOR_RANGE_LENGTH, "lucia", null, null, new int[3] { 133, 69, 255 }, new int[3] { 17, 97, 0 }), 
			"真斗-炽心" => new AgentStateDef(stateDef.StateName, AgentStateCheckWay.FOREGROUND_COLOR_RANGE_LENGTH, "manato", null, null, new int[3] { 20, 255, 255 }, new int[3] { 15, 255, 55 }), 
			"伊德海莉-蓄力段数" => new AgentStateDef(stateDef.StateName, AgentStateCheckWay.FOREGROUND_COLOR_RANGE_LENGTH, "yidhari", null, null, new int[3] { 95, 100, 245 }, new int[3] { 5, 125, 20 }, null, null, 85), 
			"琉音-客诉" => new AgentStateDef(stateDef.StateName, AgentStateCheckWay.COLOR_RANGE_CONNECT, "dialyn_cc", null, null, new int[3] { 0, 255, 255 }, new int[3] { 90, 220, 200 }, 6), 
			"琉音-好评" => new AgentStateDef(stateDef.StateName, AgentStateCheckWay.FOREGROUND_COLOR_RANGE_LENGTH, "dialyn_pr", null, null, new int[3] { 0, 255, 255 }, new int[3] { 90, 220, 200 }, null, null, 120), 
			"般岳-嗔火" => new AgentStateDef(stateDef.StateName, AgentStateCheckWay.FOREGROUND_COLOR_RANGE_LENGTH, "banyue_1", null, null, new int[3] { 23, 136, 177 }, new int[3] { 7, 153, 156 }, null, null, 120), 
			"般岳-山威" => new AgentStateDef(stateDef.StateName, AgentStateCheckWay.COLOR_RANGE_CONNECT, "banyue_2", null, null, new int[3] { 10, 102, 130 }, new int[3] { 10, 205, 244 }, 5), 
			"照-霜寒值" => new AgentStateDef(stateDef.StateName, AgentStateCheckWay.FOREGROUND_COLOR_RANGE_LENGTH, "zhao", null, null, new int[3] { 60, 255, 255 }, new int[3] { 50, 255, 255 }), 
			"叶瞬光-明心境" => new AgentStateDef(stateDef.StateName, AgentStateCheckWay.FOREGROUND_COLOR_RANGE_LENGTH, "yeshunguang_mingxinjing", null, null, new int[3] { 113, 75, 255 }, new int[3] { 10, 50, 50 }, null, null, 120), 
			"叶瞬光-青溟剑势-红" => new AgentStateDef(stateDef.StateName, AgentStateCheckWay.COLOR_RANGE_CONNECT, "yeshunguang_qingming", null, null, new int[3] { 0, 0, 255 }, new int[3] { 10, 10, 10 }, 2), 
			"叶瞬光-青溟剑势-白" => new AgentStateDef(stateDef.StateName, AgentStateCheckWay.COLOR_RANGE_CONNECT, "yeshunguang_qingming_ex", null, null, new int[3] { 0, 0, 255 }, new int[3], 10), 
			"爱芮-应援能量" => new AgentStateDef(stateDef.StateName, AgentStateCheckWay.COLOR_RANGE_CONNECT, "aria_cheer_energy", null, null, new int[3] { 90, 255, 255 }, new int[3] { 90, 200, 100 }, 2), 
			"南宫羽-重拍" => new AgentStateDef(stateDef.StateName, AgentStateCheckWay.FOREGROUND_COLOR_RANGE_LENGTH, "nangongyu", null, null, new int[3] { 0, 255, 255 }, new int[3] { 90, 220, 200 }), 
			"普罗米娅-霜刑" => new AgentStateDef(stateDef.StateName, AgentStateCheckWay.COLOR_RANGE_CONNECT, "promeia_ss", null, null, new int[3] { 0, 255, 255 }, new int[3] { 90, 255, 50 }, 2), 
			"维琳娜-风华" => new AgentStateDef(stateDef.StateName, AgentStateCheckWay.FOREGROUND_COLOR_RANGE_LENGTH, "velina", null, null, new int[3] { 0, 255, 255 }, new int[3] { 90, 220, 200 }, null, null, 135), 
			"佩洛伊斯-日珥" => new AgentStateDef(stateDef.StateName, AgentStateCheckWay.FOREGROUND_COLOR_RANGE_LENGTH, "pyrois_wise", null, null, new int[3] { 0, 255, 255 }, new int[3] { 90, 220, 200 }, null, null, 60), 
			"星辉比利-决心" => new AgentStateDef(stateDef.StateName, AgentStateCheckWay.FOREGROUND_COLOR_RANGE_LENGTH, "starlight_billy_kid", null, null, new int[3] { 0, 255, 255 }, new int[3] { 90, 220, 200 }, null, null, 120), 
			"诺姆-预热" => new AgentStateDef(stateDef.StateName, AgentStateCheckWay.FOREGROUND_COLOR_RANGE_LENGTH, "norma", null, null, new int[3] { 0, 255, 255 }, new int[3] { 90, 220, 200 }), 
			_ => stateDef, 
		};
		if (1 == 0)
		{
		}
		return result;
	}
}
