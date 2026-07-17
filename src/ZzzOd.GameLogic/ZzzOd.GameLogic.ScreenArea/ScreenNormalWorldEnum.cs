using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Screen;

namespace ZzzOd.GameLogic.ScreenArea;

/// <summary>
/// 普通大世界画面区域。
/// </summary>
public static class ScreenNormalWorldEnum
{
	/// <summary>
	/// UID 显示区域，右下角。
	/// </summary>
	public static readonly OneDragon.Core.Screen.ScreenArea Uid = new OneDragon.Core.Screen.ScreenArea
	{
		AreaName = "uid",
		PcRect = new Rect(1814, 1059, 1919, 1079)
	};
}
