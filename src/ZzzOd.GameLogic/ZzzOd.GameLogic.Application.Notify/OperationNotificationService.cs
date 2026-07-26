using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Configuration;
using OpenCvSharp;
using ZzzOd.GameLogic.Config;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.Notify;

/// <summary>
/// 复刻 BaselineParity operation_notify 的应用生命周期和节点通知分发。
/// </summary>
public sealed class OperationNotificationService : IDisposable
{
	private sealed record NotifySettings(bool Enabled, string Title, string Lifecycle, string Detail, bool MergeErrorImmediately);

	private sealed class NotificationPool : IDisposable
	{
		private readonly List<NotificationPoolItem> _items = new List<NotificationPoolItem>();

		public int MaxImages { get; set; } = 1;

		public IReadOnlyList<NotificationPoolItem> Items => _items;

		public Mat? LastImage => _items.LastOrDefault((NotificationPoolItem item) => item.Image != null)?.Image;

		private int ImageCount => _items.Count((NotificationPoolItem item) => item.Image != null);

		public void Add(string content, Mat? image)
		{
			Mat image2 = ((image == null || ImageCount >= MaxImages) ? null : image.Clone());
			_items.Add(new NotificationPoolItem(content, image2));
		}

		public void Clear()
		{
			foreach (NotificationPoolItem item in _items)
			{
				item.Image?.Dispose();
			}
			_items.Clear();
		}

		public void Dispose()
		{
			Clear();
		}
	}

	private sealed record NotificationPoolItem(string Content, Mat? Image);

	private readonly ZContext _context;

	private readonly NotificationPool _pool = new NotificationPool();

	private string? _activeAppId;

	private bool _disposed;

	/// <summary>
	/// 初始化节点通知服务。
	/// </summary>
	public OperationNotificationService(ZContext context)
	{
		_context = context;
	}

	/// <summary>
	/// 应用启动时推送生命周期开始消息并初始化节点通知池。
	/// </summary>
	public void OnApplicationStart(string appId, string appName)
	{
		_activeAppId = appId;
		NotifySettings settings = GetSettings(appId);
		if (settings.Enabled && settings.Lifecycle == "start_and_finish")
		{
			Dispatch(settings.Title, "任务「" + appName + "」运行开始", null);
		}
		_pool.Clear();
		_pool.MaxImages = ((!(settings.Detail == "merge")) ? 1 : 10);
	}

	/// <summary>
	/// 应用结束时按生命周期和合并策略发送消息。
	/// </summary>
	public void OnApplicationCompleted(string appId, string appName, bool success)
	{
		try
		{
			NotifySettings settings = GetSettings(appId);
			if (!settings.Enabled)
			{
				return;
			}
			string text = "任务「" + appName + "」运行" + (success ? "成功" : "失败");
			if (settings.Detail == "merge" && _pool.Items.Count > 0)
			{
				IReadOnlyList<string> values = ((settings.Lifecycle == "off") ? _pool.Items.Select((NotificationPoolItem item) => item.Content).ToArray() : new string[1] { text }.Concat(_pool.Items.Select((NotificationPoolItem item) => item.Content)).ToArray());
				Dispatch(settings.Title, string.Join("\n---\n", values), _pool.LastImage);
			}
			else if (settings.Lifecycle != "off")
			{
				Dispatch(settings.Title, text, _pool.LastImage);
			}
		}
		finally
		{
			_pool.Clear();
			_activeAppId = null;
		}
	}

	/// <summary>
	/// 在状态机完成一轮成功或失败节点时处理标注的节点通知。
	/// </summary>
	public void OnNodeCompleted(string operationName, Mat? lastScreenshot, string currentNodeName, MethodInfo currentNodeMethod, OperationRoundResult roundResult, string? nextNodeName, MethodInfo? nextNodeMethod)
	{
		string text = _activeAppId ?? _context.RunContext.CurrentAppId;
		if (_disposed || !GetSettings(text).Enabled)
		{
			return;
		}
		string value = ((text == null) ? operationName : _context.RunContext.GetApplicationName(text));
		NotifySettings settings = GetSettings(text);
		bool failed = roundResult.IsFail;
		if (!(settings.Detail != "off") || !(settings.Detail != "error_only" || failed))
		{
			if (_context.PushConfig.SendImage && settings.Lifecycle != "off")
			{
				_pool.MaxImages = 1;
				_pool.Add(string.Empty, lastScreenshot);
			}
			return;
		}
		List<OperationNodeNotifyAttribute> list = (from annotation in currentNodeMethod.GetCustomAttributes<OperationNodeNotifyAttribute>()
			where annotation.Timing != OperationNodeNotifyTiming.PreviousDone
			where annotation.Timing != OperationNodeNotifyTiming.CurrentSuccess || !failed
			where annotation.Timing != OperationNodeNotifyTiming.CurrentFail || failed
			select annotation).ToList();
		if ((object)nextNodeMethod != null)
		{
			list.AddRange(from annotation in nextNodeMethod.GetCustomAttributes<OperationNodeNotifyAttribute>()
				where annotation.Timing == OperationNodeNotifyTiming.PreviousDone
				select annotation);
		}
		if (list.Count != 0)
		{
			bool flag = list.Any((OperationNodeNotifyAttribute annotation) => annotation.Detail);
			bool flag2 = list.Any((OperationNodeNotifyAttribute annotation) => annotation.SendImage);
			string text2 = string.Concat(from annotation in list
				where !string.IsNullOrWhiteSpace(annotation.CustomMessage)
				select Environment.NewLine + annotation.CustomMessage);
			string text3 = $"任务「{value}」节点「{currentNodeName}」{Environment.NewLine}运行「{(failed ? "失败" : "成功")}」";
			if (flag && !string.IsNullOrWhiteSpace(roundResult.Status))
			{
				text3 = text3 + "状态「" + roundResult.Status + "」";
			}
			text3 += text2;
			_pool.Add(text3, flag2 ? lastScreenshot : null);
			if (settings.Detail == "all" || (failed && (settings.Detail == "error_only" || (settings.Detail == "merge" && settings.MergeErrorImmediately))))
			{
				Dispatch(settings.Title, text3, flag2 ? lastScreenshot : null);
			}
		}
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (!_disposed)
		{
			_disposed = true;
			_pool.Dispose();
		}
	}

	private NotifySettings GetSettings(string? appId)
	{
		int value = _context.RunContext.CurrentInstanceIndex ?? _context.InstanceIndex;
		NotifyConfig current = new YamlConfig<NotifyConfig>(_context.Environment, "notify", null, value).Current;
		if (!current.EnableNotify)
		{
			return new NotifySettings(Enabled: false, current.Title, "off", "off", current.MergeErrorImmediateNotify);
		}
		NotifyApplicationSetting notifyApplicationSetting = ((appId == null) ? new NotifyApplicationSetting
		{
			Lifecycle = "finish_only",
			Detail = "all"
		} : current.GetApplicationSetting(appId));
		return new NotifySettings(Enabled: true, current.Title, notifyApplicationSetting.Lifecycle, notifyApplicationSetting.Detail, current.MergeErrorImmediateNotify);
	}

	private void Dispatch(string title, string content, Mat? image)
	{
		Mat ownedImage = image?.Clone();
		DispatchAsync(title, content, ownedImage);
	}

	private async Task DispatchAsync(string title, string content, Mat? ownedImage)
	{
		try
		{
			OperationResult result = await _context.PushNotificationService.PushAsync(_context, title, content, ownedImage, CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
			if (!result.IsSuccess)
			{
				_context.Logger.Warning("通知推送失败。Status={Status}", result.Status);
			}
		}
		catch (Exception ex)
		{
			Exception exception = ex;
			_context.Logger.Warning(exception, "通知推送异常。");
		}
		finally
		{
			ownedImage?.Dispose();
		}
	}
}
