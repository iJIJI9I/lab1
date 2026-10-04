using Core.Dto;
using Core.Import;

string path = args.Length > 0 ? args[0] : Path.Combine("data", "sample.csv");

if (!File.Exists(path))
{
    Console.WriteLine($"Файл не знайдено: {Path.GetFullPath(path)}");
    return 1;
}

// Додаткове завдання 1: вибір імпортера за розширенням файлу
string ext = Path.GetExtension(path).ToLowerInvariant();

if (ext == ".json")
{
    RunJsonImport(path);
    return 0;
}

if (Path.GetFileNameWithoutExtension(path) == "mixed")
{
    RunMixedImport(path);
    return 0;
}

RunCsvImport(path);
return 0;

// ── Звичайний CSV ────────────────────────────────────────────────────────────
static void RunCsvImport(string path)
{
    ImportResult<ProductDto> result = ProductCsvImporter.Load(path);

    PrintStats("CSV", result.Items.Count, result.Errors.Count);

    Console.WriteLine($"Завантажено записів: {result.Items.Count}");
    foreach (ProductDto p in result.Items.Take(5))
        Console.WriteLine($"  {p.Id,-6} {p.Sku,-10} {p.Name,-26} {p.Quantity,5} {p.Unit}");

    PrintErrors(result.Errors);
}

// ── JSON (додаткове завдання 1) ──────────────────────────────────────────────
static void RunJsonImport(string path)
{
    ImportResult<ProductDto> result = ProductJsonImporter.Load(path);

    PrintStats("JSON", result.Items.Count, result.Errors.Count);

    Console.WriteLine($"Завантажено записів: {result.Items.Count}");
    foreach (ProductDto p in result.Items.Take(5))
        Console.WriteLine($"  {p.Id,-6} {p.Sku,-10} {p.Name,-26} {p.Quantity,5} {p.Unit}");

    PrintErrors(result.Errors);
}

// ── Змішаний CSV (додаткове завдання 2) ─────────────────────────────────────
static void RunMixedImport(string path)
{
    MixedImportResult result = MixedCsvImporter.Load(path);

    int total    = result.Products.Count + result.Warehouses.Count + result.Errors.Count;
    int accepted = result.Products.Count + result.Warehouses.Count;
    PrintStats("MIXED", accepted, result.Errors.Count, total);

    Console.WriteLine($"  Товарів: {result.Products.Count}");
    foreach (ProductDto p in result.Products.Take(3))
        Console.WriteLine($"    [P] {p.Id,-6} {p.Sku,-10} {p.Name,-26} {p.Quantity,5} {p.Unit}");

    Console.WriteLine($"  Складів: {result.Warehouses.Count}");
    foreach (WarehouseDto w in result.Warehouses.Take(3))
        Console.WriteLine($"    [W] {w.Id,-6} {w.Name,-28} {w.Location}  місткість: {w.Capacity}");

    PrintErrors(result.Errors);
}

// ── Допоміжні методи ─────────────────────────────────────────────────────────

// Додаткове завдання 3: статистика одним рядком
static void PrintStats(string mode, int accepted, int skipped, int? total = null)
{
    int all      = total ?? accepted + skipped;
    double pct   = all == 0 ? 0 : skipped * 100.0 / all;
    Console.WriteLine($"[{mode}] Всього: {all} | Прийнято: {accepted} | Пропущено: {skipped} | Помилок: {pct:F1}%");
}

static void PrintErrors(IReadOnlyList<string> errors)
{
    if (errors.Count == 0) return;
    Console.WriteLine($"Пропущено рядків: {errors.Count}");
    foreach (string e in errors)
        Console.WriteLine($"  ! {e}");
}