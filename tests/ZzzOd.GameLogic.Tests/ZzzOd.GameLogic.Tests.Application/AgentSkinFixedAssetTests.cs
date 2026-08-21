using System;
using System.IO;
using OneDragon.Core.Runtime;
using OneDragon.Core.Template;
using Xunit;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.GameData;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Application;

/// <summary>
/// 角色皮肤模板资产合同：露西、维琳娜、蕾米埃尔的全部启用别名必须能被生产模板索引
/// 在 battle 前后排/连携/快速支援、hollow、predefined_team 场景中发现并加载图像，
/// 且角色枚举的模板 ID 列表与 v2.5.1 参考基线一致。
/// </summary>
public sealed class AgentSkinFixedAssetTests
{
	[Trait("Category", "Integration")]
	[Fact]
	public void ProductionTemplateIndex_DiscoverAllSkinAliasesAcrossScenes()
	{
		OpenCvTestRuntime.RequireAvailable();
		string workspaceRoot = FindWorkspaceRoot();
		string runRoot = CreateRunRoot(workspaceRoot);
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(runRoot, workspaceRoot));
			string[] aliases = new string[8]
			{
				"velina", "velina_shade_of_leisure",
				"lucy", "lucy_princess_on_holiday",
				"remielle", "remielle_dark", "remielle_dark_veil", "remielle_seashade",
			};
			foreach (string alias in aliases)
			{
				string[] battleTemplateIds = new string[4]
				{
					"avatar_1_" + alias, "avatar_2_" + alias, "avatar_chain_" + alias, "avatar_quick_" + alias,
				};
				foreach (string battleTemplateId in battleTemplateIds)
				{
					AssertTemplateLoads(zContext, "battle", battleTemplateId);
				}
				AssertTemplateLoads(zContext, "hollow", "avatar_" + alias);
				AssertTemplateLoads(zContext, "predefined_team", "avatar_" + alias);
			}
		}
		finally
		{
			Directory.Delete(runRoot, recursive: true);
		}
	}

	[Fact]
	public void AgentEnumTemplateIds_MatchReferenceBaseline()
	{
		Assert.Equal(new string[2] { "lucy", "lucy_princess_on_holiday" }, AgentEnum.LUCY.Value.TemplateIdList);
		Assert.Equal(new string[2] { "velina", "velina_shade_of_leisure" }, AgentEnum.VELINA.Value.TemplateIdList);
		Assert.Equal(
			new string[4] { "remielle", "remielle_dark", "remielle_dark_veil", "remielle_seashade" },
			AgentEnum.REMIELLE.Value.TemplateIdList);
	}

	private static void AssertTemplateLoads(ZContext zContext, string subDir, string templateId)
	{
		TemplateInfo? template = zContext.TemplateLoader.GetTemplate(subDir, templateId);
		Assert.True(template != null, $"{subDir}/{templateId} 必须被生产模板索引发现");
		using (template)
		{
			Assert.False(template.Raw?.Empty() ?? true, $"{subDir}/{templateId} 的 raw.png 必须可加载");
			Assert.False(template.Mask?.Empty() ?? true, $"{subDir}/{templateId} 的 mask.png 必须可加载");
		}
	}

	private static string FindWorkspaceRoot()
	{
		for (DirectoryInfo directoryInfo = new DirectoryInfo(AppContext.BaseDirectory); directoryInfo != null; directoryInfo = directoryInfo.Parent)
		{
			if (Directory.Exists(Path.Combine(directoryInfo.FullName, "assets")) && Directory.Exists(Path.Combine(directoryInfo.FullName, "zzzod-dotnet")))
			{
				return directoryInfo.FullName;
			}
		}
		throw new DirectoryNotFoundException("未找到 zzz-od-dotnet 工作区根目录。");
	}

	private static string CreateRunRoot(string workspaceRoot)
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-agent-skin-fixed-assets", Guid.NewGuid().ToString("N"));
		CopyDirectory(Path.Combine(workspaceRoot, "config"), Path.Combine(text, "config"));
		return text;
	}

	private static void CopyDirectory(string sourceDirectory, string targetDirectory)
	{
		foreach (string item in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
		{
			string relativePath = Path.GetRelativePath(sourceDirectory, item);
			string text = Path.Combine(targetDirectory, relativePath);
			Directory.CreateDirectory(Path.GetDirectoryName(text));
			File.Copy(item, text, overwrite: true);
		}
	}
}
