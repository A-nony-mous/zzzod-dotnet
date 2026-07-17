using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Matcher;
using OneDragon.Core.Runtime;
using OneDragon.Core.Utils;
using OpenCvSharp;
using Xunit;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.GameData;
using ZzzOd.GameLogic.Operations;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Operations;

public sealed class AgentTemplateMatcherTests : IDisposable
{
	private readonly string _rootDirectory;

	public AgentTemplateMatcherTests()
	{
		_rootDirectory = Path.Combine(Path.GetTempPath(), "zzzod-agent-template-tests", Guid.NewGuid().ToString("N"));
		string[] buffer = new string[5];
		buffer[0] = _rootDirectory;
		buffer[1] = "assets";
		buffer[2] = "template";
		buffer[3] = "predefined_team";
		buffer[4] = "avatar_anby";
		Directory.CreateDirectory(Path.Combine(buffer));
	}

	[Fact]
	public void MatchTeamAgentTemplate_ReturnsAgentMatchesWithSearchRectOffset()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		using Mat mat = CreateFeatureRichAgentTemplate();
		string[] buffer = new string[6];
		buffer[0] = _rootDirectory;
		buffer[1] = "assets";
		buffer[2] = "template";
		buffer[3] = "predefined_team";
		buffer[4] = "avatar_anby";
		buffer[5] = "raw.png";
		CvImageUtils.SaveImage(mat, Path.Combine(buffer));
		using Mat image = new Mat(mat.Size(), MatType.CV_8UC1, Scalar.White);
		string[] buffer2 = new string[6];
		buffer2[0] = _rootDirectory;
		buffer2[1] = "assets";
		buffer2[2] = "template";
		buffer2[3] = "predefined_team";
		buffer2[4] = "avatar_anby";
		buffer2[5] = "mask.png";
		CvImageUtils.SaveImage(image, Path.Combine(buffer2));
		using ZContext zContext = new ZContext(new OneDragonEnvironment(_rootDirectory, _rootDirectory));
		using Mat screen = CreateScreenWithTemplate(mat, 42, 37);
		OneDragon.Core.Abstractions.Geometry.Rect rect = new OneDragon.Core.Abstractions.Geometry.Rect(30, 25, 150, 145);
		IReadOnlyList<MatchResult> collection = AgentTemplateMatcher.MatchTeamAgentTemplate(zContext, screen, rect, new string[] { "anby" });
		IReadOnlyList<MatchResult> collection2 = AgentTemplateMatcher.MatchTeamAgentTemplate(zContext, screen, rect, new string[] { "anby" });
		MatchResult matchResult = Assert.Single(collection);
		Assert.Single(collection2);
		Agent agent = Assert.IsType<Agent>(matchResult.Data);
		Assert.Equal("anby", agent.AgentId);
		Assert.InRange(matchResult.X, 35, 50);
		Assert.InRange(matchResult.Y, 30, 45);
		Assert.InRange(matchResult.Width, mat.Width - 2, mat.Width + 2);
		Assert.InRange(matchResult.Height, mat.Height - 2, mat.Height + 2);
		Assert.False(zContext.TemplateLoader.GetTemplate("predefined_team", "avatar_anby").Mask.IsDisposed);
	}

	[Fact]
	public void MatchTeamAgentTemplate_RespectsAgentFilter()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		using Mat mat = CreateFeatureRichAgentTemplate();
		string[] buffer = new string[6];
		buffer[0] = _rootDirectory;
		buffer[1] = "assets";
		buffer[2] = "template";
		buffer[3] = "predefined_team";
		buffer[4] = "avatar_anby";
		buffer[5] = "raw.png";
		CvImageUtils.SaveImage(mat, Path.Combine(buffer));
		using ZContext context = new ZContext(new OneDragonEnvironment(_rootDirectory, _rootDirectory));
		using Mat screen = CreateScreenWithTemplate(mat, 20, 20);
		OneDragon.Core.Abstractions.Geometry.Rect rect = new OneDragon.Core.Abstractions.Geometry.Rect(0, 0, 120, 120);
		IReadOnlyList<MatchResult> collection = AgentTemplateMatcher.MatchTeamAgentTemplate(context, screen, rect, new string[] { "nicole" });
		Assert.Empty(collection);
	}

	[Fact]
	public void MatchTeamAgentTemplate_MissingTemplatesReturnEmptyList()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		using ZContext context = new ZContext(new OneDragonEnvironment(_rootDirectory, _rootDirectory));
		using Mat screen = new Mat(120, 120, MatType.CV_8UC3, Scalar.Black);
		IReadOnlyList<MatchResult> collection = AgentTemplateMatcher.MatchTeamAgentTemplate(context, screen, new OneDragon.Core.Abstractions.Geometry.Rect(0, 0, 120, 120), new string[] { "anby" });
		Assert.Empty(collection);
	}

	public void Dispose()
	{
		if (Directory.Exists(_rootDirectory))
		{
			Directory.Delete(_rootDirectory, recursive: true);
		}
	}

	private static Mat CreateFeatureRichAgentTemplate()
	{
		Mat mat = new Mat(new Size(64, 64), MatType.CV_8UC3, Scalar.Black);
		Cv2.Rectangle(mat, new OpenCvSharp.Rect(4, 4, 24, 18), new Scalar(255.0, 255.0, 255.0), -1);
		Cv2.Circle(mat, new OpenCvSharp.Point(45, 18), 11, new Scalar(40.0, 220.0, 255.0), -1);
		Cv2.Line(mat, new OpenCvSharp.Point(8, 52), new OpenCvSharp.Point(56, 40), new Scalar(255.0, 80.0, 30.0), 4);
		Cv2.PutText(mat, "A7", new OpenCvSharp.Point(9, 36), HersheyFonts.HersheySimplex, 0.8, new Scalar(150.0, 255.0, 80.0), 2);
		for (int i = 0; i < 12; i++)
		{
			int x = 5 + i * 17 % 54;
			int y = 5 + i * 23 % 54;
			Cv2.Circle(mat, new OpenCvSharp.Point(x, y), 2, new Scalar(i * 30 % 255, i * 70 % 255, i * 110 % 255), -1);
		}
		return mat;
	}

	private static Mat CreateScreenWithTemplate(Mat template, int x, int y)
	{
		Mat mat = new Mat(new Size(180, 160), MatType.CV_8UC3, new Scalar(15.0, 20.0, 25.0));
		using Mat m = new Mat(mat, new OpenCvSharp.Rect(x, y, template.Width, template.Height));
		template.CopyTo(m);
		return mat;
	}

	private static bool CanUseOpenCv()
	{
		OpenCvTestRuntime.RequireAvailable();
		return true;
	}
}
