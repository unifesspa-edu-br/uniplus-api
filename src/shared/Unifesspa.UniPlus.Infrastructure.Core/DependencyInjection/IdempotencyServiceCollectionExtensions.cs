namespace Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Idempotency;

/// <summary>
/// Registra o stack de Idempotency-Key (ADR-0027): options + store EF
/// adjacente ao DbContext do módulo + <see cref="IdempotencyFilter"/> +
/// hook <see cref="MvcOptions.Filters"/> aplicando o filter globalmente
/// (cobre apenas endpoints com <c>[RequiresIdempotencyKey]</c> via guard
/// interno do próprio filter).
/// </summary>
public static class IdempotencyServiceCollectionExtensions
{
    public static IServiceCollection AddIdempotency<TDbContext>(
        this IServiceCollection services,
        IConfiguration configuration)
        where TDbContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<IdempotencyOptions>()
            .Bind(configuration.GetSection(IdempotencyOptions.SectionName))
            .Validate(static o => o.Ttl > TimeSpan.Zero, "IdempotencyOptions.Ttl deve ser positivo.")
            .Validate(static o => o.MaxBodyBytes > 0, "IdempotencyOptions.MaxBodyBytes deve ser positivo.")
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<IIdempotencyStore, EfCoreIdempotencyStore<TDbContext>>();
        services.AddScoped<IdempotencyFilter>();

        services.AddSingleton<Errors.IDomainErrorRegistration, IdempotencyDomainErrorRegistration>();

        services.PostConfigure<MvcOptions>(options =>
        {
            // ServiceFilterAttribute resolve o IdempotencyFilter do request
            // scope a cada request — honra explicitamente o lifetime Scoped
            // registrado acima e o IIdempotencyStore Scoped que ele consome.
            // Filter aplicado globalmente; ele próprio retorna early se a
            // action não tiver [RequiresIdempotencyKey].
            options.Filters.Add(new ServiceFilterAttribute(typeof(IdempotencyFilter)));
        });

        return services;
    }
}
