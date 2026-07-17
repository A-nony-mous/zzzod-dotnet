using System.Collections.Generic;

namespace ZzzOd.AppHost.Devtools;

public interface IZzzImageAnalysisService
{
	IReadOnlyList<string> GetPipelineNames();

	IReadOnlyList<ImageAnalysisStepDefinition> GetAvailableSteps();

	IReadOnlyList<string> GetTemplateNames();

	IReadOnlyList<string> GetScreenNames();

	IReadOnlyList<string> GetAreaNames(string screenName);

	ImageAnalysisPipeline LoadPipeline(string name);

	void SavePipeline(string name, ImageAnalysisPipeline pipeline);

	void RenamePipeline(string oldName, string newName);

	void DeletePipeline(string name);

	ImageAnalysisExecutionResult Execute(ImageAnalysisPipeline pipeline, byte[] imageBytes);

	ImageAnalysisColorChannels GetColorChannels(byte[] imageBytes);
}
