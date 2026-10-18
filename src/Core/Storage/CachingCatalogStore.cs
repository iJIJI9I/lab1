using Core.Abstractions;
using Core.Domain;

namespace Core.Storage;

/// <summary>
/// Додаткове завдання 1: Патерн Декоратор (Decorator).
/// Приймає внутрішній ICatalogStore і додає прозоре кешування в пам'яті.
/// </summary>
public sealed class CachingCatalogStore(ICatalogStore inner) : ICatalogStore
{
    private readonly ICatalogStore _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private readonly Dictionary<string, Product> _memoryCache = new(StringComparer.OrdinalIgnoreCase);
    private bool _cachePopulated;

    private void PrimeCache()
    {
        if (_cachePopulated) return;
        foreach (var product in _inner.List())
            _memoryCache[product.Id] = product;
        _cachePopulated = true;
    }

    public IReadOnlyList<Product> List()
    {
        PrimeCache();
        return _memoryCache.Values.ToList();
    }

    public Product? GetById(string id)
    {
        PrimeCache();
        return _memoryCache.GetValueOrDefault(id);
    }

    public void Add(Product item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _inner.Add(item);
        _memoryCache[item.Id] = item;
    }

    public void Update(Product item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _inner.Update(item);
        _memoryCache[item.Id] = item;
    }

    public bool Remove(string id)
    {
        bool removed = _inner.Remove(id);
        if (removed) _memoryCache.Remove(id);
        return removed;
    }

    public IReadOnlyList<Product> Find(Func<Product, bool> predicate)
    {
        PrimeCache();
        return _memoryCache.Values.Where(predicate).ToList();
    }
}
