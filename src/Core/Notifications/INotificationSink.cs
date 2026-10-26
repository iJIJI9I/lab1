using Core.Events;

namespace Core.Notifications;

/// <summary>
/// Додаткове завдання 1: Альтернатива подіям через патерн Sink та ін'єкцію залежностей.
/// </summary>
public interface INotificationSink
{
    void Notify(CatalogChangedEventArgs eventArgs);
}
