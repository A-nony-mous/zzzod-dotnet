using OneDragon.Core.Ocr;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Yolo;

/// <summary>
/// 业务 OCR 配置封装。
/// </summary>
public sealed class ZzzOcrService
{
	public OcrModelResolution Resolution { get; }

	public string ProfileId => Resolution.Profile.Id;

	public bool IsShutdown { get; private set; }

	public ZzzOcrService(ZContext context)
	{
		Resolution = context.ModelConfig.ResolveOcrProfile();
	}

	public void Shutdown()
	{
		IsShutdown = true;
	}
}
