using System.ComponentModel;

namespace ZzzOd.GameLogic.AutoBattle;

public enum YoloStateEventEnum
{
	[Description("闪避识别-黄光")]
	DODGE_YELLOW,
	[Description("闪避识别-红光")]
	DODGE_RED,
	[Description("闪避识别-声音")]
	DODGE_AUDIO
}
