namespace Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Idempotency;

/// <summary>
/// Registra o stack de Idempotency-Key (ADR-0027) de um módulo: options + store
/// EF (<see cref="EfCoreIdempotencyStore{TDbContext}"/>) adjacente ao DbContext
/// do módulo + uma convention que aplica o <see cref="IdempotencyFilter{TDbContext}"/>
/// aos controllers desse módulo.
/// </summary>
/// <remarks>
/// <para><b>Module-aware (monólito modular):</b> o filtro é aplicado por
/// controller do assembly do módulo via <see cref="IdempotencyControllerConvention{TDbContext}"/>,
/// não como filtro global. Quando vários módulos são co-hospedados num processo
/// único, cada controller recebe exatamente um filtro — o do seu módulo, fechado
/// em <typeparamref name="TDbContext"/> e ligado à tabela <c>idempotency_cache</c>
/// do schema correto. O store é registrado como tipo concreto
/// <see cref="EfCoreIdempotencyStore{TDbContext}"/> (não <c>IIdempotencyStore</c>):
/// sem registro não-keyed da interface, não há como o co-hosting cair no
/// "último registro vence".</para>
/// <para>Services compartilhados (TimeProvider, registro de erros de domínio)
/// usam <c>TryAdd*</c> — idempotentes quando N módulos chamam este método no
/// mesmo container.</para>
/// </remarks>
/// <typeparam name="TDbContext">DbContext do módulo que hospeda a tabela de idempotência.</typeparam>
/// <typeparam name="TApiMarker">
/// Qualquer tipo do assembly <c>.API</c> do módulo — usado para descobrir os
/// controllers aos quais o filtro se aplica. Tipá-lo (em vez de receber um
/// <c>Assembly</c>) evita passar o assembly errado por engano.
/// </typeparam>
public static class IdempotencyServiceCollectionExtensions
{
    public static IServiceCollection AddIdempotency<TDbContext, TApiMarker>(
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

        // Store concreto por módulo. Deliberadamente NÃO registramos
        // IIdempotencyStore (interface) no container: o filtro genérico recebe o
        // tipo fechado EfCoreIdempotencyStore<TDbContext>, eliminando a
        // ambiguidade de "último registro vence" no co-hosting.
        services.AddScoped<EfCoreIdempotencyStore<TDbContext>>();

        // TryAddEnumerable: cada módulo contribui o MESMO registro de erros; sem
        // o guard, N módulos inflariam o IEnumerable<IDomainErrorRegistration>
        // com duplicatas do mesmo código.
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<Errors.IDomainErrorRegistration, IdempotencyDomainErrorRegistration>());

        // Aplica o filtro apenas aos controllers do assembly do módulo (não
        // global), fechado em TDbContext. Idempotente por construção (a
        // convention não readiciona se o controller já tem o filtro).
        services.Configure<MvcOptions>(options =>
            options.Conventions.Add(
                new IdempotencyControllerConvention<TDbContext>(typeof(TApiMarker).Assembly)));

        return services;
    }
}
