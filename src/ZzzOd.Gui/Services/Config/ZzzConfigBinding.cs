namespace ZzzOd.Gui.Services.Config;

public interface IZzzConfigBinding<T>
{
    T Read();

    void Save(T value);
}

public sealed class ZzzDelegateConfigBinding<T> : IZzzConfigBinding<T>
{
    private readonly Func<T> _read;
    private readonly Action<T> _save;

    public ZzzDelegateConfigBinding(Func<T> read, Action<T> save)
    {
        _read = read;
        _save = save;
    }

    public T Read() => _read();

    public void Save(T value) => _save(value);
}

public sealed record ZzzConfigOption<T>(string Text, T Value, string? Description = null)
{
    public override string ToString() => Text;
}
