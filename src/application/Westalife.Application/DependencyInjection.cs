using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Westalife.Application.Common.Behaviors;

namespace Westalife.Application;

/// <summary>Application katmanının servis kayıtları (CQRS + doğrulama + pipeline).</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
        });

        // Pipeline behaviors — sıra önemli: önce log, sonra doğrulama.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
