using Core.Abstractions;
using Core.Domain;

namespace Core.Storage;

/// <summary>
/// Додаткове завдання 3: Фабрика вибору та конструювання сховища.
/// Ізолює правила аналізу аргументів командного рядка від Composition Root.
/// </summary>
public static class StoreFactory
{
    public static ICatalogStore Create(string[] args, string dataPath, IEnumerable<Product>? seed = null)
    {
        bool useFile  = args.Contains("--file", StringComparer.OrdinalIgnoreCase);
        bool useCache = args.Contains("--cached", StringComparer.OrdinalIgnoreCase);

        ICatalogStore store = useFile
            ? new FileCatalogStore(dataPath)
            : new InMemoryCatalogStore(seed);

        return useCache ? new CachingCatalogStore(store) : store;
    }
}
