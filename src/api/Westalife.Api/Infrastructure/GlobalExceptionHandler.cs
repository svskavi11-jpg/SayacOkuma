using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Westalife.Domain.Exceptions;
using Westalife.Infrastructure.Metering.Exceptions;

namespace Westalife.Api.Infrastructure;

/// <summary>
/// Global exception handler — tüm hatalar RFC 7807 ProblemDetails olarak döner (application/problem+json).
/// Controller'larda try-catch YOK; iş kuralları Application/Domain katmanlarından buraya taşınır.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, title) = Map(exception);

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Beklenmeyen hata: {Path}", httpContext.Request.Path);
        }
        else
        {
            _logger.LogWarning(exception, "İşlem reddedildi ({StatusCode}): {Message}", statusCode, exception.Message);
        }

        // DB kısıt ihlallerinde (unique/exclusion) gerçek PostgreSQL mesajı kullanıcıya gösterilir.
        var detail = exception.Message;
        if (exception is DbUpdateException && exception.InnerException is not null)
        {
            detail = $"{exception.Message} → {exception.InnerException.Message}";
        }

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        // Log korelasyonu için Activity/TraceId.
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        if (exception is FluentValidation.ValidationException validationException)
        {
            problem.Title = ApiConstants.Titles.ValidationFailed;
            problem.Extensions["errors"] = validationException.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }

    private static (int StatusCode, string Title) Map(Exception exception) => exception switch
    {
        FluentValidation.ValidationException => (StatusCodes.Status400BadRequest, ApiConstants.Titles.ValidationFailed),
        DomainException => (StatusCodes.Status422UnprocessableEntity, ApiConstants.Titles.DomainRuleViolation),
        DbUpdateException => (StatusCodes.Status409Conflict, ApiConstants.Titles.DataConflict),
        MeteringException => (StatusCodes.Status502BadGateway, ApiConstants.Titles.MeteringFailure),
        // NotFoundException: talep edilen kaynak DB'de yok (handler'lar bu tipi fırlatmalı).
        // InvalidOperationException burada YOK — çok geniş; beklenmeyen durumlar 500 olarak doğru şekilde raporlanır.
        Westalife.Domain.Exceptions.NotFoundException => (StatusCodes.Status404NotFound, ApiConstants.Titles.ResourceNotFound),
        _ => (StatusCodes.Status500InternalServerError, ApiConstants.Titles.UnexpectedError)
    };
}
