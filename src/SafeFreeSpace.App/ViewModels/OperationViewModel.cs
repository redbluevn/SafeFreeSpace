namespace SafeFreeSpace.App.ViewModels;

using System.Collections.ObjectModel;
using System.Windows.Input;
using SafeFreeSpace.App.Mvvm;

public sealed class OperationViewModel : ObservableObject
{
    private string _operationId = string.Empty;
    private string _state = string.Empty;
    private string _elapsedText = string.Empty;
    private bool _canCancel;
    private bool _isRunning;

    public OperationViewModel(Action cancelAction)
    {
        CancelCommand = new RelayCommand(_ => cancelAction(), _ => CanCancel);
        OutputLines = new ObservableCollection<string>();
    }

    public string OperationId
    {
        get => _operationId;
        set => SetProperty(ref _operationId, value);
    }

    public string State
    {
        get => _state;
        set => SetProperty(ref _state, value);
    }

    public string ElapsedText
    {
        get => _elapsedText;
        set => SetProperty(ref _elapsedText, value);
    }

    public bool CanCancel
    {
        get => _canCancel;
        set
        {
            if (SetProperty(ref _canCancel, value))
            {
                ((RelayCommand)CancelCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (SetProperty(ref _isRunning, value))
            {
                OnPropertyChanged(nameof(IsProgressIndeterminate));
            }
        }
    }

    public bool IsProgressIndeterminate => IsRunning;

    public ObservableCollection<string> OutputLines
    {
        get;
    }

    public ICommand CancelCommand
    {
        get;
    }

    public void AppendOutput(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        string trimmed = line.Trim();
        if (OutputLines.Count > 200)
        {
            OutputLines.RemoveAt(0);
        }

        OutputLines.Add(trimmed);
    }

    public void Reset()
    {
        OperationId = string.Empty;
        State = string.Empty;
        ElapsedText = string.Empty;
        CanCancel = false;
        IsRunning = false;
        OutputLines.Clear();
    }
}
