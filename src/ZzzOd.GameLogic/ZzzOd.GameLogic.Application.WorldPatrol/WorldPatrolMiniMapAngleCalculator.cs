using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;

namespace ZzzOd.GameLogic.Application.WorldPatrol;

/// <summary>
/// 小地图视野角度计算。
/// </summary>
public static class WorldPatrolMiniMapAngleCalculator
{
	/// <summary>
	/// 计算视野朝向，正右为 0，逆时针增加。
	/// </summary>
	public static double? Calculate(Mat viewMask, int viewAngle = 105)
	{
		ArgumentNullException.ThrowIfNull(viewMask, "viewMask");
		int rows = viewMask.Rows;
		using Mat mat = new Mat(rows, 360, MatType.CV_32FC1);
		using Mat mat2 = new Mat(rows, 360, MatType.CV_32FC1);
		FillPolarRemapMaps(mat, mat2);
		using Mat mat3 = new Mat();
		Cv2.Remap(viewMask, mat3, mat, mat2);
		int num = (int)((double)rows * 0.2);
		int num2 = (int)((double)rows * 0.4);
		using Mat mat4 = new Mat(mat3, new Rect(0, num, mat3.Cols, Math.Max(1, num2 - num)));
		using Mat mat5 = new Mat();
		mat4.ConvertTo(mat5, MatType.CV_32FC1);
		using Mat mat6 = new Mat();
		Cv2.Scharr(mat5, mat6, MatType.CV_32FC1, 1, 0);
		double[] array = FlattenFloatMat(mat6);
		double[] array2 = CreateAngularHistogramFromPeaks(array, 360);
		double[] array3 = CreateAngularHistogramFromPeaks(array.Select((double value) => 0.0 - value).ToArray(), 360);
		double[] array4 = new double[360];
		double[] array5 = new double[360];
		for (int num3 = 0; num3 < array4.Length; num3++)
		{
			array4[num3] = Math.Max(array2[num3] - array3[num3], 0.0);
			array5[num3] = Math.Max(array3[num3] - array2[num3], 0.0);
		}
		int num4 = 2;
		double[] values = Convolve(array5, 3 * num4);
		int num5 = 360 * viewAngle / 360;
		List<double[]> list = new List<double[]>();
		for (int num6 = -num4 + 1; num6 < num4; num6++)
		{
			double[] array6 = Roll(values, -num5 + num6);
			double[] array7 = new double[array4.Length];
			for (int num7 = 0; num7 < array4.Length; num7++)
			{
				array7[num7] = array4[num7] * array6[num7];
			}
			list.Add(array7);
		}
		double[] array8 = new double[array4.Length];
		int index;
		for (index = 0; index < array8.Length; index++)
		{
			array8[index] = Math.Max(list.Max((double[] result) => result[index]), 1.0);
		}
		double num8 = Math.Round(PeakConfidence(array8), 3);
		double[] array9 = array8;
		if (num8 <= 0.0)
		{
			double[] array10 = new double[array8.Length];
			int index2;
			for (index2 = 0; index2 < array8.Length; index2++)
			{
				double num9 = list.Average((double[] item) => Math.Max(item[index2], 1.0));
				double num10 = list.Min((double[] item) => Math.Max(item[index2], 1.0));
				array10[index2] = array8[index2] * num9 * num10;
			}
			array9 = Convolve(array10, 2);
			num8 = Math.Round(PeakConfidence(array8), 3);
		}
		if (num8 <= 0.0)
		{
			return null;
		}
		int num11 = 0;
		for (int num12 = 1; num12 < array9.Length; num12++)
		{
			if (array9[num12] > array9[num11])
			{
				num11 = num12;
			}
		}
		return NormalizeAngle((double)num11 * 360.0 / 360.0 + (double)viewAngle / 2.0);
	}

	private static void FillPolarRemapMaps(Mat mapX, Mat mapY)
	{
		int rows = mapX.Rows;
		int cols = mapX.Cols;
		double num = (double)rows / 2.0;
		for (int i = 0; i < rows; i++)
		{
			double num2 = (double)i / 2.0;
			for (int j = 0; j < cols; j++)
			{
				double num3 = Math.PI * 2.0 * (double)j / (double)cols;
				mapX.Set(i, j, (float)(num + num2 * Math.Cos(num3)));
				mapY.Set(i, j, (float)(num - num2 * Math.Sin(num3)));
			}
		}
	}

	private static double[] FlattenFloatMat(Mat mat)
	{
		double[] array = new double[mat.Rows * mat.Cols];
		int num = 0;
		for (int i = 0; i < mat.Rows; i++)
		{
			for (int j = 0; j < mat.Cols; j++)
			{
				array[num++] = mat.At<float>(i, j);
			}
		}
		return array;
	}

	private static double[] CreateAngularHistogramFromPeaks(double[] gradientSignal, int angularResolution)
	{
		double[] array = new double[angularResolution];
		foreach (int item in FindPeaks(gradientSignal, 35.0))
		{
			array[item % angularResolution] += 1.0;
		}
		return array;
	}

	private static IReadOnlyList<int> FindPeaks(double[] values, double height, double prominence = 0.0)
	{
		List<int> list = new List<int>();
		for (int i = 1; i < values.Length - 1; i++)
		{
			double num = values[i];
			if (!(num < height) && !(num <= values[i - 1]) && !(num <= values[i + 1]) && (!(prominence > 0.0) || !(GetProminence(values, i) < prominence)))
			{
				list.Add(i);
			}
		}
		return list;
	}

	/// <summary>
	/// 对齐 scipy.signal.find_peaks 的 prominence 判定：从峰值向两侧搜索，直到遇到更高峰或数组边界，
	/// 再以两侧最低点中较高的一侧作为基线。
	/// </summary>
	private static double GetProminence(IReadOnlyList<double> values, int peakIndex)
	{
		double num = values[peakIndex];
		double val = num;
		int num2 = peakIndex - 1;
		while (num2 >= 0 && !(values[num2] > num))
		{
			val = Math.Min(val, values[num2]);
			num2--;
		}
		double num3 = num;
		for (int i = peakIndex + 1; i < values.Count && !(values[i] > num); i++)
		{
			num3 = Math.Min(num3, values[i]);
		}
		return num - Math.Max(val, num3);
	}

	private static double PeakConfidence(double[] values)
	{
		int num = values.Length;
		double[] array = values.Concat(values).Concat(values).ToArray();
		List<double> list = new List<double>();
		foreach (int item in FindPeaks(array, 0.0, 5.0))
		{
			if (item >= num && item < 2 * num)
			{
				list.Add(array[item]);
			}
		}
		if (list.Count < 2)
		{
			return (list.Count == 1) ? 1.0 : 0.0;
		}
		double[] array2 = list.OrderBy((double value) => value).ToArray();
		double num2 = array2[^1];
		double num3 = array2[^2];
		return (num2 == 0.0) ? 0.0 : ((num2 - num3) / num2);
	}

	private static double[] Convolve(double[] values, int kernel)
	{
		double[] array = new double[values.Length];
		for (int i = -kernel + 1; i < kernel; i++)
		{
			double num = (double)(kernel - Math.Abs(i)) / (double)kernel;
			for (int j = 0; j < values.Length; j++)
			{
				int num2 = j - i;
				if (num2 >= 0 && num2 < values.Length)
				{
					array[j] += values[num2] * num;
				}
			}
		}
		return array;
	}

	private static double[] Roll(double[] values, int shift)
	{
		double[] array = new double[values.Length];
		int num = values.Length;
		for (int i = 0; i < values.Length; i++)
		{
			int num2 = ((i + shift) % num + num) % num;
			array[num2] = values[i];
		}
		return array;
	}

	private static double NormalizeAngle(double angle)
	{
		while (angle >= 360.0)
		{
			angle -= 360.0;
		}
		while (angle < 0.0)
		{
			angle += 360.0;
		}
		return angle;
	}
}
