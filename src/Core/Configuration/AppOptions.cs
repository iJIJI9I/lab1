namespace Core.Configuration;

/// <summary>
/// Кореневий клас налаштувань застосунку (тиждень 9).
/// Прив'язується до конфігурації через config.Get&lt;AppOptions&gt;().
/// </summary>
public sealed class AppOptions
{
    public StorageOptions Storage { get; init; } = new();
    public LoggingOptions Logging { get; init; } = new();
    public string EnvironmentName { get; init; } = "Development";
}

/// <summary>
/// Налаштування файлового сховища каталогу.
/// Ключ конфігурації: Storage:DataPath, Storage:FileName.
/// Змінна середовища: CROSSAPP_Storage__DataPath, CROSSAPP_Storage__FileName.
/// </summary>
public sealed class StorageOptions
{
    public string DataPath { get; init; } = "data";
    public string FileName { get; init; } = "items.json";

    /// <summary>
    /// Формує абсолютний шлях до файлу даних і гарантує існування каталогу.
    /// Якщо DataPath — відносний, відлік іде від baseDir (AppContext.BaseDirectory).
    /// Якщо DataPath — абсолютний (вже задається через CROSSAPP_DATA_PATH),
    /// він використовується без змін.
    /// </summary>
    public string ResolveFullPath(string baseDir)
    {
        // Path.Combine, а не конкатенація рядків — коректно на Windows і Linux
        var root = Path.IsPathRooted(DataPath)
            ? DataPath
            : Path.Combine(baseDir, DataPath);

        Directory.CreateDirectory(root); // без винятку, якщо вже існує

        return Path.GetFullPath(Path.Combine(root, FileName));
    }
}

/// <summary>
/// Налаштування логування. Ключ конфігурації: Logging:MinimumLevel.
/// </summary>
public sealed class LoggingOptions
{
    public string MinimumLevel { get; init; } = "Information";
}
