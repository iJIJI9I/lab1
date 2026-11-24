using System.Net;
using System.Net.Http.Json;
using Api.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Api.Tests;

/// <summary>
/// Інтеграційні тести Minimal API (додаткове завдання 4) на базі WebApplicationFactory.
/// Перевіряють повний HTTP-контракт: коди відповідей, DTO, заголовки Location та валідацію.
/// </summary>
public sealed class ProductApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ProductApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_ReturnsListOfProducts()
    {
        var response = await _client.GetAsync("/api/products");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var products = await response.Content.ReadFromJsonAsync<List<ProductDto>>();
        Assert.NotNull(products);
        Assert.NotEmpty(products);
    }

    [Fact]
    public async Task CreateReadUpdateDelete_Lifecycle_Succeeds()
    {
        // 1. POST: створення ресурсу -> 201 Created + заголовок Location
        var createRequest = new SaveProductRequest(
            Sku: "TEST-SKU-1",
            Name: "Тестовий цемент 25кг",
            Unit: "мішок",
            Quantity: 50);

        var postResponse = await _client.PostAsJsonAsync("/api/products", createRequest);
        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);

        var location = postResponse.Headers.Location;
        Assert.NotNull(location);

        var created = await postResponse.Content.ReadFromJsonAsync<ProductDto>();
        Assert.NotNull(created);
        Assert.Equal("TEST-SKU-1", created.Sku);
        Assert.Equal("Тестовий цемент 25кг", created.Name);

        // 2. GET за отриманим Location -> 200 OK
        var getResponse = await _client.GetAsync(location);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var fetched = await getResponse.Content.ReadFromJsonAsync<ProductDto>();
        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal(50, fetched.Quantity);

        // 3. PUT: повне оновлення ресурсу -> 200 OK
        var updateRequest = new SaveProductRequest(
            Sku: "TEST-SKU-1",
            Name: "Оновлена назва товару",
            Unit: "мішок",
            Quantity: 75);

        var putResponse = await _client.PutAsJsonAsync(location, updateRequest);
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);

        var updated = await putResponse.Content.ReadFromJsonAsync<ProductDto>();
        Assert.NotNull(updated);
        Assert.Equal("Оновлена назва товару", updated.Name);
        Assert.Equal(75, updated.Quantity);

        // 4. DELETE: видалення ресурсу -> 204 No Content
        var deleteResponse = await _client.DeleteAsync(location);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // 5. Перевірка: GET після DELETE -> 404 Not Found
        var getAfterDeleteResponse = await _client.GetAsync(location);
        Assert.Equal(HttpStatusCode.NotFound, getAfterDeleteResponse.StatusCode);
    }

    [Fact]
    public async Task Post_InvalidData_Returns400_WithValidationProblem()
    {
        var invalidRequest = new SaveProductRequest(
            Sku: "",
            Name: "X",
            Unit: "",
            Quantity: -5);

        var response = await _client.PostAsJsonAsync("/api/products", invalidRequest);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey("Sku") || problem.Errors.ContainsKey("sku"));
        Assert.True(problem.Errors.ContainsKey("Quantity") || problem.Errors.ContainsKey("quantity"));
        Assert.True(problem.Errors.ContainsKey("Unit") || problem.Errors.ContainsKey("unit"));
    }

    [Fact]
    public async Task Put_NonExistent_Returns404NotFound()
    {
        var updateRequest = new SaveProductRequest(
            Sku: "NON-EXISTENT",
            Name: "Неіснуючий",
            Unit: "шт",
            Quantity: 10);

        var response = await _client.PutAsJsonAsync("/api/products/missing-id-99999", updateRequest);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithPagination_ReturnsPagedResult()
    {
        var response = await _client.GetAsync("/api/products?page=1&pageSize=3");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var paged = await response.Content.ReadFromJsonAsync<PagedResult<ProductDto>>();
        Assert.NotNull(paged);
        Assert.Equal(1, paged.Page);
        Assert.Equal(3, paged.PageSize);
        Assert.True(paged.Total > 0);
        Assert.True(paged.Items.Count <= 3);
    }

    [Fact]
    public async Task Get_LowStock_ReturnsOnlyItemsBelowThreshold()
    {
        var response = await _client.GetAsync("/api/products/low-stock?threshold=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var lowStock = await response.Content.ReadFromJsonAsync<List<ProductDto>>();
        Assert.NotNull(lowStock);
        Assert.All(lowStock, item => Assert.True(item.Quantity < 10));
    }

    [Fact]
    public async Task Post_DuplicateSku_Returns409Conflict()
    {
        var duplicateRequest = new SaveProductRequest(
            Sku: "SKU-001",
            Name: "Спроба дубліката",
            Unit: "шт",
            Quantity: 10);

        var response = await _client.PostAsJsonAsync("/api/products", duplicateRequest);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
