using Core;
using Core.Abstractions;
using Core.Domain;
using Core.Dto;
using Core.Import;
using Core.Services;
using Core.Storage;

// ════════════════════════════════════════════════════════════════════════════════
// Лабораторна 5: Composition Root (ручний DI без сторонніх контейнерів)
// ════════════════════════════════════════════════════════════════════════════════

if (args.Contains("--lab4", StringComparer.OrdinalIgnoreCase))
{
    RunLab4Demo();
    return 0;
}

string dataPath = Path.Combine("data", "catalog.json");

// Ручне впровадження залежностей (Manual DI) за допомогою фабрики сховищ:
ICatalogStore store = StoreFactory.Create(args, dataPath, SampleData.Products());
var service = new CatalogService(store);

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine($"║  Сховище: {store.GetType().Name,-50} ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");

// ── 1. Додати новий товар ───────────────────────────────────────────────────
Console.WriteLine("\n[1] Додавання нового товару:");
Product created = service.Add("SKU-101", "Кабель UTP cat6", "м", 50);
Console.WriteLine($"  Створено: {created.Id} [{created.Sku}] {created.Name} — {created.Quantity} {created.Unit}");

// ── 2. Зміна стану (прихід та видача через сервіс) ──────────────────────────
Console.WriteLine("\n[2] Зміна стану (Receive +25 та Issue -15):");
service.Receive(created.Id, 25);
service.Issue(created.Id, 15);
Product? updated = service.Find(created.Id);
Console.WriteLine($"  Після операцій: {updated?.Id} [{updated?.Sku}] {updated?.Name} — {updated?.Quantity} {updated?.Unit}");

// ── 3. Пошук за id ──────────────────────────────────────────────────────────
Console.WriteLine("\n[3] Пошук за Id:");
Product? found = service.Find(created.Id);
Console.WriteLine(found != null 
    ? $"  Знайдено: {found.Id} -> {found.Name} ({found.Quantity} {found.Unit})"
    : "  Товар не знайдено!");

// ── 4. Додаткове завдання 2: пошук за предикатом ────────────────────────────
Console.WriteLine("\n[4] Пошук товарів із залишком < 30 (демонстрація Func<Product, bool>):");
var lowStock = service.Search(p => p.Quantity < 30);
foreach (var p in lowStock.Take(5))
    Console.WriteLine($"  • {p.Id} {p.Sku,-10} {p.Name,-28} {p.Quantity,5} {p.Unit}");

// ── 5. Показати каталог ─────────────────────────────────────────────────────
Console.WriteLine($"\n[5] Повний каталог (всього: {service.All().Count} позицій, перші 7):");
foreach (var p in service.All().Take(7))
    Console.WriteLine($"  {p.Id} {p.Sku,-10} {p.Name,-28} {p.Quantity,5} {p.Unit}");

// ── 6. Сценарії відмови (перевірка обробки помилок) ─────────────────────────
Console.WriteLine("\n[6] Сценарії відмови (людські повідомлення):");

TryDo("операція Receive для неіснуючого id", () =>
    service.Receive("NON-EXISTENT-ID", 10));

TryDo("спроба додати прямий дублікат id у сховище", () =>
    store.Add(created));

TryDo("спроба видати більше товару, ніж є в залишку", () =>
    service.Issue(created.Id, 99999));

return 0;

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
