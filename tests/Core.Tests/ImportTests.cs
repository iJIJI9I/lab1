using Core;
using Core.Services;
using Core.Storage;
using Xunit;

namespace Core.Tests;

public sealed class ImportTests
{
    [Fact] // 5) скасування асинхронної операції
    public async Task ImportAsync_WhenAlreadyCancelled_Throws()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var service = new CatalogService(new InMemoryCatalogStore());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ImportAsync(new JsonCatalogImporter(), "any.json", cts.Token));
    }

    [Fact] // 6) файл із некоректним записом кидає DomainException і видаляє файл у finally
    public async Task ImportAsync_WithInvalidData_ThrowsDomainException_AndCleansUp()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"invalid_catalog_{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(tempPath, """
            [
              { "Id": "ERR-1", "Sku": "SKU-ERR", "Name": "Некоректний", "Unit": "шт", "Quantity": -10 }
            ]
            """);

            var service = new CatalogService(new InMemoryCatalogStore());
            var importer = new JsonCatalogImporter();

            var ex = await Assert.ThrowsAsync<DomainException>(
                () => service.ImportAsync(importer, tempPath));

            Assert.Contains("Запис 1", ex.Message);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact] // Успішний імпорт та ідемпотентність
    public async Task ImportAsync_ValidFile_ImportsItemsAndIgnoresDuplicates()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"valid_catalog_{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(tempPath, """
            [
              { "Id": "V-1", "Sku": "SKU-V1", "Name": "Товар 1", "Unit": "шт", "Quantity": 20 },
              { "Id": "V-2", "Sku": "SKU-V2", "Name": "Товар 2", "Unit": "шт", "Quantity": 30 }
            ]
            """);

            var store = new InMemoryCatalogStore();
            var service = new CatalogService(store);
            var importer = new JsonCatalogImporter();

            var addedFirst = await service.ImportAsync(importer, tempPath);
            Assert.Equal(2, addedFirst);
            Assert.Equal(2, store.List().Count);

            // Повторний імпорт не дублює існуючі записи (ідемпотентність)
            var addedSecond = await service.ImportAsync(importer, tempPath);
            Assert.Equal(0, addedSecond);
            Assert.Equal(2, store.List().Count);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact] // Додаткове завдання 1: потоковий імпорт через IAsyncEnumerable
    public async Task ImportStreamAsync_ValidFile_StreamsAndReportsProgress()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"stream_catalog_{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(tempPath, """
            [
              { "Id": "S-1", "Sku": "SKU-S1", "Name": "Потоковий 1", "Unit": "шт", "Quantity": 15 },
              { "Id": "S-2", "Sku": "SKU-S2", "Name": "Потоковий 2", "Unit": "шт", "Quantity": 25 }
            ]
            """);

            var store = new InMemoryCatalogStore();
            var service = new CatalogService(store);
            var importer = new JsonCatalogImporter();

            var progressValues = new List<int>();
            var progress = new Progress<int>(v => progressValues.Add(v));

            var added = await service.ImportStreamAsync(importer, tempPath, progress);
            Assert.Equal(2, added);
            Assert.Equal(2, store.List().Count);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact] // Додаткове завдання 2: зв'язані токени та доказ спрацювання таймауту
    public async Task ImportAsync_LinkedTokens_TimeoutTriggersCancellation()
    {
        using var userCts = new CancellationTokenSource();
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(userCts.Token, timeoutCts.Token);

        // Даємо таймауту спрацювати
        await Task.Delay(50);

        Assert.True(linkedCts.IsCancellationRequested);
        Assert.True(timeoutCts.IsCancellationRequested);
        Assert.False(userCts.IsCancellationRequested); // доказ, що спрацював саме таймаут!

        var service = new CatalogService(new InMemoryCatalogStore());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ImportAsync(new JsonCatalogImporter(), "any.json", linkedCts.Token));
    }
}
