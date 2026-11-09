# CrossApp
Наскрізний проєкт з крос-платформного програмування.

## Структура

```text
CrossApp.slnx
README.md
src/
    Core/
        Core.csproj
        DomainException.cs              ← НОВЕ (лаб 8): виняток порушення бізнес-правил
        EnvironmentInfo.cs
        EnvironmentReport.cs
        SampleData.cs                   ← зразки 17 товарів і 4 одиниці виміру
        Abstractions/
            ICatalogStore.cs            ← інтерфейс сховища каталогу
            ICatalogImporter.cs         ← НОВЕ (лаб 8): асинхронний та потоковий імпорт
        Storage/
            InMemoryCatalogStore.cs     ← реалізація в оперативній пам'яті
            FileCatalogStore.cs         ← файлова реалізація (JSON)
            CachingCatalogStore.cs      ← декоратор кешування
            StoreFactory.cs             ← фабрика сховищ
            JsonCatalogImporter.cs      ← НОВЕ (лаб 8): FileStream(useAsync) + IAsyncEnumerable
        Services/
            CatalogService.cs           ← бізнес-сервіс (події, імпорт, скасування, прогрес)
        Events/                         ← доменні події
            CatalogChangeKind.cs        ← enum дій з товаром
            CatalogChangedEventArgs.cs  ← аргументи події зміни
        Notifications/                  ← контракти сповіщень (Sink)
            INotificationSink.cs        
        Dto/
            ProductDto.cs               ← record, формат даних
            ImportResult.cs             ← record, результат імпорту
            WarehouseDto.cs             
            OrderDto.cs                 
        Domain/
            Product.cs                  ← сутність товару (інваріанти, DomainException)
            Order.cs                    
            OrderLine.cs                
            OrderStatus.cs              
        Import/
            ProductCsvImporter.cs       
            ProductJsonImporter.cs
            MixedCsvImporter.cs
            ProductDomainMapper.cs
        Reports/                        ← звіти LINQ to Objects
            StockLine.cs                
            GroupSummary.cs             
            ReportService.cs            
            EnumerableReportExtensions.cs
    Cli/
        Cli.csproj
        Program.cs                      ← Composition Root, async Main, CancelKeyPress
        Notifications/
            ConsoleSink.cs
            FileSink.cs
tests/
    Core.Tests/                         ← НОВЕ (лаб 8): xUnit тести (посилається ТІЛЬКИ на Core)
        Core.Tests.csproj
        TestData.cs                     ← фіксований тестовий набір даних
        CatalogServiceTests.cs          ← тести бізнес-правил, інваріантів та подій
        ReportServiceTests.cs           ← тести LINQ-звітів (детермінізм, LowStock)
        ImportTests.cs                  ← тести async, CancellationToken, IAsyncEnumerable
logs/
    app.log                             ← створюється автоматично під час запуску
data/
    sample.csv
    sample.json
    mixed.csv
    catalog.json                        ← зразок каталогу товарів (17 позицій)
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

## Лабораторна 8 — Асинхронність, скасування, доменні винятки та xUnit тести

### Мета та архітектура
У лабораторній роботі реалізовано повноцінний асинхронний пайплайн завантаження каталогу без блокування потоків, підтримано кооперативне скасування операцій, впроваджено винятки доменної моделі та покриття коду модульними тестами:
* **async/await без блокувань:** відсутні псевдо-асинхронні обгортки на кшталт `Task.Run` для I/O-операцій. Повністю викорінено блокуючі виклики `.Result`, `.Wait()`, `.GetAwaiter().GetResult()` та антипатерн `async void` (за винятком обробників подій).
* **Справжнє асинхронне введення/виведення:** операції читання файлів виконуються через `FileStream(..., options: FileOptions.Asynchronous)` та `JsonSerializer.DeserializeAsyncEnumerable` / `JsonSerializer.DeserializeAsync`.
* **Кооперативне скасування (`CancellationToken`):** токен передається у всі асинхронні виклики та системні методи, а також обов'язково перевіряється в циклах обробки через `ct.ThrowIfCancellationRequested()`.
* **Коректне завершення в CLI (Graceful cancellation):** під час натискання `Ctrl+C` (`Console.CancelKeyPress`) або завершення таймауту операції CLI не падає з аварійним стектрейсом, а перехоплює `OperationCanceledException` і виводить людино-читабельне повідомлення: `Імпорт скасовано.`.
* **Доменні винятки (`DomainException`):** для помилок порушення бізнес-правил (списання більшої кількості товару, ніж є на залишку, операції з неіснуючим товаром, завантаження пошкоджених сутностей) використовується окремий типізований виняток `DomainException : Exception`. Базові валідації параметрів методів (`null`, порожні рядки) залишаються на `ArgumentException` / `ArgumentOutOfRangeException`.
* **Тестовий проєкт `tests/Core.Tests`:**
  * Додано в solution `CrossApp.slnx`.
  * Посилається **виключно** на `src/Core/Core.csproj` (ізольований від консольного шару).
  * 13 тестів у трьох тестових класах (усі успішні, тривалість < 150 мс).

### Реалізовані тести
1. **`CatalogServiceTests` (бізнес-інваріанти, доменні винятки, події):**
   * `Issue_DecreasesQuantity_WhenStockSufficient` — коректне списання товару при достатньому залишку.
   * `Issue_MoreThanQuantity_ThrowsDomainException` — списання більшої кількості, ніж є на залишку, викидає `DomainException` (не `InvalidOperationException`).
   * `Receive_RaisesChangedEventOnce_WithCorrectPayload` — підняття події `Changed` рівно 1 раз із валідними параметрами (`Delta`, `QuantityAfter`).
   * `Receive_NonExistentProduct_ThrowsDomainException` — оприбуткування для неіснуючого товару викидає `DomainException`.
2. **`ReportServiceTests` (детермінізм та граничні випадки LINQ):**
   * `LowStock_ReturnsCorrectItems_Theory` — параметризований тест `[Theory]` для перевірки дефіцитних залишків при різних порогах.
   * `TopByQuantity_ReturnsDeterministicOrder_OnTies` — перевірка стійкості сортування залишків при однакових кількостях завдяки вторинним ключам SKU/Id.
3. **`ImportTests` (асинхронність, скасування, токени, стрімінг):**
   * `ImportAsync_WhenAlreadyCancelled_ThrowsOperationCanceledException` — реакція на попередньо скасований токен без зайвого введення/виведення.
   * `ImportAsync_WithCorruptedData_ThrowsDomainException` — перевірка атомарності та очищення тимчасового файлу в блоці `finally`.
   * `ImportAsync_ValidFile_ImportsAllProducts` — успішне асинхронне читання 17 позицій у сховище.
   * `ImportAsync_IsIdempotent` — повторний імпорт не створює дублікатів (ідентичність за SKU).
   * `ImportStreamAsync_StreamsProductsAndReportsProgress` — потокове читання з підтвердженням викликів `IProgress<int>`.
   * `ImportAsync_LinkedTokens_TimeoutTriggersCancellation` — перевірка спрацьовування зв'язаних токенів саме через джерело таймауту.

### Додаткові завдання (на вищий бал)
1. **Потоковий імпорт (`IAsyncEnumerable<Product>`):** метод `StreamAsync` у `ICatalogImporter` та `JsonCatalogImporter` використовує `JsonSerializer.DeserializeAsyncEnumerable`, повертає асинхронний потік сутностей та підтримує `[EnumeratorCancellation]`. Метод `CatalogService.ImportStreamAsync` опрацьовує потік через `await foreach`.
2. **Зв'язані токени скасування:** демонстрація та тест використання `CancellationTokenSource.CreateLinkedTokenSource(userCts.Token, timeoutCts.Token)`. Доведено, що скасування викликане саме таймаутом (`timeoutCts.IsCancellationRequested == true`).
3. **Звіт про прогрес операції (`IProgress<int>`):** передача прогресу у відсотках від 0 до 100 під час читання великого файлу з відображенням у CLI без блокування основного потоку.
4. **Збір покриття коду:** налаштовано та задокументовано збір метрик покриття тестами через інструмент `XPlat Code Coverage`.

### Definition of Done (Самоперевірка)
- [x] **1. Тестовий проєкт у solution:** `tests/Core.Tests/Core.Tests.csproj` включено в `CrossApp.slnx` та налаштовано для `net10.0`.
- [x] **2. Чистота залежностей тестів:** `Core.Tests` має `ProjectReference` тільки на `Core` і не посилається на `Cli`.
- [x] **3. Асинхронне I/O:** використання `FileStream` з `useAsync: true` / `FileOptions.Asynchronous` та `DeserializeAsync*`, без `Task.Run`.
- [x] **4. Перевірка CancellationToken:** токен передано в усі виклики `*Async` та перевірено в циклах через `ThrowIfCancellationRequested()`.
- [x] **5. Graceful cancellation у CLI:** перехоплення `OperationCanceledException` з виведенням зрозумілого повідомлення замість стектрейсу помилки.
- [x] **6. Доменний виняток DomainException:** створено окремий тип винятку для бізнес-правил, відмінний від `ArgumentException`.
- [x] **7. $\ge 5$ модульних тестів (фактично 13):** покривають інваріанти, події, звіти, помилки даних, стрімінг та скасування; усі проходять (`Passed: 13, Failed: 0`).
- [x] **8. Нуль заборонених конструкцій:** 0 випадків `async void` (крім подій), `.Result`, `.Wait()`.
- [x] **9. Додаткові завдання:** `IAsyncEnumerable`, `CreateLinkedTokenSource`, `IProgress<T>`, команда збору code coverage.
- [x] **10. README оновлено, коміт `lab08`:** інструкції та перевірки зафіксовано в системі контролю версій.

---

## Лабораторна 7 — Звіти через LINQ to Objects

`Core.Reports.ReportService` приймає `IEnumerable<Product>` і повертає DTO, не доменні сутності. У CLI залишаються вибір команди та форматований вивід; LINQ-запити й обробка порожніх джерел зосереджені в Core. `SampleData` містить 17 товарів, чотири одиниці виміру та три позиції з залишком менше 10.

### Звіти

| Метод | Питання | Оператори |
|---|---|---|
| `LowStock` | Які товари потребують поповнення? | `Where`, `OrderBy`, `ThenBy`, `Select` |
| `TopByQuantity` | Які N товарів мають найбільший залишок? | `TopBy`, `OrderByDescending`, `ThenBy`, `Take`, `Select` |
| `TopByUnit` | Які N найбільших позицій у вибраній одиниці? | `Where`, `TopBy`, `Select` |
| `ByUnit` | Як розподілено товари та залишки за одиницями? | `GroupBy`, `Select`, `Count`, `Sum`, `OrderByDescending` |
| `Totals` | Які загальна кількість, сума, середнє та максимум? | `ToList`, `Count`, `Sum`, `Average`, `Max` |
| `JoinMovements` | Які рухи відповідають товарам за ID? | `Join`, `OrderBy`, `Select` |

`TopBy<T>` — власний метод-розширення з основним ключем, двома ключами розв'язання нічиєї та `Take`; він використовується в `TopByQuantity` і `TopByUnit`. Метод `ByUnitUsingQuerySyntax` повторює групування синтаксисом `from/group/into/orderby/select`: він компактно читається, коли головна операція — групування, тоді як ланцюжок методів у `ByUnit` явно задає comparer для детермінованого сортування.

### Відкладене виконання та вимірювання

У `DemonstrateDeferredExecution` створюється запит `Where`, але він виконується лише під час `Count()`. Перше перебирання знаходить **3** дефіцитні позиції; після додавання товару до джерела повторний `Count()` знаходить **4**, бо запит читає актуальну колекцію. `ToList()` фіксує ці чотири результати, тому очищення джерела не змінює знімок. `CompareMaterialization` запускає однакові фільтр та агрегати для 100 000 записів без проміжного списку і з `ToList()` після фільтра; результати перевіряються на рівність, а час залежить від середовища.

### Запуск і приклади результатів

```powershell
dotnet run --project src/Cli -- --lab7
dotnet run --project src/Cli -- report-top
dotnet run --project src/Cli -- report-low
dotnet run --project src/Cli -- report-unit
dotnet run --project src/Cli -- report-groups
dotnet run --project src/Cli -- report-groups-query
dotnet run --project src/Cli -- report-sum
dotnet run --project src/Cli -- report-join
dotnet run --project src/Cli -- report-deferred
dotnet run --project src/Cli -- report-empty
dotnet run --project src/Cli -- report-benchmark
```

Приклад виводу всіх підкоманд (час benchmark змінюється між запусками):

```text
=== report-top ===
SKU        Назва                         Залишок Од.
SKU-003    Цегла червона                    4200 шт
SKU-016    Кабель ВВГнг 3x2.5                500 м
SKU-015    Саморізи по металу                200 шт
SKU-006    Профіль CD-60 3м                  150 шт
SKU-001    Цемент М400 25кг                  120 шт
=== report-low ===
SKU-004    Щебінь фракція 20-40                5 т
SKU-002    Пісок будівельний                   8 т
SKU-009    Фарба акрилова 14кг                 9 шт
=== report-unit ===
SKU-003    Цегла червона                    4200 шт
SKU-015    Саморізи по металу                200 шт
SKU-006    Профіль CD-60 3м                  150 шт
=== report-groups ===
Од.     Позицій    Залишок
шт           12       5104
м             2        570
м2            1        110
т             2         13
=== report-groups-query ===
Од.     Позицій    Залишок
шт           12       5104
м             2        570
м2            1        110
т             2         13
=== report-sum ===
позицій 17, всього 5797, середнє 341,00, макс 4200
=== report-join ===
SKU-001    Цемент М400 25кг                 -8 шт
SKU-009    Фарба акрилова 14кг              12 шт
SKU-017    Кабель UTP cat6                 -25 м
=== report-deferred ===
Дефіцитних до зміни джерела: 3
Дефіцитних після додавання: 4
Елементів у матеріалізованому знімку: 4
=== report-empty ===
Дефіцитних позицій: 0
позицій 0, всього 0, середнє 0,00, макс 0
=== report-benchmark ===
Записів: 100 000
Результат: 50 000 позицій, залишок 2 475 000
Без ToList посередині: 2,45 мс
З ToList посередині:   2,11 мс
```

`report-groups-query` повертає ті самі зведення, що й `report-groups`. `report-top` двічі поспіль повертає однаковий порядок завдяки сортуванню за SKU та ID після залишку. `report-empty` перевіряє порожні результати фільтра й агрегатів; у проєкті використовується лише LINQ to Objects — без PLINQ та LINQ to SQL.

### Definition of Done

- [x] У Core є запити `Where`, проєкція в DTO, `GroupBy`, агрегати та топ-N.
- [x] Звіти приймають `IEnumerable<T>` і повертають `IReadOnlyList<DTO>` (агрегат повертає іменований кортеж).
- [x] Топ-N має вторинні ключі сортування; порожнє джерело обробляється без винятку.
- [x] CLI має щонайменше чотири підкоманди, читабельне вирівнювання та окреме демо відкладеного виконання.
- [x] Відкладене виконання пояснено з числами з демонстрації.
- [x] Використовується лише LINQ to Objects.
- [x] `SampleData` містить 17 товарів і чотири одиниці виміру; `report-low` має результати; створено коміт `lab07`.
- [x] Додаткові завдання: `Join`, власний `TopBy<T>` у двох звітах, query-синтаксис і benchmark на 100 000 записів.

---

## Команди запуску

### Запуск модульних тестів (Лабораторна 8)
```powershell
# Запуск усіх 13 тестів (бізнес-інваріанти, доменні винятки, події, звіти, асинхронність, скасування):
dotnet test

# Збір метрик покриття коду (додаткове завдання 4):
dotnet test --collect:"XPlat Code Coverage"
```

### Демонстрація консольного застосунку (CLI)
```powershell
# Лабораторна 8 (за замовчуванням): асинхронний імпорт, скасування, IAsyncEnumerable, зв'язані токени
dotnet run --project src/Cli

# Запуск із явним файловим сховищем:
dotnet run --project src/Cli -- --file

# Штучна перевірка скасування за токеном або таймаутом:
dotnet run --project src/Cli -- --cancel
dotnet run --project src/Cli -- --timeout

# Лабораторна 7: звіти LINQ
dotnet run --project src/Cli -- --lab7
dotnet run --project src/Cli -- report-top
dotnet run --project src/Cli -- report-low
dotnet run --project src/Cli -- report-groups
dotnet run --project src/Cli -- report-sum
dotnet run --project src/Cli -- report-deferred
dotnet run --project src/Cli -- report-benchmark

# Лабораторна 6 (демонстрація подій, підписки/відписки, логів, Func та Action):
dotnet run --project src/Cli -- --lab6
Get-Content logs/app.log

# Запуск попередніх лабораторних:
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
