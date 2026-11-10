using System.Text.Encodings.Web;
using System.Text.Json;
using Core.Abstractions;
using Core.Domain;
using Core.Dto;

namespace Core.Storage;

/// <summary>
/// Файлове сховище каталогу з атомарним записом (.tmp + File.Move).
/// Прийнятий шлях до файлу надходить із конфігурації (AppOptions.Storage.ResolveFullPath).
/// Кирилиця в JSON не екранується (UnsafeRelaxedJsonEscaping) — читається людиною.
/// </summary>
public sealed class FileCatalogStore(string path) : ICatalogStore
{
    // CamelCase + кирилиця без \uXXXX + відступи для читабельності
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly Dictionary<string, Product> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _path = Path.GetFullPath(path);
    private bool _loaded;

    private void EnsureLoaded()
    {
        if (_loaded) return;
        if (File.Exists(_path))
        {
            List<ProductDto> dtos;
            try
            {
                dtos = JsonSerializer.Deserialize<List<ProductDto>>(File.ReadAllText(_path), Options) ?? [];
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    $"Не вдалося прочитати каталог з '{_path}': {ex.Message}", ex);
            }

            _cache.Clear();
            foreach (var dto in dtos)
            {
                var product = Product.FromDto(dto); // повторно перевіряє інваріанти
                _cache[product.Id] = product;
            }
        }
        _loaded = true;
    }

    /// <summary>
    /// Атомарний запис: спочатку пишемо в .tmp, потім перейменовуємо.
    /// Якщо процес впав під час запису — старий файл залишається незмінним.
    /// </summary>
    private void Flush()
    {
        // Path.Combine, а не конкатенація рядків — переносимість між OS
        var dir = Path.GetDirectoryName(_path)!;
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var dtos = _cache.Values.Select(p => p.ToDto()).ToList();
        var tmp = _path + ".tmp";

        // 1) Пишемо у .tmp (якщо тут впали — основний файл цілий)
        File.WriteAllText(tmp, JsonSerializer.Serialize(dtos, Options));

        // 2) Атомарне перейменування (на тій же файловій системі = один inode-swap)
        File.Move(tmp, _path, overwrite: true);
    }

    public IReadOnlyList<Product> List()
    {
        EnsureLoaded();
        return _cache.Values.ToList();
    }

    public Product? GetById(string id)
    {
        EnsureLoaded();
        return _cache.GetValueOrDefault(id);
    }

    public void Add(Product item)
    {
        ArgumentNullException.ThrowIfNull(item);
        EnsureLoaded();
        if (_cache.ContainsKey(item.Id))
            throw new InvalidOperationException($"Запис з id={item.Id} уже існує.");

        _cache.Add(item.Id, item);
        Flush();
    }

    public void Update(Product item)
    {
        ArgumentNullException.ThrowIfNull(item);
        EnsureLoaded();
        if (!_cache.ContainsKey(item.Id))
            throw new InvalidOperationException($"Немає запису з id={item.Id} для оновлення.");

        _cache[item.Id] = item;
        Flush();
    }

    public bool Remove(string id)
    {
        EnsureLoaded();
        if (!_cache.Remove(id)) return false;
        Flush();
        return true;
    }

    // Додаткове завдання 2 (лаб 5): пошук за предикатом
    public IReadOnlyList<Product> Find(Func<Product, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        EnsureLoaded();
        return _cache.Values.Where(predicate).ToList();
    }
}
