using System.Diagnostics;
using System.Text.Json;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using NAudio.Wave;
using OneDragon.Core.Ocr;
using OneDragon.Core.Runtime;
using OneDragon.Core.Windows.Audio;
using OpenCvSharp;
using ZzzOd.GameLogic.Application.HollowZero.LostVoid;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.E2E;

namespace ZzzOd.RealGameE2E;

/// <summary>
/// CPU-only offline load benchmark. It reads fixed assets and local models, and never starts a game window.
/// </summary>
internal static class AutoBattleLoadBenchmark
{
    public static async Task<int> RunAsync(
        OneDragonEnvironment environment,
        E2EAutomationProfile profile,
        IReadOnlyList<string> args,
        JsonSerializerOptions jsonOptions)
    {
        AutoBattleLoadBenchmarkOptions options;
        try
        {
            options = AutoBattleLoadBenchmarkOptions.Parse(environment.WorkDirectory, args);
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }

        string[] requiredAssets = [options.ImagePath, options.LostVoidImagePath, options.AudioPath];
        string[] missingAssets = requiredAssets.Where(path => !File.Exists(path)).ToArray();
        if (missingAssets.Length > 0)
        {
            Console.Error.WriteLine($"离线基准素材缺失: {string.Join(", ", missingAssets)}");
            return 1;
        }

        using Mat image = Cv2.ImRead(options.ImagePath, ImreadModes.Color);
        using Mat lostVoidImage = Cv2.ImRead(options.LostVoidImagePath, ImreadModes.Color);
        if (image.Empty() || lostVoidImage.Empty())
        {
            Console.Error.WriteLine("离线基准图片无法读取。");
            return 1;
        }

        float[] audio = LoadAudio(options.AudioPath);
        if (audio.Length == 0)
        {
            Console.Error.WriteLine("离线基准音频无法读取或转换为空。");
            return 1;
        }

        var settings = new List<AutoBattleLoadBenchmarkSettingResult>();
        foreach (int intraOp in options.IntraOpValues)
        {
            settings.Add(await RunSettingAsync(environment, profile, options, image, lostVoidImage, audio, intraOp).ConfigureAwait(false));
        }

        AutoBattleLoadBenchmarkSettingResult selected = settings
            .OrderBy(result => result.Composite.EndToEnd.P95Milliseconds)
            .ThenBy(result => result.Composite.EndToEnd.P50Milliseconds)
            .ThenBy(result => result.IntraOpNumThreads)
            .First();

        var result = new AutoBattleLoadBenchmarkResult(
            DateTimeOffset.UtcNow,
            GameWindowStarted: false,
            "CPUExecutionProvider",
            "ORT_ENABLE_ALL",
            "ORT_SEQUENTIAL",
            1,
            options.ImagePath,
            options.LostVoidImagePath,
            options.AudioPath,
            options.Iterations,
            options.WarmupIterations,
            settings,
            selected.IntraOpNumThreads,
            "最低完整并发负载 EndToEnd P95，其次 P50，再取较小线程数。");
        Console.WriteLine(JsonSerializer.Serialize(result, jsonOptions));
        return 0;
    }

    private static async Task<AutoBattleLoadBenchmarkSettingResult> RunSettingAsync(
        OneDragonEnvironment environment,
        E2EAutomationProfile profile,
        AutoBattleLoadBenchmarkOptions options,
        Mat image,
        Mat lostVoidImage,
        float[] audio,
        int intraOp)
    {
        using ZContext context = new(environment, instanceIndex: profile.InstanceIndex);
        context.ScreenContext.Reload();
        string flashModelPath = Path.Combine(context.FlashClassifier.ModelDirectoryPath, "model.onnx");
        using var lostVoidDetector = new LostVoidDetector(context);
        string lostVoidModelPath = Path.Combine(lostVoidDetector.ModelDirectoryPath, "model.onnx");
        OcrModelResolution ocrResolution = context.ModelConfig.ResolveOcrProfile();
        var ocrParam = new OnnxOcrParam(environment, ocrResolution.Resource, useGpu: false);
        string[] missingModels = new[] { flashModelPath, lostVoidModelPath, ocrParam.DetModelPath, ocrParam.RecModelPath }
            .Where(path => !File.Exists(path))
            .ToArray();
        if (missingModels.Length > 0)
        {
            throw new FileNotFoundException($"离线基准模型缺失: {string.Join(", ", missingModels)}");
        }

        using var flash = new ImageOnnxWorkload("Flash", flashModelPath, image, intraOp, ImageLayout.YoloLetterbox);
        using var lostVoid = new ImageOnnxWorkload("LostVoidYolo", lostVoidModelPath, lostVoidImage, intraOp, ImageLayout.YoloLetterbox);
        using var ocr = new OcrOnnxWorkload(ocrParam.DetModelPath, ocrParam.RecModelPath, image, intraOp);
        using var agent = new AgentWorkload(context, image);
        using var target = new TargetWorkload(context, image);
        var workloads = new IBenchmarkWorkload[]
        {
            flash,
            new AudioWorkload(audio),
            ocr,
            agent,
            target,
            lostVoid,
        };

        for (int index = 0; index < options.WarmupIterations; index++)
        {
            foreach (IBenchmarkWorkload workload in workloads)
            {
                _ = workload.Run(index);
            }
        }

        var samples = workloads.ToDictionary(workload => workload.Name, _ => new List<BenchmarkSample>(options.Iterations));
        for (int iteration = 0; iteration < options.Iterations; iteration++)
        {
            BenchmarkSample[] round = await Task.WhenAll(workloads.Select(workload => ScheduleAsync(workload, iteration))).ConfigureAwait(false);
            foreach (BenchmarkSample sample in round)
            {
                samples[sample.Name].Add(sample);
            }
        }

        IReadOnlyList<AutoBattleLoadBenchmarkWorkloadResult> workloadResults = samples
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => AutoBattleLoadBenchmarkWorkloadResult.From(pair.Key, pair.Value))
            .ToArray();
        return new AutoBattleLoadBenchmarkSettingResult(
            intraOp,
            flash.SessionDescription,
            lostVoid.SessionDescription,
            ocr.SessionDescription,
            workloadResults,
            AutoBattleLoadBenchmarkWorkloadResult.From("Composite", samples.Values.SelectMany(value => value).ToArray()));
    }

    private static Task<BenchmarkSample> ScheduleAsync(IBenchmarkWorkload workload, int iteration)
    {
        long queuedAt = Stopwatch.GetTimestamp();
        return Task.Run(() =>
        {
            double queueDelay = Stopwatch.GetElapsedTime(queuedAt).TotalMilliseconds;
            BenchmarkSample sample = workload.Run(iteration);
            return sample with
            {
                QueueDelayMilliseconds = queueDelay,
                EndToEndMilliseconds = queueDelay + sample.PreprocessElapsedMilliseconds + sample.InferenceElapsedMilliseconds + sample.PostprocessElapsedMilliseconds,
            };
        });
    }

    private static float[] LoadAudio(string audioPath)
    {
        using var reader = new AudioFileReader(audioPath);
        var interleaved = new float[reader.Length / sizeof(float)];
        int count = reader.Read(interleaved, 0, interleaved.Length);
        Array.Resize(ref interleaved, count);
        float[] mono = AudioRecorder.ConvertInterleavedToMonoSamples(interleaved, reader.WaveFormat.Channels);
        float[] resampled = AudioRecorder.ResampleToTargetRate(mono, reader.WaveFormat.SampleRate, AudioRecorder.TargetSampleRate);
        int bufferLength = (int)(AudioRecorder.TargetSampleRate * AudioRecorder.BufferSeconds);
        return resampled.Length <= bufferLength ? resampled : resampled[^bufferLength..];
    }

    private interface IBenchmarkWorkload
    {
        string Name { get; }
        BenchmarkSample Run(int iteration);
    }

    private sealed class ImageOnnxWorkload : IBenchmarkWorkload, IDisposable
    {
        private readonly Mat _image;
        private readonly InferenceSession _session;
        private readonly string _inputName;
        private readonly int[] _dimensions;
        private readonly ImageLayout _layout;

        public ImageOnnxWorkload(string name, string modelPath, Mat image, int intraOp, ImageLayout layout)
        {
            Name = name;
            _image = image.Clone();
            _layout = layout;
            using SessionOptions options = CreateSessionOptions(intraOp);
            _session = new InferenceSession(modelPath, options);
            KeyValuePair<string, NodeMetadata> input = _session.InputMetadata.Single();
            _inputName = input.Key;
            _dimensions = ResolveDimensions(input.Value.Dimensions, layout);
            SessionDescription = new OnnxSessionDescription(modelPath, _dimensions, intraOp);
        }

        public string Name { get; }
        public OnnxSessionDescription SessionDescription { get; }

        public BenchmarkSample Run(int iteration)
        {
            long preprocessStartedAt = Stopwatch.GetTimestamp();
            DenseTensor<float> tensor = BuildImageTensor(_image, _dimensions, _layout);
            double preprocess = Stopwatch.GetElapsedTime(preprocessStartedAt).TotalMilliseconds;
            long inferenceStartedAt = Stopwatch.GetTimestamp();
            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs = _session.Run([NamedOnnxValue.CreateFromTensor(_inputName, tensor)]);
            double inference = Stopwatch.GetElapsedTime(inferenceStartedAt).TotalMilliseconds;
            long postprocessStartedAt = Stopwatch.GetTimestamp();
            ConsumeOutputs(outputs);
            double postprocess = Stopwatch.GetElapsedTime(postprocessStartedAt).TotalMilliseconds;
            return new BenchmarkSample(Name, 0d, preprocess, inference, postprocess, 0d);
        }

        public void Dispose()
        {
            _session.Dispose();
            _image.Dispose();
        }
    }

    private sealed class OcrOnnxWorkload : IBenchmarkWorkload, IDisposable
    {
        private readonly Mat _image;
        private readonly InferenceSession _detSession;
        private readonly InferenceSession _recSession;
        private readonly string _detInputName;
        private readonly string _recInputName;
        private readonly int[] _detDimensions;
        private readonly int[] _recDimensions;

        public OcrOnnxWorkload(string detModelPath, string recModelPath, Mat image, int intraOp)
        {
            Name = "OCR";
            _image = image.Clone();
            using SessionOptions detOptions = CreateSessionOptions(intraOp);
            using SessionOptions recOptions = CreateSessionOptions(intraOp);
            _detSession = new InferenceSession(detModelPath, detOptions);
            _recSession = new InferenceSession(recModelPath, recOptions);
            KeyValuePair<string, NodeMetadata> detInput = _detSession.InputMetadata.Single();
            KeyValuePair<string, NodeMetadata> recInput = _recSession.InputMetadata.Single();
            _detInputName = detInput.Key;
            _recInputName = recInput.Key;
            _detDimensions = ResolveDimensions(detInput.Value.Dimensions, ImageLayout.OcrDetection);
            _recDimensions = ResolveDimensions(recInput.Value.Dimensions, ImageLayout.OcrRecognition);
            SessionDescription = new OnnxSessionDescription(
                $"det={detModelPath};rec={recModelPath}",
                [.. _detDimensions, .. _recDimensions],
                intraOp);
        }

        public string Name { get; }
        public OnnxSessionDescription SessionDescription { get; }

        public BenchmarkSample Run(int iteration)
        {
            long preprocessStartedAt = Stopwatch.GetTimestamp();
            DenseTensor<float> detTensor = BuildImageTensor(_image, _detDimensions, ImageLayout.OcrDetection);
            DenseTensor<float> recTensor = BuildImageTensor(_image, _recDimensions, ImageLayout.OcrRecognition);
            double preprocess = Stopwatch.GetElapsedTime(preprocessStartedAt).TotalMilliseconds;
            long inferenceStartedAt = Stopwatch.GetTimestamp();
            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> detOutputs = _detSession.Run([NamedOnnxValue.CreateFromTensor(_detInputName, detTensor)]);
            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> recOutputs = _recSession.Run([NamedOnnxValue.CreateFromTensor(_recInputName, recTensor)]);
            double inference = Stopwatch.GetElapsedTime(inferenceStartedAt).TotalMilliseconds;
            long postprocessStartedAt = Stopwatch.GetTimestamp();
            ConsumeOutputs(detOutputs);
            ConsumeOutputs(recOutputs);
            double postprocess = Stopwatch.GetElapsedTime(postprocessStartedAt).TotalMilliseconds;
            return new BenchmarkSample(Name, 0d, preprocess, inference, postprocess, 0d);
        }

        public void Dispose()
        {
            _detSession.Dispose();
            _recSession.Dispose();
            _image.Dispose();
        }
    }

    private sealed class AudioWorkload : IBenchmarkWorkload
    {
        private readonly float[] _template;
        private readonly float[] _live;

        public AudioWorkload(float[] audio)
        {
            Name = "Audio";
            _template = AudioFilterUtils.HighPassFilter(audio, AudioRecorder.TargetSampleRate);
            _live = audio.ToArray();
        }

        public string Name { get; }

        public BenchmarkSample Run(int iteration)
        {
            long preprocessStartedAt = Stopwatch.GetTimestamp();
            float[] filteredLive = AudioFilterUtils.HighPassFilter(_live, AudioRecorder.TargetSampleRate);
            double preprocess = Stopwatch.GetElapsedTime(preprocessStartedAt).TotalMilliseconds;
            long inferenceStartedAt = Stopwatch.GetTimestamp();
            double correlation = AudioMathUtils.GetMaxCorr(_template, filteredLive);
            double inference = Stopwatch.GetElapsedTime(inferenceStartedAt).TotalMilliseconds;
            long postprocessStartedAt = Stopwatch.GetTimestamp();
            _ = correlation > 0.1d;
            double postprocess = Stopwatch.GetElapsedTime(postprocessStartedAt).TotalMilliseconds;
            return new BenchmarkSample(Name, 0d, preprocess, inference, postprocess, 0d);
        }
    }

    private sealed class AgentWorkload : IBenchmarkWorkload, IDisposable
    {
        private readonly ZContext _context;
        private readonly Mat _image;

        public AgentWorkload(ZContext context, Mat image)
        {
            Name = "Agent";
            _context = context;
            _image = image.Clone();
            _context.AutoBattleContext.AgentContext.InitBattleAgentContext();
        }

        public string Name { get; }

        public BenchmarkSample Run(int iteration)
        {
            long startedAt = Stopwatch.GetTimestamp();
            _ = _context.AutoBattleContext.AgentContext.CheckAgentRelated(_image, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000d + iteration, updateState: false);
            return new BenchmarkSample(Name, 0d, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds, 0d, 0d, 0d);
        }

        public void Dispose() => _image.Dispose();
    }

    private sealed class TargetWorkload : IBenchmarkWorkload, IDisposable
    {
        private readonly ZContext _context;
        private readonly Mat _image;

        public TargetWorkload(ZContext context, Mat image)
        {
            Name = "Target";
            _context = context;
            _image = image.Clone();
        }

        public string Name { get; }

        public BenchmarkSample Run(int iteration)
        {
            long startedAt = Stopwatch.GetTimestamp();
            _ = _context.AutoBattleContext.TargetContext.RunAllChecks(_image, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000d + iteration, updateState: false);
            return new BenchmarkSample(Name, 0d, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds, 0d, 0d, 0d);
        }

        public void Dispose() => _image.Dispose();
    }

    private static SessionOptions CreateSessionOptions(int intraOp)
    {
        var options = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
            IntraOpNumThreads = intraOp,
            InterOpNumThreads = 1,
            EnableMemoryPattern = true,
        };
        options.AppendExecutionProvider_CPU();
        return options;
    }

    private static int[] ResolveDimensions(IReadOnlyList<int> source, ImageLayout layout)
    {
        if (source.Count != 4)
        {
            throw new InvalidOperationException($"不支持的模型输入维度: [{string.Join(",", source)}]");
        }

        int height = source[2] > 0 ? source[2] : layout == ImageLayout.OcrDetection ? 960 : 736;
        int width = source[3] > 0 ? source[3] : layout == ImageLayout.OcrDetection ? 960 : 736;
        if (layout == ImageLayout.OcrRecognition)
        {
            height = source[2] > 0 ? source[2] : 48;
            width = source[3] > 0 ? source[3] : 320;
        }

        return [source[0] > 0 ? source[0] : 1, source[1] > 0 ? source[1] : 3, height, width];
    }

    private static DenseTensor<float> BuildImageTensor(Mat image, IReadOnlyList<int> dimensions, ImageLayout layout)
    {
        int height = dimensions[2];
        int width = dimensions[3];
        using Mat canvas = layout == ImageLayout.YoloLetterbox ? BuildLetterbox(image, width, height) : ResizeImage(image, width, height);
        int channelSize = width * height;
        float[] values = new float[3 * channelSize];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vec3b pixel = canvas.At<Vec3b>(y, x);
                int offset = (y * width) + x;
                values[offset] = pixel.Item2 / 255f;
                values[channelSize + offset] = pixel.Item1 / 255f;
                values[(2 * channelSize) + offset] = pixel.Item0 / 255f;
            }
        }

        return new DenseTensor<float>(values, dimensions.ToArray());
    }

    private static Mat BuildLetterbox(Mat image, int width, int height)
    {
        double scale = Math.Min(height / (double)image.Rows, width / (double)image.Cols);
        int scaledHeight = Math.Max(1, (int)Math.Round(image.Rows * scale));
        int scaledWidth = Math.Max(1, (int)Math.Round(image.Cols * scale));
        using Mat resized = new();
        Cv2.Resize(image, resized, new Size(scaledWidth, scaledHeight), interpolation: InterpolationFlags.Linear);
        Mat canvas = new(new Size(width, height), MatType.CV_8UC3, new Scalar(114, 114, 114));
        using Mat roi = new(canvas, new Rect(0, 0, scaledWidth, scaledHeight));
        resized.CopyTo(roi);
        return canvas;
    }

    private static Mat ResizeImage(Mat image, int width, int height)
    {
        var result = new Mat();
        Cv2.Resize(image, result, new Size(width, height), interpolation: InterpolationFlags.Linear);
        return result;
    }

    private static void ConsumeOutputs(IEnumerable<DisposableNamedOnnxValue> outputs)
    {
        float checksum = 0f;
        foreach (DisposableNamedOnnxValue output in outputs)
        {
            int count = 0;
            foreach (float value in output.AsTensor<float>())
            {
                checksum += value;
                if (++count == 16)
                {
                    break;
                }
            }
        }

        GC.KeepAlive(checksum);
    }

    private enum ImageLayout
    {
        YoloLetterbox,
        OcrDetection,
        OcrRecognition,
    }
}

internal sealed record AutoBattleLoadBenchmarkOptions(
    IReadOnlyList<int> IntraOpValues,
    int Iterations,
    int WarmupIterations,
    string ImagePath,
    string LostVoidImagePath,
    string AudioPath)
{
    public static AutoBattleLoadBenchmarkOptions Parse(string rootDirectory, IReadOnlyList<string> args)
    {
        string imagePath = Path.Combine(rootDirectory, "tests", "ZzzOd.GameLogic.Tests", "TestData", "IntelBoard", "intel-board-running.png");
        string lostVoidImagePath = Path.Combine(rootDirectory, "tests", "ZzzOd.GameLogic.Tests", "TestData", "LostVoid", "lost_void-before.png");
        string audioPath = Path.Combine(rootDirectory, "assets", "template", "dodge_audio", "template_1.wav");
        IReadOnlyList<int> intraOpValues = [1, 2, 4];
        int iterations = 30;
        for (int index = 0; index < args.Count; index++)
        {
            string argument = args[index];
            string value = index + 1 < args.Count ? args[index + 1] : string.Empty;
            switch (argument)
            {
                case "--intra-op":
                    intraOpValues = value.Equals("all", StringComparison.OrdinalIgnoreCase)
                        ? [1, 2, 4]
                        : int.TryParse(value, out int intraOp) && intraOp is 1 or 2 or 4
                            ? [intraOp]
                            : throw new ArgumentException("--intra-op 只接受 1、2、4 或 all?");
                    index++;
                    break;
                case "--iterations":
                    iterations = int.TryParse(value, out int parsed)
                        ? Math.Clamp(parsed, 10, 200)
                        : throw new ArgumentException("--iterations 只接受 10..200 的整数。");
                    index++;
                    break;
                case "--image":
                    imagePath = Path.GetFullPath(value);
                    index++;
                    break;
                case "--lost-void-image":
                    lostVoidImagePath = Path.GetFullPath(value);
                    index++;
                    break;
                case "--audio":
                    audioPath = Path.GetFullPath(value);
                    index++;
                    break;
                default:
                    throw new ArgumentException($"未知参数: {argument}");
            }
        }

        return new AutoBattleLoadBenchmarkOptions(intraOpValues, iterations, Math.Clamp(iterations / 5, 3, 10), imagePath, lostVoidImagePath, audioPath);
    }
}

internal sealed record BenchmarkSample(
    string Name,
    double QueueDelayMilliseconds,
    double PreprocessElapsedMilliseconds,
    double InferenceElapsedMilliseconds,
    double PostprocessElapsedMilliseconds,
    double EndToEndMilliseconds);

internal sealed record OnnxSessionDescription(string ModelPath, IReadOnlyList<int> InputDimensions, int IntraOpNumThreads);

internal sealed record AutoBattleLoadBenchmarkResult(
    DateTimeOffset FinishedAtUtc,
    bool GameWindowStarted,
    string Provider,
    string GraphOptimizationLevel,
    string ExecutionMode,
    int InterOpNumThreads,
    string ImagePath,
    string LostVoidImagePath,
    string AudioPath,
    int Iterations,
    int WarmupIterations,
    IReadOnlyList<AutoBattleLoadBenchmarkSettingResult> Settings,
    int SelectedIntraOpNumThreads,
    string SelectionRule);

internal sealed record AutoBattleLoadBenchmarkSettingResult(
    int IntraOpNumThreads,
    OnnxSessionDescription FlashSession,
    OnnxSessionDescription LostVoidSession,
    OnnxSessionDescription OcrSessions,
    IReadOnlyList<AutoBattleLoadBenchmarkWorkloadResult> Workloads,
    AutoBattleLoadBenchmarkWorkloadResult Composite);

internal sealed record AutoBattleLoadBenchmarkWorkloadResult(
    string Name,
    int Samples,
    AutoBattleLoadBenchmarkLatencyStats QueueDelay,
    AutoBattleLoadBenchmarkLatencyStats Preprocess,
    AutoBattleLoadBenchmarkLatencyStats Inference,
    AutoBattleLoadBenchmarkLatencyStats Postprocess,
    AutoBattleLoadBenchmarkLatencyStats EndToEnd)
{
    public static AutoBattleLoadBenchmarkWorkloadResult From(string name, IEnumerable<BenchmarkSample> samples)
    {
        BenchmarkSample[] values = samples.ToArray();
        return new AutoBattleLoadBenchmarkWorkloadResult(
            name,
            values.Length,
            AutoBattleLoadBenchmarkLatencyStats.From(values.Select(sample => sample.QueueDelayMilliseconds)),
            AutoBattleLoadBenchmarkLatencyStats.From(values.Select(sample => sample.PreprocessElapsedMilliseconds)),
            AutoBattleLoadBenchmarkLatencyStats.From(values.Select(sample => sample.InferenceElapsedMilliseconds)),
            AutoBattleLoadBenchmarkLatencyStats.From(values.Select(sample => sample.PostprocessElapsedMilliseconds)),
            AutoBattleLoadBenchmarkLatencyStats.From(values.Select(sample => sample.EndToEndMilliseconds)));
    }
}

internal sealed record AutoBattleLoadBenchmarkLatencyStats(
    double MinMilliseconds,
    double MeanMilliseconds,
    double P50Milliseconds,
    double P95Milliseconds,
    double MaxMilliseconds)
{
    public static AutoBattleLoadBenchmarkLatencyStats From(IEnumerable<double> source)
    {
        double[] values = source.Order().ToArray();
        if (values.Length == 0)
        {
            return new AutoBattleLoadBenchmarkLatencyStats(0d, 0d, 0d, 0d, 0d);
        }

        return new AutoBattleLoadBenchmarkLatencyStats(
            values[0],
            values.Average(),
            Percentile(values, 0.5d),
            Percentile(values, 0.95d),
            values[^1]);
    }

    private static double Percentile(IReadOnlyList<double> values, double percentile)
    {
        double position = (values.Count - 1) * percentile;
        int lower = (int)Math.Floor(position);
        int upper = (int)Math.Ceiling(position);
        return lower == upper ? values[lower] : values[lower] + ((values[upper] - values[lower]) * (position - lower));
    }
}
