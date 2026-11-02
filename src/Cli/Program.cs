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
// ЛАБОРАТОРНА РОБОТА 6: Доменні події, делегати Action/Func, лямбди
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

RunLab6Demo(args);
return 0;

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

static void RunLab6Demo(string[] args)
{
    Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║   ЛАБОРАТОРНА РОБОТА 6: Доменні події, Action/Func, лямбди   ║");
    Console.WriteLine("╚══════════════════════════════════════════════════════════════╝\n");

    // Сховище (ручний DI з лабораторної 5)
    string dataPath = Path.Combine(AppContext.BaseDirectory, "data", "catalog.json");
    ICatalogStore store = StoreFactory.Create(args, dataPath, SampleData.Products());
    var service = new CatalogService(store);

    // ── Крок 3. Підписка локальним методом (виведення в консоль і у файл) ───────
    var logPath = Path.Combine("logs", "app.log");
    Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

    // Очищаємо попередній лог перед новим сеансом для чистоти демонстрації
    File.WriteAllText(logPath, $"=== Сеанс {DateTime.Now:yyyy-MM-dd HH:mm:ss} ==={Environment.NewLine}");

    void OnCatalogChanged(object? sender, CatalogChangedEventArgs e)
    {
        var line = $"{e.At:HH:mm:ss} {e.Kind,-8} {e.Id} {e.Delta,4:+0;-0} → залишок {e.QuantityAfter}";
        Console.WriteLine($"  [Log] {line}");
        File.AppendAllText(logPath, line + Environment.NewLine);
    }

    service.Changed += OnCatalogChanged;

    // ── Крок 4. Другий підписник — лямбда із замиканням над лічильником ──────────
    int changes = 0;
    service.Changed += (_, _) => changes++;

    Console.WriteLine(">>> 1. Додавання та зміна товару з двома підписниками:");
    var created = service.Add("SKU-101", "Кабель UTP cat6", "м", 50);
    service.Receive(created.Id, 25);
    Console.WriteLine($"Подій отримано: {changes}\n");

    // ── Крок 5. Відписка -= і спостережуваний ефект ──────────────────────────────
    Console.WriteLine(">>> 2. Відписка локального методу (-= OnCatalogChanged):");
    service.Changed -= OnCatalogChanged;

    // «Тиха» операція: запис у консоль і файл відсутній, але лічильник збільшується
    service.Receive(created.Id, 5);
    Console.WriteLine($"Подій отримано: {changes}  (збільшився лише лічильник лямбди)\n");

    // ── Крок 6. Сценарій відмови (порушення інваріанту: подія НЕ виникає) ───────
    Console.WriteLine(">>> 3. Сценарії відмови (перевірка, що подія НЕ піднімається):");

    int changesBeforeError = changes;
    TryDo("видача товару понад залишок (інваріант)", () =>
        service.Issue(created.Id, 9999));

    TryDo("прихід з від'ємною кількістю (некоректний аргумент)", () =>
        service.Receive(created.Id, -20));

    TryDo("операція з неіснуючим id", () =>
        service.Receive("UNKNOWN-ID", 10));

    Console.WriteLine($"Лічильник подій після спроб помилок: {changes} (було {changesBeforeError} — подій не згенеровано)\n");

    // ── Крок 7. Делегат замість події там, де потрібне значення (Func) ───────────
    Console.WriteLine(">>> 4. Використання делегата Func<Product, bool> у FindAll:");
    var lowStock = service.FindAll(p => p.Quantity < 30);
    Console.WriteLine($"Знайдено товарів із залишком < 30 (всього: {lowStock.Count}):");
    foreach (var p in lowStock.Take(5))
        Console.WriteLine($"  • {p.Id} [{p.Sku}] {p.Name,-28} — {p.Quantity} {p.Unit}");
    Console.WriteLine();

    // ── Додаткове завдання 1. INotificationSink (ConsoleSink + FileSink) ─────────
    Console.WriteLine(">>> 5. Додаткове завдання 1: Альтернатива подіям через INotificationSink:");
    var sinkLogPath = Path.Combine(AppContext.BaseDirectory, "logs", "sink.log");
    var sinks = new INotificationSink[]
    {
        new ConsoleSink(),
        new FileSink(sinkLogPath)
    };
    var serviceWithSinks = new CatalogService(store, sinks);
    var sinkProduct = serviceWithSinks.Add("SKU-202", "Труба ПВХ 20мм", "м", 100);
    serviceWithSinks.Receive(sinkProduct.Id, 30);
    Console.WriteLine();

    // ── Додаткове завдання 2. Action<string> як callback під час імпорту ─────────
    Console.WriteLine(">>> 6. Додаткове завдання 2: Action<string> callback пропущені рядки в CSV:");
    string csvPath = Path.Combine("data", "sample.csv");
    if (File.Exists(csvPath))
    {
        int skippedCount = 0;
        ProductCsvImporter.Load(csvPath, onSkipped: reason =>
        {
            skippedCount++;
            Console.WriteLine($"  [Action Callback] Пропущено: {reason}");
        });
        Console.WriteLine($"Всього пропущено рядків через callback Action<string>: {skippedCount}\n");
    }

    Console.WriteLine($"Лог-файл успішно оновлено: {Path.GetFullPath(logPath)}");
}

static void TryDo(string title, Action action)
{
    try
    {
        action();
        Console.WriteLine($"  ⚠  {title}: помилка НЕ спрацювала!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  ✓  {title}:");
        Console.WriteLine($"       {ex.GetType().Name} — {ex.Message}");
    }
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
