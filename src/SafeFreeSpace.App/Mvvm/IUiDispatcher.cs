namespace SafeFreeSpace.App.Mvvm;

public interface IUiDispatcher
{
    void Invoke(Action action);

    void BeginInvoke(Action action);
}
