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
		new("config/auto_battle/击破站场-强攻速切.merged.yml", "E521B0BDCA872F38E04DC9B81E5E81781B8EDC7D2F3C7057CA183BCEADA3B940"),
		new("config/auto_battle/强攻站场-击破支援速切.merged.yml", "0FB051FB6FB8132D3992D343AEE58B0DFC7D9906D3BD039D28FAB2FCC8F6C44D"),
		new("config/auto_battle/全配队通用.merged.yml", "2AC30EF3C43563549A11D0FA6C8281F020DE258B973C371EDDA5FC66353F0D37"),
		new("config/auto_battle/自动守护.merged.yml", "FE6B44296A94D4741A1FC921AE30B1776A1C181ED558E927FF1011E48332F6C4"),
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
		new("assets/game_data/screen_info/_od_merged.yml", "2BAB46FF1048E9FDD9886EA5B4420AD7137ADDD638F6C9D7DCA26A088D04AB76"),
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
