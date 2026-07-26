using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ZzzOd.AppHost.Resources;

namespace ZzzOd.GameLogic.Tests.AppHost;

/// <summary>
/// 真实资源目录、流式下载、取消和安全解压测试。
/// </summary>
public sealed class ResourceDownloadServiceTests
{
	private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			return Task.FromResult(responder(request));
		}
	}

	private sealed class BlockingStream : MemoryStream
	{
		public BlockingStream(byte[] content)
			: base(content, writable: false)
		{
		}

		public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default(CancellationToken))
		{
			await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
			return 0;
		}
	}

	private const string FlashDefaultModel = "yolov8n-640-flash-20250921";

	/// <summary>OCR 压缩包应流式下载并安装全部必需文件。</summary>
	[Fact]
	public async Task OcrDownloadStreamsArchiveAndInstallsRequiredFiles()
	{
		string root = CreateRoot();
		try
		{
			byte[] archive = CreateZip(new Dictionary<string, string>
			{
				["ppocrv6/det.onnx"] = "det",
				["ppocrv6/rec.onnx"] = "rec",
				["ppocrv6/cls.onnx"] = "cls",
				["ppocrv6/ppocrv6_dict.txt"] = "dict",
				["ppocrv6/simfang.ttf"] = "font"
			});
			using ZzzResourceDownloadService service = CreateService(root, (HttpRequestMessage _) => Response(archive));
			List<ZzzResourceDownloadStatusDto> statuses = new List<ZzzResourceDownloadStatusDto>();
			service.StatusChanged += delegate(object? _, ZzzResourceDownloadStatusDto status)
			{
				statuses.Add(status);
			};
			await service.DownloadAsync("ocr", "ppocrv6");
			string[] buffer = new string[5];
			buffer[0] = root;
			buffer[1] = "assets";
			buffer[2] = "models";
			buffer[3] = "onnx_ocr";
			buffer[4] = "ppocrv6";
			string directory = Path.Combine(buffer);
			Assert.True(File.Exists(Path.Combine(directory, "det.onnx")));
			Assert.True(File.Exists(Path.Combine(directory, "rec.onnx")));
			Assert.True(File.Exists(Path.Combine(directory, "cls.onnx")));
			Assert.True(File.Exists(Path.Combine(directory, "ppocrv6_dict.txt")));
			Assert.True(File.Exists(Path.Combine(directory, "simfang.ttf")));
			Assert.Contains((IEnumerable<ZzzResourceDownloadStatusDto>)statuses, (Predicate<ZzzResourceDownloadStatusDto>)((ZzzResourceDownloadStatusDto status) => status.IsRunning));
			List<ZzzResourceDownloadStatusDto> list = statuses;
			Assert.Equal("下载资源成功", list[list.Count - 1].Message);
			List<ZzzResourceDownloadStatusDto> list2 = statuses;
			Assert.True(list2[list2.Count - 1].IsInstalled);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	/// <summary>取消 YOLO 下载后不应留下半成品目录。</summary>
	[Fact]
	public async Task YoloDownloadCanBeCancelledWithoutLeavingPartialInstall()
	{
		string root = CreateRoot();
		try
		{
			WriteModelConfig(root, "flash_classifier: yolov8n-640-flash-20250921\n");
			byte[] archive = CreateZip(new Dictionary<string, string>
			{
				["model.onnx"] = new string('x', 200000),
				["labels.csv"] = "idx,name\n0,target"
			});
			ZzzResourceDownloadService service = CreateService(root, (HttpRequestMessage _) => Response(archive, slow: true));
			try
			{
				List<ZzzResourceDownloadStatusDto> statuses = new List<ZzzResourceDownloadStatusDto>();
				service.StatusChanged += delegate(object? _, ZzzResourceDownloadStatusDto item)
				{
					statuses.Add(item);
				};
				Task download = service.DownloadAsync("flash_classifier", "yolov8n-640-flash-20250921");
				Assert.True(SpinWait.SpinUntil(() => service.GetItems().Single((ZzzResourceDownloadItemDto item) => item.ResourceId == "flash_classifier").Status.IsRunning, TimeSpan.FromSeconds(2L)));
				Assert.True(service.Cancel("flash_classifier"));
				await download;
				ZzzResourceDownloadStatusDto status = service.GetItems().Single((ZzzResourceDownloadItemDto item) => item.ResourceId == "flash_classifier").Status;
				Assert.Equal("下载已取消", status.Message);
				Assert.False(status.IsInstalled);
				Assert.Contains((IEnumerable<ZzzResourceDownloadStatusDto>)statuses, (Predicate<ZzzResourceDownloadStatusDto>)((ZzzResourceDownloadStatusDto item) => item.IsRunning && item.ProgressPercent == 0.0 && item.Message == "下载中"));
				Assert.Contains((IEnumerable<ZzzResourceDownloadStatusDto>)statuses, (Predicate<ZzzResourceDownloadStatusDto>)((ZzzResourceDownloadStatusDto item) => item.IsRunning && item.IsCancelling && item.Message == "取消中"));
				List<ZzzResourceDownloadStatusDto> list = statuses;
				Assert.Equal("下载已取消", list[list.Count - 1].Message);
				string[] buffer = new string[5];
				buffer[0] = root;
				buffer[1] = "assets";
				buffer[2] = "models";
				buffer[3] = "flash_classifier";
				buffer[4] = "yolov8n-640-flash-20250921";
				Assert.False(Directory.Exists(Path.Combine(buffer)));
			}
			finally
			{
				if (service != null)
				{
					((IDisposable)service).Dispose();
				}
			}
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	/// <summary>Zip Slip 路径应被拒绝。</summary>
	[Fact]
	public async Task ZipSlipArchiveIsRejectedAndDoesNotEscapeTargetDirectory()
	{
		string root = CreateRoot();
		try
		{
			string[] buffer = new string[5];
			buffer[0] = root;
			buffer[1] = "assets";
			buffer[2] = "models";
			buffer[3] = "hollow_zero_event";
			buffer[4] = "yolov8s-736-hollow-zero-event-1130";
			string installed = Path.Combine(buffer);
			Directory.CreateDirectory(installed);
			File.WriteAllText(Path.Combine(installed, "model.onnx"), "old-model");
			File.WriteAllText(Path.Combine(installed, "labels.csv"), "old-labels");
			WriteModelConfig(root, "hollow_zero_event: yolov8s-736-hollow-zero-event-1130\n");
			byte[] archive = CreateZip(new Dictionary<string, string> { ["../escape.txt"] = "bad" });
			using ZzzResourceDownloadService service = CreateService(root, (HttpRequestMessage _) => Response(archive));
			await service.DownloadAsync("hollow_zero_event", "yolov8s-736-hollow-zero-event-1130");
			ZzzResourceDownloadStatusDto status = service.GetItems().Single((ZzzResourceDownloadItemDto item) => item.ResourceId == "hollow_zero_event").Status;
			Assert.Equal("下载资源失败 请尝试更换代理", status.Message);
			Assert.NotNull(status.Error);
			Assert.Equal("old-model", File.ReadAllText(Path.Combine(installed, "model.onnx")));
			string[] buffer2 = new string[5];
			buffer2[0] = root;
			buffer2[1] = "assets";
			buffer2[2] = "models";
			buffer2[3] = "hollow_zero_event";
			buffer2[4] = "escape.txt";
			Assert.False(File.Exists(Path.Combine(buffer2)));
			Assert.False(File.Exists(Path.Combine(root, "assets", "models", "escape.txt")));
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	/// <summary>YOLO 压缩包应安装真实目录项并发布完整进度。</summary>
	[Fact]
	public async Task YoloDownloadInstallsCatalogModelAndPublishesProgress()
	{
		string root = CreateRoot();
		try
		{
			WriteModelConfig(root, "flash_classifier: yolov8n-640-flash-20250921\n");
			byte[] archive = CreateZip(new Dictionary<string, string>
			{
				["release/model.onnx"] = "new-model",
				["release/labels.csv"] = "idx,name\n0,target"
			});
			using ZzzResourceDownloadService service = CreateService(root, (HttpRequestMessage _) => Response(archive));
			List<ZzzResourceDownloadStatusDto> statuses = new List<ZzzResourceDownloadStatusDto>();
			service.StatusChanged += delegate(object? _, ZzzResourceDownloadStatusDto status)
			{
				statuses.Add(status);
			};
			await service.DownloadAsync("flash_classifier", "yolov8n-640-flash-20250921");
			string[] buffer = new string[5];
			buffer[0] = root;
			buffer[1] = "assets";
			buffer[2] = "models";
			buffer[3] = "flash_classifier";
			buffer[4] = "yolov8n-640-flash-20250921";
			string directory = Path.Combine(buffer);
			Assert.Equal("new-model", File.ReadAllText(Path.Combine(directory, "model.onnx")));
			Assert.True(File.Exists(Path.Combine(directory, "labels.csv")));
			Assert.Contains((IEnumerable<ZzzResourceDownloadStatusDto>)statuses, (Predicate<ZzzResourceDownloadStatusDto>)((ZzzResourceDownloadStatusDto status) => status.IsRunning && status.ProgressPercent == 0.0));
			Assert.Contains((IEnumerable<ZzzResourceDownloadStatusDto>)statuses, (Predicate<ZzzResourceDownloadStatusDto>)((ZzzResourceDownloadStatusDto status) => status.IsRunning && status.ProgressPercent.GetValueOrDefault() == 100.0 && status.Message.StartsWith("正在下载 ", StringComparison.Ordinal)));
			List<ZzzResourceDownloadStatusDto> list = statuses;
			Assert.Equal("下载资源成功", list[list.Count - 1].Message);
			List<ZzzResourceDownloadStatusDto> list2 = statuses;
			Assert.True(list2[list2.Count - 1].IsInstalled);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	/// <summary>目录穿越和未列入目录的模型 id 都应在发送 HTTP 请求前拒绝。</summary>
	[Theory]
	[InlineData(new object[] { "../escape" })]
	[InlineData(new object[] { "missing-model" })]
	public async Task InvalidOrUnknownModelIdIsRejectedBeforeDownload(string modelId)
	{
		string root = CreateRoot();
		try
		{
			int requests = 0;
			ZzzResourceDownloadService service = CreateService(root, delegate
			{
				requests++;
				return Response(Array.Empty<byte>());
			});
			try
			{
				await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.DownloadAsync("flash_classifier", modelId));
				Assert.Equal(0, requests);
				Assert.False(Directory.Exists(Path.Combine(root, "assets", "models", "escape")));
			}
			finally
			{
				if (service != null)
				{
					((IDisposable)service).Dispose();
				}
			}
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	/// <summary>未知资源解析失败后不应占用下载槽，重复调用仍返回未知资源。</summary>
	[Fact]
	public async Task UnknownResourceCanBeRetriedAfterDescriptorFailure()
	{
		string root = CreateRoot();
		try
		{
			ZzzResourceDownloadService service = CreateService(root, (HttpRequestMessage _) => Response(Array.Empty<byte>()));
			try
			{
				await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.DownloadAsync("unknown", "model"));
				await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.DownloadAsync("unknown", "model"));
				Assert.False(service.Cancel("unknown"));
			}
			finally
			{
				if (service != null)
				{
					((IDisposable)service).Dispose();
				}
			}
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	/// <summary>同一资源并行下载时，第二次调用不应覆盖首个任务的运行状态。</summary>
	[Fact]
	public async Task ConcurrentDownloadForSameResourceKeepsFirstDownloadStatus()
	{
		string root = CreateRoot();
		try
		{
			WriteModelConfig(root, "flash_classifier: yolov8n-640-flash-20250921\n");
			byte[] archive = CreateZip(new Dictionary<string, string>
			{
				["model.onnx"] = "model",
				["labels.csv"] = "labels"
			});
			ZzzResourceDownloadService service = CreateService(root, (HttpRequestMessage _) => Response(archive, slow: true));
			try
			{
				Task first = service.DownloadAsync("flash_classifier", "yolov8n-640-flash-20250921");
				Assert.True(SpinWait.SpinUntil(() => service.GetItems().Single((ZzzResourceDownloadItemDto item) => item.ResourceId == "flash_classifier").Status.IsRunning, TimeSpan.FromSeconds(2L)));
				await Assert.ThrowsAsync<InvalidOperationException>(() => service.DownloadAsync("flash_classifier", "yolov8n-640-flash-20250921"));
				ZzzResourceDownloadStatusDto running = service.GetItems().Single((ZzzResourceDownloadItemDto item) => item.ResourceId == "flash_classifier").Status;
				Assert.True(running.IsRunning);
				Assert.Equal("yolov8n-640-flash-20250921", running.ModelId);
				Assert.True(service.Cancel("flash_classifier"));
				await first;
			}
			finally
			{
				if (service != null)
				{
					((IDisposable)service).Dispose();
				}
			}
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	/// <summary>新安装替换失败时应恢复原安装并删除临时备份。</summary>
	[Fact]
	public async Task FailedReplacementRestoresOldInstallation()
	{
		string root = CreateRoot();
		try
		{
			string target = CreateInstalledFlashModel(root, "old-model", "old-labels");
			byte[] archive = CreateZip(new Dictionary<string, string>
			{
				["model.onnx"] = "new-model",
				["labels.csv"] = "new-labels"
			});
			using ZzzResourceDownloadService service = CreateService(moveDirectory: delegate(string source, string destination)
			{
				if (source.Contains(".install-", StringComparison.Ordinal))
				{
					throw new IOException("install move failed");
				}
				Directory.Move(source, destination);
			}, root: root, responder: (HttpRequestMessage _) => Response(archive));
			await service.DownloadAsync("flash_classifier", "yolov8n-640-flash-20250921");
			Assert.Equal("old-model", File.ReadAllText(Path.Combine(target, "model.onnx")));
			Assert.Equal("old-labels", File.ReadAllText(Path.Combine(target, "labels.csv")));
			Assert.Empty(Directory.EnumerateDirectories(Path.GetDirectoryName(target), "yolov8n-640-flash-20250921.backup-*"));
			Assert.Equal("下载资源失败 请尝试更换代理", service.GetItems().Single((ZzzResourceDownloadItemDto item) => item.ResourceId == "flash_classifier").Status.Message);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	/// <summary>旧安装恢复失败时应保留备份目录及原文件。</summary>
	[Fact]
	public async Task FailedRollbackPreservesBackupDirectory()
	{
		string root = CreateRoot();
		try
		{
			string target = CreateInstalledFlashModel(root, "old-model", "old-labels");
			byte[] archive = CreateZip(new Dictionary<string, string>
			{
				["model.onnx"] = "new-model",
				["labels.csv"] = "new-labels"
			});
			using ZzzResourceDownloadService service = CreateService(moveDirectory: delegate(string source, string destination)
			{
				if (source.Contains(".install-", StringComparison.Ordinal) || source.Contains(".backup-", StringComparison.Ordinal))
				{
					throw new IOException("move failed");
				}
				Directory.Move(source, destination);
			}, root: root, responder: (HttpRequestMessage _) => Response(archive));
			await service.DownloadAsync("flash_classifier", "yolov8n-640-flash-20250921");
			string backup = Assert.Single(Directory.EnumerateDirectories(Path.GetDirectoryName(target), "yolov8n-640-flash-20250921.backup-*"));
			Assert.False(Directory.Exists(target));
			Assert.Equal("old-model", File.ReadAllText(Path.Combine(backup, "model.onnx")));
			Assert.Equal("old-labels", File.ReadAllText(Path.Combine(backup, "labels.csv")));
			ZzzResourceDownloadStatusDto status = service.GetItems().Single((ZzzResourceDownloadItemDto item) => item.ResourceId == "flash_classifier").Status;
			Assert.Contains("备份目录已保留", status.Error, StringComparison.Ordinal);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	/// <summary>目录应来自全局 model.yml 和真实已安装模型。</summary>
	[Fact]
	public void CatalogUsesGlobalModelConfigAndScansInstalledYoloDirectories()
	{
		string text = CreateRoot();
		try
		{
			Directory.CreateDirectory(Path.Combine(text, "config"));
			File.WriteAllText(Path.Combine(text, "config", "model.yml"), "flash_classifier: yolov8n-640-flash-20250906\nocr: ppocrv5\nocr_use_gpu: true\n");
			foreach (string installedModel in new string[2] { "yolov8n-640-flash-20250906", "local-model" })
			{
				string[] buffer = new string[5];
				buffer[0] = text;
				buffer[1] = "assets";
				buffer[2] = "models";
				buffer[3] = "flash_classifier";
				buffer[4] = installedModel;
				string text2 = Path.Combine(buffer);
				Directory.CreateDirectory(text2);
				File.WriteAllText(Path.Combine(text2, "model.onnx"), "model");
				File.WriteAllText(Path.Combine(text2, "labels.csv"), "labels");
			}
			using ZzzResourceDownloadService zzzResourceDownloadService = CreateService(text, (HttpRequestMessage _) => Response(Array.Empty<byte>()));
			IReadOnlyList<ZzzResourceDownloadItemDto> items = zzzResourceDownloadService.GetItems();
			ZzzResourceDownloadItemDto zzzResourceDownloadItemDto = items.Single((ZzzResourceDownloadItemDto item) => item.ResourceId == "ocr");
			Assert.Equal("ppocrv5", zzzResourceDownloadItemDto.SelectedModelId);
			Assert.True(zzzResourceDownloadItemDto.UseGpu);
			Assert.Equal(new string[2] { "ppocrv5", "ppocrv6" }, zzzResourceDownloadItemDto.Options.Select((ZzzResourceModelOptionDto option) => option.ModelId));
			ZzzResourceDownloadItemDto zzzResourceDownloadItemDto2 = items.Single((ZzzResourceDownloadItemDto item) => item.ResourceId == "flash_classifier");
			Assert.Equal("yolov8n-640-flash-20250906", zzzResourceDownloadItemDto2.SelectedModelId);
			Assert.True(zzzResourceDownloadItemDto2.Status.IsInstalled);
			Assert.Contains((IEnumerable<ZzzResourceModelOptionDto>)zzzResourceDownloadItemDto2.Options, (Predicate<ZzzResourceModelOptionDto>)((ZzzResourceModelOptionDto option) => option.ModelId == "local-model"));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	private static ZzzResourceDownloadService CreateService(string root, Func<HttpRequestMessage, HttpResponseMessage> responder, Action<string, string>? moveDirectory = null)
	{
		return new ZzzResourceDownloadService(root, NullLogger<ZzzResourceDownloadService>.Instance, (string? _) => new FakeHandler(responder), moveDirectory);
	}

	private static HttpResponseMessage Response(byte[] content, bool slow = false)
	{
		Stream content2 = (slow ? new BlockingStream(content) : new MemoryStream(content, writable: false));
		StreamContent streamContent = new StreamContent(content2);
		streamContent.Headers.ContentLength = content.Length;
		streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
		return new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = streamContent
		};
	}

	private static byte[] CreateZip(IReadOnlyDictionary<string, string> files)
	{
		using MemoryStream memoryStream = new MemoryStream();
		using (ZipArchive zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
		{
			foreach (KeyValuePair<string, string> file in files)
			{
				file.Deconstruct(out var key, out var value);
				string entryName = key;
				string value2 = value;
				ZipArchiveEntry zipArchiveEntry = zipArchive.CreateEntry(entryName);
				using StreamWriter streamWriter = new StreamWriter(zipArchiveEntry.Open());
				streamWriter.Write(value2);
			}
		}
		return memoryStream.ToArray();
	}

	private static string CreateRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), $"zzz-resource-download-{Guid.NewGuid():N}");
		Directory.CreateDirectory(text);
		return text;
	}

	private static void WriteModelConfig(string root, string content)
	{
		Directory.CreateDirectory(Path.Combine(root, "config"));
		File.WriteAllText(Path.Combine(root, "config", "model.yml"), content);
	}

	private static string CreateInstalledFlashModel(string root, string model, string labels)
	{
		WriteModelConfig(root, "flash_classifier: yolov8n-640-flash-20250921\n");
		string[] buffer = new string[5];
		buffer[0] = root;
		buffer[1] = "assets";
		buffer[2] = "models";
		buffer[3] = "flash_classifier";
		buffer[4] = "yolov8n-640-flash-20250921";
		string text = Path.Combine(buffer);
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "model.onnx"), model);
		File.WriteAllText(Path.Combine(text, "labels.csv"), labels);
		return text;
	}
}
