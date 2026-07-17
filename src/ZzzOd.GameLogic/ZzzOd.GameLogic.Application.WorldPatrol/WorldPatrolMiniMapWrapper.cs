using System;
using System.Collections.Generic;
using OpenCvSharp;
using ZzzOd.GameLogic.Application.WorldPatrol.Operations;

namespace ZzzOd.GameLogic.Application.WorldPatrol;

/// <summary>
/// 锄大地小地图封装。
/// </summary>
public sealed class WorldPatrolMiniMapWrapper : IDisposable
{
	/// <summary>视野角度。</summary>
	public const int TotalViewAngle = 105;

	private readonly Lazy<Mat> _hsv;

	private readonly Lazy<Mat> _yuv;

	private readonly Lazy<Mat> _viewMask;

	private readonly Lazy<Mat> _playerMask;

	private readonly Lazy<Mat> _circleMask;

	private readonly Lazy<Mat> _roadMask;

	private readonly Lazy<double?> _viewAngle;

	/// <summary>RGB 小地图。</summary>
	public Mat Rgb { get; }

	/// <summary>HSV 图像。</summary>
	public Mat Hsv => _hsv.Value;

	/// <summary>YUV 图像。</summary>
	public Mat Yuv => _yuv.Value;

	/// <summary>视野 mask。</summary>
	public Mat ViewMask => _viewMask.Value;

	/// <summary>玩家 mask。</summary>
	public Mat PlayerMask => _playerMask.Value;

	/// <summary>圆形 mask。</summary>
	public Mat CircleMask => _circleMask.Value;

	/// <summary>道路 mask。</summary>
	public Mat RoadMask => _roadMask.Value;

	/// <summary>是否找到玩家标记。</summary>
	public bool PlayMaskFound => Cv2.CountNonZero(PlayerMask) > 50;

	/// <summary>视野朝向。</summary>
	public double? ViewAngle => _viewAngle.Value;

	/// <summary>
	/// 初始化小地图封装。
	/// </summary>
	public WorldPatrolMiniMapWrapper(Mat rgb)
	{
		Rgb = rgb.Clone();
		_hsv = new Lazy<Mat>(() => Convert(ColorConversionCodes.RGB2HSV));
		_yuv = new Lazy<Mat>(() => Convert(ColorConversionCodes.RGB2YUV));
		_viewMask = new Lazy<Mat>(BuildViewMask);
		_playerMask = new Lazy<Mat>(BuildPlayerMask);
		_circleMask = new Lazy<Mat>(BuildCircleMask);
		_roadMask = new Lazy<Mat>(BuildRoadMask);
		_viewAngle = new Lazy<double?>(() => WorldPatrolMiniMapAngleCalculator.Calculate(ViewMask));
	}

	/// <summary>
	/// 创建路线执行使用的小地图快照。
	/// </summary>
	public WorldPatrolMiniMapSnapshot ToSnapshot()
	{
		return new WorldPatrolMiniMapSnapshot(PlayMaskFound, ViewAngle, Rgb.Rows, RoadMask.Clone(), Rgb.Clone());
	}

	/// <summary>
	/// 将 .NET 截图链路的 BGR 小地图转成 BaselineParity 业务层使用的 RGB 小地图。
	/// </summary>
	public static Mat ConvertBgrToRgb(Mat bgr)
	{
		ArgumentNullException.ThrowIfNull(bgr, "bgr");
		Mat mat = new Mat();
		Cv2.CvtColor(bgr, mat, ColorConversionCodes.BGR2RGB);
		return mat;
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (_hsv.IsValueCreated)
		{
			_hsv.Value.Dispose();
		}
		if (_yuv.IsValueCreated)
		{
			_yuv.Value.Dispose();
		}
		if (_viewMask.IsValueCreated)
		{
			_viewMask.Value.Dispose();
		}
		if (_playerMask.IsValueCreated)
		{
			_playerMask.Value.Dispose();
		}
		if (_circleMask.IsValueCreated)
		{
			_circleMask.Value.Dispose();
		}
		if (_roadMask.IsValueCreated)
		{
			_roadMask.Value.Dispose();
		}
		Rgb.Dispose();
	}

	private Mat Convert(ColorConversionCodes code)
	{
		Mat mat = new Mat();
		Cv2.CvtColor(Rgb, mat, code);
		return mat;
	}

	private Mat BuildViewMask()
	{
		Mat[] array = Cv2.Split(Yuv);
		try
		{
			using Mat mat = new Mat();
			using Mat mat2 = new Mat();
			Cv2.InRange(array[1], new Scalar(100.0), new Scalar(110.0), mat);
			Cv2.InRange(array[2], new Scalar(140.0), new Scalar(150.0), mat2);
			Mat mat3 = new Mat();
			Cv2.BitwiseOr(mat, mat2, mat3);
			MorphAndBlur(mat3);
			return mat3;
		}
		finally
		{
			Mat[] array2 = array;
			foreach (Mat mat4 in array2)
			{
				mat4.Dispose();
			}
		}
	}

	private Mat BuildRoadMask()
	{
		Mat mat = new Mat();
		Cv2.InRange(Rgb, new Scalar(0.0, 0.0, 0.0), new Scalar(40.0, 40.0, 40.0), mat);
		for (int i = 0; i < Rgb.Rows; i++)
		{
			for (int j = 0; j < Rgb.Cols; j++)
			{
				Vec3b vec3b = Rgb.At<Vec3b>(i, j);
				byte b = Math.Max(vec3b.Item0, Math.Max(vec3b.Item1, vec3b.Item2));
				byte b2 = Math.Min(vec3b.Item0, Math.Min(vec3b.Item1, vec3b.Item2));
				if (b - b2 > 5)
				{
					mat.Set(i, j, (byte)0);
				}
			}
		}
		Cv2.BitwiseAnd(mat, CircleMask, mat);
		Cv2.BitwiseOr(mat, PlayerMask, mat);
		Morph(mat);
		return mat;
	}

	private Mat BuildPlayerMask()
	{
		using Mat mat = new Mat();
		Cv2.InRange(Hsv, new Scalar(14.0, 90.0, 175.0), new Scalar(34.0, 255.0, 255.0), mat);
		Cv2.FindContours(mat, out Point[][] contours, out HierarchyIndex[] _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
		Mat mat2 = new Mat(Rgb.Rows, Rgb.Cols, MatType.CV_8UC1, Scalar.Black);
		List<Point[]> list = new List<Point[]>();
		Point[][] array = contours;
		foreach (Point[] array2 in array)
		{
			double num = Cv2.ArcLength(array2, closed: true);
			if ((!(num < 60.0) && !(num > 70.0)) || 1 == 0)
			{
				double num2 = Cv2.ContourArea(array2);
				if ((!(num2 < 240.0) && !(num2 > 300.0)) || 1 == 0)
				{
					list.Add(array2);
				}
			}
		}
		if (list.Count > 0)
		{
			Cv2.DrawContours(mat2, list, -1, Scalar.White, -1);
		}
		return mat2;
	}

	private Mat BuildCircleMask()
	{
		Mat mat = new Mat(Rgb.Rows, Rgb.Cols, MatType.CV_8UC1, Scalar.Black);
		int num = Rgb.Rows / 2;
		Cv2.Circle(mat, new Point(num, num), Math.Max(0, num - 7), Scalar.White, -1);
		Cv2.Circle(mat, new Point(207, 189), 50, Scalar.Black, -1);
		return mat;
	}

	private static void MorphAndBlur(Mat mask)
	{
		Morph(mask);
		Cv2.GaussianBlur(mask, mask, new Size(3, 3), 0.0);
	}

	private static void Morph(Mat mask)
	{
		using Mat mat = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
		Cv2.MorphologyEx(mask, mask, MorphTypes.Close, mat);
		Cv2.MorphologyEx(mask, mask, MorphTypes.Open, mat);
	}
}
