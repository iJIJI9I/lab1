# CrossApp
Наскрізний проєкт з крос-платформного програмування.

## Структура

```text
CrossApp.slnx
README.md
src/
    Core/
        Core.csproj
        EnvironmentInfo.cs
        EnvironmentReport.cs
        SampleData.cs           ← зразки 16 товарів (лаб 5)
        Abstractions/           ← НОВЕ (лаб 5): контракти сховищ
            ICatalogStore.cs    ← інтерфейс сховища каталогу
        Storage/                ← НОВЕ (лаб 5): реалізації сховищ
            InMemoryCatalogStore.cs ← реалізація в оперативній пам'яті
            FileCatalogStore.cs     ← файлова реалізація (JSON)
            CachingCatalogStore.cs  ← доп. завд. 1: патерн Декоратор
            StoreFactory.cs         ← доп. завд. 3: фабрика вибору сховища
        Services/               ← НОВЕ (лаб 5): бізнес-сервіси
            CatalogService.cs   ← сервіс з ін'єкцією через конструктор
        Dto/
            ProductDto.cs       ← record, формат даних (лаб 3, 5)
            ImportResult.cs     ← record, результат імпорту (лаб 3)
            WarehouseDto.cs     ← record, формат складу (лаб 3)
            OrderDto.cs         ← record, формат замовлення (лаб 4)
        Domain/                 ← сутності з бізнес-інваріантами (лаб 4)
            Product.cs          ← інкапсульований товар на складі
            Order.cs            ← агрегат замовлення + захищена колекція
            OrderLine.cs        ← рядок замовлення (internal-конструктор)
            OrderStatus.cs      ← enum Draft / Confirmed / Cancelled
        Import/
            ProductCsvImporter.cs
            ProductJsonImporter.cs
            MixedCsvImporter.cs
            ProductDomainMapper.cs
    Cli/
        Cli.csproj
        Program.cs              ← Composition Root (ручний DI)
data/
    sample.csv
    sample.json
    mixed.csv
    catalog.json                ← з'являється при запуску з --file
```

---

## Лабораторна 5 — Сервісний шар: інтерфейси, два сховища, ручний DI

### Архітектура та компоненти

* **Інтерфейс сховища:** `ICatalogStore` (`src/Core/Abstractions/ICatalogStore.cs`)
  * `IReadOnlyList<Product> List()`
  * `Product? GetById(string id)`
  * `void Add(Product item)`
  * `void Update(Product item)`
  * `bool Remove(string id)`
  * `IReadOnlyList<Product> Find(Func<Product, bool> predicate)` — *додаткове завдання 2*
* **Реалізації сховища:**
  * `InMemoryCatalogStore` (`src/Core/Storage/InMemoryCatalogStore.cs`) — зберігання в пам'яті на основі `Dictionary<string, Product>`.
  * `FileCatalogStore` (`src/Core/Storage/FileCatalogStore.cs`) — файлове JSON-сховище (`data/catalog.json`) з кешуванням у пам'яті та серіалізацією через `ProductDto` (метод `Flush()`).
  * `CachingCatalogStore` (`src/Core/Storage/CachingCatalogStore.cs`) — *додаткове завдання 1*: декоратор для прозорого кешування будь-якого `ICatalogStore`.
* **Сервісний шар:** `CatalogService` (`src/Core/Services/CatalogService.cs`)
  * Залежить **виключно** від абстракції `ICatalogStore` (Dependency Inversion Principle).
  * Ін'єкція залежності виконується через первинний конструктор `CatalogService(ICatalogStore store)`.
  * Реалізує бізнес-операції: `Add`, `Receive`, `Issue`, `All`, `Find`, `Search`.
* **Фабрика вибору сховища:** `StoreFactory` (`src/Core/Storage/StoreFactory.cs`) — *додаткове завдання 3*: конструювання сховища за аргументами CLI.

### Схема залежностей
```text
Cli (Program.cs) ──> CatalogService ──> ICatalogStore (Interface)
                         │                     ▲
                    (Manual DI)         ┌──────┴──────┬──────────────┐
                                        │             │              │
                              InMemoryCatalogStore FileCatalogStore CachingCatalogStore
```

### Definition of Done (Самоперевірка)
- [x] **1. Інтерфейс сховища в Core:** `ICatalogStore` у просторі імен `Core.Abstractions`.
- [x] **2. Дві робочі реалізації:** `InMemoryCatalogStore` та `FileCatalogStore`.
- [x] **3. Сервіс залежить лише від інтерфейсу:** `CatalogService(ICatalogStore store)` без згадок файлів чи конкретних класів.
- [x] **4. Перемикання аргументом CLI:** аргумент `--file` перемикає на `FileCatalogStore`, за замовчуванням — `InMemoryCatalogStore`.
- [x] **5. Повний сценарій:** додати, змінити (Receive/Issue), показати список, знайти за Id.
- [x] **6. Зрозумілі повідомлення про помилки:** неіснуючий Id, спроба додати дублікат Id, перевитрата товару.
- [x] **7. Без сторонніх контейнерів:** `Microsoft.Extensions.DependencyInjection` не підключено (Pure DI).
- [x] **8. Додаткові завдання:**
  - Декоратор `CachingCatalogStore` (`--cached`)
  - Пошук за предикатом `Func<Product, bool>`
  - Фабрика `StoreFactory.Create(args, ...)`

---

## Команди запуску Лабораторної 5

```powershell
# 1. Режим у пам'яті (за замовчуванням, дані не зберігаються після виходу):
dotnet run --project src/Cli --framework net10.0

# 2. Файловий режим (дані зберігаються у data/catalog.json і накопичуються):
dotnet run --project src/Cli --framework net10.0 -- --file

# 3. Режим з декоратором кешування (додаткове завдання 1):
dotnet run --project src/Cli --framework net10.0 -- --file --cached

# 4. Запуск демонстрації попередньої лабораторної 4:
dotnet run --project src/Cli --framework net10.0 -- --lab4
```

---

## Попередні лабораторні роботи

### Лабораторна 4 — Доменна модель: сутності, інваріанти, інкапсуляція
* `Product` — товар на складі із захищеним полем кількості, методами `RegisterArrival`, `Issue`, фабричним методом `Create` та мапінгом `ToDto` / `FromDto`.
* `Order` + `OrderLine` — агрегат замовлення із захищеною колекцією рядків та перевіркою переходів станів `OrderStatus` через `switch expression`.

### Лабораторна 3 — Імпорт CSV / JSON
* Record-типи DTO: `ProductDto`, `WarehouseDto`, `ImportResult<T>`.
* Імпортери: `ProductCsvImporter`, `ProductJsonImporter`, `MixedCsvImporter`.

### Лабораторна 2 — Бібліотека Core та консольний клієнт Cli
* Відокремлення спільного коду бібліотеки `Core` від точки входу `Cli`.
* Мультитаргетинг: `net8.0` та `net10.0`.