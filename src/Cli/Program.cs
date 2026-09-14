using System.Text.Json;
using Core;

bool isJson = args.Contains("--json");
EnvironmentReport report = EnvironmentInfo.Collect();

if (isJson)
{
    var options = new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    var systemInfo = new
    {
        App = "CrossApp",
        Student = "Андрійчук Ілля",
        report.OsDescription,
        report.FrameworkDescription,
        report.ProcessArchitecture,
        report.DetectedRid,
        report.ReportedRid,
        report.BaseDirectory,
        CurrentDirectory = Environment.CurrentDirectory,
        Domain = "Склад (товари, партії, залишки, переміщення)"
    };

    Console.WriteLine(JsonSerializer.Serialize(systemInfo, options));
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
    Console.WriteLine(new string('-', 52));
    Console.WriteLine("Предметна область  : Склад (товари, партії, залишки, переміщення)");
}