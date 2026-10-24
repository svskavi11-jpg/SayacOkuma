namespace Westalife.MbusReader;

/// <summary>
/// UI iş parçacığına SENKRON aktarım yapan IProgress (Progress&lt;T&gt; gönderimi asenkron olduğundan,
/// tarama bittiğinde bazı bildirimler henüz işlenmemiş olabilir; bu sınıf sonuç tutarlılığını garanti eder).
/// </summary>
internal sealed class SyncProgress<T> : IProgress<T>
{
    private readonly Control _owner;
    private readonly Action<T> _handler;

    public SyncProgress(Control owner, Action<T> handler)
    {
        _owner = owner;
        _handler = handler;
    }

    public void Report(T value)
    {
        if (_owner.IsDisposed)
        {
            return;
        }

        _owner.Invoke(() => _handler(value));
    }
}
