using System.Text.Json;
using Core;

bool isJson = args.Contains("--json");
EnvironmentReport report = EnvironmentInfo.Collect();

if (isJson)
{
    var options = new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping, WriteIndented = true  };
    var systemInfo = new CliSystemInfo(
        "CrossApp",
        "Андрійчук Ілля",
        report.OsDescription,
        report.FrameworkDescription,
        report.ProcessArchitecture,
        report.DetectedRid,
        report.ReportedRid,
        report.BaseDirectory,
        report.BuildNote,
        Environment.CurrentDirectory);

    var jsonContext = new CliJsonContext(options);
    Console.WriteLine(JsonSerializer.Serialize(systemInfo, jsonContext.CliSystemInfo));
}
else
{
    Console.WriteLine("CrossApp – інформація про середовище");
    Console.WriteLine(new string('-', 52));
    Console.WriteLine($"ОС                 : {report.OsDescription}");
    Console.WriteLine($"Runtime            : {report.FrameworkDescription}");
    Console.WriteLine($"Архітектура процесу: {report.ProcessArchitecture}");
    Console.WriteLine($"RID (визначено)    : {report.DetectedRid}");
    Console.WriteLine($"RID (від .NET)     : {report.ReportedRid}");
    Console.WriteLine($"Каталог застосунку : {report.BaseDirectory}");
    Console.WriteLine($"TFM збірки         : {report.BuildNote}");
    Console.WriteLine(new string('-', 52));
    Console.WriteLine("Натисніть будь-яку клавішу для виходу...");
    Console.ReadKey();
}

internal sealed record CliSystemInfo(
    string App,
    string Student,
    string OsDescription,
    string FrameworkDescription,
    string ProcessArchitecture,
    string DetectedRid,
    string ReportedRid,
    string BaseDirectory,
    string BuildNote,
    string CurrentDirectory);

[System.Text.Json.Serialization.JsonSourceGenerationOptions(GenerationMode = System.Text.Json.Serialization.JsonSourceGenerationMode.Metadata)]
[System.Text.Json.Serialization.JsonSerializable(typeof(CliSystemInfo))]
internal partial class CliJsonContext : System.Text.Json.Serialization.JsonSerializerContext
{
}