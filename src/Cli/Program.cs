using Core.Domain;
using Core.Dto;
using Core.Import;

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                         Лабораторна 4 — Демонстрація                       ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

RunLab4Demo();

// Попередній код лаб 1-3 можна запустити через аргументи командного рядка
// Ігноруємо dotnet-флаги (--nologo тощо)
string[] fileArgs = args.Where(a => !a.StartsWith("--")).ToArray();
if (fileArgs.Length > 0)
    RunLegacy(fileArgs);

return 0;

// ════════════════════════════════════════════════════════════════════════════════
// Лаб 4: доменна модель
// ════════════════════════════════════════════════════════════════════════════════
static void RunLab4Demo()
{
    RunProductScenarios();
    Console.WriteLine();
    RunOrderScenarios();
    Console.WriteLine();
    RunDomainMapperScenario();
}

// ── Сценарії для Product ─────────────────────────────────────────────────────
static void RunProductScenarios()
{
    Console.WriteLine("╔══════════════════════════════════════════════════╗");
    Console.WriteLine("║          PRODUCT — Сценарій 1: успіх             ║");
    Console.WriteLine("╚══════════════════════════════════════════════════╝");

    Product cement = Product.Create("P-001", "sku-001", "Цемент М400 25кг", "шт", 100);
    Console.WriteLine($"  Створено:    {cement}");

    cement.RegisterArrival(50);
    Console.WriteLine($"  Після приходу +50:  {cement}");

    cement.Issue(30);
    Console.WriteLine($"  Після видачі  -30:  {cement}");

    // Маппінг DTO і назад
    ProductDto dto = cement.ToDto();
    Product    restored = Product.FromDto(dto);
    Console.WriteLine($"  Відновлено з DTO:   {restored}");

    Console.WriteLine();
    Console.WriteLine("╔══════════════════════════════════════════════════╗");
    Console.WriteLine("║     PRODUCT — Сценарій 2: порушення інваріантів  ║");
    Console.WriteLine("╚══════════════════════════════════════════════════╝");

    TryDo("видача більша за залишок",
          () => cement.Issue(1000));

    TryDo("порожній SKU",
          () => Product.Create("P-002", " ", "Пісок", "т", 10));

    TryDo("від'ємний початковий залишок",
          () => Product.Create("P-003", "SKU-003", "Цегла", "шт", -5));

    TryDo("нульова кількість приходу",
          () => cement.RegisterArrival(0));

    TryDo("від'ємна кількість видачі",
          () => cement.Issue(-10));

    // Перевіряємо, що стан після всіх відмов не змінився
    Console.WriteLine();
    Console.WriteLine($"  Стан після всіх відмов: {cement}  ← залишок не змінився");
}

// ── Сценарії для Order ───────────────────────────────────────────────────────
static void RunOrderScenarios()
{
    Console.WriteLine("╔══════════════════════════════════════════════════╗");
    Console.WriteLine("║          ORDER — Сценарій 1: успіх               ║");
    Console.WriteLine("╚══════════════════════════════════════════════════╝");

    Order order = Order.Create("O-001", "C-007");
    Console.WriteLine($"  Створено:   {order}");

    order.AddLine("P-001", "Цемент М400 25кг", 250.00m, 10);
    order.AddLine("P-002", "Пісок будівельний", 80.50m,  5);
    Console.WriteLine($"  Після рядків: {order}");
    foreach (OrderLine line in order.Lines)
        Console.WriteLine($"    {line}");

    order.Confirm();
    Console.WriteLine($"  Після підтвердження: {order}");

    // Маппінг DTO і назад
    OrderDto dto     = order.ToDto();
    Order    restored = Order.FromDto(dto);
    Console.WriteLine($"  Відновлено з DTO: {restored}");

    Console.WriteLine();
    Console.WriteLine("╔══════════════════════════════════════════════════╗");
    Console.WriteLine("║     ORDER — Сценарій 2: порушення інваріантів    ║");
    Console.WriteLine("╚══════════════════════════════════════════════════╝");

    TryDo("додавання рядку до підтвердженого замовлення",
          () => order.AddLine("P-003", "Щебінь", 120m, 3));

    TryDo("підтвердження вже підтвердженого замовлення",
          () => order.Confirm());

    TryDo("підтвердження порожнього замовлення",
          () =>
          {
              Order empty = Order.Create("O-002", "C-008");
              empty.Confirm();
          });

    TryDo("рядок із нульовою кількістю",
          () =>
          {
              Order draft = Order.Create("O-003", "C-009");
              draft.AddLine("P-001", "Товар", 100m, 0);
          });

    TryDo("рядок із від'ємною ціною",
          () =>
          {
              Order draft = Order.Create("O-004", "C-010");
              draft.AddLine("P-001", "Товар", -50m, 2);
          });

    TryDo("скасування підтвердженого замовлення",
          () => order.Cancel());

    // Перевіряємо, що стан після всіх відмов не змінився
    Console.WriteLine();
    Console.WriteLine($"  Стан після всіх відмов: {order}  ← статус не змінився");
}

// ── Додаткове завдання 1: ProductDomainMapper ───────────────────────────────
static void RunDomainMapperScenario()
{
    Console.WriteLine("╔══════════════════════════════════════════════════╗");
    Console.WriteLine("║  Доп. завдання: ImportResult → Domain entities   ║");
    Console.WriteLine("╚══════════════════════════════════════════════════╝");

    string csvPath = Path.Combine("data", "sample.csv");
    if (!File.Exists(csvPath))
    {
        Console.WriteLine($"  Файл {csvPath} не знайдено, сценарій пропущено.");
        return;
    }

    ImportResult<ProductDto> csvResult = ProductCsvImporter.Load(csvPath);
    DomainImportResult domainResult    = ProductDomainMapper.ToDomain(csvResult);

    Console.WriteLine($"  CSV завантажено: {csvResult.Items.Count} DTO, {csvResult.Errors.Count} CSV-помилок");
    Console.WriteLine($"  Domain: {domainResult.Products.Count} сутностей, {domainResult.RejectedByDomain.Count} відхилень");

    foreach (Product p in domainResult.Products.Take(3))
        Console.WriteLine($"    ✓ {p}");

    foreach (string err in domainResult.RejectedByDomain.Take(3))
        Console.WriteLine($"    ✗ {err}");
}

// ════════════════════════════════════════════════════════════════════════════════
// Спільний допоміжний обробник винятків
// ════════════════════════════════════════════════════════════════════════════════
static void TryDo(string title, Action action)
{
    try
    {
        action();
        Console.WriteLine($"  ⚠  {title}: виняток НЕ спрацював — інваріант відсутній!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  ✓  {title}:");
        Console.WriteLine($"       {ex.GetType().Name} — {ex.Message}");
    }
}

// ════════════════════════════════════════════════════════════════════════════════
// Код попередніх лабораторних (запуск через аргументи)
// ════════════════════════════════════════════════════════════════════════════════
static void RunLegacy(string[] args)
{
    string path = args[0];

    if (!File.Exists(path))
    {
        Console.WriteLine($"Файл не знайдено: {Path.GetFullPath(path)}");
        return;
    }

    string ext = Path.GetExtension(path).ToLowerInvariant();

    if (ext == ".json")
    {
        RunJsonImport(path);
        return;
    }

    if (Path.GetFileNameWithoutExtension(path) == "mixed")
    {
        RunMixedImport(path);
        return;
    }

    RunCsvImport(path);
}

static void RunCsvImport(string path)
{
    ImportResult<ProductDto> result = ProductCsvImporter.Load(path);
    PrintStats("CSV", result.Items.Count, result.Errors.Count);
    Console.WriteLine($"Завантажено записів: {result.Items.Count}");
    foreach (ProductDto p in result.Items.Take(5))
        Console.WriteLine($"  {p.Id,-6} {p.Sku,-10} {p.Name,-26} {p.Quantity,5} {p.Unit}");
    PrintErrors(result.Errors);
}

static void RunJsonImport(string path)
{
    ImportResult<ProductDto> result = ProductJsonImporter.Load(path);
    PrintStats("JSON", result.Items.Count, result.Errors.Count);
    Console.WriteLine($"Завантажено записів: {result.Items.Count}");
    foreach (ProductDto p in result.Items.Take(5))
        Console.WriteLine($"  {p.Id,-6} {p.Sku,-10} {p.Name,-26} {p.Quantity,5} {p.Unit}");
    PrintErrors(result.Errors);
}

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

static void PrintStats(string mode, int accepted, int skipped, int? total = null)
{
    int    all = total ?? accepted + skipped;
    double pct = all == 0 ? 0 : skipped * 100.0 / all;
    Console.WriteLine($"[{mode}] Всього: {all} | Прийнято: {accepted} | Пропущено: {skipped} | Помилок: {pct:F1}%");
}

static void PrintErrors(IReadOnlyList<string> errors)
{
    if (errors.Count == 0) return;
    Console.WriteLine($"Пропущено рядків: {errors.Count}");
    foreach (string e in errors)
        Console.WriteLine($"  ! {e}");
}