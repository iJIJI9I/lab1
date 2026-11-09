using Core.Domain;

namespace Core.Abstractions;

public interface ICatalogImporter
{
    Task<IReadOnlyList<Product>> LoadAsync(string path, CancellationToken ct = default);

    // Додаткове завдання 1: потоковий імпорт через IAsyncEnumerable
    IAsyncEnumerable<Product> StreamAsync(string path, CancellationToken ct = default);
}
