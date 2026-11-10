using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using Core.Abstractions;
using Core.Configuration;
using Core.Domain;
using Core.Dto;
using Core.Import;
using Core.Notifications;
using Core.Reports;
using Core.Services;
using Core.Storage;
using Microsoft.Extensions.Configuration;
using System.Globalization;
using Cli.Notifications;
using Core;
using Core.Events;

// ════════════════════════════════════════════════════════════════════════════════
// ЛАБОРАТОРНА РОБОТА 9: Шляхи, JSON, конфігурація, Secrets
// ════════════════════════════════════════════════════════════════════════════════

// ── Збірка конфігурації (завдання 2, 3, 4) ───────────────────────────────────
var envName = Environment.GetEnvironmentVariable("CROSSAPP_ENVIRONMENT") ?? "Development";

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)                                    // базовий каталог для JSON-файлів
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)  // базові налаштування (є у Git)
    .AddJsonFile($"appsettings.{envName}.json", optional: true)              // перевизначення для середовища
    .AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true)          // секрети поза репозиторієм
    .AddEnvironmentVariables(prefix: "CROSSAPP_")                            // CROSSAPP_Storage__DataPath та ін.
    .AddCommandLine(args)                                                     // --Storage:DataPath=tmp (опційно)
    .Build();

// Прив'язка секції до класу AppOptions (завдання 3)
var options = config.Get<AppOptions>() ?? new AppOptions();

// Проста змінна CROSSAPP_DATA_PATH перекриває Storage:DataPath (завдання 4)
var legacyDataPath = Environment.GetEnvironmentVariable("CROSSAPP_DATA_PATH");
var storage = string.IsNullOrWhiteSpace(legacyDataPath)
    ? options.Storage
    : new StorageOptions { DataPath = legacyDataPath, FileName = options.Storage.FileName };

// Визначити повний шлях до файлу (завдання 5, 7)
// AppContext.BaseDirectory — незалежно від того, з якого cwd запущено dotnet run
var dataFilePath = storage.ResolveFullPath(AppContext.BaseDirectory);

// Секрет — читається зі user-secrets або env (завдання 6)
var apiKey = config["Integration:ApiKey"];

// ── Маршрутизація команд ─────────────────────────────────────────────────────
if (args.Contains("--lab4", StringComparer.OrdinalIgnoreCase))
{
    RunLab4Demo();
    return 0;
}

if (args.Contains("--lab5", StringComparer.OrdinalIgnoreCase))
{
    RunLab5Demo(args, dataFilePath);
    return 0;
}

if (args.Contains("--lab6", StringComparer.OrdinalIgnoreCase))
{
    RunLab6Demo(args, dataFilePath);
    return 0;
}

string? reportCommand = args.FirstOrDefault(argument =>
    !argument.StartsWith("--", StringComparison.Ordinal));
if (args.Contains("--lab7", StringComparer.OrdinalIgnoreCase) ||
    reportCommand is "report-top" or "report-low" or "report-unit" or "report-groups" or
        "report-groups-query" or "report-sum" or "report-join" or "report-deferred" or
        "report-empty" or "report-benchmark")
{
    RunLab7Report(args, reportCommand ?? "report-top", dataFilePath);
    return 0;
}

if (args.Contains("--lab9", StringComparer.OrdinalIgnoreCase) ||
    reportCommand is "export" or "import")
{
    await RunLab9DemoAsync(args, config, storage, dataFilePath, apiKey, reportCommand);
    return 0;
}

// За замовчуванням (або з прапорцями --lab8, --file, --cancel, --timeout) запускається Лабораторна 8:
await RunLab8DemoAsync(args, dataFilePath);
return 0;

// ════════════════════════════════════════════════════════════════════════════════
// ЛАБОРАТОРНА РОБОТА 9: Конфігурація, Secrets, JSON I/O, переносимість шляхів
// ════════════════════════════════════════════════════════════════════════════════
static async Task RunLab9DemoAsync(
    string[] args,
    IConfiguration config,
    StorageOptions storage,
    string dataFilePath,
    string? apiKey,
    string? command)
{
    if (command == "export")
    {
        Console.WriteLine($"Файл даних : {dataFilePath}");
        Console.WriteLine(apiKey is null
            ? "ApiKey: не заданий"
            : $"ApiKey: присутній, довжина {apiKey.Length}");

        var store = new FileCatalogStore(dataFilePath);
        var service = new CatalogService(store);

        // Якщо файл ще порожній або не існує — додаємо початкові дані
        if (store.List().Count == 0)
        {
            service.Add("A-100", "Цемент М400", "мішок", 40);
            service.Add("B-200", "Арматура d12", "шт", 120);
            service.Add("C-300", "Пінопласт 50мм", "аркуш", 60);
        }

        var items = store.List();
        Console.WriteLine($"Експортовано {items.Count} записи.");
        Console.WriteLine($"{storage.DataPath}/{storage.FileName}:");
        Console.WriteLine(ExportToJson(items));
        return;
    }

    Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║   ЛАБОРАТОРНА РОБОТА 9: Шляхи, JSON, конфігурація, Secrets  ║");
    Console.WriteLine("╚══════════════════════════════════════════════════════════════╝\n");

    // ── 1. Показати шлях до файлу та API-ключ (завдання 5, 6) ───────────────
    Console.WriteLine($"Файл даних    : {dataFilePath}");
    Console.WriteLine($"Профіль       : {config["EnvironmentName"]}");
    Console.WriteLine($"Рівень логу   : {config["Logging:MinimumLevel"]}");
    Console.WriteLine(apiKey is null
        ? "ApiKey         : не заданий"
        : $"ApiKey         : присутній, довжина {apiKey.Length}");
    Console.WriteLine();

    // ── 2. Запис у FileCatalogStore (атомарний .tmp + File.Move) (завдання 5) ─
    Console.WriteLine(">>> 1. Запис у FileCatalogStore (атомарний запис .tmp → файл):");
    var fileStore = new FileCatalogStore(dataFilePath);
    var svc = new CatalogService(fileStore);

    var p1 = svc.Add("SKU-A100", "Цемент М400 25кг", "мішок", 40);
    var p2 = svc.Add("SKU-B200", "Арматура d12 6м", "шт", 120);
    var p3 = svc.Add("SKU-C300", "Пінопласт 50мм 1200×600", "аркуш", 60);
    svc.Receive(p1.Id, 15);
    Console.WriteLine($"  Збережено у файл: {dataFilePath}");
    Console.WriteLine($"  Записів у сховищі: {fileStore.List().Count}");
    Console.WriteLine();

    // ── 3. Експорт (читання з FileCatalogStore + JSON з кирилицею) ───────────
    Console.WriteLine(">>> 2. Експорт каталогу в JSON (кирилиця без \\uXXXX):");
    var exportJson = ExportToJson(fileStore.List());
    Console.WriteLine(exportJson[..Math.Min(500, exportJson.Length)]);
    Console.WriteLine();

    // ── 4. Імпорт із JSON-рядка ───────────────────────────────────────────────
    Console.WriteLine(">>> 3. Імпорт нових товарів із JSON:");
    var importJson = """
    [
      { "id": "P-IMP-01", "sku": "SKU-IMP01", "name": "Шпаклівка фінішна 25кг", "unit": "мішок", "quantity": 30 },
      { "id": "P-IMP-02", "sku": "SKU-IMP02", "name": "Фарба інтер'єрна 10л",   "unit": "відро", "quantity": 18 }
    ]
    """;
    var importStore = new InMemoryCatalogStore();
    var importSvc = new CatalogService(importStore);
    var imported = ImportFromJson(importJson, importSvc);
    Console.WriteLine($"  Імпортовано: {imported} нових записів.");
    Console.WriteLine();

    // ── 5. Перевірка переносимості шляху (завдання 7) ─────────────────────────
    Console.WriteLine(">>> 4. Перевірка переносимості шляхів:");
    Console.WriteLine($"  AppContext.BaseDirectory : {AppContext.BaseDirectory}");
    Console.WriteLine($"  Environment.CurrentDirectory: {Environment.CurrentDirectory}");
    Console.WriteLine($"  Збігаються: {string.Equals(Path.GetFullPath(AppContext.BaseDirectory), Path.GetFullPath(Environment.CurrentDirectory), StringComparison.OrdinalIgnoreCase)}");
    Console.WriteLine($"  Файл даних (однаковий незалежно від cwd): {dataFilePath}");
    Console.WriteLine();

    // ── 6. Перевизначення CROSSAPP_DATA_PATH ──────────────────────────────────
    Console.WriteLine(">>> 5. Перевизначення шляху змінною CROSSAPP_DATA_PATH:");
    var envDataPath = Environment.GetEnvironmentVariable("CROSSAPP_DATA_PATH");
    Console.WriteLine(envDataPath is null
        ? "  CROSSAPP_DATA_PATH: не задано (використовується Storage:DataPath з appsettings.json)"
        : $"  CROSSAPP_DATA_PATH = {envDataPath}  ← перекриває appsettings.json");
    Console.WriteLine();

    Console.WriteLine(">>> 6. Перевірка атомарності запису (файл .tmp + File.Move):");
    Console.WriteLine($"  Перевірте: файл {Path.ChangeExtension(dataFilePath, ".tmp")} не залишився після запису.");
    Console.WriteLine($"  Якщо процес впав під час запису — старий файл збережено (атомарність гарантовано).");
}

// ── Допоміжні функції: Експорт/Імпорт JSON ────────────────────────────────────

// Опції серіалізації: кирилиця відображається безпосередньо, а не як \uXXXX
static JsonSerializerOptions BuildJsonOpts() => new()
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
};

static string ExportToJson(IReadOnlyList<Product> products)
{
    var dtos = products.Select(p => p.ToDto()).ToList();
    return JsonSerializer.Serialize(dtos, BuildJsonOpts());
}

static int ImportFromJson(string json, CatalogService service)
{
    List<ProductDto> dtos;
    try
    {
        dtos = JsonSerializer.Deserialize<List<ProductDto>>(json, BuildJsonOpts()) ?? [];
    }
    catch (JsonException ex)
    {
        Console.WriteLine($"  Помилка парсингу JSON: {ex.Message}");
        return 0;
    }

    var added = 0;
    foreach (var dto in dtos)
    {
        try
        {
            service.Add(dto.Sku, dto.Name, dto.Unit, dto.Quantity);
            added++;
        }
        catch (DomainException ex)
        {
            Console.WriteLine($"  Запис пропущено ({dto.Sku}): {ex.Message}");
        }
        catch (InvalidOperationException)
        {
            // вже існує — ідемпотентність
        }
    }
    return added;
}

// ════════════════════════════════════════════════════════════════════════════════
// Лабораторна 8 (збережено для сумісності)
// ════════════════════════════════════════════════════════════════════════════════
static async Task RunLab8DemoAsync(string[] args, string dataFilePath)
{
    Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║   ЛАБОРАТОРНА РОБОТА 8: async/await, скасування, тести       ║");
    Console.WriteLine("╚══════════════════════════════════════════════════════════════╝\n");

    bool useFile = args.Contains("--file", StringComparer.OrdinalIgnoreCase);

    // Шукаємо catalog.json у робочому каталозі або в AppContext.BaseDirectory
    string catalogPath = File.Exists(Path.Combine("data", "catalog.json"))
        ? Path.Combine("data", "catalog.json")
        : Path.Combine(AppContext.BaseDirectory, "data", "catalog.json");

    if (!File.Exists(catalogPath))
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(catalogPath))!);
        var sampleJson = JsonSerializer.Serialize(
            SampleData.Products().Select(p => p.ToDto()),
            new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        File.WriteAllText(catalogPath, sampleJson);
    }

    ICatalogStore store = useFile
        ? new FileCatalogStore(catalogPath)
        : new InMemoryCatalogStore();

    var service = new CatalogService(store);
    var importer = new JsonCatalogImporter();

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
        Console.WriteLine("\n[Сигнал Ctrl+C перехоплено, надсилаємо запит на скасування...]");
    };

    if (args.Contains("--cancel", StringComparer.OrdinalIgnoreCase))
        cts.Cancel();
    else if (args.Contains("--timeout", StringComparer.OrdinalIgnoreCase))
        cts.CancelAfter(TimeSpan.FromMilliseconds(5));

    Console.WriteLine($">>> 1. Асинхронний імпорт файлу: {catalogPath}");
    var progress = new Progress<int>(percent => Console.Write($"\r[Прогрес імпорту: {percent,3}%] "));

    try
    {
        var added = await service.ImportAsync(importer, catalogPath, progress, cts.Token);
        Console.WriteLine();
        Console.WriteLine($"Імпортовано записів: {added}");
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("\nІмпорт скасовано.");
    }
    catch (DomainException ex)
    {
        Console.WriteLine($"\nДані відхилено: {ex.Message}");
    }
    catch (FileNotFoundException)
    {
        Console.WriteLine($"\nФайл не знайдено: {catalogPath}");
    }

    if (useFile || args.Contains("--cancel", StringComparer.OrdinalIgnoreCase) ||
        args.Contains("--timeout", StringComparer.OrdinalIgnoreCase))
        return;

    Console.WriteLine("\n>>> 2. Демонстрація кооперативного скасування (CancelAfter / таймаут):");
    using (var timeoutCts = new CancellationTokenSource())
    {
        timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(1));
        await Task.Delay(10);
        try
        {
            await service.ImportAsync(importer, catalogPath, timeoutCts.Token);
            Console.WriteLine("Імпортовано успішно (неочікувано)");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Імпорт скасовано. (успішно перехоплено OperationCanceledException)");
        }
    }

    Console.WriteLine("\n>>> 3. Демонстрація обробки зіпсованого файлу (DomainException):");
    var corruptedTempPath = Path.Combine(Path.GetTempPath(), $"corrupted_{Guid.NewGuid():N}.json");
    try
    {
        await File.WriteAllTextAsync(corruptedTempPath, """
        [
          { "Id": "ERR-1", "Sku": "SKU-BAD", "Name": "Зіпсований товар", "Unit": "шт", "Quantity": -50 }
        ]
        """);
        await service.ImportAsync(importer, corruptedTempPath);
    }
    catch (DomainException ex)
    {
        Console.WriteLine($"Дані відхилено: {ex.Message}");
    }
    finally
    {
        if (File.Exists(corruptedTempPath)) File.Delete(corruptedTempPath);
    }

    Console.WriteLine("\n>>> 4. Демонстрація обробки відсутнього файлу (FileNotFoundException):");
    try
    {
        await service.ImportAsync(importer, "non_existent_file.json");
    }
    catch (FileNotFoundException ex)
    {
        Console.WriteLine($"Файл не знайдено: {ex.FileName}");
    }

    Console.WriteLine("\n>>> 5. Додаткове завдання 1: Потоковий імпорт (IAsyncEnumerable):");
    var streamStore = new InMemoryCatalogStore();
    var streamService = new CatalogService(streamStore);
    var streamAdded = await streamService.ImportStreamAsync(importer, catalogPath);
    Console.WriteLine($"Потоково завантажено у сховище: {streamAdded} записів.");

    Console.WriteLine("\n>>> 6. Додаткове завдання 2: Зв'язані токени (User + Timeout):");
    using var userCts = new CancellationTokenSource();
    using var timerCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(10));
    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(userCts.Token, timerCts.Token);
    await Task.Delay(20);
    Console.WriteLine($"Користувач скасував: {userCts.IsCancellationRequested}");
    Console.WriteLine($"Таймаут скасував:    {timerCts.IsCancellationRequested}");
    Console.WriteLine($"Зв'язаний токен:     {linkedCts.IsCancellationRequested} (спрацював саме таймаут)");
}

// ════════════════════════════════════════════════════════════════════════════════
// Лабораторна 7 (збережено для сумісності)
// ════════════════════════════════════════════════════════════════════════════════
static void RunLab7Report(string[] args, string command, string dataFilePath)
{
    if (command == "report-deferred")
    {
        var deferred = ReportService.DemonstrateDeferredExecution(
            SampleData.Products(),
            Product.Create("P-TEMP", "SKU-999", "Тестова скоба", "шт", 3),
            threshold: 10);
        Console.WriteLine($"Дефіцитних до зміни джерела: {deferred.BeforeAddition}");
        Console.WriteLine($"Дефіцитних після додавання: {deferred.AfterAddition}");
        Console.WriteLine($"Елементів у матеріалізованому знімку: {deferred.SnapshotCount}");
        return;
    }

    if (command == "report-benchmark")
    {
        var benchmarkProducts = Enumerable.Range(1, 100_000)
            .Select(index => Product.Create(
                $"B-{index:D6}",
                $"BENCH-{index:D6}",
                "Тестовий товар",
                "шт",
                index % 200))
            .ToList();
        var benchmark = ReportService.CompareMaterialization(benchmarkProducts, threshold: 100);
        Console.WriteLine($"Записів: {benchmarkProducts.Count:N0}");
        Console.WriteLine($"Результат: {benchmark.ResultCount:N0} позицій, залишок {benchmark.TotalQuantity:N0}");
        Console.WriteLine($"Без ToList посередині: {benchmark.WithoutMaterializationMilliseconds:F2} мс");
        Console.WriteLine($"З ToList посередині:   {benchmark.WithMaterializationMilliseconds:F2} мс");
        return;
    }

    IReadOnlyList<Product> items;
    if (command == "report-empty")
    {
        items = [];
    }
    else
    {
        // Шлях через конфігурацію (лаб 9) — але для lab7 залишаємо catalog.json для сумісності
        var lab7DataPath = Path.Combine(AppContext.BaseDirectory, "data", "catalog.json");
        ICatalogStore store = StoreFactory.Create(args, lab7DataPath, SampleData.Products());
        items = store.List();
    }

    switch (command)
    {
        case "report-top":
            PrintStockLines(ReportService.TopByQuantity(items, 5));
            break;
        case "report-low":
            PrintStockLines(ReportService.LowStock(items, 10));
            break;
        case "report-unit":
            PrintStockLines(ReportService.TopByUnit(items, "шт", 3));
            break;
        case "report-groups":
            PrintGroupSummaries(ReportService.ByUnit(items));
            break;
        case "report-groups-query":
            PrintGroupSummaries(ReportService.ByUnitUsingQuerySyntax(items));
            break;
        case "report-sum":
            PrintTotals(ReportService.Totals(items));
            break;
        case "report-join":
            var movements = new[]
            {
                new StockMovement("P-001", -8),
                new StockMovement("P-009", 12),
                new StockMovement("P-017", -25),
                new StockMovement("MISSING", 4)
            };
            foreach (var movement in ReportService.JoinMovements(items, movements))
                Console.WriteLine($"  {movement.Sku,-10} {movement.Name,-28} {movement.Delta,6} {movement.Unit}");
            break;
        case "report-empty":
            Console.WriteLine($"Дефіцитних позицій: {ReportService.LowStock(items, 10).Count}");
            PrintTotals(ReportService.Totals(items));
            break;
        default:
            Console.WriteLine(
                "report-top | report-low | report-unit | report-groups | report-groups-query | " +
                "report-sum | report-join | report-deferred | report-empty | report-benchmark");
            break;
    }
}

static void PrintStockLines(IReadOnlyList<StockLine> lines)
{
    Console.WriteLine($"  {"SKU",-10} {"Назва",-28} {"Залишок",8} Од.");
    foreach (var line in lines)
        Console.WriteLine($"  {line.Sku,-10} {line.Name,-28} {line.Quantity,8} {line.Unit}");
}

static void PrintGroupSummaries(IReadOnlyList<GroupSummary> groups)
{
    Console.WriteLine($"  {"Од.",-6} {"Позицій",8} {"Залишок",10}");
    foreach (var group in groups)
        Console.WriteLine($"  {group.Key,-6} {group.Count,8} {group.TotalQuantity,10}");
}

static void PrintTotals((int Items, int Total, double Average, int Max) totals)
{
    string average = totals.Average.ToString("F2", CultureInfo.GetCultureInfo("uk-UA"));
    Console.WriteLine(
        $"  позицій {totals.Items}, всього {totals.Total}, середнє {average}, макс {totals.Max}");
}

// ════════════════════════════════════════════════════════════════════════════════
// Лабораторна 6 (збережено для сумісності)
// ════════════════════════════════════════════════════════════════════════════════
static void RunLab6Demo(string[] args, string dataFilePath)
{
    Console.WriteLine("=== Демонстрація Лабораторної 6 ===");
    // Шлях отримуємо через конфігурацію (лаб 9), а не Path.Combine з рядком
    ICatalogStore store = StoreFactory.Create(args, dataFilePath, SampleData.Products());
    var service = new CatalogService(store);

    int changes = 0;
    service.Changed += (_, e) =>
    {
        changes++;
        Console.WriteLine($"  [Подія] {e.Kind,-8} {e.Id} {e.Delta,4:+0;-0} -> залишок {e.QuantityAfter}");
    };

    var created = service.Add("SKU-101", "Кабель UTP cat6", "м", 50);
    service.Receive(created.Id, 25);
    Console.WriteLine($"Подій отримано: {changes}");
}

// ════════════════════════════════════════════════════════════════════════════════
// Лабораторна 5 (збережено для сумісності)
// ════════════════════════════════════════════════════════════════════════════════
static void RunLab5Demo(string[] args, string dataFilePath)
{
    ICatalogStore store = StoreFactory.Create(args, dataFilePath, SampleData.Products());
    var service = new CatalogService(store);

    Console.WriteLine("=== Демонстрація Лабораторної 5 ===");
    Console.WriteLine($"Сховище: {store.GetType().Name}");
    var created = service.Add("SKU-101", "Кабель UTP cat6", "м", 50);
    service.Receive(created.Id, 25);
    service.Issue(created.Id, 15);
    Console.WriteLine($"Товар після операцій: {service.Find(created.Id)}");
}

// ════════════════════════════════════════════════════════════════════════════════
// Лабораторна 4 (збережено для сумісності)
// ════════════════════════════════════════════════════════════════════════════════
static void RunLab4Demo()
{
    Console.WriteLine("=== Демонстрація Лабораторної 4 ===");
    Product cement = Product.Create("P-001", "sku-001", "Цемент М400 25кг", "шт", 100);
    Console.WriteLine($"Створено: {cement}");
    cement.RegisterArrival(50);
    cement.Issue(30);
    Console.WriteLine($"Після операцій: {cement}");
}
