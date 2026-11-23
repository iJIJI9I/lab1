using System.Security.Cryptography;
using Api.Contracts;
using Api.Infrastructure;
using Core.Abstractions;
using Core.Domain;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Api.Endpoints;

public static class ProductEndpoints
{
    public static void MapProducts(this WebApplication app)
    {
        var group = app.MapGroup("/api/products").WithTags("Products");

        // 1. GET /api/products (з підтримкою пагінації та пошуку — додаткове завдання 1)
        group.MapGet("/", (
            ICatalogStore store,
            int? page,
            int? pageSize,
            string? search) =>
        {
            var query = store.List().AsEnumerable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(p =>
                    p.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    p.Sku.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            var allItems = query.Select(ToDto).ToList();

            // Якщо передані параметри пагінації — повертаємо PagedResult
            if (page.HasValue || pageSize.HasValue || !string.IsNullOrWhiteSpace(search))
            {
                int p = Math.Max(1, page ?? 1);
                int ps = Math.Clamp(pageSize ?? 20, 1, 100);
                int total = allItems.Count;
                int totalPages = (int)Math.Ceiling(total / (double)ps);
                var pagedItems = allItems.Skip((p - 1) * ps).Take(ps).ToList();

                return Results.Ok(new PagedResult<ProductDto>(pagedItems, total, p, ps, totalPages));
            }

            // Стандартний список за методичкою
            return Results.Ok(allItems);
        })
        .WithName("GetProducts")
        .WithSummary("Отримати список товарів (підтримує пагінацію ?page=1&pageSize=20&search=)");

        // 2. Варіант домену Склад: GET /api/products/low-stock?threshold=5 (сторінка 5 методички)
        group.MapGet("/low-stock", (ICatalogStore store, int? threshold) =>
        {
            int limit = threshold ?? 5;
            var lowStock = store.List()
                .Where(p => p.Quantity < limit)
                .Select(ToDto)
                .ToList();

            return TypedResults.Ok(lowStock);
        })
        .WithName("GetLowStockProducts")
        .WithSummary("Отримати товари з дефіцитним залишком (варіант Склад)");

        // 3. GET /api/products/{id}
        group.MapGet("/{id}", Results<Ok<ProductDto>, NotFound> (string id, ICatalogStore store) =>
        {
            return store.GetById(id) is { } found
                ? TypedResults.Ok(ToDto(found))
                : TypedResults.NotFound();
        })
        .WithName("GetProductById")
        .WithSummary("Отримати товар за ідентифікатором");

        // 4. POST /api/products (з фільтром валідації FluentValidation — додаткове завдання 2)
        group.MapPost("/", Results<Created<ProductDto>, ValidationProblem> (SaveProductRequest request, ICatalogStore store) =>
        {
            if (!request.TryValidate(out var errors))
                return TypedResults.ValidationProblem(errors);

            var id = Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();
            var created = Product.Create(id, request.Sku, request.Name, request.Unit, request.Quantity);

            store.Add(created);

            return TypedResults.Created($"/api/products/{created.Id}", ToDto(created));
        })
        .AddEndpointFilter<ValidationFilter<SaveProductRequest>>()
        .WithName("CreateProduct")
        .WithSummary("Створити новий товар");

        // 5. PUT /api/products/{id}
        group.MapPut("/{id}", Results<Ok<ProductDto>, NotFound, ValidationProblem> (
            string id,
            SaveProductRequest request,
            ICatalogStore store) =>
        {
            if (!request.TryValidate(out var errors))
                return TypedResults.ValidationProblem(errors);

            if (store.GetById(id) is null)
                return TypedResults.NotFound();

            // PUT замінює стан: get-only поля не мутуємо, створюємо валідний Product з тим самим id
            var replacement = Product.Create(id, request.Sku, request.Name, request.Unit, request.Quantity);
            store.Update(replacement);

            return TypedResults.Ok(ToDto(replacement));
        })
        .AddEndpointFilter<ValidationFilter<SaveProductRequest>>()
        .WithName("UpdateProduct")
        .WithSummary("Повністю оновити наявний товар");

        // 6. DELETE /api/products/{id}
        group.MapDelete("/{id}", Results<NoContent, NotFound> (string id, ICatalogStore store) =>
        {
            return store.Remove(id)
                ? TypedResults.NoContent()
                : TypedResults.NotFound();
        })
        .WithName("DeleteProduct")
        .WithSummary("Видалити товар за ідентифікатором");
    }

    private static ProductDto ToDto(Product p) => new(p.Id, p.Sku, p.Name, p.Unit, p.Quantity);
}
