using OneDragon.Core.Screen;
using Xunit;
using ZzzOd.GameLogic.ScreenArea;

namespace ZzzOd.GameLogic.Tests.ScreenArea;

public class ScreenNormalWorldEnumTests
{
	[Fact]
	public void Uid_AreaMatchesPythonDefinition()
	{
		OneDragon.Core.Screen.ScreenArea uid = ScreenNormalWorldEnum.Uid;
		Assert.IsType<OneDragon.Core.Screen.ScreenArea>(uid);
		Assert.Equal("uid", uid.AreaName);
		Assert.Equal(1814, uid.X1);
		Assert.Equal(1059, uid.Y1);
		Assert.Equal(1919, uid.X2);
		Assert.Equal(1079, uid.Y2);
		Assert.Equal(105, uid.Width);
		Assert.Equal(20, uid.Height);
	}
}
