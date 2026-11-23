using Api.Contracts;
using Api.Endpoints;
using Api.Infrastructure;
using Core;
using Core.Abstractions;
using Core.Storage;
using FluentValidation;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Реєстрація валідаторів FluentValidation (додаткове завдання 2)
builder.Services.AddValidatorsFromAssemblyContaining<SaveProductRequestValidator>();

// Реєстрація генератора OpenAPI
builder.Services.AddOpenApi();

// Формат машиннозчитуваного опису помилок RFC 9457
builder.Services.AddProblemDetails();

// Мапінг DomainException -> 409 Conflict (додаткове завдання 3)
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

// Реєстрація сховища в DI як Singleton з початковими даними каталогу
builder.Services.AddSingleton<ICatalogStore>(_ => new InMemoryCatalogStore(SampleData.Products()));

var app = builder.Build();

// Глобальна обробка винятків — не показує клієнтові stack trace (DoD 3)
app.UseExceptionHandler();
app.UseStatusCodePages();

// OpenAPI і Scalar UI лише в Development (DoD 1, сторінка 5)
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Реєстрація кінцевих точок CRUD каталогу товарів
app.MapProducts();

app.Run();

// Робимо клас Program доступним для WebApplicationFactory в tests/Api.Tests (додаткове завдання 4)
public partial class Program { }
