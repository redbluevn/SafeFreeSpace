namespace SafeFreeSpace.App.Mvvm;

using System.Windows.Threading;

public sealed class WpfDispatcher : IUiDispatcher
{
    private readonly Dispatcher _dispatcher;

    public WpfDispatcher(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public void Invoke(Action action)
    {
        _dispatcher.Invoke(action);
    }

    public void BeginInvoke(Action action)
    {
        _dispatcher.BeginInvoke(action);
    }
}
