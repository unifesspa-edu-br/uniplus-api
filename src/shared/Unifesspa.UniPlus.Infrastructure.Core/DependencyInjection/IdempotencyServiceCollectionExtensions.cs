namespace Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

using Idempotency;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Registra o stack de Idempotency-Key (ADR-0027) do processo: as opções, o store EF sobre o contexto único dos módulos e a
/// convention que aplica o <see cref="IdempotencyFilter"/> aos controllers.
/// </summary>
/// <remarks>
/// O processo tem um só store e um só filtro: a tabela <c>idempotency_cache</c> é uma, no esquema da plataforma. O registro de
/// erros de domínio usa <c>TryAddEnumerable</c>, e o <see cref="TimeProvider"/> é o registrado pelo host: esta extensão não
/// registra relógio.
/// </remarks>
public static class IdempotencyServiceCollectionExtensions
{
    public static IServiceCollection AddIdempotency(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOpcoesDaIdempotencia(configuration);
        services.AddScoped<IIdempotencyStore, EfCoreIdempotencyStore>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<Errors.IDomainErrorRegistration, IdempotencyDomainErrorRegistration>());
        services.Configure<MvcOptions>(options => options.Conventions.Add(new IdempotencyControllerConvention()));

        return services;
    }

    private static void AddOpcoesDaIdempotencia(this IServiceCollection services, IConfiguration configuration) =>
        services
            .AddOptions<IdempotencyOptions>()
            .Bind(configuration.GetSection(IdempotencyOptions.SectionName))
            .Validate(static o => o.Ttl > TimeSpan.Zero, "IdempotencyOptions.Ttl deve ser positivo.")
            .Validate(static o => o.MaxBodyBytes > 0, "IdempotencyOptions.MaxBodyBytes deve ser positivo.")
            .ValidateOnStart();
}
