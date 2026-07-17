using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OneDragon.Core.Configuration;
using OneDragon.Core.Ocr;
using OneDragon.Core.Runtime;
using ZzzOd.GameLogic.Config;

namespace ZzzOd.AppHost.Resources;

/// <summary>
/// OCR 和 ZZZ YOLO 模型的生产下载服务。
/// </summary>
public sealed class ZzzResourceDownloadService : IZzzResourceDownloadService, IDisposable
{
	private sealed record ResourceDescriptor(string ResourceId, string ModelId, string TargetDirectory, IReadOnlyList<string> DownloadSources, IReadOnlyList<string> RequiredFiles);

	private const string OcrResourceId = "ocr";

	private const string FlashResourceId = "flash_classifier";

	private const string HollowResourceId = "hollow_zero_event";

	private const string LostVoidResourceId = "lost_void_det";

	private readonly string _runRoot;

	private readonly ILogger<ZzzResourceDownloadService> _logger;

	private readonly Func<string?, HttpMessageHandler> _handlerFactory;

	private readonly Action<string, string> _moveDirectory;

	private readonly ConcurrentDictionary<string, CancellationTokenSource> _downloads = new ConcurrentDictionary<string, CancellationTokenSource>(StringComparer.Ordinal);

	private readonly ConcurrentDictionary<string, ZzzResourceDownloadStatusDto> _statuses = new ConcurrentDictionary<string, ZzzResourceDownloadStatusDto>(StringComparer.Ordinal);

	private bool _disposed;

	/// <inheritdoc />
	public event EventHandler<ZzzResourceDownloadStatusDto>? StatusChanged;

	/// <summary>
	/// 初始化生产资源下载服务。
	/// </summary>
	public ZzzResourceDownloadService(ZzzRunRoot runRoot, ILogger<ZzzResourceDownloadService> logger)
		: this(runRoot.Path, logger, CreateHttpHandler)
	{
	}

	internal ZzzResourceDownloadService(string runRoot, ILogger<ZzzResourceDownloadService> logger, Func<string?, HttpMessageHandler> handlerFactory, Action<string, string>? moveDirectory = null)
	{
		_runRoot = Path.GetFullPath(runRoot);
		_logger = logger;
		_handlerFactory = handlerFactory;
		_moveDirectory = moveDirectory ?? new Action<string, string>(Directory.Move);
	}

	/// <inheritdoc />
	public IReadOnlyList<ZzzResourceDownloadItemDto> GetItems()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ZzzOd.GameLogic.Config.ModelConfig modelConfig = LoadModelConfig();
		return new ZzzResourceDownloadItemDto[4]
		{
			CreateOcrItem(modelConfig),
			CreateYoloItem("flash_classifier", "闪光识别", "flash_classifier", modelConfig.FlashClassifier, modelConfig.FlashClassifierGpu),
			CreateYoloItem("hollow_zero_event", "空洞格子识别", "hollow_zero_event", modelConfig.HollowZeroEvent, modelConfig.HollowZeroEventGpu),
			CreateYoloItem("lost_void_det", "迷失之地识别", "lost_void_det", modelConfig.LostVoidDet, modelConfig.LostVoidDetGpu)
		};
	}

	/// <inheritdoc />
	public async Task DownloadAsync(string resourceId, string modelId, CancellationToken cancellationToken = default(CancellationToken))
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(resourceId, "resourceId");
		ArgumentException.ThrowIfNullOrWhiteSpace(modelId, "modelId");
		ResourceDescriptor descriptor = ResolveDescriptor(resourceId, modelId);
		using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		if (!_downloads.TryAdd(resourceId, linked))
		{
			throw new InvalidOperationException("该资源正在下载。");
		}
		try
		{
			Publish(new ZzzResourceDownloadStatusDto(resourceId, descriptor.ModelId, IsInstalled(descriptor), IsRunning: true, IsCancelling: false, 0.0, "下载中"));
			await DownloadAndInstallAsync(descriptor, linked.Token).ConfigureAwait(continueOnCapturedContext: false);
			Publish(new ZzzResourceDownloadStatusDto(resourceId, descriptor.ModelId, IsInstalled: true, IsRunning: false, IsCancelling: false, 100.0, "下载资源成功"));
			_logger.LogInformation("下载资源成功 {ResourceId} {ModelId}", resourceId, descriptor.ModelId);
		}
		catch (OperationCanceledException) when (linked.IsCancellationRequested)
		{
			Publish(new ZzzResourceDownloadStatusDto(resourceId, descriptor.ModelId, IsInstalled(descriptor), IsRunning: false, IsCancelling: false, null, "下载已取消"));
			_logger.LogInformation("下载已取消 {ResourceId} {ModelId}", resourceId, descriptor.ModelId);
		}
		catch (Exception ex2)
		{
			Exception exception = ex2;
			Publish(new ZzzResourceDownloadStatusDto(resourceId, descriptor.ModelId, IsInstalled(descriptor), IsRunning: false, IsCancelling: false, null, "下载资源失败 请尝试更换代理", exception.Message));
			_logger.LogError(exception, "下载资源失败 {ResourceId} {ModelId}", resourceId, descriptor.ModelId);
		}
		finally
		{
			_downloads.TryRemove(resourceId, out CancellationTokenSource _);
		}
	}

	/// <inheritdoc />
	public bool Cancel(string resourceId)
	{
		if (!_downloads.TryGetValue(resourceId, out CancellationTokenSource value))
		{
			return false;
		}
		if (_statuses.TryGetValue(resourceId, out ZzzResourceDownloadStatusDto value2))
		{
			Publish(value2 with
			{
				IsCancelling = true,
				Message = "取消中"
			});
		}
		value.Cancel();
		return true;
	}

	/// <inheritdoc />
	public void Dispose()
	{
		_disposed = true;
		foreach (CancellationTokenSource value in _downloads.Values)
		{
			value.Cancel();
		}
		_downloads.Clear();
		this.StatusChanged = null;
	}

	private ZzzResourceDownloadItemDto CreateOcrItem(ZzzOd.GameLogic.Config.ModelConfig config)
	{
		ZzzResourceModelOptionDto[] options = (from @group in (from profile in OcrModelRegistry.Default.GetSupportedProfiles()
				where profile.Resource != null
				select profile).GroupBy<OcrModelProfile, string>((OcrModelProfile profile) => profile.Resource.ResourceId, StringComparer.Ordinal)
			select new ZzzResourceModelOptionDto(@group.Key, @group.Key)).ToArray();
		string resourceId = config.ResolveOcrProfile().Resource.ResourceId;
		ResourceDescriptor descriptor = ResolveDescriptor("ocr", resourceId);
		return new ZzzResourceDownloadItemDto("ocr", "OCR识别", options, resourceId, config.OcrUseGpu, GetStatus(descriptor));
	}

	private ZzzResourceDownloadItemDto CreateYoloItem(string resourceId, string title, string category, string selected, bool useGpu)
	{
		string path = Path.Combine(_runRoot, "assets/models", category);
		List<string> list = (Directory.Exists(path) ? (from name in (from text2 in Directory.EnumerateDirectories(path)
				where IsSafeDirectoryName(Path.GetFileName(text2))
				where File.Exists(Path.Combine(text2, "model.onnx")) && HasYoloLabelFile(text2, Path.GetFileName(text2))
				select text2).Select(Path.GetFileName)
			where !string.IsNullOrWhiteSpace(name)
			select name).Cast<string>().ToList() : new List<string>());
		string defaultYoloModel = GetDefaultYoloModel(resourceId);
		if (!list.Contains<string>(defaultYoloModel, StringComparer.Ordinal))
		{
			list.Add(defaultYoloModel);
		}
		string text = (list.Contains<string>(selected, StringComparer.Ordinal) ? selected : defaultYoloModel);
		ResourceDescriptor descriptor = ResolveDescriptor(resourceId, text);
		return new ZzzResourceDownloadItemDto(resourceId, title, list.Select((string model) => new ZzzResourceModelOptionDto(model, model)).ToArray(), text, useGpu, GetStatus(descriptor));
	}

	private ZzzResourceDownloadStatusDto GetStatus(ResourceDescriptor descriptor)
	{
		if (_statuses.TryGetValue(descriptor.ResourceId, out ZzzResourceDownloadStatusDto value) && string.Equals(value.ModelId, descriptor.ModelId, StringComparison.Ordinal))
		{
			return value with
			{
				IsInstalled = IsInstalled(descriptor)
			};
		}
		bool flag = IsInstalled(descriptor);
		return new ZzzResourceDownloadStatusDto(descriptor.ResourceId, descriptor.ModelId, flag, IsRunning: false, IsCancelling: false, null, flag ? "已下载" : "下载");
	}

	private async Task DownloadAndInstallAsync(ResourceDescriptor descriptor, CancellationToken cancellationToken)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(descriptor.TargetDirectory));
		string archivePath = descriptor.TargetDirectory + ".download";
		string stagingDirectory = descriptor.TargetDirectory + $".install-{Guid.NewGuid():N}";
		string backupDirectory = descriptor.TargetDirectory + $".backup-{Guid.NewGuid():N}";
		bool backupCreated = false;
		try
		{
			await DownloadArchiveAsync(descriptor, archivePath, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			ExtractArchiveSafely(archivePath, stagingDirectory, cancellationToken);
			NormalizeStagingDirectory(stagingDirectory, descriptor.RequiredFiles);
			ValidateInstalledFiles(stagingDirectory, descriptor.RequiredFiles);
			if (Directory.Exists(descriptor.TargetDirectory))
			{
				_moveDirectory(descriptor.TargetDirectory, backupDirectory);
				backupCreated = true;
			}
			try
			{
				_moveDirectory(stagingDirectory, descriptor.TargetDirectory);
				if (Directory.Exists(backupDirectory))
				{
					Directory.Delete(backupDirectory, recursive: true);
					backupCreated = false;
				}
			}
			catch (Exception ex)
			{
				try
				{
					if (Directory.Exists(descriptor.TargetDirectory))
					{
						Directory.Delete(descriptor.TargetDirectory, recursive: true);
					}
					if (backupCreated && Directory.Exists(backupDirectory))
					{
						_moveDirectory(backupDirectory, descriptor.TargetDirectory);
					}
				}
				catch (Exception ex2)
				{
					Exception rollbackException = ex2;
					throw new AggregateException("资源安装失败，旧安装回滚失败；备份目录已保留。", ex, rollbackException);
				}
				throw;
			}
		}
		finally
		{
			if (File.Exists(archivePath))
			{
				File.Delete(archivePath);
			}
			if (Directory.Exists(stagingDirectory))
			{
				Directory.Delete(stagingDirectory, recursive: true);
			}
		}
	}

	private async Task DownloadArchiveAsync(ResourceDescriptor descriptor, string archivePath, CancellationToken cancellationToken)
	{
		Exception lastException = null;
		foreach (string source in descriptor.DownloadSources)
		{
			cancellationToken.ThrowIfCancellationRequested();
			try
			{
				_logger.LogInformation("开始下载 {Url}", source);
				using HttpMessageHandler handler = _handlerFactory(ReadPersonalProxy());
				using HttpClient client = new HttpClient(handler, disposeHandler: true)
				{
					Timeout = TimeSpan.FromMinutes(10L)
				};
				using HttpResponseMessage response = await client.GetAsync(source, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				response.EnsureSuccessStatusCode();
				long? total = response.Content.Headers.ContentLength;
				await using Stream input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				await using (FileStream output = new FileStream(archivePath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true))
				{
					byte[] buffer = new byte[65536];
					long downloaded = 0L;
					long lastReported = Stopwatch.GetTimestamp();
					while (true)
					{
						int read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
						if (read == 0)
						{
							break;
						}
						await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
						downloaded += read;
						if (Stopwatch.GetElapsedTime(lastReported) >= TimeSpan.FromSeconds(1L))
						{
							lastReported = Stopwatch.GetTimestamp();
							ReportProgress(descriptor, downloaded, total);
						}
					}
					ReportProgress(descriptor, downloaded, total);
					_logger.LogInformation("下载完成 {Path}", archivePath);
				}
				return;
			}
			catch (Exception ex) when (!(ex is OperationCanceledException))
			{
				lastException = ex;
				if (File.Exists(archivePath))
				{
					File.Delete(archivePath);
				}
			}
		}
		throw new InvalidOperationException("所有资源下载源均失败。", lastException);
	}

	private void ReportProgress(ResourceDescriptor descriptor, long downloaded, long? total)
	{
		double? num = ((total.HasValue && total.GetValueOrDefault() > 0) ? new double?((double)downloaded * 100.0 / (double)total.Value) : ((double?)null));
		string text = ((total.HasValue && total.GetValueOrDefault() > 0) ? $"正在下载 {(double)downloaded / 1048576.0:F2}/{(double)total.Value / 1048576.0:F2} MB ({num:F2}%)" : $"正在下载 {(double)downloaded / 1048576.0:F2} MB");
		Publish(new ZzzResourceDownloadStatusDto(descriptor.ResourceId, descriptor.ModelId, IsInstalled: false, IsRunning: true, IsCancelling: false, num, text));
		_logger.LogInformation("{Message}", text);
	}

	private void Publish(ZzzResourceDownloadStatusDto status)
	{
		_statuses[status.ResourceId] = status;
		this.StatusChanged?.Invoke(this, status);
	}

	private ResourceDescriptor ResolveDescriptor(string resourceId, string modelId)
	{
		if (string.Equals(resourceId, "ocr", StringComparison.Ordinal))
		{
			IReadOnlySet<string> ocrCatalog = GetOcrCatalog();
			ValidateCatalogModel(resourceId, modelId, ocrCatalog);
			OcrModelResolution ocrModelResolution = OcrModelResolver.Resolve(modelId);
			OcrModelResource resource = ocrModelResolution.Resource;
			return new ResourceDescriptor(resourceId, resource.ResourceId, Path.Combine(_runRoot, "assets/models", "onnx_ocr", resource.ResourceId), resource.DownloadSources, new string[5] { resource.DetectionModelFileName, resource.RecognitionModelFileName, resource.ClassificationModelFileName, resource.DictionaryFileName, resource.FontFileName });
		}
		if (1 == 0)
		{
		}
		string text = resourceId switch
		{
			"flash_classifier" => "flash_classifier", 
			"hollow_zero_event" => "hollow_zero_event", 
			"lost_void_det" => "lost_void_det", 
			_ => throw new ArgumentOutOfRangeException("resourceId", resourceId, "未知资源。"), 
		};
		if (1 == 0)
		{
		}
		string text2 = text;
		ValidateCatalogModel(resourceId, modelId, GetYoloCatalog(resourceId, text2));
		return new ResourceDescriptor(resourceId, modelId, Path.Combine(_runRoot, "assets/models", text2, modelId), new string[2]
		{
			"https://github.com/OneDragon-Anything/OneDragon-YOLO/releases/download/zzz_model/" + modelId + ".zip",
			"https://gitee.com/OneDragon-Anything/OneDragon-YOLO/releases/download/zzz_model/" + modelId + ".zip"
		}, new string[2]
		{
			"model.onnx",
			GetYoloLabelFileName(modelId)
		});
	}

	private IReadOnlySet<string> GetOcrCatalog()
	{
		return (from profile in OcrModelRegistry.Default.GetSupportedProfiles()
			where profile.Resource != null
			select profile.Resource.ResourceId).Where(IsSafeDirectoryName).ToHashSet<string>(StringComparer.Ordinal);
	}

	private IReadOnlySet<string> GetYoloCatalog(string resourceId, string category)
	{
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal) { GetDefaultYoloModel(resourceId) };
		string path = Path.Combine(_runRoot, "assets/models", category);
		if (!Directory.Exists(path))
		{
			return hashSet;
		}
		foreach (string item in Directory.EnumerateDirectories(path))
		{
			string fileName = Path.GetFileName(item);
			if (IsSafeDirectoryName(fileName) && File.Exists(Path.Combine(item, "model.onnx")) && HasYoloLabelFile(item, fileName))
			{
				hashSet.Add(fileName);
			}
		}
		return hashSet;
	}

	private static string GetDefaultYoloModel(string resourceId)
	{
		ZzzOd.GameLogic.Config.ModelConfig modelConfig = new ZzzOd.GameLogic.Config.ModelConfig();
		if (1 == 0)
		{
		}
		string result = resourceId switch
		{
			"flash_classifier" => modelConfig.FlashClassifier, 
			"hollow_zero_event" => modelConfig.HollowZeroEvent, 
			"lost_void_det" => modelConfig.LostVoidDet, 
			_ => throw new ArgumentOutOfRangeException("resourceId", resourceId, "未知资源。"), 
		};
		if (1 == 0)
		{
		}
		return result;
	}

	private static string GetYoloLabelFileName(string modelId)
	{
		return modelId.StartsWith("yolov26", StringComparison.OrdinalIgnoreCase) ? "model_label.txt" : "labels.csv";
	}

	private static bool HasYoloLabelFile(string directory, string? modelId)
	{
		return File.Exists(Path.Combine(directory, "labels.csv")) || (modelId != null && modelId.StartsWith("yolov26", StringComparison.OrdinalIgnoreCase) && File.Exists(Path.Combine(directory, "model_label.txt")));
	}

	private static void ValidateCatalogModel(string resourceId, string modelId, IReadOnlySet<string> catalog)
	{
		if (!IsSafeDirectoryName(modelId) || !catalog.Contains(modelId))
		{
			throw new ArgumentOutOfRangeException("modelId", modelId, "资源 " + resourceId + " 不包含该模型。");
		}
	}

	private static bool IsSafeDirectoryName(string? modelId)
	{
		return !string.IsNullOrWhiteSpace(modelId) && !(modelId == ".") && !(modelId == "..") && string.Equals(Path.GetFileName(modelId), modelId, StringComparison.Ordinal) && modelId.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && modelId.IndexOf(Path.DirectorySeparatorChar) < 0 && modelId.IndexOf(Path.AltDirectorySeparatorChar) < 0;
	}

	private bool IsInstalled(ResourceDescriptor descriptor)
	{
		return descriptor.RequiredFiles.All((string file) => File.Exists(Path.Combine(descriptor.TargetDirectory, file)));
	}

	private ZzzOd.GameLogic.Config.ModelConfig LoadModelConfig()
	{
		return new YamlConfig<ZzzOd.GameLogic.Config.ModelConfig>(new OneDragonEnvironment(_runRoot), "model").Current;
	}

	private string? ReadPersonalProxy()
	{
		EnvConfig current = new YamlConfig<EnvConfig>(new OneDragonEnvironment(_runRoot), "env").Current;
		return (string.Equals(current.ProxyType, "personal", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(current.PersonalProxy)) ? current.PersonalProxy : null;
	}

	private static void ExtractArchiveSafely(string archivePath, string destination, CancellationToken cancellationToken)
	{
		Directory.CreateDirectory(destination);
		string value = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
		using ZipArchive zipArchive = ZipFile.OpenRead(archivePath);
		foreach (ZipArchiveEntry entry in zipArchive.Entries)
		{
			cancellationToken.ThrowIfCancellationRequested();
			string fullPath = Path.GetFullPath(Path.Combine(destination, entry.FullName));
			if (!fullPath.StartsWith(value, StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidDataException("压缩包包含非法路径 " + entry.FullName);
			}
			if (string.IsNullOrEmpty(entry.Name))
			{
				Directory.CreateDirectory(fullPath);
				continue;
			}
			Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
			entry.ExtractToFile(fullPath, overwrite: true);
		}
	}

	private static void NormalizeStagingDirectory(string stagingDirectory, IReadOnlyList<string> requiredFiles)
	{
		if (requiredFiles.All((string file) => File.Exists(Path.Combine(stagingDirectory, file))))
		{
			return;
		}
		string text = Directory.EnumerateDirectories(stagingDirectory, "*", SearchOption.AllDirectories).FirstOrDefault((string directory) => requiredFiles.All((string file) => File.Exists(Path.Combine(directory, file))));
		if (text != null)
		{
			string text2 = stagingDirectory + ".normalized";
			Directory.Move(text, text2);
			Directory.Delete(stagingDirectory, recursive: true);
			Directory.Move(text2, stagingDirectory);
		}
	}

	private static void ValidateInstalledFiles(string directory, IReadOnlyList<string> requiredFiles)
	{
		string[] array = requiredFiles.Where((string file) => !File.Exists(Path.Combine(directory, file))).ToArray();
		if (array.Length != 0)
		{
			throw new InvalidDataException("资源压缩包缺少文件 " + string.Join(", ", array));
		}
	}

	private static HttpMessageHandler CreateHttpHandler(string? proxy)
	{
		HttpClientHandler httpClientHandler = new HttpClientHandler
		{
			AutomaticDecompression = DecompressionMethods.All
		};
		if (!string.IsNullOrWhiteSpace(proxy))
		{
			httpClientHandler.Proxy = new WebProxy(proxy);
			httpClientHandler.UseProxy = true;
		}
		return httpClientHandler;
	}
}
