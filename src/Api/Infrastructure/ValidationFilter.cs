using FluentValidation;

namespace Api.Infrastructure;

/// <summary>
/// Фільтр кінцевої точки для додаткового завдання 2:
/// Автоматична валідація DTO за допомогою FluentValidation через AddEndpointFilter.
/// Повертає 400 ValidationProblem у форматі RFC 9457 у разі помилок.
/// </summary>
public sealed class ValidationFilter<T>(IValidator<T> validator) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var argument = context.Arguments.OfType<T>().FirstOrDefault();
        if (argument is not null)
        {
            var result = await validator.ValidateAsync(argument, context.HttpContext.RequestAborted);
            if (!result.IsValid)
            {
                return TypedResults.ValidationProblem(result.ToDictionary());
            }
        }

        return await next(context);
    }
}
