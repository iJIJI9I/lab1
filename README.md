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
        SampleData.cs                   ← зразки 16 товарів (лаб 5)
        Abstractions/                   ← контракти сховищ (лаб 5)
            ICatalogStore.cs            ← інтерфейс сховища каталогу
        Storage/                        ← реалізації сховищ (лаб 5)
            InMemoryCatalogStore.cs     ← реалізація в оперативній пам'яті
            FileCatalogStore.cs         ← файлова реалізація (JSON)
            CachingCatalogStore.cs      ← доп. завд. 1 (лаб 5): патерн Декоратор
            StoreFactory.cs             ← доп. завд. 3 (лаб 5): фабрика сховищ
        Services/                       ← бізнес-сервіси (лаб 5, 6)
            CatalogService.cs           ← подія Changed, OnChanged, FindAll(Func)
        Events/                         ← НОВЕ (лаб 6): доменні події
            CatalogChangeKind.cs        ← enum: Added, Received, Issued, Removed
            CatalogChangedEventArgs.cs  ← EventArgs: Id, Kind, Delta, QuantityAfter, At
        Notifications/                  ← НОВЕ (лаб 6, доп. 1): контракти сповіщень
            INotificationSink.cs        ← інтерфейс приймача сповіщень
        Dto/
            ProductDto.cs               ← record, формат даних (лаб 3, 5)
            ImportResult.cs             ← record, результат імпорту (лаб 3)
            WarehouseDto.cs             ← record, формат складу (лаб 3)
            OrderDto.cs                 ← record, формат замовлення (лаб 4)
        Domain/                         ← сутності з бізнес-інваріантами (лаб 4)
            Product.cs                  ← інкапсульований товар на складі
            Order.cs                    ← агрегат замовлення + захищена колекція
            OrderLine.cs                ← рядок замовлення (internal-конструктор)
            OrderStatus.cs              ← enum Draft / Confirmed / Cancelled
        Import/
            ProductCsvImporter.cs       ← оновлено (лаб 6, доп. 2): Action<string> callback
            ProductJsonImporter.cs
            MixedCsvImporter.cs
            ProductDomainMapper.cs
    Cli/
        Cli.csproj
        Program.cs                      ← Composition Root, підписка/відписка на події
        Notifications/                  ← НОВЕ (лаб 6, доп. 1): реалізації Sink
            ConsoleSink.cs              ← виведення сповіщень у консоль
            FileSink.cs                 ← запис сповіщень у logs/sink.log
logs/
    app.log                             ← створюється автоматично під час запуску
data/
    sample.csv
    sample.json
    mixed.csv
    catalog.json                        ← з'являється при запуску з --file
```

---

## Лабораторна 6 — Доменні події, делегати Action/Func, лямбди

### Мета та архітектура
Реалізовано механізм сповіщення зовнішнього коду про зміни стану без залежності доменного шару від способу їх оброблення:
* **Подія в домені:** `CatalogService` оголошує `public event EventHandler<CatalogChangedEventArgs>? Changed;` і викликає її через приватний метод `OnChanged`.
* **Підняття лише після успішної зміни:** `Changed?.Invoke(...)` викликається суворо **після** успішної валідації інваріантів та оновлення сховища. Якщо операція кидає виняток, подія не генерується.
* **Підписники в CLI:**
  1. *Підписник 1 (локальний метод `OnCatalogChanged`):* форматує подію та записує рядок у консоль і у файл `logs/app.log`.
  2. *Підписник 2 (лямбда із замиканням `(_, _) => changes++`):* рахує загальну кількість подій за допомогою захопленої локальної змінної `changes`.
* **Відписка (`-=`):** продемонстровано відписку локального методу `service.Changed -= OnCatalogChanged;`. Наступні виклики стають «тихими» (не пишуть у консоль і файл, але лічильник другого підписника продовжує зростати).
* **Делегат замість події (`Func<Product, bool>`):** метод `FindAll(predicate)` у `CatalogService` приймає лямбда-вираз для фільтрації товарів.

### Додаткові завдання
1. **Інтерфейс `INotificationSink`:** контракт сповіщень у `Core.Notifications` та реалізації `ConsoleSink`, `FileSink` у `Cli.Notifications`, що передаються через конструктор сервісу (патерн Sink як альтернатива подіям).
2. **`Action<string>` у методі імпорту:** у `ProductCsvImporter.Load(path, onSkipped)` додано делегат `Action<string>?`, який сповіщає про кожен пропущений або некоректний рядок CSV.

### Definition of Done (Самоперевірка)
- [x] **1. Тип даних події в Core:** `CatalogChangedEventArgs : EventArgs` (`Id`, `Kind`, `Delta`, `QuantityAfter`, `At`).
- [x] **2. Подія `event EventHandler<...>` у сервісі:** підняття `OnChanged` суворо після успішної зміни стану.
- [x] **3. Core не пише в консоль і файл:** у просторі імен `Core` відсутні звернення до `Console` та шляхів файлових логів.
- [x] **4. Два підписники в CLI:** локальний метод та лямбда із замиканням над `changes`.
- [x] **5. Демонстрація `-=`:** показано відписку та припинення виводу в консоль/файл за збереження роботи другого підписника.
- [x] **6. Використано `Func` та `Action` у сигнатурах:** `FindAll(Func<Product, bool>)` та `Load(..., Action<string>?)`.
- [x] **7. Файл `logs/app.log`:** формується через `Path.Combine`, каталог створюється кодом (`Directory.CreateDirectory`).
- [x] **8. README оновлено, коміт `lab06`:** документація заповнена, проект протестовано.

---

## Команди запуску

```powershell
# 1. Запуск Лабораторної 6 (демонстрація подій, підписки/відписки, логів, Func та Action):
dotnet run --project src/Cli

# 2. Запуск у файловому режимі сховища:
dotnet run --project src/Cli -- --file

# 3. Перевірка лог-файлу подій:
Get-Content logs/app.log

# 4. Запуск попередніх лабораторних:
dotnet run --project src/Cli -- --lab5
dotnet run --project src/Cli -- --lab4
```

---

## Попередні лабораторні роботи

### Лабораторна 5 — Сервісний шар: інтерфейси, два сховища, ручний DI
* `ICatalogStore` — інтерфейс абстракції сховища.
* `InMemoryCatalogStore`, `FileCatalogStore`, `CachingCatalogStore` (декоратор), `StoreFactory`.
* `CatalogService` — бізнес-сервіс із залежністю через первинний конструктор (Pure DI).

### Лабораторна 4 — Доменна модель: сутності, інваріанти, інкапсуляція
* `Product` — сутність товару з методами `RegisterArrival`, `Issue`, фабрикою `Create` та мапінгом DTO.
* `Order` + `OrderLine` — агрегат замовлення з колекцією рядків та станами `OrderStatus`.

### Лабораторні 2–3 — Core/Cli архітектура та імпорт CSV/JSON
* Бібліотека `Core` та консольна точка входу `Cli`.
* Record-типи DTO та парсери `ProductCsvImporter`, `ProductJsonImporter`, `MixedCsvImporter`.
