using System.Runtime.InteropServices;
using System.Text.Json;

bool isJson = args.Contains("--json");

if (isJson)
{
    var systemInfo = new
    {
        App = "CrossApp",
        Student = "Андрійчук Ілля",
        OSDescription = RuntimeInformation.OSDescription,
        OSVersion = Environment.OSVersion.ToString(),
        ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
        DotNetVersion = Environment.Version.ToString(),
        Framework = RuntimeInformation.FrameworkDescription,
        AppDirectory = AppContext.BaseDirectory,
        CurrentDirectory = Environment.CurrentDirectory,
        Domain = "Склад (товари, партії, залишки, переміщення)"
    };

    // Виведення єдиним JSON-рядком
    var options = new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
Console.WriteLine(JsonSerializer.Serialize(systemInfo, options));
}
else
{
    Console.WriteLine("CrossApp – практикум з крос-платформного програмування");
    Console.WriteLine("Студент: Андрійчук Ілля, група ФЕІ-33");
    Console.WriteLine(new string('-', 52));
    Console.WriteLine($"ОС (OSDescription) : {RuntimeInformation.OSDescription}");
    Console.WriteLine($"ОС (Environment)   : {Environment.OSVersion}");
    Console.WriteLine($"Архітектура процесу : {RuntimeInformation.ProcessArchitecture}");
    Console.WriteLine($"Версія .NET (CLR)   : {Environment.Version}");
    Console.WriteLine($"Runtime             : {RuntimeInformation.FrameworkDescription}");
    Console.WriteLine($"Каталог застосунку : {AppContext.BaseDirectory}");
    Console.WriteLine($"Поточний каталог   : {Environment.CurrentDirectory}");
    Console.WriteLine(new string('-', 52));
    Console.WriteLine("Предметна область: Склад (товари, партії, залишки, переміщення)");
}