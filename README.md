# CrossApp
Наскрізний проєкт з крос-платформного програмування.

## Структура

```text
CrossApp.slnx
README.md
src/
    Core/
        Core.csproj                     ← multi-targeting (net9.0;net10.0)
        DomainException.cs              ← виняток порушення бізнес-правил
        EnvironmentInfo.cs
        EnvironmentReport.cs
        SampleData.cs                   ← зразки 17 товарів і 4 одиниці виміру
        Abstractions/
            ICatalogStore.cs            ← інтерфейс сховища каталогу
            ICatalogImporter.cs         ← асинхронний та потоковий імпорт
        Storage/
            InMemoryCatalogStore.cs     ← потокобезпечна in-memory реалізація (lock, лаб 10)
            FileCatalogStore.cs         ← файлова реалізація (атомарний .tmp + File.Move, лаб 9)
            CachingCatalogStore.cs      ← декоратор кешування
            StoreFactory.cs             ← фабрика сховищ
            JsonCatalogImporter.cs      ← FileStream(useAsync) + IAsyncEnumerable
        Configuration/                  ← налаштування застосунку (лаб 9)
            AppOptions.cs               ← StorageOptions, LoggingOptions, WarehouseOptions
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
        Cli.csproj                      ← UserSecretsId, Microsoft.Extensions.Configuration.*
        Program.cs                      ← маршрутизація команд (лаби 4-9), config stack
        appsettings.json                ← базові налаштування (Production, Warning)
        appsettings.Development.json    ← налаштування розробки (Development, Debug)
        Notifications/
            ConsoleSink.cs
            FileSink.cs
    Api/                                ← НОВЕ (лаб 10): Minimal API веб-сервіс
        Api.csproj                      ← Microsoft.AspNetCore.OpenApi, Scalar, FluentValidation
        Program.cs                      ← конфігурація DI, ProblemDetails, OpenAPI, Scalar UI
        Api.http                        ← HTTP-запити для тестування ендпоінтів
        appsettings.json
        appsettings.Development.json
        Properties/
            launchSettings.json         ← фіксований порт 5080
        Contracts/
            ProductDto.cs               ← вихідний контракт товару (ізоляція домену)
            SaveProductRequest.cs       ← вхідний контракт + валідатор FluentValidation
            PagedResult.cs              ← контракт пагінації результатів
        Endpoints/
            ProductEndpoints.cs         ← маршрути CRUD, low-stock, пагінація, TypedResults
        Infrastructure/
            DomainExceptionHandler.cs   ← мапінг DomainException -> 409 Conflict ProblemDetails
            ValidationFilter.cs         ← EndpointFilter для FluentValidation (400 Bad Request)
tests/
    Core.Tests/                         ← xUnit тести ядра (посилається ТІЛЬКИ на Core)
        Core.Tests.csproj
        TestData.cs                     ← фіксований тестовий набір даних
        CatalogServiceTests.cs          ← тести бізнес-правил, інваріантів та подій
        ReportServiceTests.cs           ← тести LINQ-звітів (детермінізм, LowStock)
        ImportTests.cs                  ← тести async, CancellationToken, IAsyncEnumerable
    Api.Tests/                          ← НОВЕ (лаб 10): інтеграційні тести API
        Api.Tests.csproj
        ProductApiTests.cs              ← WebApplicationFactory тести (CRUD, валідація, 404, 409)
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

## Лабораторна 9 — Шляхи, JSON, конфігурація, Secrets

### Мета та архітектура
Забезпечено повну кросплатформність застосунку:
* **Усунення абсолютних шляхів:** у проєкті відсутні жорстко закодовані Windows-шляхи (`C:\`, `\`) та Linux-шляхи (`/home/`). Усі шляхи обчислюються динамічно через `Path.Combine` та `Path.GetFullPath` відносно базового каталогу застосунку `AppContext.BaseDirectory`.
* **Ієрархічний стек конфігурації (`IConfiguration`):**
  1. `appsettings.json` (базовий, присутній у репозиторії)
  2. `appsettings.{Environment}.json` (перевизначення для розробки)
  3. `user-secrets` (конфіденційні дані розробника поза репозиторієм)
  4. Змінні середовища (`CROSSAPP_...`)
  5. Аргументи командного рядка (`--Storage:DataPath=...`)
* **Типізовані налаштування (`Core.Configuration.AppOptions`):** біндинг конфігурації через `config.Get<AppOptions>()`, підтримка секцій `Storage`, `Logging`, `Warehouse` та `EnvironmentName`.
* **Атомарний запис файлу (`FileCatalogStore`):** збереження через проміжний файл `.tmp` та подальший `File.Move(tmp, path, overwrite: true)`. Якщо процес перерветься — вихідний файл даних не пошкодиться.
* **Людино-читабельний JSON з кирилицею:** серіалізація з використанням `JavaScriptEncoder.UnsafeRelaxedJsonEscaping` та `JsonNamingPolicy.CamelCase` — українські літери записуються як є, без послідовностей `\uXXXX`.

### Таблиця конфігураційних ключів

| Ключ конфігурації | Змінна середовища | Значення за замовчуванням | Призначення |
|---|---|---|---|
| `Storage:DataPath` | `CROSSAPP_Storage__DataPath` | `"data"` | Каталог для зберігання файлу даних |
| `Storage:FileName` | `CROSSAPP_Storage__FileName` | `"items.json"` | Ім'я файлу каталогу |
| `Logging:MinimumLevel` | `CROSSAPP_Logging__MinimumLevel` | `"Warning"` (Prod) / `"Debug"` (Dev) | Мінімальний рівень логування |
| `Warehouse:DefaultUnit` | `CROSSAPP_Warehouse__DefaultUnit` | `"од"` (Prod) / `"мішок"` (Dev) | Доменне налаштування одиниці виміру |
| `Integration:ApiKey` | `CROSSAPP_Integration__ApiKey` | *З user-secrets* (`"local-dev-key"`) | Навчальний секретний ключ інтеграції |
| `EnvironmentName` | `CROSSAPP_ENVIRONMENT` | `"Development"` | Назва активного профілю середовища |

> **Спеціальна змінна:** Проста змінна середовища `CROSSAPP_DATA_PATH` перекриває `Storage:DataPath`.

### Приклади встановлення змінних середовища

**PowerShell (Windows):**
```powershell
# Зміна каталогу даних:
$env:CROSSAPP_DATA_PATH = "C:\Users\User\custom_data"

# Зміна профілю середовища:
$env:CROSSAPP_ENVIRONMENT = "Production"

# Перевизначення вкладеного ключа (подвійне підкреслення):
$env:CROSSAPP_Storage__FileName = "custom_catalog.json"
$env:CROSSAPP_Integration__ApiKey = "prod-secret-key-12345"

# Очищення змінної:
$env:CROSSAPP_DATA_PATH = $null
```

**Bash (Linux / macOS):**
```bash
export CROSSAPP_DATA_PATH="/tmp/data"
export CROSSAPP_ENVIRONMENT="Production"
export CROSSAPP_Storage__FileName="custom_catalog.json"
export CROSSAPP_Integration__ApiKey="prod-secret-key-12345"
```

### Робота з User Secrets
Секрети зберігаються **поза репозиторієм** у профілі користувача:
* **Windows:** `%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json`
* **Linux / macOS:** `~/.microsoft/usersecrets/<UserSecretsId>/secrets.json`

```powershell
# Ініціалізація (вже виконано, ID записано в Cli.csproj):
dotnet user-secrets init --project src/Cli

# Встановлення секретного ключа:
dotnet user-secrets set "Integration:ApiKey" "local-dev-key" --project src/Cli

# Перевірка списку секретів:
dotnet user-secrets list --project src/Cli
```

### Definition of Done (Самоперевірка Лабораторної 9)
- [x] **1. Експорт та імпорт JSON:** команда `export` виводить вміст сховища у читабельному JSON, коректно працює десеріалізація без `\uXXXX`.
- [x] **2. Відсутність абсолютних шляхів і паролів:** пошук `"C:\"`, `"/home/"`, `"\\"` по коду `src` дає порожній результат.
- [x] **3. `appsettings.json` у репозиторії без секретів:** у файлах конфігурації немає паролів чи токенів.
- [x] **4. Перевизначення `CROSSAPP_DATA_PATH`:** проста змінна середовища пріоритетно перекриває шлях з конфігурації.
- [x] **5. Читання секрету:** `Integration:ApiKey` читається з `user-secrets` або змінних середовища, друкується лише довжина.
- [x] **6. Переносимість шляхів:** запуск з кореня репозиторію та з каталогу `src/Cli` веде до одного й того самого файлу через `AppContext.BaseDirectory`.
- [x] **7. Атомарний запис:** реалізовано патерн `.tmp` + `File.Move(overwrite: true)` у `FileCatalogStore`.
- [x] **8. Сумісність:** Лабораторна 8 запускається за замовчуванням (тести зелені, 13/13). Створено коміт `lab09`.

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

## Лабораторна 10 — Minimal API: CRUD над Core, валідація, OpenAPI (Scalar)

### Мета та архітектура
Створено другий незалежний вхідний проєкт `src/Api` (Minimal API), що працює над спільною доменною моделлю та сервісами бібліотеки `src/Core` паралельно з консольним клієнтом `src/Cli`:
* **Архітектурний розподіл:**
  * `src/Core` — бізнес-правила, інваріанти, сховища та сервіси (не залежить від веб-шару).
  * `src/Api` — HTTP-хост на базі ASP.NET Core Minimal API (посилається на `Core`).
  * `tests/Api.Tests` — окремий тестовий проєкт для наскрізних інтеграційних тестів через `WebApplicationFactory`.
* **Домен предметної області ("Склад"):** сутність товару з атрибутами `Id`, `Sku`, `Name`, `Unit`, `Quantity`, `Note`.
* **Ізоляція доменної моделі (DTO):**
  * Вхідний контракт: `SaveProductRequest(Sku, Name, Unit, Quantity, Note)` — клієнт не може підробити `Id`.
  * Вихідний контракт: `ProductDto(Id, Sku, Name, Unit, Quantity, Note)` — сутність `Product` ніколи не повертається напряму в JSON.
* **Централізована валідація (RFC 9457 `ValidationProblemDetails`):**
  * Помилки валідації повертають статус `400 Bad Request` зі словником `errors` по кожному полю. Застосунок гарантовано не падає з кодом `500 Internal Server Error`.
* **Впровадження залежностей (Dependency Injection):**
  * `ICatalogStore` зареєстровано як **Singleton** (`InMemoryCatalogStore`), забезпечуючи спільний стан між HTTP-запитами.
  * Реалізацію `InMemoryCatalogStore` зроблено **потокобезпечною** через блокування `lock (_lock)`.
  * `CatalogService` зареєстровано як **Scoped/Transient**.
* **OpenAPI та Scalar UI:**
  * Ендпоінт специфікації: `GET /openapi/v1.json`.
  * Інтерактивна веб-документація: `GET /scalar/v1`.
  * **Захист у продакшені:** підключення OpenAPI та Scalar активується **суворо в режимі `Development`** (`if (app.Environment.IsDevelopment())`).
* **Обробка винятків без витоку даних:**
  * Використано `app.UseExceptionHandler()` та `builder.Services.AddProblemDetails()`.
  * Клієнт отримує стандартний об'єкт `ProblemDetails`, стек-трейс та внутрішні деталі сервера приховані.
* **Фіксований порт:** `http://localhost:5080` налаштовано у `src/Api/Properties/launchSettings.json`.

---

### Таблиця HTTP-маршрутів (`/api/products`)

| HTTP-метод | Маршрут | Опис | Очікувані коди відповідей |
|---|---|---|---|
| `GET` | `/api/products` | Отримати всі товари або сторінку (`?page=1&pageSize=20&search=...`) | `200 OK` (`PagedResult<ProductDto>`) |
| `GET` | `/api/products/{id}` | Отримати один товар за унікальним GUID | `200 OK` (`ProductDto`), `404 Not Found` |
| `POST` | `/api/products` | Створити новий товар у каталозі | `201 Created` (`Location: /api/products/{id}`), `400 Bad Request` |
| `PUT` | `/api/products/{id}` | Повне оновлення існуючого товару | `200 OK` (`ProductDto`), `400 Bad Request`, `404 Not Found` |
| `DELETE` | `/api/products/{id}` | Видалити товар зі сховища | `204 NoContent`, `404 Not Found` |
| `GET` | `/api/products/low-stock` | Доменний звіт: позиції з залишком $\le$ threshold (`?threshold=5`) | `200 OK` (`IEnumerable<ProductDto>`) |
| `GET` | `/health` | Healthcheck стану працездатності сервісу | `200 OK` (`{"status":"ok"}`) |

---

### Додаткові завдання (всі 4 виконано на максимальний бал)

1. **Пагінація та пошук (`PagedResult<T>`):**
   * Ендпоінт `GET /api/products?page=1&pageSize=10&search=цемент` підтримує параметри сторінки, розміру сторінки та пошуку за підрядком у назві або SKU.
   * Повертає типізовану структуру: `items`, `page`, `pageSize`, `totalCount`, `totalPages`, `hasPreviousPage`, `hasNextPage`.
2. **Інтеграція FluentValidation через EndpointFilter:**
   * Реалізовано клас `SaveProductRequestValidator : AbstractValidator<SaveProductRequest>` з бізнес-правилами (непорожні SKU, Name, Unit, невід'ємний залишок).
   * Створено generic-фільтр `ValidationFilter<T> : IEndpointFilter`, підключений через `.AddEndpointFilter<ValidationFilter<SaveProductRequest>>()`.
   * Автоматично трансформує помилки FluentValidation у стандартизований RFC 9457 `ValidationProblemDetails`.
3. **Мапінг `DomainException` -> 409 Conflict через `IExceptionHandler`:**
   * Створено `DomainExceptionHandler : IExceptionHandler`.
   * При спробі створити товар із дублюючим SKU або порушенні інваріантів домену сервер повертає чіткий статус `409 Conflict` з типом `ProblemDetails` замість `500 Internal Server Error`.
4. **Наскрізні інтеграційні тести через `WebApplicationFactory`:**
   * Проєкт `tests/Api.Tests` містить 7 тестів у класі `ProductApiTests : IClassFixture<WebApplicationFactory<Program>>`:
     * `GetProducts_ReturnsSuccessAndList` — отримання списку.
     * `GetProductById_ReturnsNotFound_WhenNotExists` — коректний 404.
     * `PostProduct_WithInvalidData_Returns400ValidationProblemDetails` — перевірка валідатора і RFC 9457.
     * `PostProduct_And_GetById_FullCrudCycle` — повний цикл: POST 201 -> перевірка `Location` -> GET 200 -> PUT 200 -> DELETE 204 -> GET 404.
     * `PostProduct_WithDuplicateSku_Returns409Conflict` — перевірка обробника `DomainException`.
     * `GetLowStock_ReturnsFilteredItems` — перевірка доменного звіту дефіциту.
     * `GetProducts_WithPagination_ReturnsPagedResult` — перевірка пагінатора.

---

### Definition of Done (Самоперевірка Лабораторної 10)
- [x] **1. Друга точка входу:** створено проєкт `src/Api`, обидва застосунки (`Cli` та `Api`) використовують спільне ядро `Core`.
- [x] **2. CRUD endpoints:** реалізовано всі 5 базових маршрутів під префіксом `/api/products` + доменний маршрут `low-stock`.
- [x] **3. Ізоляція домену (DTO):** запити приймають `SaveProductRequest`, відповіді повертають `ProductDto`. Сутність `Product` інкапсульована.
- [x] **4. Валідація без 500:** некоректні дані повертають 400 Bad Request з `ValidationProblemDetails`.
- [x] **5. OpenAPI та Scalar UI:** документація `/scalar/v1` та схема `/openapi/v1.json` доступні у Development та відключені у Production.
- [x] **6. Singleton DI сховища:** `ICatalogStore` зареєстровано як Singleton, забезпечено потокобезпечність `InMemoryCatalogStore`.
- [x] **7. Обробка винятків:** `UseExceptionHandler` + `AddProblemDetails` приховують технічні стектрейси.
- [x] **8. Фіксований порт:** налаштовано порт `5080` у `launchSettings.json`.
- [x] **9. Сценарій `Api.http`:** файл містить повну послідовність запитів для тестування життєвого циклу API.
- [x] **10. Усі 4 додаткові завдання:** пагінація `PagedResult`, FluentValidation `EndpointFilter`, `DomainExceptionHandler` (409 Conflict), тести `WebApplicationFactory` (7/7 passed).
- [x] **11. CLI та Core.Tests не зламані:** консольний застосунок працює без змін, усі 13 тестів `Core.Tests` зелені.

---

## Команди запуску

### Запуск тестів (Лабораторні 8 та 10)
```powershell
# Запуск УСІХ тестів розв'язку (13 модульних Core.Tests + 7 інтеграційних Api.Tests):
dotnet test

# Запуск суто інтеграційних тестів Minimal API (WebApplicationFactory):
dotnet test tests/Api.Tests/Api.Tests.csproj

# Запуск суто модульних тестів ядра (бізнес-інваріанти, скасування, стрімінг):
dotnet test tests/Core.Tests/Core.Tests.csproj
```

### Запуск та тестування веб-сервісу Minimal API (Лабораторна 10)
```powershell
# 1. Запуск API сервера на порті 5080:
dotnet run --project src/Api

# 2. Документація Scalar UI у браузері:
# Відкрийте: http://localhost:5080/scalar/v1
# Специфікація OpenAPI: http://localhost:5080/openapi/v1.json
```

**Приклади curl-запитів для перевірки всіх операцій:**
```powershell
# Перевірка працездатності:
curl -i http://localhost:5080/health

# 1. Отримати всі товари:
curl -i http://localhost:5080/api/products

# 2. Пагінація та пошук:
curl -i "http://localhost:5080/api/products?page=1&pageSize=5&search=цемент"

# 3. Створити новий товар:
curl -i -X POST http://localhost:5080/api/products `
  -H "Content-Type: application/json" `
  -d '{"sku":"CEM-400","name":"Портландцемент 50кг","unit":"мішок","quantity":35,"note":"Склад 1"}'

# 4. Отримати створений товар за ID:
curl -i http://localhost:5080/api/products/<GUID_ID>

# 5. Оновити товар:
curl -i -X PUT http://localhost:5080/api/products/<GUID_ID> `
  -H "Content-Type: application/json" `
  -d '{"sku":"CEM-400","name":"Портландцемент М500","unit":"мішок","quantity":50,"note":"Оновлено"}'

# 6. Перевірка валідації (поверне 400 ValidationProblemDetails, не 500):
curl -i -X POST http://localhost:5080/api/products `
  -H "Content-Type: application/json" `
  -d '{"sku":"","name":"","unit":"","quantity":-10}'

# 7. Звіт позицій з низьким залишком (дефіцит):
curl -i "http://localhost:5080/api/products/low-stock?threshold=10"

# 8. Видалити товар:
curl -i -X DELETE http://localhost:5080/api/products/<GUID_ID>
```

---

### Демонстрація консольного застосунку (CLI)
```powershell
# Лабораторна 9: експорт каталогу в JSON (очікуваний вивід за варіантом "Склад"):
dotnet run --project src/Cli -- export

# Лабораторна 9: повна демонстрація (шляхи, переносимість, імпорт/експорт, атомарність, секрети):
dotnet run --project src/Cli -- --lab9

# Лабораторна 9: перевірка списку User Secrets:
dotnet user-secrets list --project src/Cli

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
