using System.Collections.Generic;

namespace ZzzOd.AppHost.Devtools;

public interface IZzzScreenManageService
{
	IReadOnlyList<string> ListScreenNames();

	ZzzScreenDocument LoadScreen(string screenName);

	void SaveScreen(ZzzScreenDocument screen);

	void DeleteScreen(string screenId);

	void RebuildMergedConfig();

	byte[] ReadImage(string filePath);

	byte[] CaptureScreenshot();

	ZzzImportedTemplateArea ImportTemplateArea(string templateConfigPath);
}
