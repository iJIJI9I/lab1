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
        Dto/
            ProductDto.cs       ← record, формат даних (лаб 3)
            ImportResult.cs     ← record, результат імпорту (лаб 3)
            WarehouseDto.cs     ← record, формат складу (лаб 3)
            OrderDto.cs         ← record, формат замовлення (лаб 4)
        Domain/                 ← НОВЕ (лаб 4): сутності з поведінкою
            Product.cs          ← інкапсульований товар на складі
            Order.cs            ← агрегат замовлення + захищена колекція
            OrderLine.cs        ← рядок замовлення (internal-конструктор)
            OrderStatus.cs      ← enum Draft / Confirmed / Cancelled
        Import/
            ProductCsvImporter.cs
            ProductJsonImporter.cs
            MixedCsvImporter.cs
            ProductDomainMapper.cs  ← НОВЕ (лаб 4, доп.): ImportResult → Domain
    Cli/
        Cli.csproj
        Program.cs
data/
    sample.csv
    sample.json
    mixed.csv
```

## Лабораторна 4 — Доменна модель: сутності, інваріанти, інкапсуляція

### Обрані сутності

| Сутність | Тип | Роль |
|:---|:---|:---|
| `Product` | `sealed class` | Товар на складі з залишком; операції: прихід, видача |
| `Order` | `sealed class` | Агрегат замовлення з колекцією рядків |
| `OrderLine` | `sealed class` | Рядок замовлення (internal-конструктор, керується агрегатом) |
| `OrderStatus` | `enum` | Стани Draft → Confirmed / Cancelled |

**Зв'язок:** `Order` містить захищений `List<OrderLine>` (назовні — `IReadOnlyList<OrderLine>`), переходи станів реалізовано через `switch expression`.

---

### Інваріанти

#### Product

| # | Правило | Тип винятку | Метод |
|---|:---|:---|:---|
| 1 | `Id` не може бути порожнім | `ArgumentException` | `Create` |
| 2 | `SKU` не може бути порожнім | `ArgumentException` | `Create` |
| 3 | `Name` не може бути порожньою | `ArgumentException` | `Create` |
| 4 | Початковий залишок ≥ 0 | `ArgumentOutOfRangeException` | `Create` |
| 5 | Кількість приходу > 0 | `ArgumentOutOfRangeException` | `RegisterArrival` |
| 6 | Кількість видачі > 0 | `ArgumentOutOfRangeException` | `Issue` |
| 7 | Не видавати більше, ніж є в залишку | `InvalidOperationException` | `Issue` |

#### Order / OrderLine

| # | Правило | Тип винятку | Метод |
|---|:---|:---|:---|
| 1 | `Id` замовлення не порожній | `ArgumentException` | `Create` |
| 2 | `CustomerId` не порожній | `ArgumentException` | `Create` |
| 3 | Кількість у рядку > 0 | `ArgumentOutOfRangeException` | `AddLine` |
| 4 | Ціна у рядку ≥ 0 | `ArgumentOutOfRangeException` | `AddLine` |
| 5 | Рядки не додаються до не-Draft замовлення | `InvalidOperationException` | `AddLine` |
| 6 | Не підтверджувати порожнє замовлення | `InvalidOperationException` | `Confirm` |
| 7 | Перехід стану лише з Draft (switch expression) | `InvalidOperationException` | `Confirm`, `Cancel` |

### Самоперевірка (Definition of Done)

- [x] Сутності в `Core/Domain`; records лаб 3 лишились як DTO
- [x] Стан інкапсульовано: `private int _quantity`, `{ get; }`, `private set`
- [x] Фабричний метод `Create`; публічного конструктора немає
- [x] ≥ 3 інваріанти з осмисленими повідомленнями (фактично 7 + 7)
- [x] Правильний вибір типу: `Argument*` для входу, `InvalidOperation*` для стану
- [x] `ToDto()` / `FromDto()` в обох сутностях
- [x] CLI демонструє успіх та відмову; stack trace не виводиться
- [x] Колекція `Lines` назовні — `IReadOnlyList<OrderLine>` через `AsReadOnly()`
- [x] `Domain` не залежить від `Console`, `File` або `Cli`
- [x] Доп. завдання 1: `ProductDomainMapper` — `ImportResult → Domain entities`
- [x] Доп. завдання 3: `OrderStatus` enum + `switch expression` для переходів

---

## Команди

Збірка й запуск:

```powershell
dotnet build src/Core/Core.csproj
dotnet run --project src/Cli --framework net10.0
```

Запуск з CSV-файлом (лаб 1-3):

```powershell
dotnet run --project src/Cli --framework net10.0 -- data/sample.csv
dotnet run --project src/Cli --framework net10.0 -- data/sample.json
dotnet run --project src/Cli --framework net10.0 -- data/mixed.csv
```

---

## Попередні лабораторні

### Лаб 2 — Core + Cli

`Core` — class library без точки входу. `EnvironmentInfo.Collect()` збирає дані платформи й повертає `EnvironmentReport`. `Cli` — єдина залежність `Cli → Core`.

### Лаб 3 — Імпорт CSV / JSON

`Core/Dto/` містить record-типи `ProductDto`, `ImportResult<T>`, `WarehouseDto`.
`Core/Import/` містить `ProductCsvImporter`, `ProductJsonImporter`, `MixedCsvImporter`.

### Multi-targeting

`Core` і `Cli` збираються для `net8.0` та `net10.0`.

```powershell
dotnet run --no-build --project src/Cli --framework net8.0
dotnet run --no-build --project src/Cli --framework net10.0
```

### Публікація

| RID | Режим | Розмір | Файлів | Runtime потрібен? |
|:---|:---|---:|---:|:---|
| win-x64 | self-contained | 77.06 MiB | 194 | Ні |
| win-x64 | framework-dependent | 0.22 MiB | 7 | Так, .NET 10 |
| win-x64 | single-file | 70.13 MiB | 3 | Ні |
| win-x64 | trimmed | 19.76 MiB | 35 | Ні |