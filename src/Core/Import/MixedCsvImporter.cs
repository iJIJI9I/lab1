using System.Text;
using Core.Dto;

namespace Core.Import;

/// <summary>
/// Результат змішаного імпорту: товари, склади і помилки в одному об'єкті.
/// </summary>
public sealed record MixedImportResult(
    IReadOnlyList<ProductDto>   Products,
    IReadOnlyList<WarehouseDto> Warehouses,
    IReadOnlyList<string>       Errors);

public static class MixedCsvImporter
{
    private const char Separator = ';';

    public static MixedImportResult Load(string path)
    {
        var products   = new List<ProductDto>();
        var warehouses = new List<WarehouseDto>();
        var errors     = new List<string>();

        string[] lines = File.ReadAllLines(path, Encoding.UTF8);

        for (int i = 0; i < lines.Length; i++)
        {
            int    number = i + 1;
            string line   = lines[i];

            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                continue;

            switch (ParseLine(line))
            {
                case ParseProduct ok:
                    products.Add(ok.Value);
                    break;
                case ParseWarehouse ok:
                    warehouses.Add(ok.Value);
                    break;
                case ParseFailed failed:
                    errors.Add($"рядок {number}: {failed.Reason}");
                    break;
            }
        }

        return new MixedImportResult(products, warehouses, errors);
    }

    private static ParseOutcome ParseLine(string line)
    {
        string[] parts = line.Split(Separator, StringSplitOptions.TrimEntries);

        return parts switch
        {
            // Занадто мало колонок для будь-якого типу
            { Length: < 2 }
                => new ParseFailed($"рядок занадто короткий: {parts.Length} колонок"),

            // Префікс "P" → товар: P;id;sku;name;unit;qty
            ["P", var id, var sku, var name, var unit, var qty]
                when int.TryParse(qty, out int q) && q >= 0
                => new ParseProduct(new ProductDto(id, sku, name, unit, q)),

            ["P", ..] => new ParseFailed("невірний формат товару (очікую P;id;sku;name;unit;qty≥0)"),

            // Префікс "W" → склад: W;id;name;location;capacity
            ["W", var id, var name, var location, var cap]
                when int.TryParse(cap, out int c) && c > 0
                => new ParseWarehouse(new WarehouseDto(id, name, location, c)),

            ["W", ..] => new ParseFailed("невірний формат складу (очікую W;id;name;location;capacity>0)"),

            // Невідомий префікс
            [var prefix, ..]
                => new ParseFailed($"невідомий префікс '{prefix}' (очікую P або W)")
        };
    }

    private abstract record ParseOutcome;
    private sealed record ParseProduct(ProductDto Value)     : ParseOutcome;
    private sealed record ParseWarehouse(WarehouseDto Value) : ParseOutcome;
    private sealed record ParseFailed(string Reason)         : ParseOutcome;
}
