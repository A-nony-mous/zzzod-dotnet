using Avalonia.Controls;

namespace ZzzOd.Gui.Shell;

public enum ZzzGuiOperationState
{
    Idle,

    Loading,

    Succeeded,

    Failed,

    Canceled,

    TimedOut,
}

public sealed record ZzzGuiOperationRecord(
    Guid OperationId,
    string Route,
    string Operation,
    ZzzGuiOperationState State,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    string? CancellationReason,
    Exception? FirstException);

public sealed class ZzzGuiOperationTracker
{
    private readonly object _gate = new();
    private readonly List<ZzzGuiOperationRecord> _records = [];

    public event EventHandler<ZzzGuiOperationRecord>? RecordChanged;

    public IReadOnlyList<ZzzGuiOperationRecord> Records
    {
        get
        {
            lock (_gate)
            {
                return _records.ToArray();
            }
        }
    }

    public Guid Start(string route, string operation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(route);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ZzzGuiOperationRecord record = new(Guid.NewGuid(), route, operation, ZzzGuiOperationState.Loading, DateTimeOffset.UtcNow, null, null, null);
        Add(record);
        return record.OperationId;
    }

    public void Complete(Guid operationId, ZzzGuiOperationState state, string? cancellationReason = null, Exception? exception = null)
    {
        if (state is ZzzGuiOperationState.Idle or ZzzGuiOperationState.Loading)
        {
            throw new ArgumentOutOfRangeException(nameof(state), state, "操作必须进入终态。");
        }

        ZzzGuiOperationRecord? changed = null;
        lock (_gate)
        {
            int index = _records.FindIndex(record => record.OperationId == operationId);
            if (index < 0 || _records[index].State != ZzzGuiOperationState.Loading)
            {
                return;
            }

            ZzzGuiOperationRecord current = _records[index];
            changed = current with
            {
                State = state,
                EndedAt = DateTimeOffset.UtcNow,
                CancellationReason = cancellationReason,
                FirstException = current.FirstException ?? exception,
            };
            _records[index] = changed;
        }

        RecordChanged?.Invoke(this, changed);
    }

    public void CancelRoute(string route, string reason)
    {
        Guid[] operationIds;
        lock (_gate)
        {
            operationIds = _records
                .Where(record => string.Equals(record.Route, route, StringComparison.Ordinal) && record.State == ZzzGuiOperationState.Loading)
                .Select(record => record.OperationId)
                .ToArray();
        }

        foreach (Guid operationId in operationIds)
        {
            Complete(operationId, ZzzGuiOperationState.Canceled, reason);
        }
    }

    private void Add(ZzzGuiOperationRecord record)
    {
        lock (_gate)
        {
            _records.Add(record);
        }

        RecordChanged?.Invoke(this, record);
    }
}

public interface IZzzPageLifecycle
{
    void CancelPageOperations(string reason)
    {
    }

    void OnPageLeave()
    {
    }

    void OnPageShown()
    {
    }

    void OnPageHidden()
    {
    }

    void DisposePage()
    {
    }
}

public sealed class ZzzPageLifecycleService
{
    private readonly ZzzGuiOperationTracker _operations;
    private Control? _currentPage;
    private string? _currentRoute;

    public ZzzPageLifecycleService()
        : this(new ZzzGuiOperationTracker())
    {
    }

    public ZzzPageLifecycleService(ZzzGuiOperationTracker operations)
    {
        _operations = operations;
    }

    public ZzzGuiOperationTracker Operations => _operations;

    public void NavigateTo(Control nextPage)
        => NavigateTo(nextPage, nextPage.GetType().Name);

    public void NavigateTo(Control nextPage, string route)
    {
        if (ReferenceEquals(_currentPage, nextPage))
        {
            return;
        }

        Guid navigationId = _operations.Start(route, "page-navigation");

        if (_currentPage is IZzzPageLifecycle previousLifecycle)
        {
            previousLifecycle.CancelPageOperations("page-leave");
            previousLifecycle.OnPageLeave();
            previousLifecycle.OnPageHidden();
        }

        if (!string.IsNullOrWhiteSpace(_currentRoute))
        {
            _operations.CancelRoute(_currentRoute, "page-leave");
        }

        _currentPage = nextPage;
        _currentRoute = route;
        if (nextPage is IZzzPageLifecycle nextLifecycle)
        {
            nextLifecycle.OnPageShown();
        }

        _operations.Complete(navigationId, ZzzGuiOperationState.Succeeded);
    }

    public void DisposeCurrent()
    {
        if (_currentPage is IZzzPageLifecycle lifecycle)
        {
            lifecycle.CancelPageOperations("window-closed");
            lifecycle.OnPageLeave();
            lifecycle.OnPageHidden();
            lifecycle.DisposePage();
        }

        if (!string.IsNullOrWhiteSpace(_currentRoute))
        {
            _operations.CancelRoute(_currentRoute, "window-closed");
        }

        _currentPage = null;
        _currentRoute = null;
    }
}
