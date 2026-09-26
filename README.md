# CrossApp
Наскрізний проєкт з крос-платформного програмування. Лабораторна 2 відокремлює спільний код бібліотеки `Core` від точки входу `Cli`.

## Структура

```text
CrossApp.slnx
README.md
src/
	Core/
		Core.csproj
		EnvironmentInfo.cs
		EnvironmentReport.cs
	Cli/
		Cli.csproj
		Program.cs
```

`Core` — class library без точки входу. У ній `EnvironmentInfo.Collect()` збирає дані платформи й повертає `EnvironmentReport`; вона не друкує дані. `Cli` має єдину залежність `Cli -> Core`, викликає цей метод і форматує результат. Циклічної залежності немає. Цього тижня доменна модель навмисно не реалізується. Наступні каталоги Core за планом семестру: `Dto/`, `Domain/`, `Storage/`.

## Команди

Додання бібліотеки та посилання (виконано для цього solution):

```powershell
dotnet new classlib -n Core -o src/Core -f net10.0
dotnet sln CrossApp.slnx add src/Core/Core.csproj
dotnet add src/Cli/Cli.csproj reference src/Core/Core.csproj
```

Збірка й запуск:

```powershell
dotnet sln CrossApp.slnx list
dotnet build CrossApp.slnx
dotnet build src/Core/Core.csproj
dotnet run --project src/Cli --framework net10.0
dotnet run --project src/Cli --framework net10.0 -- --json
```

`Program.cs` не використовує `RuntimeInformation`: збір середовища належить `Core`, а CLI відповідає за вивід. Звичайний режим очікує натискання клавіші перед завершенням.

## Multi-targeting

`Core` і `Cli` збираються для `net8.0` та `net10.0`. У `EnvironmentInfo` директива `#if NET10_0_OR_GREATER` додає до звіту `BuildNote`, тому рядок вказує TFM, для якого скомпільовано бібліотеку.

Перевірено командою `dotnet build CrossApp.slnx`; створено `bin/Debug/net8.0` і `bin/Debug/net10.0`. Вивід `net8.0` містить `збірка під net8.0`, а `net10.0` — `збірка під net10.0`. У системі SDK `10.0.100`, встановлені .NET runtimes 9.0.11 і 10.0.12, але немає .NET 8 runtime. Для демонстрації net8-збірки застосовано major roll-forward на доступний runtime:

```powershell
$env:DOTNET_ROLL_FORWARD = "Major"
dotnet run --no-build --project src/Cli --framework net8.0 -- --json
```

У такому запуску `BuildNote` показує TFM компіляції (`net8.0`), а `FrameworkDescription` — фактично використаний runtime (`.NET 9.0.11`). Для звичайного використання net8-збірки встановіть сумісний .NET 8 runtime.

## Публікація

Публікацію виконано для `win-x64`, `net10.0`. Для кожного режиму задано окремий вихідний каталог, усі вони під `artifacts/publish/` і не потрапляють у Git.

```powershell
dotnet publish src/Cli/Cli.csproj -c Release -f net10.0 -r win-x64 --self-contained true -o artifacts/publish/win-x64-self-contained
dotnet publish src/Cli/Cli.csproj -c Release -f net10.0 -r win-x64 --self-contained false -o artifacts/publish/win-x64-framework-dependent
```

Framework-dependent містить застосунок і залежності, але не .NET runtime, тому на цільовій машині потрібен .NET 10. Self-contained також містить runtime; він більший, зате не потребує попередньої інсталяції .NET і прив'язаний до RID.

Розмір пораховано сумою розмірів файлів через PowerShell (`Measure-Object Length -Sum`); MiB = байти / 1,048,576.

| RID | Режим | Розмір каталогу | Файлів | Потрібен встановлений runtime? |
| :--- | :--- | ---: | ---: | :--- |
| win-x64 | self-contained | 77.06 MiB (80,803,422 байти) | 194 | Ні |
| win-x64 | framework-dependent | 0.22 MiB (226,254 байти) | 7 | Так, .NET 10 |

Обидва виконувані файли запускалися безпосередньо з `artifacts/publish/.../Cli.exe --json`; у звіті вказано відповідний каталог публікації й RID `win-x64`.

## Додаткові параметри

Single-file і trimmed варіанти також зібрано для `net10.0` та запущено з publish-каталогів:

```powershell
dotnet publish src/Cli/Cli.csproj -c Release -f net10.0 -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/publish/win-x64-single-file
dotnet publish src/Cli/Cli.csproj -c Release -f net10.0 -r win-x64 --self-contained true -p:PublishTrimmed=true -o artifacts/publish/win-x64-trimmed
```

| Варіант | Розмір | Файлів | Запуск |
| :--- | ---: | ---: | :--- |
| Self-contained + single-file | 70.13 MiB (73,541,845 байт) | 3 | Успішний |
| Self-contained + trimming | 19.76 MiB (20,714,771 байт) | 35 | Успішний |

Trimming може видалити типи й члени, доступ до яких здійснюється через reflection і які статичний аналізатор не бачить. Початковий reflection-based виклик `System.Text.Json` спричиняв `IL2026` і падав у trimmed-застосунку. Серіалізацію переведено на source-generated metadata (`CliJsonContext`); фінальна trimmed-публікація збирається без попереджень і JSON-режим працює. Для іншого коду з reflection trimming усе одно потрібно перевіряти окремо.

## Перевірка

- `dotnet sln CrossApp.slnx list` показує обидва проєкти.
- `dotnet build CrossApp.slnx` і окремий build `Core` успішні.
- `Core` не посилається на `Cli`; `Cli.csproj` містить `ProjectReference` на `Core.csproj`.
- `bin/`, `obj/` і `artifacts/publish/` виключені з Git.
- Публікації self-contained, framework-dependent, single-file і trimmed запускаються з каталогів `publish` у JSON-режимі.