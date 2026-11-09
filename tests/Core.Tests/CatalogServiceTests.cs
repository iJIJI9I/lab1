using Core;
using Core.Domain;
using Core.Events;
using Core.Services;
using Core.Storage;
using Xunit;

namespace Core.Tests;

public sealed class CatalogServiceTests
{
    [Fact] // 1) інваріант: відмова і незмінний стан
    public void Receive_WithNegativeQuantity_Throws_AndKeepsState()
    {
        var store = new InMemoryCatalogStore();
        var service = new CatalogService(store);
        var p = service.Add("SKU-1", "Кабель", "м", 10);

        Assert.Throws<ArgumentOutOfRangeException>(() => service.Receive(p.Id, -5));
        Assert.Equal(10, store.GetById(p.Id)!.Quantity);
    }

    [Fact] // 2) подія з тижня 6 як спосіб перевірки
    public void Add_RaisesChangedExactlyOnce()
    {
        var service = new CatalogService(new InMemoryCatalogStore());
        var events = new List<CatalogChangedEventArgs>();
        service.Changed += (_, e) => events.Add(e);

        service.Add("SKU-2", "Гвинт", "шт", 7);

        Assert.Single(events);
        Assert.Equal(CatalogChangeKind.Added, events[0].Kind);
        Assert.Equal(7, events[0].QuantityAfter);
        Assert.Equal(7, events[0].Delta);
    }

    [Fact] // 3) перевірка DomainException при спробі видати більше ніж є
    public void Issue_MoreThanQuantity_ThrowsDomainException_AndKeepsState()
    {
        var store = new InMemoryCatalogStore();
        var service = new CatalogService(store);
        var p = service.Add("SKU-3", "Фарба", "л", 10);

        var ex = Assert.Throws<DomainException>(() => service.Issue(p.Id, 25));
        Assert.Contains("Не можна видати 25", ex.Message);
        Assert.Equal(10, store.GetById(p.Id)!.Quantity);
    }

    [Fact] // 4) перевірка DomainException при операції з неіснуючим id
    public void Receive_NonExistentId_ThrowsDomainException()
    {
        var service = new CatalogService(new InMemoryCatalogStore());
        var ex = Assert.Throws<DomainException>(() => service.Receive("NON-EXISTENT", 5));
        Assert.Contains("Немає запису з id=NON-EXISTENT", ex.Message);
    }
}
