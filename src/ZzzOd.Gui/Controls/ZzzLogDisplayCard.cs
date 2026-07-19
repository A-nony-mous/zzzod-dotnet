using System.Threading.Channels;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Controls;

internal sealed partial class ZzzLogDisplayCard : UserControl, IZzzPageLifecycle
{
    private readonly IZzzAppBackend _backend;
    private readonly TextBox _output;
    private readonly List<string> _lines = [];
    private ChannelReader<ZzzBackendEvent>? _reader;
    private CancellationTokenSource? _cancellation;
    private bool _active;
    private bool _following = true;
    private string _statusText = "已停止";

    public ZzzLogDisplayCard(IZzzAppBackend backend, int maxLines = 300)
    {
        _backend = backend;
        MaxLines = maxLines;
        AvaloniaXamlLoader.Load(this);
        _output = this.FindControl<TextBox>("OutputText")
            ?? throw new InvalidOperationException("日志显示控件缺少输出区域。");
    }

    public int MaxLines { get; set; }

    public IReadOnlyList<string> Lines => _lines;

    public bool IsActive => _active;

    public bool IsFollowing => _following;

    public string DisplayText => _output.Text ?? string.Empty;

    public string StatusText => _statusText;

    public void Start()
    {
        _active = true;
		_statusText = (_following ? "跟随中" : "已暂停跟随");
        EnsureSubscribed();
    }

    public void Pause()
    {
        _active = false;
		_statusText = "已暂停";
    }

    public void Stop()
    {
        _active = false;
		_statusText = "已停止";
    }

    public void SetFollowing(bool following)
    {
        _following = following;
		_statusText = ((!following) ? "已暂停跟随" : (_active ? "跟随中" : "已停止"));
        if (following)
        {
            UpdateOutput();
        }
    }

    public void AppendLine(string line) => Append(line);

    internal static string FormatOperationTrace(ZzzOperationTraceDto trace)
    {
        ArgumentNullException.ThrowIfNull(trace);
        string path = trace.ResultKind == "transition" && !string.IsNullOrWhiteSpace(trace.CurrentNode) && !string.IsNullOrWhiteSpace(trace.NextNode)
            ? $"{trace.CurrentNode} -> {trace.NextNode}"
            : !string.IsNullOrWhiteSpace(trace.PreviousNode) && !string.IsNullOrWhiteSpace(trace.CurrentNode)
                ? $"{trace.PreviousNode} -> {trace.CurrentNode}"
                : trace.CurrentNode ?? trace.NextNode ?? "当前节点";
        string status = string.IsNullOrWhiteSpace(trace.Status) ? trace.ResultKind ?? "未知" : trace.Status;
        string retry = trace.RetryCount > 0 ? $"，重试 {trace.RetryCount}" : string.Empty;
        string exception = string.IsNullOrWhiteSpace(trace.ExceptionType)
            ? string.Empty
            : $"，异常 {trace.ExceptionType}: {trace.ExceptionMessage ?? status}";
        return $"[{trace.Timestamp:HH:mm:ss}] [Operation] {trace.Operation} 节点 {path} 返回状态 {status}{retry}{exception}";
    }

    public void OnPageShown() => Start();

    public void OnPageHidden()
    {
        Pause();
        Unsubscribe();
    }

    public void OnPageLeave() => OnPageHidden();

    public void DisposePage()
    {
        Stop();
        Unsubscribe();
    }

    private void EnsureSubscribed()
    {
        if (_reader is not null)
        {
            return;
        }

        _reader = _backend.SubscribeEvents();
        _cancellation = new CancellationTokenSource();
        ChannelReader<ZzzBackendEvent> reader = _reader;
        CancellationToken token = _cancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                List<string> batch = new();
                while (await reader.WaitToReadAsync(token).ConfigureAwait(false))
                {
                    while (reader.TryRead(out ZzzBackendEvent? item))
                    {
                        if (!_active)
                        {
                            continue;
                        }

                        if (item.Type == "log.appended" && item.Data is ZzzLogEntryDto log)
                        {
                            batch.Add($"[{log.Timestamp:HH:mm:ss}] [{log.Level}] {log.Message}");
                        }
                        else if (item.Type == "run.operationTrace" && item.Data is ZzzOperationTraceDto trace)
                        {
                            batch.Add(FormatOperationTrace(trace));
                        }
                    }

                    if (batch.Count > 0)
                    {
                        List<string> linesToAppend = batch.ToList();
                        batch.Clear();
                        await Dispatcher.UIThread.InvokeAsync(() => AppendBatch(linesToAppend));
                    }

                    await Task.Delay(100, token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }
        });
    }

    private void Unsubscribe()
    {
        _cancellation?.Cancel();
        if (_reader is not null)
        {
            _backend.UnsubscribeEvents(_reader);
        }

        _cancellation?.Dispose();
        _cancellation = null;
        _reader = null;
    }

    private void Append(string line)
    {
        _lines.Add(line);
        if (_lines.Count > MaxLines)
        {
            _lines.RemoveRange(0, _lines.Count - MaxLines);
        }

        if (_following)
        {
            UpdateOutput();
        }
    }

    private void AppendBatch(List<string> newLines)
    {
        _lines.AddRange(newLines);
        if (_lines.Count > MaxLines)
        {
            _lines.RemoveRange(0, _lines.Count - MaxLines);
        }

        if (_following)
        {
            UpdateOutput();
        }
    }

    private void UpdateOutput()
    {
        _output.Text = _lines.Count == 0 ? string.Empty : string.Join(Environment.NewLine, _lines);
    }
}

