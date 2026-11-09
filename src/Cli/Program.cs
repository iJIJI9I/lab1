using System.Globalization;
using Cli.Notifications;
using Core;
using Core.Abstractions;
using Core.Domain;
using Core.Dto;
using Core.Events;
using Core.Import;
using Core.Notifications;
using Core.Reports;
using Core.Services;
using Core.Storage;

// ════════════════════════════════════════════════════════════════════════════════
// ЛАБОРАТОРНА РОБОТА 8: async/await, скасування, доменні винятки, unit-тести
// ════════════════════════════════════════════════════════════════════════════════

if (args.Contains("--lab4", StringComparer.OrdinalIgnoreCase))
{
    RunLab4Demo();
    return 0;
}

if (args.Contains("--lab5", StringComparer.OrdinalIgnoreCase))
{
    RunLab5Demo(args);
    return 0;
}

if (args.Contains("--lab6", StringComparer.OrdinalIgnoreCase))
{
    RunLab6Demo(args);
    return 0;
}

string? reportCommand = args.FirstOrDefault(argument =>
    !argument.StartsWith("--", StringComparison.Ordinal));
if (args.Contains("--lab7", StringComparer.OrdinalIgnoreCase) ||
    reportCommand is "report-top" or "report-low" or "report-unit" or "report-groups" or
        "report-groups-query" or "report-sum" or "report-join" or "report-deferred" or
        "report-empty" or "report-benchmark")
{
    RunLab7Report(args, reportCommand ?? "report-top");
    return 0;
}

await RunLab8DemoAsync(args);
return 0;

static async Task RunLab8DemoAsync(string[] args)
{
    Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║   ЛАБОРАТОРНА РОБОТА 8: async/await, скасування, тести       ║");
    Console.WriteLine("╚══════════════════════════════════════════════════════════════╝\n");

    bool useFile = args.Contains("--file", StringComparer.OrdinalIgnoreCase);
    string dataPath = Path.Combine("data", "catalog.json");

    ICatalogStore store = useFile
        ? new FileCatalogStore(dataPath)
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

    // Демонстрація таймауту або миттєвого скасування за прапорцем
    if (args.Contains("--cancel", StringComparer.OrdinalIgnoreCase))
    {
        cts.Cancel();
    }
    else if (args.Contains("--timeout", StringComparer.OrdinalIgnoreCase))
    {
        cts.CancelAfter(TimeSpan.FromMilliseconds(5));
    }

    // ── Крок 5: Основний асинхронний імпорт із обробкою винятків ───────────────
    Console.WriteLine($">>> 1. Асинхронний імпорт файлу: {dataPath}");
    var progress = new Progress<int>(percent =>
    {
        Console.Write($"\r[Прогрес імпорту: {percent,3}%] ");
    });

    try
    {
        var added = await service.ImportAsync(importer, dataPath, progress, cts.Token);
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
        Console.WriteLine($"\nФайл не знайдено: {dataPath}");
    }

    // Якщо це цільовий запуск з прапорцем (наприклад лише для --file або --cancel), виходимо
    if (useFile || args.Contains("--cancel", StringComparer.OrdinalIgnoreCase) || args.Contains("--timeout", StringComparer.OrdinalIgnoreCase))
    {
        return;
    }

    // ── Демонстрація сценаріїв для захисту ─────────────────────────────────────
    Console.WriteLine("\n>>> 2. Демонстрація кооперативного скасування (CancelAfter / таймаут):");
    using (var timeoutCts = new CancellationTokenSource())
    {
        timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(1));
        await Task.Delay(10); // гарантуємо спрацювання таймауту

        try
        {
            await service.ImportAsync(importer, dataPath, timeoutCts.Token);
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
        if (File.Exists(corruptedTempPath))
            File.Delete(corruptedTempPath);
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
    var streamAdded = await streamService.ImportStreamAsync(importer, dataPath);
    Console.WriteLine($"Потоково завантажено у сховище: {streamAdded} записів.");

    Console.WriteLine("\n>>> 6. Додаткове завдання 2: Зв'язані токени (User + Timeout):");
    using (var userCts = new CancellationTokenSource())
    using (var timerCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(10)))
    using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(userCts.Token, timerCts.Token))
    {
        await Task.Delay(20);
        Console.WriteLine($"Користувач скасував: {userCts.IsCancellationRequested}");
        Console.WriteLine($"Таймаут скасував:    {timerCts.IsCancellationRequested}");
        Console.WriteLine($"Зв'язаний токен:     {linkedCts.IsCancellationRequested} (спрацював саме таймаут)");
    }
}

// ════════════════════════════════════════════════════════════════════════════════
// Лабораторна 7 (збережено для сумісності з прапорцем --lab7)
// ════════════════════════════════════════════════════════════════════════════════
static void RunLab7Report(string[] args, string command)
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
        string dataPath = Path.Combine(AppContext.BaseDirectory, "data", "catalog.json");
        ICatalogStore store = StoreFactory.Create(args, dataPath, SampleData.Products());
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
// Лабораторна 6 (збережено для сумісності з прапорцем --lab6)
// ════════════════════════════════════════════════════════════════════════════════
static void RunLab6Demo(string[] args)
{
    Console.WriteLine("=== Демонстрація Лабораторної 6 ===");
    string dataPath = Path.Combine(AppContext.BaseDirectory, "data", "catalog.json");
    ICatalogStore store = StoreFactory.Create(args, dataPath, SampleData.Products());
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
// Лабораторна 5 (збережено для сумісності з прапорцем --lab5)
// ════════════════════════════════════════════════════════════════════════════════
static void RunLab5Demo(string[] args)
{
    string dataPath = Path.Combine("data", "catalog.json");
    ICatalogStore store = StoreFactory.Create(args, dataPath, SampleData.Products());
    var service = new CatalogService(store);

    Console.WriteLine("=== Демонстрація Лабораторної 5 ===");
    Console.WriteLine($"Сховище: {store.GetType().Name}");
    var created = service.Add("SKU-101", "Кабель UTP cat6", "м", 50);
    service.Receive(created.Id, 25);
    service.Issue(created.Id, 15);
    Console.WriteLine($"Товар після операцій: {service.Find(created.Id)}");
}

// ════════════════════════════════════════════════════════════════════════════════
// Лабораторна 4 (збережено для сумісності з прапорцем --lab4)
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
