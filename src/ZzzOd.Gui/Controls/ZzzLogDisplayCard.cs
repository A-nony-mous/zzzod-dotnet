using System.Threading.Channels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Controls;

internal sealed partial class ZzzLogDisplayCard : UserControl, IZzzPageLifecycle
{
    private static readonly TimeSpan DefaultFollowResumeDelay = TimeSpan.FromSeconds(15);
    private const double BottomTolerance = 1d;

    private readonly IZzzAppBackend _backend;
    private readonly TextBox _output;
    private readonly ScrollViewer _logScrollViewer;
    private readonly List<string> _lines = [];
    private readonly DispatcherTimer _followResumeTimer;
    private ChannelReader<ZzzBackendEvent>? _reader;
    private CancellationTokenSource? _cancellation;
    private bool _active;
    private bool _following = true;
    private string _statusText = "已停止";

    public ZzzLogDisplayCard(IZzzAppBackend backend, int maxLines = 300)
        : this(backend, maxLines, DefaultFollowResumeDelay)
    {
    }

    internal ZzzLogDisplayCard(IZzzAppBackend backend, int maxLines, TimeSpan followResumeDelay)
    {
        _backend = backend;
        MaxLines = maxLines;
        AvaloniaXamlLoader.Load(this);
        _logScrollViewer = this.FindControl<ScrollViewer>("LogScrollViewer")
            ?? throw new InvalidOperationException("日志显示控件缺少滚动区域。");
        _output = this.FindControl<TextBox>("OutputText")
            ?? throw new InvalidOperationException("日志显示控件缺少输出区域。");
        _followResumeTimer = new DispatcherTimer { Interval = followResumeDelay };
        _followResumeTimer.Tick += OnFollowResumeTimerTick;
        _logScrollViewer.ScrollChanged += OnScrollChanged;
        _logScrollViewer.PointerWheelChanged += OnPointerWheelChanged;
        _logScrollViewer.PointerPressed += OnPointerPressed;
        _logScrollViewer.PointerReleased += OnPointerReleased;
    }

    private double ScrollMaximum => Math.Max(0d, _logScrollViewer.Extent.Height - _logScrollViewer.Viewport.Height);

    public int MaxLines { get; set; }

    public IReadOnlyList<string> Lines => _lines;

    public bool IsActive => _active;

    public bool IsFollowing => _following;

    public string DisplayText => _output.Text ?? string.Empty;

    public string StatusText => _statusText;

    internal ScrollViewer ScrollViewport => _logScrollViewer;

    internal bool FollowResumeTimerEnabled => _followResumeTimer.IsEnabled;

    public void Start()
    {
        _active = true;
        _following = true;
        _statusText = "跟随中";
        _followResumeTimer.Stop();
        ScrollToEnd();
        EnsureSubscribed();
    }

    public void Pause()
    {
        _active = false;
        _following = false;
        _followResumeTimer.Stop();
        _statusText = "已暂停";
    }

    public void Stop()
    {
        _active = false;
        _following = false;
        _followResumeTimer.Stop();
        _statusText = "已停止";
    }

    public void SetFollowing(bool following)
    {
        _following = following;
        _followResumeTimer.Stop();
        _statusText = !following ? "已暂停跟随" : (_active ? "跟随中" : "已停止");
        if (following)
        {
            ScrollToEnd();
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
        _followResumeTimer.Tick -= OnFollowResumeTimerTick;
    }

    internal void PauseFollowingUntilIdle()
    {
        if (!_active)
        {
            return;
        }

        _following = false;
        _statusText = "已暂停跟随";
        _followResumeTimer.Stop();
        _followResumeTimer.Start();
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

        UpdateOutput();
    }

    private void AppendBatch(List<string> newLines)
    {
        _lines.AddRange(newLines);
        if (_lines.Count > MaxLines)
        {
            _lines.RemoveRange(0, _lines.Count - MaxLines);
        }

        UpdateOutput();
    }

    private void UpdateOutput()
    {
        _output.Text = _lines.Count == 0 ? string.Empty : string.Join(Environment.NewLine, _lines);
        ScrollToEnd();
    }

    /// <summary>
    /// 跟随状态下把视口对齐到末尾。文本刚变更时布局还没跑，这里拿到的是旧的 <see cref="ScrollViewer.Extent"/>；
    /// 布局完成后 <see cref="ScrollViewer.ScrollChanged"/> 会带着新的 Extent 再调用一次，最终一定落到真正的末尾。
    /// </summary>
    private void ScrollToEnd()
    {
        if (!_following)
        {
            return;
        }

        double maximum = ScrollMaximum;
        if (Math.Abs(_logScrollViewer.Offset.Y - maximum) <= double.Epsilon)
        {
            return;
        }

        _logScrollViewer.Offset = new Vector(_logScrollViewer.Offset.X, maximum);
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs args) => PauseFollowingUntilIdle();

    private void OnPointerPressed(object? sender, PointerPressedEventArgs args) => PauseFollowingUntilIdle();

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs args) => PauseFollowingUntilIdle();

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs args)
    {
        if (_following)
        {
            // 只有布局改变了内容量或视口高度才需要重新对齐，跟随自身造成的偏移变化不参与。
            if (Math.Abs(args.ExtentDelta.Y) > double.Epsilon || Math.Abs(args.ViewportDelta.Y) > double.Epsilon)
            {
                ScrollToEnd();
            }

            return;
        }

        if (!_active)
        {
            return;
        }

        double maximum = ScrollMaximum;
        bool scrollable = maximum > BottomTolerance;
        bool atBottom = maximum - _logScrollViewer.Offset.Y <= BottomTolerance;
        if (scrollable && atBottom)
        {
            _following = true;
            _statusText = "跟随中";
            _followResumeTimer.Stop();
        }
    }

    private void OnFollowResumeTimerTick(object? sender, EventArgs args)
    {
        _followResumeTimer.Stop();
        if (!_active)
        {
            return;
        }

        if (_output.SelectionStart != _output.SelectionEnd)
        {
            // 用户还留着选中的日志，等选中取消后再恢复跟随。
            _followResumeTimer.Start();
            return;
        }

        _following = true;
        _statusText = "跟随中";
        ScrollToEnd();
    }
}

