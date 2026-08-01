using System;
using System.IO;
using System.Security.Cryptography;
using Xunit;

namespace ZzzOd.GameLogic.Tests.Audit;

[Trait("Category", "UpstreamSync")]
public sealed class UpstreamRangeResourceContractTests
{
	private static readonly ResourceHash[] AutoBattleResources =
	[
		new("config/auto_battle_state_handler/速切模板-柏妮思.sample.yml", "053F7234A191A1C0CFBE5C8EBC8255B94C0E697BB3B07428BE54B25CC5C76F10"),
		new("config/auto_battle_state_handler/速切模板-叶瞬光.sample.yml", "22C3A7B15525D4E9039773720AADE654DBD50140AEF6D58C05258653865DB63C"),
		new("config/auto_battle/击破站场-强攻速切.merged.yml", "1D1E4399467F0CA639F441C4AF55749993A0BE823F316C07BB252811E1A496E2"),
		new("config/auto_battle/强攻站场-击破支援速切.merged.yml", "3DB9199F68DDA058B68058CA89FD3A4C8DD1AF70B851225D42B2A18F086D37C8"),
		new("config/auto_battle/全配队通用.merged.yml", "F39A033E010734BECAED5E5C2B50B9DC41295FAA800B063B5E2C40C024BD1138"),
		new("config/auto_battle/自动守护.merged.yml", "F102CFBB20D05D1179C1A6EC5A53CED87D6801E6450BA84B129233937A15785F"),
		new("config/auto_battle_operation/柏妮思-短喷.sample.yml", "7FC68398005F60C1FDA7D6F6762CE0D48E282202ED9316B2EFEA1A3955630B93"),
	];

	private static readonly string[] DeletedBurniceOperations =
	[
		"config/auto_battle_operation/柏妮思-短按单双喷.sample.yml",
		"config/auto_battle_operation/柏妮思-短按接长按特殊攻击.sample.yml",
		"config/auto_battle_operation/柏妮思-长按特殊攻击直到能量用完.sample.yml",
		"config/auto_battle_operation/柏妮思-长长按普通攻击.sample.yml",
	];

	private static readonly ResourceHash[] ScreenAndTemplateResources =
	[
		new("assets/game_data/screen_info/_od_merged.yml", "47913F94233C4C9A92EAAD6256FCEB6B0EFD26B8E7186212A9331E57D6A3505B"),
		new("assets/template/hollow/avatar_norma/raw.png", "18038B78C0F3E94638460A1E1D49EBD760741458400B497589FD30D4830145F6"),
		new("assets/template/hollow/avatar_norma/mask.png", "5F562680464076662A086EF6E4DD356A3A5818359464F20AF4857FDC9AE0E8F6"),
		new("assets/template/predefined_team/avatar_norma/raw.png", "158DD6DC4541EA7D25FDD020726F4776139DDC46D21AC3816AB4BE0359B320DB"),
		new("assets/template/predefined_team/avatar_norma/mask.png", "CE8B39CB8BD5CC316EC54B25A6DAF93AEDA76B2458717BC8D78A48AE8065B79D"),
	];

	[Fact]
	public void AutoBattleResources_MatchAuditedVersionAndDeletedOperationsAreAbsent()
	{
		string workspaceRoot = FindWorkspaceRoot();
		AssertResources(workspaceRoot, AutoBattleResources);

		foreach (string relativePath in DeletedBurniceOperations)
		{
			Assert.False(File.Exists(Path.Combine(workspaceRoot, relativePath)), relativePath);
		}
	}

	[Fact]
	public void ScreenAndNormaTemplateResources_MatchAuditedVersion()
	{
		AssertResources(FindWorkspaceRoot(), ScreenAndTemplateResources);
	}

	private static void AssertResources(string workspaceRoot, ResourceHash[] resources)
	{
		foreach (ResourceHash resource in resources)
		{
			string path = Path.Combine(workspaceRoot, resource.RelativePath.Replace('/', Path.DirectorySeparatorChar));
			Assert.True(File.Exists(path), path);
			Assert.Equal(resource.Sha256, GetSha256(path));
		}
	}

	private static string FindWorkspaceRoot()
	{
		for (DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
		{
			if (Directory.Exists(Path.Combine(directory.FullName, "config"))
				&& Directory.Exists(Path.Combine(directory.FullName, "assets"))
				&& Directory.Exists(Path.Combine(directory.FullName, "zzzod-dotnet")))
			{
				return directory.FullName;
			}
		}

		throw new DirectoryNotFoundException("未找到 zzz-od-dotnet 工作区根目录。");
	}

	private static string GetSha256(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

	private sealed record ResourceHash(string RelativePath, string Sha256);
}
