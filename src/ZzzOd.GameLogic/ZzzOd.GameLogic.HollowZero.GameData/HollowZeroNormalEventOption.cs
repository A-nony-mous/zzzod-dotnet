namespace ZzzOd.GameLogic.HollowZero.GameData;

public class HollowZeroNormalEventOption
{
	public string OptionName { get; set; }

	public string? Desc { get; set; }

	public float Wait { get; set; }

	public string OcrWord { get; set; }

	public float LcsPercent { get; set; }

	public HollowZeroNormalEventOption(string optionName, string? desc = null, float wait = 1f, string? ocrWord = null, float lcsPercent = 0.5f)
	{
		OptionName = optionName;
		Desc = desc;
		Wait = wait;
		OcrWord = ocrWord ?? optionName;
		LcsPercent = lcsPercent;
	}
}
