namespace ShooterRecordsLab.Application;

public sealed class RecordsException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
