namespace Publicizer;

public interface ITaskLogger
{
    public void Error(string message);
    public void Warning(string message);
    public void Info(string message);
    public void Verbose(string message);
}
