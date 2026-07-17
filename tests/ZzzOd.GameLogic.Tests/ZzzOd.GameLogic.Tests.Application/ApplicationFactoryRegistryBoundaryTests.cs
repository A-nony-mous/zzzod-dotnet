using System;
using System.IO;
using System.Reflection;
using OneDragon.Core.Runtime;
using Xunit;
using ZzzOd.GameLogic.Config;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Tests.Application;

public sealed class ApplicationFactoryRegistryBoundaryTests : IDisposable
{
	private readonly string _rootDirectory;

	public ApplicationFactoryRegistryBoundaryTests()
	{
		_rootDirectory = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Path.Combine(_rootDirectory, "config", "00"));
		File.WriteAllText(Path.Combine(_rootDirectory, "config", "00", "battle_assistant.yml"), "control_method: ds4\nauto_battle_config: 不应被普通应用读取");
	}

	[Fact]
	public void OrdinaryApplicationFactoriesDoNotLoadBattleAssistantControlMethod()
	{
		using ZContext zContext = new ZContext(new OneDragonEnvironment(_rootDirectory));
		Assert.False(IsBattleAssistantConfigLoaded(zContext));
		zContext.ApplicationFactoryRegistry.CreateCoffeeFactory().CreateApplication(0, "one_dragon");
		zContext.ApplicationFactoryRegistry.CreateWorldPatrolFactory().CreateApplication(0, "default");
		Assert.False(IsBattleAssistantConfigLoaded(zContext));
	}

	private static bool IsBattleAssistantConfigLoaded(ZContext context)
	{
		FieldInfo fieldInfo = typeof(ZContext).GetField("_battleAssistantConfig", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("找不到 _battleAssistantConfig 字段。");
		Lazy<BattleAssistantConfig> lazy = (Lazy<BattleAssistantConfig>)fieldInfo.GetValue(context);
		return lazy.IsValueCreated;
	}

	public void Dispose()
	{
		if (Directory.Exists(_rootDirectory))
		{
			Directory.Delete(_rootDirectory, recursive: true);
		}
	}
}
