namespace Publicizer;

public interface ITaskLogger
{
    void Error(string message);
    void Warning(string message);
    void Info(string message);
    void Verbose(string message);
}
