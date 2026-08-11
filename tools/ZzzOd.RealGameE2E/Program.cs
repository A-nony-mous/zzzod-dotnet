using System.Diagnostics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Runtime.InteropServices;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Ocr;
using OneDragon.Core.Runtime;
using OneDragon.Core.Screen;
using OneDragon.Core.Windows.Platform;
using OneDragon.Core.Yolo;
using OpenCvSharp;
using ZzzOd.AppHost;
using ZzzOd.AppHost.E2E;
using ZzzOd.GameLogic.Application;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Controller;
using ZzzOd.GameLogic.E2E;
using ZzzOd.GameLogic.Operations;
using ZzzOd.GameLogic.Operations.EnterGame;
using GeometryPoint = OneDragon.Core.Abstractions.Geometry.Point;

namespace ZzzOd.RealGameE2E;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        Console.InputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        WindowsDpiAwareness.TryEnablePerMonitorDpiAwareness();

        RealGameE2ECommandLine commandLine = RealGameE2ECommandLine.Parse(args);
        ZzzRunRootResolution runRootResolution = ZzzRunRootResolver.Resolve(commandLine.RunRootArguments);
        string command = commandLine.Command;
        string rootDirectory = runRootResolution.RunRoot.Path;
        Directory.SetCurrentDirectory(rootDirectory);
        OneDragonEnvironment environment = new(rootDirectory);
        E2EAutomationProfile profile = E2EAutomationProfile.Load(environment);
        ApplyInstanceConfigOverlay(commandLine.InstanceConfigRoot, rootDirectory, profile.InstanceIndex);
        E2EResourceValidationResult resources = new E2EResourceValidator().Validate(environment, profile);
		resources.RunRootSource = runRootResolution.Source.ToString();
        string evidenceDirectory = profile.ResolveEvidenceOutputDirectory(environment);
        Directory.CreateDirectory(evidenceDirectory);

        return command switch
        {
            "prepare-only" => RunPrepareOnly(profile, resources, evidenceDirectory),
            "preflight" => await RunPreflightAsync(environment, profile, resources, evidenceDirectory).ConfigureAwait(false),
            "run-app" => await RunAppAsync(environment, profile, resources, evidenceDirectory, commandLine.CommandArguments.FirstOrDefault()).ConfigureAwait(false),
            "run-app-f10" => await RunAppAsync(environment, profile, resources, evidenceDirectory, commandLine.CommandArguments.FirstOrDefault(), enableF10Stop: true).ConfigureAwait(false),
            "run-app-current-f10" => await RunAppAsync(environment, profile, resources, evidenceDirectory, commandLine.CommandArguments.FirstOrDefault(), enableF10Stop: true, keepCurrentScreen: true).ConfigureAwait(false),
            "run-auto-battle-f10" => await RunAppAsync(environment, profile, resources, evidenceDirectory, "auto_battle", enableF10Stop: true, keepCurrentScreen: true).ConfigureAwait(false),
            "capture-current" => RunCaptureCurrent(environment, profile, resources, evidenceDirectory, commandLine.CommandArguments.FirstOrDefault()),
            "probe-key-j" => await RunProbeKeyJAsync(environment, profile, resources, evidenceDirectory, commandLine.CommandArguments.FirstOrDefault()).ConfigureAwait(false),
            "probe-click-point" => await RunProbeClickPointAsync(environment, profile, resources, evidenceDirectory, commandLine.CommandArguments),
            "ocr-image" => RunOcrImage(environment, profile, resources, commandLine.CommandArguments),
            "recognize-image" => RunRecognizeImage(environment, profile, resources, commandLine.CommandArguments),
            "benchmark-flash-onnx" => RunFlashOnnxBenchmark(environment, profile, resources, commandLine.CommandArguments),
            "benchmark-autobattle-load" => await RunAutoBattleLoadBenchmark(environment, profile, resources, commandLine.CommandArguments).ConfigureAwait(false),
            _ => Usage(command),
        };
    }

    /// <summary>
    /// 执行不创建游戏上下文的 E2E 资源预检，并写入 evidence。
    /// </summary>
    /// <param name="profile">E2E 配置。</param>
    /// <param name="resources">资源校验结果。</param>
    /// <param name="evidenceDirectory">evidence 输出目录。</param>
    /// <returns>资源完整时返回 0，否则返回 2。</returns>
    private static int RunPrepareOnly(
        E2EAutomationProfile profile,
        E2EResourceValidationResult resources,
        string evidenceDirectory)
    {
        DateTimeOffset startedAtUtc = DateTimeOffset.UtcNow;
        RealGameEvidence evidence = RealGameEvidence.Create("prepare-only", Environment.CommandLine, profile, resources, startedAtUtc);
        evidence.ResultStatus = resources.IsValid ? RealGameEvidenceStatus.Pass : RealGameEvidenceStatus.Blocked;
        evidence.ResultMessage = resources.IsValid
            ? "E2E resources are ready; game context was not created."
            : resources.FailureSummary;
        evidence.Finish(DateTimeOffset.UtcNow);
        WriteEvidence(evidenceDirectory, "prepare-only.json", evidence);
        Console.WriteLine(JsonSerializer.Serialize(evidence, JsonOptions));
        return resources.IsValid ? 0 : 2;
    }

    /// <summary>
    /// 将显式给出的实例配置复制到 staging 的可变实例目录。
    /// </summary>
    /// <param name="sourceRoot">实例配置源目录。</param>
    /// <param name="runRoot">已解析的 staging 运行根。</param>
    /// <param name="instanceIndex">E2E profile 选择的实例编号。</param>
    private static void ApplyInstanceConfigOverlay(string? sourceRoot, string runRoot, int instanceIndex)
    {
        if (string.IsNullOrWhiteSpace(sourceRoot))
        {
            return;
        }

        if (!Path.IsPathFullyQualified(sourceRoot))
        {
            throw new ArgumentException("--instance-config-root 必须使用绝对路径。", nameof(sourceRoot));
        }

        string sourceFullPath = Path.GetFullPath(sourceRoot);
        if (!Directory.Exists(sourceFullPath))
        {
            throw new DirectoryNotFoundException($"实例配置源目录不存在: {sourceFullPath}");
        }

        string configRoot = Path.GetFullPath(Path.Combine(runRoot, "config"));
        string destinationRoot = Path.GetFullPath(Path.Combine(configRoot, instanceIndex.ToString("00")));
        if (!destinationRoot.StartsWith(configRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"实例配置目标路径越出 staging config: {destinationRoot}");
        }

        if (string.Equals(sourceFullPath, destinationRoot, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (Directory.Exists(destinationRoot))
        {
            Directory.Delete(destinationRoot, recursive: true);
        }

        foreach (string sourceFile in Directory.EnumerateFiles(sourceFullPath, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceFullPath, sourceFile);
            string destinationFile = Path.Combine(destinationRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
            File.Copy(sourceFile, destinationFile);
        }
    }

    private static int RunCaptureCurrent(
        OneDragonEnvironment environment,
        E2EAutomationProfile profile,
        E2EResourceValidationResult resources,
        string evidenceDirectory,
        string? appId)
    {
        string resolvedAppId = string.IsNullOrWhiteSpace(appId) ? "capture-current" : appId.Trim();
        DateTimeOffset startedAtUtc = DateTimeOffset.UtcNow;
        var evidence = RealGameEvidence.Create(resolvedAppId, Environment.CommandLine, profile, resources, startedAtUtc);
        evidence.ApplicationId = resolvedAppId;

        try
        {
            resources.EnsureValid();
            using ZContext context = CreateInitializedContext(environment, profile);
            Environment.SetEnvironmentVariable(ActionLevelDebugEvidenceWriter.DirectoryEnvironmentVariable, evidenceDirectory);
            Environment.SetEnvironmentVariable(ActionLevelDebugEvidenceWriter.AppIdEnvironmentVariable, resolvedAppId);
            evidence.GamePath = context.GameAccountConfig.GamePath;
            evidence.GameRegion = context.GameAccountConfig.GameRegion;
            evidence.WindowTitle = context.GameWindowTitle;
            bool windowReady = TryInitializeExistingGameWindow(context, evidence, out _, out string? initializationFailure);
            evidence.CaptureReadiness = Capture(context, profile, evidenceDirectory, $"{resolvedAppId}-capture-current");
            if (windowReady)
            {
                RealGamePreflightObservation? observation = ObservePreflightScreen(context, evidence.CaptureReadiness?.ScreenshotPath);
                if (observation is not null)
                {
                    evidence.RecognitionSummary.Add(
                        $"Captured current screen: world={observation.WorldScreenName ?? "<none>"}, active={observation.ActiveScreenName ?? "<none>"}, ocr={string.Join("|", observation.OcrTexts.Take(12))}");
                }
            }
            else if (!string.IsNullOrWhiteSpace(initializationFailure))
            {
                evidence.RecognitionSummary.Add(initializationFailure);
            }

            evidence.ResultStatus = windowReady ? RealGameEvidenceStatus.Pass : RealGameEvidenceStatus.Blocked;
            evidence.ResultMessage = windowReady ? "captured current game window" : "game window was not usable";
            evidence.Finish(DateTimeOffset.UtcNow);
            WriteEvidence(evidenceDirectory, $"{resolvedAppId}-capture-current.json", evidence);
            Console.WriteLine(JsonSerializer.Serialize(evidence, JsonOptions));
            return windowReady ? 0 : 2;
        }
        catch (Exception exception)
        {
            evidence.ResultStatus = RealGameEvidenceStatus.Fail;
            evidence.ResultMessage = exception.ToString();
            evidence.Finish(DateTimeOffset.UtcNow);
            WriteEvidence(evidenceDirectory, $"{resolvedAppId}-capture-current.json", evidence);
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static int RunOcrImage(
        OneDragonEnvironment environment,
        E2EAutomationProfile profile,
		E2EResourceValidationResult resources,
        IReadOnlyList<string> imagePaths)
    {
		resources.EnsureValid();
        if (imagePaths.Count == 0)
        {
            return Usage("ocr-image requires at least one image path");
        }

        using ZContext context = new(environment, instanceIndex: profile.InstanceIndex);
        context.ScreenContext.Reload();
        string profileId = profile.OcrProfile ?? context.ModelConfig.ResolveOcrProfile().Profile.Id;
        double detLimitSideLen = Math.Max(context.ProjectConfig.ScreenStandardWidth, context.ProjectConfig.ScreenStandardHeight);
        if (!context.UseOcrProfile(profileId, detLimitSideLen: detLimitSideLen))
        {
            Console.Error.WriteLine($"OCR profile 初始化失败: {profileId}");
            return 1;
        }

        var results = new List<ImageOcrEvidence>();
        foreach (string imagePath in imagePaths)
        {
            string resolvedPath = Path.GetFullPath(imagePath);
            using Mat image = Cv2.ImRead(resolvedPath);
            if (image.Empty())
            {
                results.Add(new ImageOcrEvidence(resolvedPath, 0, 0, profileId, [], "image-empty"));
                continue;
            }

            IReadOnlyList<OcrMatchResult> words = context.OcrService.Matcher.Ocr(image, threshold: 0d, mergeLineDistance: -1d);
            results.Add(new ImageOcrEvidence(
                resolvedPath,
                image.Width,
                image.Height,
                profileId,
                words
                    .OrderBy(word => word.Y)
                    .ThenBy(word => word.X)
                    .Select(ImageOcrWordEvidence.From)
                    .ToArray(),
                null));
        }

        Console.WriteLine(JsonSerializer.Serialize(results, JsonOptions));
        return 0;
    }

    private static int RunRecognizeImage(
        OneDragonEnvironment environment,
        E2EAutomationProfile profile,
		E2EResourceValidationResult resources,
        IReadOnlyList<string> imagePaths)
    {
		resources.EnsureValid();
        if (imagePaths.Count == 0)
        {
            return Usage("recognize-image requires at least one image path");
        }

        using ZContext context = new(environment, instanceIndex: profile.InstanceIndex);
        context.ScreenContext.Reload();
        string profileId = profile.OcrProfile ?? context.ModelConfig.ResolveOcrProfile().Profile.Id;
        double detLimitSideLen = Math.Max(context.ProjectConfig.ScreenStandardWidth, context.ProjectConfig.ScreenStandardHeight);
        if (!context.UseOcrProfile(profileId, detLimitSideLen: detLimitSideLen))
        {
            Console.Error.WriteLine($"OCR profile 初始化失败: {profileId}");
            return 1;
        }

        var results = new List<ImageRecognitionEvidence>();
        foreach (string imagePath in imagePaths)
        {
            string resolvedPath = Path.GetFullPath(imagePath);
            using Mat image = Cv2.ImRead(resolvedPath);
            if (image.Empty())
            {
                results.Add(new ImageRecognitionEvidence(resolvedPath, 0, 0, profileId, null, null, [], "image-empty"));
                continue;
            }

            string? worldScreen = ScreenUtils.GetMatchScreenName(context, image, ["大世界-普通", "大世界-勘域"]);
            string? activeScreen = ScreenUtils.GetMatchScreenName(context, image);
            results.Add(new ImageRecognitionEvidence(
                resolvedPath,
                image.Width,
                image.Height,
                profileId,
                worldScreen,
                activeScreen,
                [
                    AreaRecognitionEvidence.From(context, image, "大世界", "信息", binary: true),
                    AreaRecognitionEvidence.From(context, image, "大世界", "星期"),
                    AreaRecognitionEvidence.From(context, image, "大世界-普通", "功能导览"),
                    AreaRecognitionEvidence.From(context, image, "大世界-普通", "预备编队"),
                    AreaRecognitionEvidence.From(context, image, "画面-通用", "返回"),
                    AreaRecognitionEvidence.From(context, image, "菜单", "按钮-返回"),
                    AreaRecognitionEvidence.From(context, image, "菜单", "菜单-动态壁纸"),
                ],
                null));
        }

        Console.WriteLine(JsonSerializer.Serialize(results, JsonOptions));
        return 0;
    }

    private static int RunFlashOnnxBenchmark(
        OneDragonEnvironment environment,
        E2EAutomationProfile profile,
		E2EResourceValidationResult resources,
        IReadOnlyList<string> args)
    {
		resources.EnsureValid();
        string imagePath = args.Count > 0 && !string.IsNullOrWhiteSpace(args[0])
            ? Path.GetFullPath(args[0])
            : Path.Combine(
                environment.WorkDirectory,
                "zzzod-dotnet",
                "tests",
                "ZzzOd.GameLogic.Tests",
                "TestData",
                "IntelBoard",
                "intel-board-running.png");
        int iterations = args.Count > 1 && int.TryParse(args[1], out int parsedIterations)
            ? Math.Clamp(parsedIterations, 10, 2000)
            : 100;
        int warmupIterations = Math.Clamp(iterations / 10, 5, 20);

        using Mat bgr = Cv2.ImRead(imagePath, ImreadModes.Color);
        if (bgr.Empty())
        {
            Console.Error.WriteLine($"基准输入图片无法读取: {imagePath}");
            return 1;
        }

        using ZContext context = new(environment, instanceIndex: profile.InstanceIndex);
        if (!context.FlashClassifier.InitModel())
        {
            Console.Error.WriteLine("闪光识别模型初始化失败");
            return 1;
        }

        string modelPath = context.FlashClassifier.CoreClassifier.Config.ModelPath;

        for (int index = 0; index < warmupIterations; index++)
        {
            _ = context.FlashClassifier.CoreClassifier.Run(bgr);
        }

        var classifierDiagnostics = new List<YoloClassificationRunDiagnostics>(iterations);
        for (int index = 0; index < iterations; index++)
        {
            classifierDiagnostics.Add(context.FlashClassifier.CoreClassifier.RunWithDiagnostics(bgr));
        }

        double[] colorConversionElapsed = new double[iterations];

        double[] fullPipelineElapsed = Measure(iterations, () =>
        {
            _ = context.FlashClassifier.CoreClassifier.Run(bgr);
        });

        using SessionOptions sessionOptions = new()
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
            EnableMemoryPattern = true,
        };
        sessionOptions.AppendExecutionProvider_CPU();
        using InferenceSession session = new(modelPath, sessionOptions);
        KeyValuePair<string, NodeMetadata> input = session.InputMetadata.Single();
        int[] inputDimensions = input.Value.Dimensions.Select(dimension => checked((int)dimension)).ToArray();
        int inputElementCount = inputDimensions.Aggregate(1, checked((count, dimension) => count * dimension));
        var inputTensor = new DenseTensor<float>(new float[inputElementCount], inputDimensions);
        IReadOnlyCollection<NamedOnnxValue> inputs =
        [
            NamedOnnxValue.CreateFromTensor(input.Key, inputTensor),
        ];

        for (int index = 0; index < warmupIterations; index++)
        {
            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = session.Run(inputs);
        }

        double[] inferenceOnlyElapsed = Measure(iterations, () =>
        {
            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = session.Run(inputs);
        });

        var output = new
        {
            ImagePath = imagePath,
            ImageWidth = bgr.Width,
            ImageHeight = bgr.Height,
            ModelPath = modelPath,
            Provider = "CPUExecutionProvider",
            InputDimensions = inputDimensions,
            Iterations = iterations,
            WarmupIterations = warmupIterations,
            SessionOptions = new
            {
                GraphOptimizationLevel = "ORT_ENABLE_ALL",
                ExecutionMode = "ORT_SEQUENTIAL",
                EnableMemoryPattern = true,
                IntraOpNumThreads = (int?)null,
                InterOpNumThreads = (int?)null,
            },
            ColorConversion = CalculateLatencyStats(colorConversionElapsed),
            InferenceOnly = CalculateLatencyStats(inferenceOnlyElapsed),
            ClassifierWithoutColorConversion = new
            {
                Preprocess = CalculateLatencyStats(classifierDiagnostics.Select(item => item.PreprocessElapsedMilliseconds)),
                Inference = CalculateLatencyStats(classifierDiagnostics.Select(item => item.InferenceElapsedMilliseconds)),
                Postprocess = CalculateLatencyStats(classifierDiagnostics.Select(item => item.PostprocessElapsedMilliseconds)),
                Total = CalculateLatencyStats(classifierDiagnostics.Select(item => item.TotalElapsedMilliseconds)),
            },
            FullPipeline = CalculateLatencyStats(fullPipelineElapsed),
        };
        Console.WriteLine(JsonSerializer.Serialize(output, JsonOptions));
        return 0;
    }

	private static async Task<int> RunAutoBattleLoadBenchmark(
		OneDragonEnvironment environment,
		E2EAutomationProfile profile,
		E2EResourceValidationResult resources,
		IReadOnlyList<string> args)
	{
		resources.EnsureValid();
		return await AutoBattleLoadBenchmark.RunAsync(environment, profile, args, JsonOptions).ConfigureAwait(false);
	}

    private static double[] Measure(int iterations, Action action)
    {
        var elapsed = new double[iterations];
        for (int index = 0; index < iterations; index++)
        {
            long startedAt = Stopwatch.GetTimestamp();
            action();
            elapsed[index] = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        }

        return elapsed;
    }

    private static object CalculateLatencyStats(IEnumerable<double> samples)
    {
        double[] sorted = samples.Order().ToArray();
        return new
        {
            MinMilliseconds = sorted[0],
            MeanMilliseconds = sorted.Average(),
            P50Milliseconds = Percentile(sorted, 0.50d),
            P95Milliseconds = Percentile(sorted, 0.95d),
            P99Milliseconds = Percentile(sorted, 0.99d),
            MaxMilliseconds = sorted[^1],
        };
    }

    private static double Percentile(IReadOnlyList<double> sorted, double percentile)
    {
        double position = (sorted.Count - 1) * percentile;
        int lower = (int)Math.Floor(position);
        int upper = (int)Math.Ceiling(position);
        if (lower == upper)
        {
            return sorted[lower];
        }

        double weight = position - lower;
        return sorted[lower] + ((sorted[upper] - sorted[lower]) * weight);
    }

    private static async Task<int> RunPreflightAsync(
        OneDragonEnvironment environment,
        E2EAutomationProfile profile,
        E2EResourceValidationResult resources,
        string evidenceDirectory)
    {
        DateTimeOffset startedAtUtc = DateTimeOffset.UtcNow;
        var evidence = RealGameEvidence.Create("preflight", Environment.CommandLine, profile, resources, startedAtUtc);

        try
        {
            resources.EnsureValid();
            using ZContext context = CreateInitializedContext(environment, profile);
            Environment.SetEnvironmentVariable(ActionLevelDebugEvidenceWriter.DirectoryEnvironmentVariable, evidenceDirectory);
            Environment.SetEnvironmentVariable(ActionLevelDebugEvidenceWriter.AppIdEnvironmentVariable, "preflight");
            evidence.GamePath = context.GameAccountConfig.GamePath;
            evidence.GameRegion = context.GameAccountConfig.GameRegion;
            evidence.WindowTitle = context.GameWindowTitle;
            evidence.CaptureReadiness = Capture(context, profile, evidenceDirectory, "preflight-initial");

            bool windowReady = TryInitializeExistingGameWindow(context, evidence, out bool windowExists, out string? initializationFailure);
            if (TryBlockUnsafePreflightScreen(context, "preflight", evidence, windowReady, out string? blockedReason))
            {
                evidence.ResultStatus = RealGameEvidenceStatus.Blocked;
                evidence.ResultMessage = blockedReason;
                evidence.Finish(DateTimeOffset.UtcNow);
                WriteEvidence(evidenceDirectory, "preflight.json", evidence);
                Console.WriteLine(JsonSerializer.Serialize(evidence, JsonOptions));
                return 2;
            }

            OperationResult result = await RealGamePreflightRunner.ResolveSessionAsync(
                windowExists,
                windowReady,
                initializationFailure,
                () => new OpenAndEnterGame(context).ExecuteAsync(),
                () => new WaitNormalWorld(context, checkOnce: true).ExecuteAsync(),
                () => new BackToNormalWorld(context).ExecuteAsync(),
                () => new EnterGame(context).ExecuteAsync(),
                evidence.RecognitionSummary).ConfigureAwait(false);

            evidence.ResultStatus = result.IsSuccess ? RealGameEvidenceStatus.Pass : RealGameEvidenceStatus.Blocked;
            evidence.ResultMessage = result.Status;
            evidence.CaptureReadiness = Capture(context, profile, evidenceDirectory, "preflight-final") ?? evidence.CaptureReadiness;
            evidence.Finish(DateTimeOffset.UtcNow);
            WriteEvidence(evidenceDirectory, "preflight.json", evidence);
            Console.WriteLine(JsonSerializer.Serialize(evidence, JsonOptions));
            return result.IsSuccess ? 0 : 2;
        }
        catch (Exception exception)
        {
            evidence.ResultStatus = RealGameEvidenceStatus.Fail;
            evidence.ResultMessage = exception.ToString();
            evidence.Finish(DateTimeOffset.UtcNow);
            WriteEvidence(evidenceDirectory, "preflight.json", evidence);
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static async Task<int> RunProbeKeyJAsync(
        OneDragonEnvironment environment,
        E2EAutomationProfile profile,
        E2EResourceValidationResult resources,
        string evidenceDirectory,
        string? appId)
    {
        string resolvedAppId = string.IsNullOrWhiteSpace(appId) ? "coffee" : appId.Trim();
        DateTimeOffset startedAtUtc = DateTimeOffset.UtcNow;
        var evidence = RealGameEvidence.Create(resolvedAppId, Environment.CommandLine, profile, resources, startedAtUtc);
        evidence.ApplicationId = resolvedAppId;

        try
        {
            resources.EnsureValid();
            using ZContext context = CreateInitializedContext(environment, profile);
            Environment.SetEnvironmentVariable(ActionLevelDebugEvidenceWriter.DirectoryEnvironmentVariable, evidenceDirectory);
            Environment.SetEnvironmentVariable(ActionLevelDebugEvidenceWriter.AppIdEnvironmentVariable, resolvedAppId);
            evidence.GamePath = context.GameAccountConfig.GamePath;
            evidence.GameRegion = context.GameAccountConfig.GameRegion;
            evidence.WindowTitle = context.GameWindowTitle;
            evidence.CaptureReadiness = Capture(context, profile, evidenceDirectory, $"{resolvedAppId}-probe-key-j-initial");

            bool windowReady = TryInitializeExistingGameWindow(context, evidence, out bool windowExists, out string? initializationFailure);
            if (TryBlockUnsafePreflightScreen(context, resolvedAppId, evidence, windowReady, out string? blockedReason))
            {
                evidence.ResultStatus = RealGameEvidenceStatus.Blocked;
                evidence.ResultMessage = blockedReason;
                evidence.Finish(DateTimeOffset.UtcNow);
                WriteEvidence(evidenceDirectory, $"{resolvedAppId}-probe-key-j.json", evidence);
                Console.WriteLine(JsonSerializer.Serialize(evidence, JsonOptions));
                return 2;
            }

            OperationResult preflight = await RealGamePreflightRunner.ResolveSessionAsync(
                windowExists,
                windowReady,
                initializationFailure,
                () => new OpenAndEnterGame(context).ExecuteAsync(),
                () => new WaitNormalWorld(context, checkOnce: true).ExecuteAsync(),
                () => new BackToNormalWorld(context).ExecuteAsync(),
                () => new EnterGame(context).ExecuteAsync(),
                evidence.RecognitionSummary).ConfigureAwait(false);
            if (!preflight.IsSuccess)
            {
                evidence.ResultStatus = RealGameEvidenceStatus.Blocked;
                evidence.ResultMessage = preflight.Status;
                evidence.CaptureReadiness = Capture(context, profile, evidenceDirectory, $"{resolvedAppId}-probe-key-j-blocked");
                evidence.Finish(DateTimeOffset.UtcNow);
                WriteEvidence(evidenceDirectory, $"{resolvedAppId}-probe-key-j.json", evidence);
                Console.WriteLine(JsonSerializer.Serialize(evidence, JsonOptions));
                return 2;
            }

            if (context.Controller is not ZPcController controller)
            {
                evidence.ResultStatus = RealGameEvidenceStatus.Fail;
                evidence.ResultMessage = "当前控制器不是 ZPcController，无法执行 key=j 探针。";
                evidence.Finish(DateTimeOffset.UtcNow);
                WriteEvidence(evidenceDirectory, $"{resolvedAppId}-probe-key-j.json", evidence);
                Console.WriteLine(JsonSerializer.Serialize(evidence, JsonOptions));
                return 1;
            }

            var transport = new Transport(context, "probe", "probe", waitAtLast: false);
            string fileStem = ActionLevelDebugEvidenceWriter.CreateFileStem($"{resolvedAppId}-probe-key-j");
            (_, Mat? before) = context.Controller.Screenshot();
            using (before)
            {
                MapScreenRecognitionSummary beforeSummary = transport.GetMapScreenRecognitionSummary(before);
                string? beforePath = ActionLevelDebugEvidenceWriter.WriteScreenshot(fileStem, "before", before);

                bool pressed = controller.OpenMap();
                await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false);

                (_, Mat? after) = context.Controller.Screenshot();
                using (after)
                {
                    MapScreenRecognitionSummary afterSummary = transport.GetMapScreenRecognitionSummary(after);
                    string? afterPath = ActionLevelDebugEvidenceWriter.WriteScreenshot(fileStem, "after", after);
                    ActionLevelDebugEvidenceWriter.Write(new ActionLevelDebugEvidence
                    {
                        FileStem = fileStem,
                        AppId = resolvedAppId,
                        OperationName = "transport-open-map-key-probe",
                        NodeName = "probe-key-j",
                        DotNetMethod = "ZzzOd.GameLogic.Controller.ZPcController.OpenMap()",
                        BaselineParityRequirement = "Transport.open_map uses click_area 大世界/地图; this probe only tests whether key=j works in the current foreground session.",
                        BeforeScreenshotPath = beforePath,
                        BeforeRecognitionSummary = beforeSummary,
                        ActionKind = "key_press",
                        ActionTarget = "key=j",
                        ExpectedNextState = "地图 page, IsMapScreen true",
                        AfterScreenshotPath = afterPath,
                        AfterRecognitionSummary = afterSummary,
                        TransitionResult = afterSummary.IsMapScreen ? "entered_map" : pressed ? "key_sent_but_not_entered_map" : "key_send_failed",
                        FailureReason = afterSummary.IsMapScreen ? null : pressed ? "after key=j did not satisfy IsMapScreen" : "ZPcController.OpenMap returned false",
                        RetryStoppedBecauseOfSuspectedLoop = false,
                    });

                    evidence.ResultStatus = afterSummary.IsMapScreen ? RealGameEvidenceStatus.Pass : RealGameEvidenceStatus.Fail;
                    evidence.ResultMessage = afterSummary.IsMapScreen ? "key=j entered map" : "key=j did not enter map";
                    evidence.CaptureReadiness = Capture(context, profile, evidenceDirectory, $"{resolvedAppId}-probe-key-j-after");
                    evidence.Finish(DateTimeOffset.UtcNow);
                    WriteEvidence(evidenceDirectory, $"{resolvedAppId}-probe-key-j.json", evidence);
                    Console.WriteLine(JsonSerializer.Serialize(evidence, JsonOptions));
                    return afterSummary.IsMapScreen ? 0 : 2;
                }
            }
        }
        catch (Exception exception)
        {
            evidence.ResultStatus = RealGameEvidenceStatus.Fail;
            evidence.ResultMessage = exception.ToString();
            evidence.Finish(DateTimeOffset.UtcNow);
            WriteEvidence(evidenceDirectory, $"{resolvedAppId}-probe-key-j.json", evidence);
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static async Task<int> RunProbeClickPointAsync(
        OneDragonEnvironment environment,
        E2EAutomationProfile profile,
        E2EResourceValidationResult resources,
        string evidenceDirectory,
        IReadOnlyList<string> args)
    {
        if (args.Count < 3 ||
            string.IsNullOrWhiteSpace(args[0]) ||
            !int.TryParse(args[1], out int x) ||
            !int.TryParse(args[2], out int y))
        {
            return Usage("probe-click-point requires: <app_id> <x> <y> [pc_alt] [no-preflight]");
        }

        string resolvedAppId = args[0].Trim();
        bool pcAlt = args.Count < 4 || !bool.TryParse(args[3], out bool parsedPcAlt) || parsedPcAlt;
        bool skipPreflight = args.Any(arg => string.Equals(arg, "no-preflight", StringComparison.OrdinalIgnoreCase));
        DateTimeOffset startedAtUtc = DateTimeOffset.UtcNow;
        var evidence = RealGameEvidence.Create(resolvedAppId, Environment.CommandLine, profile, resources, startedAtUtc);
        evidence.ApplicationId = resolvedAppId;

        try
        {
            resources.EnsureValid();
            using ZContext context = CreateInitializedContext(environment, profile);
            Environment.SetEnvironmentVariable(ActionLevelDebugEvidenceWriter.DirectoryEnvironmentVariable, evidenceDirectory);
            Environment.SetEnvironmentVariable(ActionLevelDebugEvidenceWriter.AppIdEnvironmentVariable, resolvedAppId);
            evidence.GamePath = context.GameAccountConfig.GamePath;
            evidence.GameRegion = context.GameAccountConfig.GameRegion;
            evidence.WindowTitle = context.GameWindowTitle;
            evidence.CaptureReadiness = Capture(context, profile, evidenceDirectory, $"{resolvedAppId}-probe-click-point-initial");

            bool windowReady = TryInitializeExistingGameWindow(context, evidence, out bool windowExists, out string? initializationFailure);
            if (!windowReady && skipPreflight)
            {
                evidence.ResultStatus = RealGameEvidenceStatus.Blocked;
                evidence.ResultMessage = initializationFailure ?? "game window was not usable";
                evidence.Finish(DateTimeOffset.UtcNow);
                WriteEvidence(evidenceDirectory, $"{resolvedAppId}-probe-click-point.json", evidence);
                Console.WriteLine(JsonSerializer.Serialize(evidence, JsonOptions));
                return 2;
            }

            if (!skipPreflight && TryBlockUnsafePreflightScreen(context, resolvedAppId, evidence, windowReady, out string? blockedReason))
            {
                evidence.ResultStatus = RealGameEvidenceStatus.Blocked;
                evidence.ResultMessage = blockedReason;
                evidence.Finish(DateTimeOffset.UtcNow);
                WriteEvidence(evidenceDirectory, $"{resolvedAppId}-probe-click-point.json", evidence);
                Console.WriteLine(JsonSerializer.Serialize(evidence, JsonOptions));
                return 2;
            }

            if (!skipPreflight)
            {
                OperationResult preflight = await RealGamePreflightRunner.ResolveSessionAsync(
                    windowExists,
                    windowReady,
                    initializationFailure,
                    () => new OpenAndEnterGame(context).ExecuteAsync(),
                    () => new WaitNormalWorld(context, checkOnce: true).ExecuteAsync(),
                    () => new BackToNormalWorld(context).ExecuteAsync(),
                    () => new EnterGame(context).ExecuteAsync(),
                    evidence.RecognitionSummary).ConfigureAwait(false);
                if (!preflight.IsSuccess)
                {
                    evidence.ResultStatus = RealGameEvidenceStatus.Blocked;
                    evidence.ResultMessage = preflight.Status;
                    evidence.CaptureReadiness = Capture(context, profile, evidenceDirectory, $"{resolvedAppId}-probe-click-point-blocked");
                    evidence.Finish(DateTimeOffset.UtcNow);
                    WriteEvidence(evidenceDirectory, $"{resolvedAppId}-probe-click-point.json", evidence);
                    Console.WriteLine(JsonSerializer.Serialize(evidence, JsonOptions));
                    return 2;
                }
            }
            else
            {
                evidence.RecognitionSummary.Add("Skipped preflight for current-screen click probe.");
            }

            if (context.Controller is null)
            {
                evidence.ResultStatus = RealGameEvidenceStatus.Fail;
                evidence.ResultMessage = "当前上下文没有控制器，无法执行点击探针。";
                evidence.Finish(DateTimeOffset.UtcNow);
                WriteEvidence(evidenceDirectory, $"{resolvedAppId}-probe-click-point.json", evidence);
                Console.WriteLine(JsonSerializer.Serialize(evidence, JsonOptions));
                return 1;
            }

            var transport = new Transport(context, "probe", "probe", waitAtLast: false);
            string fileStem = ActionLevelDebugEvidenceWriter.CreateFileStem($"{resolvedAppId}-probe-click-point-{x}-{y}");
            (_, Mat? before) = context.Controller.Screenshot();
            using (before)
            {
                MapScreenRecognitionSummary beforeSummary = transport.GetMapScreenRecognitionSummary(before);
                string? beforePath = ActionLevelDebugEvidenceWriter.WriteScreenshot(fileStem, "before", before);
                bool clicked = context.Controller.Click(new GeometryPoint(x, y), pcAlt: pcAlt, gamepadAction: "map");
                await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false);

                (_, Mat? after) = context.Controller.Screenshot();
                using (after)
                {
                    MapScreenRecognitionSummary afterSummary = transport.GetMapScreenRecognitionSummary(after);
                    string? afterPath = ActionLevelDebugEvidenceWriter.WriteScreenshot(fileStem, "after", after);
                    ActionLevelDebugEvidenceWriter.Write(new ActionLevelDebugEvidence
                    {
                        FileStem = fileStem,
                        AppId = resolvedAppId,
                        OperationName = "transport-open-map-click-point-probe",
                        NodeName = "probe-click-point",
                        DotNetMethod = "OneDragon.Core.Controller.ControllerBase.Click(Point, pcAlt)",
                        BaselineParityRequirement = "Transport.open_map uses click_area 大世界/地图; this probe only searches the current clickable point for that area.",
                        BeforeScreenshotPath = beforePath,
                        BeforeRecognitionSummary = beforeSummary,
                        ActionKind = "click_area",
                        ActionTarget = $"大世界/地图 candidate x={x}, y={y}, pcAlt={pcAlt}",
                        ExpectedNextState = "地图 page, IsMapScreen true",
                        AfterScreenshotPath = afterPath,
                        AfterRecognitionSummary = afterSummary,
                        TransitionResult = afterSummary.IsMapScreen ? "entered_map" : clicked ? "clicked_but_not_entered_map" : "click_failed",
                        FailureReason = afterSummary.IsMapScreen ? null : clicked ? "after click did not satisfy IsMapScreen" : "Controller.Click returned false",
                        RetryStoppedBecauseOfSuspectedLoop = false,
                    });

                    evidence.ResultStatus = afterSummary.IsMapScreen ? RealGameEvidenceStatus.Pass : RealGameEvidenceStatus.Fail;
                    evidence.ResultMessage = afterSummary.IsMapScreen ? "click point entered map" : "click point did not enter map";
                    evidence.CaptureReadiness = Capture(context, profile, evidenceDirectory, $"{resolvedAppId}-probe-click-point-after");
                    evidence.Finish(DateTimeOffset.UtcNow);
                    WriteEvidence(evidenceDirectory, $"{resolvedAppId}-probe-click-point.json", evidence);
                    Console.WriteLine(JsonSerializer.Serialize(evidence, JsonOptions));
                    return afterSummary.IsMapScreen ? 0 : 2;
                }
            }
        }
        catch (Exception exception)
        {
            evidence.ResultStatus = RealGameEvidenceStatus.Fail;
            evidence.ResultMessage = exception.ToString();
            evidence.Finish(DateTimeOffset.UtcNow);
            WriteEvidence(evidenceDirectory, $"{resolvedAppId}-probe-click-point.json", evidence);
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static bool TryInitializeExistingGameWindow(
        ZContext context,
        RealGameEvidence evidence,
        out bool windowExists,
        out string? initializationFailure)
    {
        initializationFailure = null;
        windowExists = context.Controller?.IsGameWindowReady == true;
        if (!windowExists)
        {
            evidence.RecognitionSummary.Add("Initial game window is not usable.");
            return false;
        }

        try
        {
            bool ready = context.Controller?.InitBeforeContextRun() == true;
            evidence.RecognitionSummary.Add(ready ? "Initial game window is usable." : "Initial game window initialization returned false.");
            if (!ready)
            {
                initializationFailure = "Initial game window initialization returned false.";
            }

            return ready;
        }
        catch (InvalidOperationException exception)
        {
            initializationFailure = exception.Message;
            evidence.RecognitionSummary.Add($"Initial game window exists but initialization failed: {exception.Message}");
            return false;
        }
    }

    private static async Task<int> RunAppAsync(
        OneDragonEnvironment environment,
        E2EAutomationProfile profile,
        E2EResourceValidationResult resources,
        string evidenceDirectory,
        string? appId,
        bool enableF10Stop = false,
        bool keepCurrentScreen = false)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return Usage("run-app requires an application id");
        }

        DateTimeOffset startedAtUtc = DateTimeOffset.UtcNow;
        var evidence = RealGameEvidence.Create(appId, Environment.CommandLine, profile, resources, startedAtUtc);
        evidence.ApplicationId = appId;

        try
        {
            resources.EnsureValid();
            using ZContext context = CreateInitializedContext(environment, profile);
            Environment.SetEnvironmentVariable(ActionLevelDebugEvidenceWriter.DirectoryEnvironmentVariable, evidenceDirectory);
            Environment.SetEnvironmentVariable(ActionLevelDebugEvidenceWriter.AppIdEnvironmentVariable, appId);
            Environment.SetEnvironmentVariable(ActionLevelDebugEvidenceWriter.CaptureModeEnvironmentVariable, "targeted");
            evidence.GamePath = context.GameAccountConfig.GamePath;
            evidence.GameRegion = context.GameAccountConfig.GameRegion;
            evidence.WindowTitle = context.GameWindowTitle;
            if (!keepCurrentScreen)
            {
                evidence.CaptureReadiness = Capture(context, profile, evidenceDirectory, $"{appId}-initial");
            }

            bool windowReady = TryInitializeExistingGameWindow(context, evidence, out bool windowExists, out string? initializationFailure);
            if (keepCurrentScreen)
            {
                if (!windowReady)
                {
                    evidence.ResultStatus = RealGameEvidenceStatus.Blocked;
                    evidence.ResultMessage = initializationFailure ?? "当前游戏窗口不可用";
                    evidence.Finish(DateTimeOffset.UtcNow);
                    WriteEvidence(evidenceDirectory, $"{appId}.json", evidence);
                    Console.WriteLine(JsonSerializer.Serialize(evidence, JsonOptions));
                    return 2;
                }

                evidence.RecognitionSummary.Add("保留当前画面，跳过大世界前置检查。");
            }
            else
            {
                if (TryBlockUnsafePreflightScreen(context, appId, evidence, windowReady, out string? blockedReason))
                {
                    evidence.ResultStatus = RealGameEvidenceStatus.Blocked;
                    evidence.ResultMessage = blockedReason;
                    evidence.Finish(DateTimeOffset.UtcNow);
                    WriteEvidence(evidenceDirectory, $"{appId}.json", evidence);
                    Console.WriteLine(JsonSerializer.Serialize(evidence, JsonOptions));
                    return 2;
                }

                DiscardRoutineCapture(evidence);

                OperationResult preflight = await RealGamePreflightRunner.ResolveSessionAsync(
                    windowExists,
                    windowReady,
                    initializationFailure,
                    () => new OpenAndEnterGame(context).ExecuteAsync(),
                    () => new WaitNormalWorld(context, checkOnce: true).ExecuteAsync(),
                    () => new BackToNormalWorld(context).ExecuteAsync(),
                    () => new EnterGame(context).ExecuteAsync(),
                    evidence.RecognitionSummary).ConfigureAwait(false);
                if (!preflight.IsSuccess)
                {
                    evidence.ResultStatus = RealGameEvidenceStatus.Blocked;
                    evidence.ResultMessage = preflight.Status;
                    evidence.CaptureReadiness = Capture(context, profile, evidenceDirectory, $"{appId}-blocked") ?? evidence.CaptureReadiness;
                    evidence.Finish(DateTimeOffset.UtcNow);
                    WriteEvidence(evidenceDirectory, $"{appId}.json", evidence);
                    Console.WriteLine(JsonSerializer.Serialize(evidence, JsonOptions));
                    return 2;
                }

                OperationResult worldReady = await new WaitNormalWorld(context, checkOnce: true).ExecuteAsync().ConfigureAwait(false);
                if (!worldReady.IsSuccess)
                {
                    string reason = $"preflight completed but current screen is not a runnable world state: {worldReady.Status}";
                    evidence.RecognitionSummary.Add(reason);
                    evidence.ResultStatus = RealGameEvidenceStatus.Blocked;
                    evidence.ResultMessage = reason;
                    evidence.CaptureReadiness = Capture(context, profile, evidenceDirectory, $"{appId}-blocked-not-world") ?? evidence.CaptureReadiness;
                    evidence.Finish(DateTimeOffset.UtcNow);
                    WriteEvidence(evidenceDirectory, $"{appId}.json", evidence);
                    Console.WriteLine(JsonSerializer.Serialize(evidence, JsonOptions));
                    return 2;
                }

                evidence.RecognitionSummary.Add($"Confirmed runnable world state before app start: {worldReady.Status}");
            }
            ZApplicationFactory factory = (ZApplicationFactory)context.ApplicationFactoryRegistry.CreateFactory(appId);
            evidence.ApplicationGroupId = factory.GroupId;

            OperationResult result;
            using var stopKeyCancellation = new CancellationTokenSource();
            Task<OperationResult> runTask = context.RunContext
                .RunApplicationAsync(appId, profile.InstanceIndex, factory.GroupId, stopKeyCancellation.Token);
            if (enableF10Stop)
            {
                string stopKey = context.EnvConfig.KeyStopRunning;
                context.Logger.Information("应用 {AppId} 已启动，按 {StopKey} 停止并释放全部输入。", appId, stopKey);
                Task stopTask = WaitForStopKeyAsync(stopKey, stopKeyCancellation.Token);
                Task completed = await Task.WhenAny(runTask, stopTask).ConfigureAwait(false);
                if (ReferenceEquals(completed, stopTask))
                {
                    context.Logger.Information("收到 {StopKey}，停止应用 {AppId}。", stopKey, appId);
                    evidence.CaptureReadiness = Capture(context, profile, evidenceDirectory, $"{appId}-f10") ?? evidence.CaptureReadiness;
                    await context.RunContext.StopRunningAsync().ConfigureAwait(false);
                    stopKeyCancellation.Cancel();
                    try
                    {
                        await runTask.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                    }

                    evidence.ResultStatus = RealGameEvidenceStatus.Stopped;
                    evidence.ResultMessage = $"用户按 {stopKey.ToUpperInvariant()} 停止应用";
                    evidence.Finish(DateTimeOffset.UtcNow);
                    WriteEvidence(evidenceDirectory, $"{appId}.json", evidence);
                    Console.WriteLine(JsonSerializer.Serialize(evidence, JsonOptions));
                    return 0;
                }

                stopKeyCancellation.Cancel();
            }

            result = await runTask.ConfigureAwait(false);

            evidence.ResultStatus = result.IsSuccess ? RealGameEvidenceStatus.Pass : RealGameEvidenceStatus.Fail;
            evidence.ResultMessage = result.Status;
            if (!result.IsSuccess && !keepCurrentScreen)
            {
                evidence.CaptureReadiness = Capture(context, profile, evidenceDirectory, $"{appId}-after") ?? evidence.CaptureReadiness;
            }
            evidence.Finish(DateTimeOffset.UtcNow);
            WriteEvidence(evidenceDirectory, $"{appId}.json", evidence);
            Console.WriteLine(JsonSerializer.Serialize(evidence, JsonOptions));
            return result.IsSuccess ? 0 : 2;
        }
        catch (Exception exception)
        {
            evidence.ResultStatus = RealGameEvidenceStatus.Fail;
            evidence.ResultMessage = exception.ToString();
            if (resources.IsValid)
            {
                try
                {
                    using ZContext context = CreateInitializedContext(environment, profile);
                    if (TryInitializeExistingGameWindow(context, evidence, out _, out _))
                    {
                        evidence.CaptureReadiness = Capture(context, profile, evidenceDirectory, $"{appId}-exception");
                    }
                }
                catch
                {
                }
            }
            evidence.Finish(DateTimeOffset.UtcNow);
            WriteEvidence(evidenceDirectory, $"{appId}.json", evidence);
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void DiscardRoutineCapture(RealGameEvidence evidence)
    {
        string? screenshotPath = evidence.CaptureReadiness?.ScreenshotPath;
        if (!string.IsNullOrWhiteSpace(screenshotPath) && File.Exists(screenshotPath))
        {
            File.Delete(screenshotPath);
        }

        evidence.CaptureReadiness = null;
    }

    private static bool TryBlockUnsafePreflightScreen(
        ZContext context,
        string appId,
        RealGameEvidence evidence,
        bool windowReady,
        out string? blockedReason)
    {
        blockedReason = null;
        if (!windowReady)
        {
            return false;
        }

        RealGamePreflightObservation? observation = ObservePreflightScreen(context, evidence.CaptureReadiness?.ScreenshotPath);
        if (observation is null)
        {
            evidence.RecognitionSummary.Add("Preflight screen guard skipped because no initial screenshot was available.");
            return false;
        }

        evidence.RecognitionSummary.Add(
            $"Preflight screen observed: world={observation.WorldScreenName ?? "<none>"}, active={observation.ActiveScreenName ?? "<none>"}, ocr={string.Join("|", observation.OcrTexts.Take(8))}");

        RealGamePreflightGuardResult guard = RealGamePreflightScreenGuard.Evaluate(
            new RealGamePreflightScreenState(
                observation.WorldScreenName,
                observation.ActiveScreenName,
                observation.OcrTexts));
        if (!guard.IsBlocked)
        {
            return false;
        }

        blockedReason = guard.Reason;
        evidence.RecognitionSummary.Add(guard.Reason ?? "Preflight blocked by unsafe screen guard.");
        string fileStem = ActionLevelDebugEvidenceWriter.CreateFileStem($"{Sanitize(appId)}-preflight-unsafe-dialogue-blocked");
        ActionLevelDebugEvidenceWriter.Write(new ActionLevelDebugEvidence
        {
            FileStem = fileStem,
            AppId = appId,
            OperationName = "real-game preflight",
            NodeName = "画面识别",
            DotNetMethod = "ZzzOd.RealGameE2E.Program.TryBlockUnsafePreflightScreen()",
            BaselineParityRequirement = "Real-game preflight must not advance NPC dialogue while recovering to normal world; manual login or recovery is required before app execution.",
            BeforeScreenshotPath = observation.ScreenshotPath,
            BeforeRecognitionSummary = observation,
            ActionKind = "blocked_preflight",
            ActionTarget = "suspected NPC dialogue before BackToNormalWorld",
            ExpectedNextState = "大世界-普通 or login screen",
            AfterScreenshotPath = observation.ScreenshotPath,
            AfterRecognitionSummary = observation,
            TransitionResult = "blocked_by_unsafe_dialogue",
            FailureReason = blockedReason,
            RetryStoppedBecauseOfSuspectedLoop = true,
        });

        return true;
    }

    private static RealGamePreflightObservation? ObservePreflightScreen(ZContext context, string? screenshotPath)
    {
        if (string.IsNullOrWhiteSpace(screenshotPath) || !File.Exists(screenshotPath))
        {
            return null;
        }

        using Mat image = Cv2.ImRead(screenshotPath);
        if (image.Empty())
        {
            return null;
        }

        string? worldScreen = ScreenUtils.GetMatchScreenName(context, image, ["大世界-普通", "大世界-勘域"]);
        string? activeScreen = ScreenUtils.GetMatchScreenName(context, image);
        IReadOnlyList<string> ocrTexts = context.OcrService.Matcher
            .Ocr(image, threshold: 0d, mergeLineDistance: -1d)
            .OrderBy(result => result.Y)
            .ThenBy(result => result.X)
            .Select(result => result.Text)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToArray();

        return new RealGamePreflightObservation(screenshotPath, worldScreen, activeScreen, ocrTexts);
    }

    private static ZContext CreateInitializedContext(OneDragonEnvironment environment, E2EAutomationProfile profile)
    {
        var launcher = new ZApplicationLauncher(
            () => new ZContext(environment, instanceIndex: profile.InstanceIndex),
            initializeContext: true,
            initializeOcrProfile: true,
            validateAssets: true);
        return launcher.CreateContext();
    }

    private static RealGameCaptureEvidence? Capture(
        ZContext context,
        E2EAutomationProfile profile,
        string evidenceDirectory,
        string label)
    {
        if (context.Controller is not OneDragon.Core.Windows.Controller.WindowsGameController controller)
        {
            return null;
        }

        E2ECaptureReadinessEvidence readiness = new E2ECaptureReadinessProbe().Probe(controller);
        string? screenshotPath = null;
        if (readiness.FailureReason is null)
        {
            try
            {
                (_, Mat? screen) = controller.Screenshot();
                using (screen)
                {
                    if (screen is not null)
                    {
                        screenshotPath = Path.Combine(evidenceDirectory, $"{Sanitize(label)}.png");
                        Cv2.ImWrite(screenshotPath, screen);
                    }
                }
            }
            catch
            {
                screenshotPath = null;
            }
        }

        return new RealGameCaptureEvidence
        {
            Label = label,
            ScreenshotMethod = readiness.ScreenshotMethod,
            WindowHandle = readiness.WindowHandle,
            FirstFrameWidth = readiness.FirstFrameWidth,
            FirstFrameHeight = readiness.FirstFrameHeight,
            FailureReason = readiness.FailureReason,
            ScreenshotPath = screenshotPath,
            CapturedAtUtc = DateTimeOffset.UtcNow,
            ProfileScreenshotMethod = profile.ScreenshotMethod,
        };
    }

    private static void WriteEvidence(string evidenceDirectory, string fileName, RealGameEvidence evidence)
    {
        File.WriteAllText(Path.Combine(evidenceDirectory, fileName), JsonSerializer.Serialize(evidence, JsonOptions));
    }

    private static int Usage(string command)
    {
        Console.Error.WriteLine($"Unsupported command: {command}");
        Console.Error.WriteLine("可选参数: --instance-config-root <绝对实例配置目录>，复制到 staging 的 config/<实例编号>。");
        Console.Error.WriteLine("Usage: dotnet run --project zzzod-dotnet/tools/ZzzOd.RealGameE2E/ZzzOd.RealGameE2E.csproj -- --run-root <staging> prepare-only");
        Console.Error.WriteLine("Usage: dotnet run --project zzzod-dotnet/tools/ZzzOd.RealGameE2E/ZzzOd.RealGameE2E.csproj -- --run-root <staging> preflight");
        Console.Error.WriteLine("Usage: dotnet run --project zzzod-dotnet/tools/ZzzOd.RealGameE2E/ZzzOd.RealGameE2E.csproj -- run-app <app_id>");
        Console.Error.WriteLine("Usage: dotnet run --project zzzod-dotnet/tools/ZzzOd.RealGameE2E/ZzzOd.RealGameE2E.csproj -- run-app-f10 <app_id>");
        Console.Error.WriteLine("Usage: dotnet run --project zzzod-dotnet/tools/ZzzOd.RealGameE2E/ZzzOd.RealGameE2E.csproj -- run-app-current-f10 <app_id>");
        Console.Error.WriteLine("Usage: dotnet run --project zzzod-dotnet/tools/ZzzOd.RealGameE2E/ZzzOd.RealGameE2E.csproj -- run-auto-battle-f10");
        Console.Error.WriteLine("Usage: dotnet run --project zzzod-dotnet/tools/ZzzOd.RealGameE2E/ZzzOd.RealGameE2E.csproj -- capture-current [app_id]");
        Console.Error.WriteLine("Usage: dotnet run --project zzzod-dotnet/tools/ZzzOd.RealGameE2E/ZzzOd.RealGameE2E.csproj -- probe-key-j [app_id]");
        Console.Error.WriteLine("Usage: dotnet run --project zzzod-dotnet/tools/ZzzOd.RealGameE2E/ZzzOd.RealGameE2E.csproj -- probe-click-point <app_id> <x> <y> [pc_alt] [no-preflight]");
        Console.Error.WriteLine("Usage: dotnet run --project zzzod-dotnet/tools/ZzzOd.RealGameE2E/ZzzOd.RealGameE2E.csproj -- ocr-image <image_path> [image_path...]");
        Console.Error.WriteLine("Usage: dotnet run --project zzzod-dotnet/tools/ZzzOd.RealGameE2E/ZzzOd.RealGameE2E.csproj -- recognize-image <image_path> [image_path...]");
        Console.Error.WriteLine("Usage: dotnet run --project zzzod-dotnet/tools/ZzzOd.RealGameE2E/ZzzOd.RealGameE2E.csproj -- benchmark-flash-onnx [image_path] [iterations]");
        Console.Error.WriteLine("Usage: dotnet run --project zzzod-dotnet/tools/ZzzOd.RealGameE2E/ZzzOd.RealGameE2E.csproj -- benchmark-autobattle-load [--intra-op 1|2|4|all] [--iterations 10..200] [--image path] [--lost-void-image path] [--audio path]");
        return 64;
    }

    private static async Task WaitForStopKeyAsync(string stopKey, CancellationToken cancellationToken)
    {
        int virtualKey = RealGameE2EHotkey.ResolveVirtualKey(stopKey);
        bool wasDown = false;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool isDown = (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
            if (isDown && !wasDown)
            {
                return;
            }

            wasDown = isDown;
            await Task.Delay(25, cancellationToken).ConfigureAwait(false);
        }
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    private static string Sanitize(string text)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        return new string(text.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
    }
}

/// <summary>
/// 实机 E2E 命令和运行根参数解析结果。
/// </summary>
/// <param name="Command">实机 E2E 子命令。</param>
/// <param name="CommandArguments">子命令参数。</param>
/// <param name="RunRootArguments">交给共享运行根解析器的参数。</param>
/// <param name="InstanceConfigRoot">显式复制到 staging 的实例配置目录。</param>
public sealed record RealGameE2ECommandLine(
    string Command,
    IReadOnlyList<string> CommandArguments,
    IReadOnlyList<string> RunRootArguments,
    string? InstanceConfigRoot)
{
    /// <summary>
    /// 从完整命令行中提取 run-root 参数和 E2E 子命令。
    /// </summary>
    /// <param name="args">进程命令行参数。</param>
    /// <returns>拆分后的命令行。</returns>
    public static RealGameE2ECommandLine Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        List<string> runRootArguments = [];
        List<string> commandArguments = [];
        string? command = null;
        string? instanceConfigRoot = null;
        for (int index = 0; index < args.Count; index++)
        {
            string argument = args[index];
            if (string.Equals(argument, "--run-root", StringComparison.Ordinal))
            {
                if (index + 1 >= args.Count)
                {
                    throw new ArgumentException("--run-root 缺少路径参数。", nameof(args));
                }

                runRootArguments.Add(argument);
                runRootArguments.Add(args[++index]);
                continue;
            }

            if (argument.StartsWith("--run-root=", StringComparison.Ordinal))
            {
                runRootArguments.Add(argument);
                continue;
            }

            if (string.Equals(argument, "--instance-config-root", StringComparison.Ordinal))
            {
                if (index + 1 >= args.Count || string.IsNullOrWhiteSpace(args[index + 1]))
                {
                    throw new ArgumentException("--instance-config-root 缺少路径参数。", nameof(args));
                }

                if (instanceConfigRoot is not null)
                {
                    throw new ArgumentException("--instance-config-root 只能指定一次。", nameof(args));
                }

                instanceConfigRoot = args[++index];
                continue;
            }

            if (argument.StartsWith("--instance-config-root=", StringComparison.Ordinal))
            {
                if (instanceConfigRoot is not null)
                {
                    throw new ArgumentException("--instance-config-root 只能指定一次。", nameof(args));
                }

                instanceConfigRoot = argument["--instance-config-root=".Length..];
                if (string.IsNullOrWhiteSpace(instanceConfigRoot))
                {
                    throw new ArgumentException("--instance-config-root 缺少路径参数。", nameof(args));
                }
                continue;
            }

            if (command is null)
            {
                command = argument.Trim().ToLowerInvariant();
            }
            else
            {
                commandArguments.Add(argument);
            }
        }

        return new RealGameE2ECommandLine(command ?? "preflight", commandArguments, runRootArguments, instanceConfigRoot);
    }
}

/// <summary>
/// 实机 E2E 停止热键解析。
/// </summary>
public static class RealGameE2EHotkey
{
    /// <summary>
    /// 将 env.yml 中的停止热键转换为 Windows virtual-key code。
    /// </summary>
    public static int ResolveVirtualKey(string? key)
    {
        string normalized = key?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalized.Length == 1 && normalized[0] is >= 'a' and <= 'z')
        {
            return char.ToUpperInvariant(normalized[0]);
        }

        if (normalized.Length == 1 && normalized[0] is >= '0' and <= '9')
        {
            return normalized[0];
        }

        if (normalized.StartsWith('f')
            && int.TryParse(normalized[1..], out int functionKey)
            && functionKey is >= 1 and <= 24)
        {
            return 0x70 + functionKey - 1;
        }

        return normalized switch
        {
            "esc" or "escape" => 0x1B,
            "tab" => 0x09,
            "enter" or "return" => 0x0D,
            "space" => 0x20,
            "left" => 0x25,
            "up" => 0x26,
            "right" => 0x27,
            "down" => 0x28,
            _ => throw new ArgumentException($"不支持的停止热键: {key}", nameof(key)),
        };
    }
}

internal enum RealGameEvidenceStatus
{
    Pass,
    Fail,
    Blocked,
    Stopped,
}

internal sealed class RealGameEvidence
{
    public string ApplicationId { get; set; } = string.Empty;
    public string? ApplicationGroupId { get; set; }
    public string Command { get; set; } = string.Empty;
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset? FinishedAtUtc { get; set; }
    public string ScreenshotMethod { get; set; } = string.Empty;
    public string InputMode { get; set; } = string.Empty;
    public string? OcrProfile { get; set; }
    public string? ModelProfile { get; set; }
    public bool OcrUsed { get; set; }
    public bool YoloUsed { get; set; }
    public bool AutoBattleUsed { get; set; }
    public string? LogPath { get; set; }
    public string? GamePath { get; set; }
    public string? GameRegion { get; set; }
    public string? WindowTitle { get; set; }
    public string RunRoot { get; set; } = string.Empty;
    public string RunRootSource { get; set; } = string.Empty;
    public int? ManifestSchemaVersion { get; set; }
    public string ManifestRid { get; set; } = string.Empty;
    public string ManifestSourceSummary { get; set; } = string.Empty;
    public string ConfigRoot { get; set; } = string.Empty;
    public string InstanceConfigRoot { get; set; } = string.Empty;
    public List<E2EEvidenceResourceSnapshot> Resources { get; set; } = [];
    public List<string> RecognitionSummary { get; set; } = [];
    public RealGameCaptureEvidence? CaptureReadiness { get; set; }
    public RealGameEvidenceStatus ResultStatus { get; set; } = RealGameEvidenceStatus.Blocked;
    public string? ResultMessage { get; set; }

    public static RealGameEvidence Create(
        string applicationId,
        string command,
        E2EAutomationProfile profile,
        E2EResourceValidationResult resources,
        DateTimeOffset startedAtUtc)
    {
        OneDragonEnvironment environment = new(Directory.GetCurrentDirectory());
        return new RealGameEvidence
        {
            ApplicationId = applicationId,
            Command = command,
            StartedAtUtc = startedAtUtc,
            ScreenshotMethod = profile.ScreenshotMethod,
            InputMode = profile.InputMode,
            OcrProfile = profile.OcrProfile,
            ModelProfile = profile.ModelProfile,
            OcrUsed = true,
            YoloUsed = UsesYolo(applicationId),
            AutoBattleUsed = UsesAutoBattle(applicationId),
            LogPath = Path.Combine(environment.WorkDirectory, ".log"),
            RunRoot = resources.RunRoot,
            RunRootSource = resources.RunRootSource,
            ManifestSchemaVersion = resources.ManifestSchemaVersion,
            ManifestRid = resources.ManifestRid,
            ManifestSourceSummary = resources.ManifestSourceSummary,
            ConfigRoot = profile.ResolveConfigRoot(environment),
            InstanceConfigRoot = profile.ResolveInstanceConfigRoot(environment),
            Resources = resources.Items.Select(E2EEvidenceResourceSnapshot.From).ToList(),
        };
    }

    public void Finish(DateTimeOffset finishedAtUtc)
    {
        FinishedAtUtc = finishedAtUtc;
    }

    private static bool UsesYolo(string applicationId) =>
        applicationId is "auto_battle" or "world_patrol" or "charge_plan" or "notorious_hunt" or "intel_board" or "lost_void";

    private static bool UsesAutoBattle(string applicationId) =>
        applicationId is "auto_battle" or "charge_plan" or "notorious_hunt" or "world_patrol" or "intel_board" or "lost_void";
}

internal sealed class RealGameCaptureEvidence
{
    public string Label { get; set; } = string.Empty;
    public long WindowHandle { get; set; }
    public string ScreenshotMethod { get; set; } = string.Empty;
    public string ProfileScreenshotMethod { get; set; } = string.Empty;
    public int? FirstFrameWidth { get; set; }
    public int? FirstFrameHeight { get; set; }
    public string? ScreenshotPath { get; set; }
    public string? FailureReason { get; set; }
    public DateTimeOffset CapturedAtUtc { get; set; }
}

internal sealed record ImageOcrEvidence(
    string ImagePath,
    int Width,
    int Height,
    string OcrProfile,
    IReadOnlyList<ImageOcrWordEvidence> Words,
    string? FailureReason);

internal sealed record ImageOcrWordEvidence(
    string Text,
    double Confidence,
    int X,
    int Y,
    int Width,
    int Height)
{
    public static ImageOcrWordEvidence From(OcrMatchResult result) =>
        new(result.Text, result.Confidence, result.X, result.Y, result.Width, result.Height);
}

internal sealed record ImageRecognitionEvidence(
    string ImagePath,
    int Width,
    int Height,
    string OcrProfile,
    string? WorldScreen,
    string? ActiveScreen,
    IReadOnlyList<AreaRecognitionEvidence> Areas,
    string? FailureReason);

internal sealed record AreaRecognitionEvidence(
    string ScreenName,
    string AreaName,
    string Result,
    bool Binary,
    MatchRecognitionEvidence? Match)
{
    public static AreaRecognitionEvidence From(ZContext context, Mat image, string screenName, string areaName, bool binary = false)
    {
        FindAreaResultEnum result = binary
            ? ScreenUtils.FindAreaBinary(context, image, screenName, areaName)
            : ScreenUtils.FindArea(context, image, screenName, areaName);
        MatchRecognitionEvidence? match = null;
        OneDragon.Core.Screen.ScreenArea? area = context.ScreenContext.GetArea(screenName, areaName);
        if (area?.IsTemplateArea == true)
        {
            OneDragon.Core.Matcher.MatchResult? templateMatch = ScreenUtils.FindTemplateCoordInArea(context, image, screenName, areaName);
            if (templateMatch is not null)
            {
                match = MatchRecognitionEvidence.From(templateMatch);
            }
        }

        return new AreaRecognitionEvidence(screenName, areaName, result.ToString(), binary, match);
    }
}

internal sealed record MatchRecognitionEvidence(
    double Confidence,
    int X,
    int Y,
    int Width,
    int Height,
    int CenterX,
    int CenterY)
{
    public static MatchRecognitionEvidence From(OneDragon.Core.Matcher.MatchResult result) =>
        new(result.Confidence, result.X, result.Y, result.Width, result.Height, result.Center.X, result.Center.Y);
}

internal sealed record RealGamePreflightObservation(
    string ScreenshotPath,
    string? WorldScreenName,
    string? ActiveScreenName,
    IReadOnlyList<string> OcrTexts);

