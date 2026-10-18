using Core.Domain;

namespace Core.Abstractions;

public interface ICatalogStore
{
    IReadOnlyList<Product> List();
    Product? GetById(string id);
    void Add(Product item);
    void Update(Product item);
    bool Remove(string id);

    // Додаткове завдання 2: пошук за предикатом
    IReadOnlyList<Product> Find(Func<Product, bool> predicate);
}
