using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging.Abstractions;
using OneDragon.Core.Runtime;
using Xunit;
using ZzzOd.AppHost;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.RedemptionCode;
using ZzzOd.Gui.Pages.ApplicationSettings;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class RedemptionCodeAppSettingPageTests
{
	private sealed class BackendSession : IDisposable
	{
		private readonly ZzzRuntimeManager _runtime;

		private readonly ZzzBattleAssistantRuntimeSource _battleAssistantRuntimeSource;

		private readonly ZzzLogFanOutLoggerProvider _logProvider;

		public ZzzAppBackend Backend { get; }

		public BackendSession(string runRoot)
		{
			_runtime = new ZzzRuntimeManager(runRoot, NullLogger<ZzzRuntimeManager>.Instance);
			ZzzBackendEventBus eventBus = new ZzzBackendEventBus();
			_battleAssistantRuntimeSource = new ZzzBattleAssistantRuntimeSource();
			_logProvider = new ZzzLogFanOutLoggerProvider(new ZzzRunRoot(runRoot), eventBus);
			Backend = new ZzzAppBackend(_runtime, eventBus, _battleAssistantRuntimeSource, _logProvider, new ZzzHostModeOptions(ZzzHostMode.Gui), new ZzzApiOptions(), NullLogger<ZzzAppBackend>.Instance);
		}

		public void Dispose()
		{
			_runtime.Dispose();
			_battleAssistantRuntimeSource.Dispose();
			_logProvider.Dispose();
		}
	}

	[Fact]
	public void PageUsesAxamlFluentRowsAndPythonTexts()
	{
		string path = FindDirectory();
		string text = File.ReadAllText(Path.Combine(path, "ZzzRedemptionCodeAppSettingPage.axaml"));
		string actualString = File.ReadAllText(Path.Combine(path, "ZzzRedemptionCodeAppSettingPage.axaml.cs"));
		AssertOrder(text, "兑换码", "过期日期", "Delete", "新增");
		Assert.Contains("fa:FASettingsExpanderItem", text, StringComparison.Ordinal);
		Assert.Contains("fa:FAInfoBar", text, StringComparison.Ordinal);
		Assert.Contains("fa:FASymbolIcon", text, StringComparison.Ordinal);
		Assert.Contains("请输入兑换码", actualString, StringComparison.Ordinal);
		Assert.Contains("20990101", actualString, StringComparison.Ordinal);
		Assert.Contains("DateTime.Now", actualString, StringComparison.Ordinal);
		Assert.Contains("AddDays(30)", actualString, StringComparison.Ordinal);
		Assert.Contains("兑换码已存在", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("ZENLESSGIFT", text, StringComparison.Ordinal);
		Assert.DoesNotContain("USERCODE", text, StringComparison.Ordinal);
		Assert.DoesNotContain("new StackPanel", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("PageModel", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("Python", text, StringComparison.Ordinal);
	}

	[Fact]
	public void BackendMergesSampleAndUserInPythonOrderAndPersistsUserCrudOnly()
	{
		string text = CreateRunRootWithCodes();
		try
		{
			string path = Path.Combine(text, "config", "redemption_codes.sample.yml");
			string expected = File.ReadAllText(path);
			using BackendSession backendSession = new BackendSession(text);
			ZzzBackendResult<IReadOnlyList<ZzzRedemptionCodeDto>> redemptionCodes = backendSession.Backend.GetRedemptionCodes();
			Assert.True(redemptionCodes.Success, redemptionCodes.Error);
			Assert.Collection(redemptionCodes.Value, delegate(ZzzRedemptionCodeDto row)
			{
				Assert.Equal(new ZzzRedemptionCodeDto("SAMPLE_ONLY", 20990101, ReadOnly: true), row);
			}, delegate(ZzzRedemptionCodeDto row)
			{
				Assert.Equal(new ZzzRedemptionCodeDto("OVERRIDE", 20991231, ReadOnly: false), row);
			}, delegate(ZzzRedemptionCodeDto row)
			{
				Assert.Equal(new ZzzRedemptionCodeDto("USER1", 20990102, ReadOnly: false), row);
			});
			ZzzBackendResult<IReadOnlyList<ZzzRedemptionCodeDto>> zzzBackendResult = backendSession.Backend.AddRedemptionCode(" SAMPLE_ONLY ", 20990103);
			Assert.False(zzzBackendResult.Success);
			Assert.Equal(ZzzBackendErrorCode.Validation, zzzBackendResult.ErrorCode);
			Assert.Equal("兑换码已存在", zzzBackendResult.Error);
			Assert.True(backendSession.Backend.AddRedemptionCode(" USER2 ", 20990103).Success);
			Assert.True(backendSession.Backend.UpdateRedemptionCode("USER2", "USER3", 20990104).Success);
			ZzzBackendResult<IReadOnlyList<ZzzRedemptionCodeDto>> zzzBackendResult2 = backendSession.Backend.DeleteRedemptionCode("SAMPLE_ONLY");
			Assert.False(zzzBackendResult2.Success);
			Assert.Equal(ZzzBackendErrorCode.NotFound, zzzBackendResult2.ErrorCode);
			Assert.True(backendSession.Backend.DeleteRedemptionCode("USER1").Success);
			RedemptionCodeConfig redemptionCodeConfig = new RedemptionCodeConfig(new OneDragonEnvironment(text));
			Assert.Equal(20991231, redemptionCodeConfig.UserCodesDict["OVERRIDE"]);
			Assert.Equal(20990104, redemptionCodeConfig.UserCodesDict["USER3"]);
			Assert.DoesNotContain("USER1", redemptionCodeConfig.UserCodesDict.Keys);
			Assert.DoesNotContain("USER2", redemptionCodeConfig.UserCodesDict.Keys);
			Assert.Equal(expected, File.ReadAllText(path));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void PageUsesRealBackendForNewUpdateDeleteAndKeepsSampleReadonly()
	{
		string text = CreateRunRootWithCodes();
		try
		{
			BackendSession session = new BackendSession(text);
			try
			{
				GuiParityAndFacadeTests.RunOnUiThread(delegate
				{
					ZzzRedemptionCodeAppSettingPage zzzRedemptionCodeAppSettingPage = new ZzzRedemptionCodeAppSettingPage(session.Backend);
					Assert.Collection(zzzRedemptionCodeAppSettingPage.Rows, delegate(ZzzRedemptionCodeRowModel zzzRedemptionCodeRowModel3)
					{
						Assert.Equal("SAMPLE_ONLY", zzzRedemptionCodeRowModel3.Code);
						Assert.True(zzzRedemptionCodeRowModel3.IsReadOnly);
						Assert.False(zzzRedemptionCodeRowModel3.CanDelete);
					}, delegate(ZzzRedemptionCodeRowModel zzzRedemptionCodeRowModel3)
					{
						Assert.Equal("OVERRIDE", zzzRedemptionCodeRowModel3.Code);
					}, delegate(ZzzRedemptionCodeRowModel zzzRedemptionCodeRowModel3)
					{
						Assert.Equal("USER1", zzzRedemptionCodeRowModel3.Code);
					});
					ZzzRedemptionCodeRowModel zzzRedemptionCodeRowModel = zzzRedemptionCodeAppSettingPage.AddRowForTest();
					Assert.True(zzzRedemptionCodeRowModel.IsNew);
					Assert.Equal(ZzzRedemptionCodeAppSettingPage.CreateDefaultEndDate(DateTime.Today), int.Parse(zzzRedemptionCodeRowModel.EndDateText, CultureInfo.InvariantCulture));
					zzzRedemptionCodeRowModel.Code = "USER2";
					zzzRedemptionCodeRowModel.EndDateText = "20990103";
					zzzRedemptionCodeAppSettingPage.CommitRowForTest(zzzRedemptionCodeRowModel);
					ZzzRedemptionCodeRowModel zzzRedemptionCodeRowModel2 = Assert.Single(zzzRedemptionCodeAppSettingPage.Rows, (ZzzRedemptionCodeRowModel zzzRedemptionCodeRowModel3) => zzzRedemptionCodeRowModel3.Code == "USER2");
					zzzRedemptionCodeRowModel2.Code = "USER3";
					zzzRedemptionCodeRowModel2.EndDateText = "20990104";
					zzzRedemptionCodeAppSettingPage.CommitRowForTest(zzzRedemptionCodeRowModel2);
					ZzzRedemptionCodeRowModel row = Assert.Single(zzzRedemptionCodeAppSettingPage.Rows, (ZzzRedemptionCodeRowModel zzzRedemptionCodeRowModel3) => zzzRedemptionCodeRowModel3.Code == "USER3");
					zzzRedemptionCodeAppSettingPage.DeleteRowForTest(row);
					Assert.DoesNotContain((IEnumerable<ZzzRedemptionCodeRowModel>)zzzRedemptionCodeAppSettingPage.Rows, (Predicate<ZzzRedemptionCodeRowModel>)((ZzzRedemptionCodeRowModel zzzRedemptionCodeRowModel3) => zzzRedemptionCodeRowModel3.Code == "USER3"));
					Assert.Contains((IEnumerable<ZzzRedemptionCodeRowModel>)zzzRedemptionCodeAppSettingPage.Rows, (Predicate<ZzzRedemptionCodeRowModel>)((ZzzRedemptionCodeRowModel zzzRedemptionCodeRowModel3) => zzzRedemptionCodeRowModel3.Code == "SAMPLE_ONLY" && zzzRedemptionCodeRowModel3.IsReadOnly));
				});
				RedemptionCodeConfig redemptionCodeConfig = new RedemptionCodeConfig(new OneDragonEnvironment(text));
				Assert.DoesNotContain("USER2", redemptionCodeConfig.UserCodesDict.Keys);
				Assert.DoesNotContain("USER3", redemptionCodeConfig.UserCodesDict.Keys);
				Assert.Equal(20990101, redemptionCodeConfig.SampleCodesDict["SAMPLE_ONLY"]);
			}
			finally
			{
				if (session != null)
				{
					((IDisposable)session).Dispose();
				}
			}
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	private static void AssertOrder(string text, params string[] markers)
	{
		int num = -1;
		foreach (string text2 in markers)
		{
			int num2 = text.IndexOf(text2, StringComparison.Ordinal);
			Assert.True(num2 > num, "未按顺序找到 " + text2 + "。");
			num = num2;
		}
	}

	private static string FindDirectory()
	{
		for (DirectoryInfo directoryInfo = new DirectoryInfo(AppContext.BaseDirectory); directoryInfo != null; directoryInfo = directoryInfo.Parent)
		{
			string[] buffer = new string[5];
			buffer[0] = directoryInfo.FullName;
			buffer[1] = "src";
			buffer[2] = "ZzzOd.Gui";
			buffer[3] = "Pages";
			buffer[4] = "ApplicationSettings";
			string text = Path.Combine(buffer);
			if (Directory.Exists(text))
			{
				return text;
			}
		}
		throw new DirectoryNotFoundException("未找到应用设置目录。");
	}

	private static string CreateRunRootWithCodes()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-redemption-code-settings", Guid.NewGuid().ToString("N"));
		string text2 = Path.Combine(text, "config");
		Directory.CreateDirectory(text2);
		File.WriteAllText(Path.Combine(text2, "redemption_codes.sample.yml"), "codes:\n  SAMPLE_ONLY: 20990101\n  OVERRIDE: 20260707");
		File.WriteAllText(Path.Combine(text2, "redemption_codes.yml"), "codes:\n  OVERRIDE: 20991231\n  USER1: 20990102");
		return text;
	}
}
