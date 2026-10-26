using Core.Events;
using Core.Notifications;

namespace Cli.Notifications;

public sealed class ConsoleSink : INotificationSink
{
    public void Notify(CatalogChangedEventArgs e)
    {
        Console.WriteLine($"[ConsoleSink] {e.At:HH:mm:ss} {e.Kind,-8} {e.Id} {e.Delta,4:+0;-0} -> залишок {e.QuantityAfter}");
    }
}
