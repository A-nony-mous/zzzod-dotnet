namespace ZzzOd.GameLogic.AutoBattle;

internal sealed class LatestValueSlot<T>
    where T : class
{
    private readonly object _lock = new();
    private bool _active;
    private T? _pending;

    public bool Submit(T value, out T? replaced)
    {
        lock (_lock)
        {
            if (!_active)
            {
                _active = true;
                replaced = null;
                return true;
            }

            replaced = _pending;
            _pending = value;
            return false;
        }
    }

    public T? CompleteActive()
    {
        lock (_lock)
        {
            T? next = _pending;
            _pending = null;
            if (next is null)
            {
                _active = false;
            }

            return next;
        }
    }

    public T? ClearPending()
    {
        lock (_lock)
        {
            T? pending = _pending;
            _pending = null;
            return pending;
        }
    }
}
