namespace Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

using Idempotency;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
/// <para>O registro de erros de domínio usa <c>TryAdd*</c> — idempotente quando N
/// módulos chamam este método no mesmo container. O <see cref="TimeProvider"/> é o
/// registrado pelo host: esta extensão não registra relógio.</para>
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

        RegistrarOpcoes(services, configuration);
        RegistrarArmazenamento<TDbContext>(services);
        AplicarFiltroAosControllersDoModulo<TDbContext, TApiMarker>(services);

        return services;
    }

    private static void RegistrarOpcoes(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<IdempotencyOptions>()
            .Bind(configuration.GetSection(IdempotencyOptions.SectionName))
            .Validate(static o => o.Ttl > TimeSpan.Zero, "IdempotencyOptions.Ttl deve ser positivo.")
            .Validate(static o => o.MaxBodyBytes > 0, "IdempotencyOptions.MaxBodyBytes deve ser positivo.")
            .ValidateOnStart();
    }

    /// <summary>
    /// O store é concreto por módulo. A interface <c>IIdempotencyStore</c> não é registrada de propósito: o filtro
    /// genérico recebe o tipo fechado <see cref="EfCoreIdempotencyStore{TDbContext}"/>, o que elimina o "último
    /// registro vence" no co-hosting. O registro de erros usa <c>TryAddEnumerable</c>: cada módulo contribui o MESMO
    /// registro, e sem o guard N módulos duplicariam o código no <c>IEnumerable&lt;IDomainErrorRegistration&gt;</c>.
    /// </summary>
    private static void RegistrarArmazenamento<TDbContext>(IServiceCollection services)
        where TDbContext : DbContext
    {
        services.AddScoped<EfCoreIdempotencyStore<TDbContext>>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<Errors.IDomainErrorRegistration, IdempotencyDomainErrorRegistration>());
    }

    /// <summary>
    /// O filtro vale só para os controllers do assembly do módulo, e não global, fechado em
    /// <typeparamref name="TDbContext"/>. A convention é idempotente: não readiciona o filtro a um controller que já o
    /// tem.
    /// </summary>
    private static void AplicarFiltroAosControllersDoModulo<TDbContext, TApiMarker>(IServiceCollection services)
        where TDbContext : DbContext
    {
        services.Configure<MvcOptions>(options =>
            options.Conventions.Add(
                new IdempotencyControllerConvention<TDbContext>(typeof(TApiMarker).Assembly)));
    }
}
