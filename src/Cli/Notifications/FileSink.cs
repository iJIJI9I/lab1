using Core.Events;
using Core.Notifications;

namespace Cli.Notifications;

public sealed class FileSink : INotificationSink
{
    private readonly string _path;

    public FileSink(string path)
    {
        _path = path;
        string? dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
    }

    public void Notify(CatalogChangedEventArgs e)
    {
        var line = $"[FileSink] {e.At:HH:mm:ss} {e.Kind,-8} {e.Id} {e.Delta,4:+0;-0} -> залишок {e.QuantityAfter}";
        File.AppendAllText(_path, line + Environment.NewLine);
    }
}
