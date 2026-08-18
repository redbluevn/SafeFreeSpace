namespace SafeFreeSpace.App;

using System.Windows;
using SafeFreeSpace.App.Mvvm;
using SafeFreeSpace.App.ViewModels;
using SafeFreeSpace.Core.Interfaces;
using SafeFreeSpace.Core.Services;
using SafeFreeSpace.Infrastructure.Windows.History;
using SafeFreeSpace.Infrastructure.Windows.Pipes;
using SafeFreeSpace.Infrastructure.Windows.Storage;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        IVolumeInventory inventory = new WindowsVolumeInventory();
        var safetyPolicy = new SafetyPolicy();
        var phraseService = new ConfirmationPhraseService();
        IClock clock = new SystemClock();
        IOperationHistory history = new JsonlOperationHistory();
        try
        {
            await history.MarkAbandonedAsync().ConfigureAwait(true);
            await history.ApplyRetentionAsync(TimeSpan.FromDays(30)).ConfigureAwait(true);
        }
        catch (Exception)
        {
            // Journal maintenance is best-effort; a corrupt journal must not crash startup.
        }

        var mainWindow = new MainWindow();
        IUiDispatcher dispatcher = new WpfDispatcher(mainWindow.Dispatcher);
        var viewModel = new MainViewModel(
            inventory,
            () => new NamedPipePrivilegedOperationClient(),
            history,
            safetyPolicy,
            phraseService,
            clock,
            dispatcher);
        mainWindow.DataContext = viewModel;
        mainWindow.Show();

        try
        {
            await viewModel.InitializeAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            viewModel.ErrorMessage = $"Khởi tạo thất bại: {ex.Message}";
        }
    }
}
