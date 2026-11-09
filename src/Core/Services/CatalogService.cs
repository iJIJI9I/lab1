using Core.Abstractions;
using Core.Domain;
using Core.Events;
using Core.Notifications;

namespace Core.Services;

public sealed class CatalogService(
    ICatalogStore store,
    IEnumerable<INotificationSink>? sinks = null)
{
    private readonly ICatalogStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly List<INotificationSink> _sinks = (sinks ?? []).ToList();

    // ── Подія в сервісі за конвенцією .NET ─────────────────────────────────────
    public event EventHandler<CatalogChangedEventArgs>? Changed;

    // Підняття події виключно після успішної зміни стану
    private void OnChanged(Product p, CatalogChangeKind kind, int delta)
    {
        var args = new CatalogChangedEventArgs(p.Id, kind, delta, p.Quantity);
        Changed?.Invoke(this, args);

        // Додаткове завдання 1 (лаб 6): якщо підключено INotificationSink
        foreach (var sink in _sinks)
            sink.Notify(args);
    }

    public Product Add(string sku, string name, string unit, int quantity)
    {
        var product = Product.Create(Guid.NewGuid().ToString("N")[..8], sku, name, unit, quantity);
        _store.Add(product); // інваріанти перевірено в Product.Create
        OnChanged(product, CatalogChangeKind.Added, quantity); // після зміни, не до
        return product;
    }

    public void Receive(string id, int quantity)
    {
        var product = _store.GetById(id)
            ?? throw new DomainException($"Немає запису з id={id}.");

        product.RegisterArrival(quantity); // якщо цей виклик кине виняток — події не буде
        _store.Update(product);
        OnChanged(product, CatalogChangeKind.Received, quantity);
    }

    public void Issue(string id, int quantity)
    {
        var product = _store.GetById(id)
            ?? throw new DomainException($"Немає запису з id={id}.");

        product.Issue(quantity); // інваріант: перевитрата кине DomainException
        _store.Update(product);
        OnChanged(product, CatalogChangeKind.Issued, -quantity);
    }

    public bool Remove(string id)
    {
        var product = _store.GetById(id);
        if (product is null) return false;

        if (_store.Remove(id))
        {
            OnChanged(product, CatalogChangeKind.Removed, -product.Quantity);
            return true;
        }

        return false;
    }

    public IReadOnlyList<Product> All() => _store.List();

    public Product? Find(string id) => _store.GetById(id);

    // ── Делегат Func<Product, bool> у сигнатурі методу (тиждень 6-7) ────────────
    public IReadOnlyList<Product> FindAll(Func<Product, bool> predicate) =>
        _store.List().Where(predicate).ToList();

    // ── Асинхронний імпорт із CancellationToken (тиждень 8, Крок 4) ────────────
    public Task<int> ImportAsync(
        ICatalogImporter importer,
        string path,
        CancellationToken ct = default)
        => ImportAsync(importer, path, progress: null, ct);

    public async Task<int> ImportAsync(
        ICatalogImporter importer,
        string path,
        IProgress<int>? progress,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(importer);

        var items = await importer.LoadAsync(path, ct);
        var added = 0;
        for (var i = 0; i < items.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var item = items[i];

            if (_store.GetById(item.Id) is not null)
                continue; // ідемпотентність імпорту

            _store.Add(item);
            OnChanged(item, CatalogChangeKind.Added, item.Quantity);
            added++;

            progress?.Report((int)((i + 1) * 100.0 / items.Count));
        }

        return added;
    }

    // ── Додаткове завдання 1: потоковий імпорт через IAsyncEnumerable ──────────
    public async Task<int> ImportStreamAsync(
        ICatalogImporter importer,
        string path,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(importer);

        var added = 0;
        var totalProcessed = 0;
        await foreach (var item in importer.StreamAsync(path, ct))
        {
            ct.ThrowIfCancellationRequested();
            totalProcessed++;

            if (_store.GetById(item.Id) is null)
            {
                _store.Add(item);
                OnChanged(item, CatalogChangeKind.Added, item.Quantity);
                added++;
            }

            progress?.Report(totalProcessed);
        }

        return added;
    }
}
