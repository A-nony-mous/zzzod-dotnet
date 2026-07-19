using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Ocr;
using OneDragon.Core.Runtime;
using ZzzOd.GameLogic.Const;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application;

/// <summary>
/// ZZZ 应用启动器。
/// </summary>
public class ZApplicationLauncher
{
	private readonly Func<ZContext> _contextFactory;

	private readonly bool _initializeContext;

	private readonly bool _initializeOcrProfile;

	private readonly bool _validateAssets;

	/// <summary>
	/// 当前上下文。
	/// </summary>
	public ZContext? Context { get; private set; }

	/// <summary>
	/// 需要发送通知的应用。
	/// </summary>
	public IReadOnlyDictionary<string, string> NotifyAppMap => EnsureContext().RunContext.NotifyAppMap;

	/// <summary>
	/// 初始化启动器。
	/// </summary>
	public ZApplicationLauncher(Func<ZContext>? contextFactory = null, bool initializeContext = true, bool initializeOcrProfile = true, bool validateAssets = true)
	{
		_contextFactory = contextFactory ?? ((Func<ZContext>)(() => new ZContext(new OneDragonEnvironment(Environment.CurrentDirectory))));
		_initializeContext = initializeContext;
		_initializeOcrProfile = initializeOcrProfile;
		_validateAssets = validateAssets;
	}

	/// <summary>
	/// 创建上下文。
	/// </summary>
	public virtual ZContext CreateContext()
	{
		Context = _contextFactory();
		if (_initializeContext)
		{
			InitializeContext(Context);
		}
		return Context;
	}

	/// <summary>
	/// 确保上下文已创建。
	/// </summary>
	public ZContext EnsureContext()
	{
		return Context ?? CreateContext();
	}

	/// <summary>
	/// 运行默认应用组。
	/// </summary>
	public Task<IReadOnlyList<OperationResult>> RunDefaultGroupAsync(int instanceIndex, string groupId = "default", CancellationToken cancellationToken = default(CancellationToken))
	{
		IReadOnlyList<string> defaultGroupApps = EnsureContext().RunContext.DefaultGroupApps;
		if (defaultGroupApps.Count == 0)
		{
			throw new InvalidOperationException("默认应用组未注册。");
		}
		return RunApplicationsAsync(defaultGroupApps, instanceIndex, groupId, cancellationToken);
	}

	/// <summary>
	/// 按顺序运行指定应用。
	/// </summary>
	public async Task<IReadOnlyList<OperationResult>> RunApplicationsAsync(IEnumerable<string> appIds, int instanceIndex, string groupId = "default", CancellationToken cancellationToken = default(CancellationToken))
	{
		ArgumentNullException.ThrowIfNull(appIds, "appIds");
		ZContext context = EnsureContext();
		List<OperationResult> results = new List<OperationResult>();
		foreach (string appId in appIds)
		{
			cancellationToken.ThrowIfCancellationRequested();
			OperationResult result = await context.RunContext.RunApplicationAsync(appId, instanceIndex, groupId, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			results.Add(result);
			if (!result.IsSuccess)
			{
				break;
			}
		}
		return results;
	}

	/// <summary>
	/// 暂停或恢复当前应用。
	/// </summary>
	public void PauseOrResume()
	{
		EnsureContext().RunContext.SwitchContextPauseAndRun();
	}

	/// <summary>
	/// 停止当前运行。
	/// </summary>
	public Task StopAsync(TimeSpan? gracefulShutdownTimeout = null)
	{
		return EnsureContext().RunContext.StopRunningAsync(gracefulShutdownTimeout);
	}

	/// <summary>
	/// 更新指定实例所有 run record。
	/// </summary>
	public void CheckAndUpdateAllRunRecord(int instanceIndex)
	{
		EnsureContext().RunContext.CheckAndUpdateAllRunRecord(instanceIndex);
	}

	/// <summary>
	/// 初始化运行应用所需的上下文。
	/// </summary>
	protected virtual void InitializeContext(ZContext context)
	{
		ArgumentNullException.ThrowIfNull(context, "context");
		context.SetReadyForApplication(ready: false);
		RunInitializationStage("应用注册", delegate
		{
			RegisterBuiltInApplications(context);
			EnsureBuiltInApplicationsRegistered(context);
		});
		if (_validateAssets)
		{
			RunInitializationStage("assets", delegate
			{
				ValidateRequiredAssets(context);
			});
		}
		if (_initializeOcrProfile)
		{
			RunInitializationStage("OCR profile", delegate
			{
				InitializeOcrProfile(context);
			});
		}
		RunInitializationStage("screen_info", delegate
		{
			context.ScreenContext.Reload();
		});
		RunInitializationStage("controller", delegate
		{
			InitializeController(context);
			if (context.Controller == null)
			{
				throw new InvalidOperationException("ZContext.InitController 未绑定 Controller。");
			}
		});
		RunInitializationStage("应用运行前资源", delegate
		{
			InitializeForApplication(context);
		});
		context.SetReadyForApplication(ready: true);
	}

	/// <summary>
	/// 注册内置应用。
	/// </summary>
	protected virtual void RegisterBuiltInApplications(ZContext context)
	{
		ArgumentNullException.ThrowIfNull(context, "context");
		context.ApplicationFactoryRegistry.RegisterBuiltInApplications();
	}

	/// <summary>
	/// 按配置初始化 OCR profile。
	/// </summary>
	protected virtual void InitializeOcrProfile(ZContext context)
	{
		ArgumentNullException.ThrowIfNull(context, "context");
		OcrModelResolution ocrModelResolution = context.ModelConfig.ResolveOcrProfile();
		double value = Math.Max(context.ProjectConfig.ScreenStandardWidth, context.ProjectConfig.ScreenStandardHeight);
		string id = ocrModelResolution.Profile.Id;
		double? detLimitSideLen = value;
		if (!UseOcrProfile(context, id, context.ModelConfig.OcrUseGpu, detLimitSideLen))
		{
			throw new InvalidOperationException("OCR profile 初始化失败：" + ocrModelResolution.Profile.Id);
		}
	}

	/// <summary>
	/// 创建 OCR profile。测试和平台启动器可以覆盖该入口观察配置传递。
	/// </summary>
	/// <param name="context">业务上下文。</param>
	/// <param name="profileId">OCR profile id。</param>
	/// <param name="useGpu">是否使用 GPU。</param>
	/// <param name="detLimitSideLen">检测长边限制。</param>
	/// <returns>是否初始化成功。</returns>
	protected virtual bool UseOcrProfile(ZContext context, string profileId, bool useGpu, double? detLimitSideLen)
	{
		return context.UseOcrProfile(profileId, useGpu, null, detLimitSideLen);
	}

	/// <summary>
	/// 初始化控制器。
	/// </summary>
	protected virtual void InitializeController(ZContext context)
	{
		ArgumentNullException.ThrowIfNull(context, "context");
		context.InitController();
	}

	/// <summary>
	/// 初始化应用运行前资源。
	/// </summary>
	protected virtual void InitializeForApplication(ZContext context)
	{
		ArgumentNullException.ThrowIfNull(context, "context");
		context.InitForApplication();
	}

	private static void ValidateRequiredAssets(ZContext context)
	{
		string[] source = new string[3]
		{
			GameConst.GetModelPath(context.Environment),
			GameConst.GetTemplatePath(context.Environment),
			GameConst.GetScreenInfoPath(context.Environment)
		};
		string[] array = source.Where((string directory) => !Directory.Exists(directory)).ToArray();
		if (array.Length != 0)
		{
			throw new DirectoryNotFoundException("缺少必要 assets 目录：" + string.Join(", ", array));
		}
	}

	private static void EnsureBuiltInApplicationsRegistered(ZContext context)
	{
		string[] array = ZzzApplicationIds.All.Where((string appId) => !context.RunContext.IsAppRegistered(appId)).ToArray();
		if (array.Length != 0)
		{
			throw new InvalidOperationException("内置应用注册不完整：" + string.Join(", ", array));
		}
		if (context.RunContext.DefaultGroupApps.Count == 0)
		{
			throw new InvalidOperationException("默认应用组未注册。");
		}
	}

	private static void RunInitializationStage(string stageName, Action action)
	{
		try
		{
			action();
		}
		catch (Exception ex) when (!(ex is InvalidOperationException) || ex.InnerException == null)
		{
			throw new InvalidOperationException(stageName + " 初始化失败：" + ex.Message, ex);
		}
	}
}
