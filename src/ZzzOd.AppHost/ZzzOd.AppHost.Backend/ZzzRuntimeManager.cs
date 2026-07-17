using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;
using OpenCvSharp;
using ZzzOd.AppHost.Notifications;
using ZzzOd.GameLogic.Application;
using ZzzOd.GameLogic.Application.Notify;
using ZzzOd.GameLogic.Config;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.AppHost.Backend;

/// <summary>
/// ZZZ 运行时上下文管理器。
/// </summary>
public sealed class ZzzRuntimeManager : IDisposable
{
	private readonly Lock _lock = new Lock();

	private readonly ILogger<ZzzRuntimeManager> _logger;

	private readonly Func<int, ZContext>? _contextFactory;

	private readonly IPushNotificationService? _pushNotificationService;

	private ZContext? _context;

	private bool _disposed;

	/// <summary>
	/// 运行根目录。
	/// </summary>
	public string RunRoot { get; }

	/// <summary>
	/// 当前实例编号。
	/// </summary>
	public int ActiveInstanceIndex { get; private set; }

	/// <summary>
	/// 当前上下文是否已经创建。
	/// </summary>
	public bool HasContext => _context != null;

	/// <summary>
	/// 当前是否存在未停止的应用运行。
	/// </summary>
	public bool IsRunActive
	{
		get
		{
			using (_lock.EnterScope())
			{
				ObjectDisposedException.ThrowIf(_disposed, this);
				return HasActiveRunUnsafe();
			}
		}
	}

	/// <summary>
	/// 初始化运行时管理器。
	/// </summary>
	/// <param name="runRoot">运行根目录。</param>
	/// <param name="logger">日志。</param>
	/// <param name="pushNotificationService">生产推送服务。</param>
	public ZzzRuntimeManager(string runRoot, ILogger<ZzzRuntimeManager> logger, IZzzPushNotificationService? pushNotificationService = null)
		: this(runRoot, logger, null, pushNotificationService)
	{
	}

	/// <summary>
	/// 使用指定上下文工厂初始化运行时管理器。
	/// </summary>
	/// <param name="runRoot">运行根目录。</param>
	/// <param name="logger">日志。</param>
	/// <param name="contextFactory">上下文工厂。</param>
	/// <param name="pushNotificationService">生产推送服务。</param>
	internal ZzzRuntimeManager(string runRoot, ILogger<ZzzRuntimeManager> logger, Func<int, ZContext>? contextFactory, IZzzPushNotificationService? pushNotificationService = null)
	{
		RunRoot = Path.GetFullPath(runRoot);
		_logger = logger;
		_contextFactory = contextFactory;
		_pushNotificationService = ((pushNotificationService == null) ? null : new AppHostPushNotificationAdapter(pushNotificationService));
		ActiveInstanceIndex = ReadConfiguredActiveInstanceIndex();
	}

	/// <summary>
	/// 获取当前上下文。
	/// </summary>
	/// <returns>当前上下文。</returns>
	public ZContext EnsureContext()
	{
		using (_lock.EnterScope())
		{
			ObjectDisposedException.ThrowIf(_disposed, this);
			if (_context == null)
			{
				_context = CreateContext(ActiveInstanceIndex);
			}
			return _context;
		}
	}

	/// <summary>
	/// 获取当前上下文。
	/// </summary>
	/// <returns>当前上下文或 null。</returns>
	public ZContext? TryGetContext()
	{
		using (_lock.EnterScope())
		{
			return _context;
		}
	}

	/// <summary>
	/// 按当前实例重新创建生产运行时上下文。
	/// </summary>
	/// <returns>重新初始化结果。</returns>
	public ZzzBackendResult<bool> ReinitializeContext()
	{
		using (_lock.EnterScope())
		{
			ObjectDisposedException.ThrowIf(_disposed, this);
			if (HasActiveRunUnsafe())
			{
				return ZzzBackendResult<bool>.Fail(ZzzBackendErrorCode.Conflict, "运行中不能重新初始化脚本环境。");
			}
			try
			{
				_context?.Dispose();
				_context = null;
				_context = CreateContext(ActiveInstanceIndex);
				return ZzzBackendResult<bool>.Ok(value: true);
			}
			catch (Exception ex)
			{
				_context = null;
				return ZzzBackendResult<bool>.Fail(ZzzBackendErrorCode.NotReady, ex.Message);
			}
		}
	}

	/// <summary>
	/// 按 BaselineParity screenshot_and_save_debug 的 independent 模式采集并遮挡 UID。
	/// </summary>
	/// <returns>PNG 截图字节。</returns>
	public ZzzBackendResult<byte[]> CaptureDebugScreenshot()
	{
		try
		{
			ZContext zContext = EnsureContext();
			if (!zContext.ReadyForApplication || zContext.Controller == null)
			{
				return ZzzBackendResult<byte[]>.Fail(ZzzBackendErrorCode.NotReady, "控制器未初始化。");
			}
			if (!zContext.Controller.IsGameWindowReady)
			{
				return ZzzBackendResult<byte[]>.Fail(ZzzBackendErrorCode.NotReady, "游戏窗口未就绪。");
			}
			using Mat mat = zContext.Controller.Screenshot(independent: true).Screen;
			if (mat == null)
			{
				return ZzzBackendResult<byte[]>.Fail(ZzzBackendErrorCode.NotReady, "截图失败。");
			}
			Cv2.ImEncode(".png", mat, out byte[] buf);
			return ZzzBackendResult<byte[]>.Ok(buf);
		}
		catch (Exception ex)
		{
			return ZzzBackendResult<byte[]>.Fail(ZzzBackendErrorCode.NotReady, ex.Message);
		}
	}

	/// <summary>
	/// 切换当前实例。
	/// </summary>
	/// <param name="instanceIndex">实例编号。</param>
	/// <returns>切换结果。</returns>
	public ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> ActivateInstance(int instanceIndex)
	{
		using (_lock.EnterScope())
		{
			ObjectDisposedException.ThrowIf(_disposed, this);
			if (HasActiveRunUnsafe())
			{
				return ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>>.Fail(ZzzBackendErrorCode.Conflict, "运行中不能切换实例。");
			}
			if (ReadInstanceMetadata().All((OneDragonInstanceConfigItem item) => item.Idx != instanceIndex))
			{
				return ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>>.Fail(ZzzBackendErrorCode.NotFound, $"实例不存在 {instanceIndex:00}");
			}
			if (_context != null && _context.InstanceIndex == instanceIndex)
			{
				ActiveInstanceIndex = instanceIndex;
				SaveActiveInstance(instanceIndex);
				return ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>>.Ok(ListInstances());
			}
			_context?.Dispose();
			_context = null;
			ActiveInstanceIndex = instanceIndex;
			_context = CreateContext(instanceIndex);
			SaveActiveInstance(instanceIndex);
			return ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>>.Ok(ListInstances());
		}
	}

	/// <summary>
	/// 新增实例。
	/// </summary>
	/// <returns>实例列表。</returns>
	public ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> CreateInstance()
	{
		using (_lock.EnterScope())
		{
			ObjectDisposedException.ThrowIf(_disposed, this);
			if (HasActiveRunUnsafe())
			{
				return InstanceMutationBlocked();
			}
			try
			{
				string configRoot = GetConfigRoot();
				Directory.CreateDirectory(configRoot);
				HashSet<int> hashSet = (from item in ReadInstanceMetadata()
					select item.Idx).ToHashSet();
				int num = 0;
				do
				{
					num++;
				}
				while (hashSet.Contains(num));
				Directory.CreateDirectory(GetInstanceDirectory(num));
				YamlConfig<OneDragonConfig> yamlConfig = CreateOneDragonConfig();
				yamlConfig.Current.InstanceList.Add(new OneDragonInstanceConfigItem
				{
					Idx = num,
					Name = num.ToString("00"),
					Active = false,
					ActiveInOneDragon = true
				});
				yamlConfig.Save();
				return ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>>.Ok(ListInstances());
			}
			catch (Exception ex)
			{
				return ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>>.Fail(ZzzBackendErrorCode.NotReady, ex.Message);
			}
		}
	}

	/// <summary>
	/// 更新实例元数据。
	/// </summary>
	/// <param name="request">更新请求。</param>
	/// <returns>实例列表。</returns>
	public ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> UpdateInstance(ZzzUpdateInstanceRequest request)
	{
		ArgumentNullException.ThrowIfNull(request, "request");
		using (_lock.EnterScope())
		{
			ObjectDisposedException.ThrowIf(_disposed, this);
			if (HasActiveRunUnsafe())
			{
				return InstanceMutationBlocked();
			}
			try
			{
				YamlConfig<OneDragonConfig> yamlConfig = CreateOneDragonConfig();
				OneDragonInstanceConfigItem oneDragonInstanceConfigItem = yamlConfig.Current.InstanceList.FirstOrDefault((OneDragonInstanceConfigItem item) => item.Idx == request.Index);
				if (oneDragonInstanceConfigItem == null)
				{
					return ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>>.Fail(ZzzBackendErrorCode.NotFound, $"实例不存在 {request.Index:00}");
				}
				if (request.Name != null)
				{
					oneDragonInstanceConfigItem.Name = (string.IsNullOrWhiteSpace(request.Name) ? request.Index.ToString("00") : request.Name.Trim());
				}
				if (request.ActiveInOneDragon.HasValue)
				{
					oneDragonInstanceConfigItem.ActiveInOneDragon = request.ActiveInOneDragon.Value;
				}
				yamlConfig.Save();
				return ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>>.Ok(ListInstances());
			}
			catch (Exception ex)
			{
				return ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>>.Fail(ZzzBackendErrorCode.NotReady, ex.Message);
			}
		}
	}

	/// <summary>
	/// 删除实例。
	/// </summary>
	/// <param name="instanceIndex">实例编号。</param>
	/// <returns>实例列表。</returns>
	public ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> DeleteInstance(int instanceIndex)
	{
		using (_lock.EnterScope())
		{
			ObjectDisposedException.ThrowIf(_disposed, this);
			if (HasActiveRunUnsafe())
			{
				return InstanceMutationBlocked();
			}
			IReadOnlyList<OneDragonInstanceConfigItem> readOnlyList = ReadInstanceMetadata();
			if (readOnlyList.All((OneDragonInstanceConfigItem item) => item.Idx != instanceIndex))
			{
				return ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>>.Fail(ZzzBackendErrorCode.NotFound, $"实例不存在 {instanceIndex:00}");
			}
			if (readOnlyList.Count <= 1)
			{
				return ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>>.Fail(ZzzBackendErrorCode.Conflict, "至少保留一个实例。");
			}
			try
			{
				YamlConfig<OneDragonConfig> yamlConfig = CreateOneDragonConfig();
				yamlConfig.Current.InstanceList.RemoveAll((OneDragonInstanceConfigItem item) => item.Idx == instanceIndex);
				yamlConfig.Save();
				string instanceDirectory = GetInstanceDirectory(instanceIndex);
				string fullPath = Path.GetFullPath(GetConfigRoot());
				string fullPath2 = Path.GetFullPath(instanceDirectory);
				string value = (fullPath.EndsWith(Path.DirectorySeparatorChar) ? fullPath : (fullPath + Path.DirectorySeparatorChar));
				if (Directory.Exists(fullPath2) && fullPath2.StartsWith(value, StringComparison.OrdinalIgnoreCase))
				{
					Directory.Delete(fullPath2, recursive: true);
				}
				return ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>>.Ok(ListInstances());
			}
			catch (Exception ex)
			{
				return ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>>.Fail(ZzzBackendErrorCode.NotReady, ex.Message);
			}
		}
	}

	/// <summary>
	/// 获取实例列表。
	/// </summary>
	/// <returns>实例列表。</returns>
	public IReadOnlyList<ZzzInstanceDto> ListInstances()
	{
		return (from item in ReadInstanceMetadata()
			select new ZzzInstanceDto(item.Idx, item.Name, item.Idx == ActiveInstanceIndex, GetInstanceDirectory(item.Idx), item.ActiveInOneDragon)).ToArray();
	}

	/// <inheritdoc />
	public void Dispose()
	{
		using (_lock.EnterScope())
		{
			if (!_disposed)
			{
				_disposed = true;
				_context?.Dispose();
				_context = null;
			}
		}
	}

	private ZContext CreateContext(int instanceIndex)
	{
		if (_contextFactory != null)
		{
			ZContext zContext = _contextFactory(instanceIndex);
			AttachPushNotificationService(zContext);
			return zContext;
		}
		_logger.LogInformation("初始化 ZContext，实例 {InstanceIndex}", instanceIndex);
		OneDragonEnvironment environment = new OneDragonEnvironment(RunRoot);
		ZApplicationLauncher zApplicationLauncher = new ZApplicationLauncher(() => new ZContext(environment, null, instanceIndex));
		ZContext zContext2 = zApplicationLauncher.CreateContext();
		AttachPushNotificationService(zContext2);
		return zContext2;
	}

	private void AttachPushNotificationService(ZContext context)
	{
		if (_pushNotificationService != null)
		{
			context.PushNotificationService = _pushNotificationService;
		}
	}

	private string GetConfigRoot()
	{
		return Path.Combine(RunRoot, "config");
	}

	private string GetInstanceDirectory(int index)
	{
		return Path.Combine(GetConfigRoot(), index.ToString("00"));
	}

	private IReadOnlyList<OneDragonInstanceConfigItem> ReadInstanceMetadata()
	{
		try
		{
			return CreateOneDragonConfig().Current.InstanceList;
		}
		catch
		{
			return Array.Empty<OneDragonInstanceConfigItem>();
		}
	}

	private void SaveActiveInstance(int instanceIndex)
	{
		YamlConfig<OneDragonConfig> yamlConfig = CreateOneDragonConfig();
		foreach (OneDragonInstanceConfigItem instance in yamlConfig.Current.InstanceList)
		{
			instance.Active = instance.Idx == instanceIndex;
		}
		yamlConfig.Save();
	}

	private YamlConfig<OneDragonConfig> CreateOneDragonConfig()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment(RunRoot);
		IReadOnlyList<string> subDirectories = Array.Empty<string>();
		return new YamlConfig<OneDragonConfig>(environment, "one_dragon", null, null, subDirectories);
	}

	private int ReadConfiguredActiveInstanceIndex()
	{
		try
		{
			return CreateOneDragonConfig().Current.InstanceList.FirstOrDefault((OneDragonInstanceConfigItem item) => item.Active)?.Idx ?? 0;
		}
		catch
		{
			return 0;
		}
	}

	private bool HasActiveRunUnsafe()
	{
		return _context != null && !_context.RunContext.IsContextStop;
	}

	private static ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> InstanceMutationBlocked()
	{
		return ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>>.Fail(ZzzBackendErrorCode.Conflict, "运行中不能修改实例。");
	}
}
