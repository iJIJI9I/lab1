using Core.Abstractions;
using Core.Domain;

namespace Core.Services;

public sealed class CatalogService(ICatalogStore store)
{
    // Сервіс залежить виключно від абстракції ICatalogStore (DIP)
    private readonly ICatalogStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public Product Add(string sku, string name, string unit, int quantity)
    {
        var product = Product.Create(Guid.NewGuid().ToString("N")[..8], sku, name, unit, quantity);
        _store.Add(product); // інваріанти перевірено в Product.Create
        return product;
    }

    public void Receive(string id, int quantity)
    {
        var product = _store.GetById(id)
            ?? throw new InvalidOperationException($"Немає запису з id={id}.");

        product.RegisterArrival(quantity); // метод сутності з лаб 4
        _store.Update(product);
    }

    public void Issue(string id, int quantity)
    {
        var product = _store.GetById(id)
            ?? throw new InvalidOperationException($"Немає запису з id={id}.");

        product.Issue(quantity); // метод сутності з лаб 4
        _store.Update(product);
    }

    public IReadOnlyList<Product> All() => _store.List();

    public Product? Find(string id) => _store.GetById(id);

    // Додаткове завдання 2: пошук за довільним предикатом
    public IReadOnlyList<Product> Search(Func<Product, bool> predicate) => _store.Find(predicate);
}
