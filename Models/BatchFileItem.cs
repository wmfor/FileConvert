using ReactiveUI;

namespace FileConvert.Models;

public enum BatchStatus { Pending, Converting, Done, Error }

public class BatchFileItem : ReactiveObject
{
    private BatchStatus _status = BatchStatus.Pending;
    private string _statusText = "Pending";

    public string FilePath  { get; init; } = "";
    public string FileName  { get; init; } = "";
    public string FileType  { get; init; } = "";

    public BatchStatus Status
    {
        get => _status;
        set
        {
            this.RaiseAndSetIfChanged(ref _status, value);
            StatusText = value switch
            {
                BatchStatus.Pending    => "Pending",
                BatchStatus.Converting => "Converting",
                BatchStatus.Done       => "Done",
                BatchStatus.Error      => "Error",
                _                      => ""
            };
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }
}
