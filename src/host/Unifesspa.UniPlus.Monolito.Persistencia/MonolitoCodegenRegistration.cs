namespace Unifesspa.UniPlus.Monolito.Persistencia;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

using Wolverine;

/// <summary>
/// Opt-in de codegen Wolverine da persistência do monólito (ADR-0098). Declara que <see cref="IUnitOfWork"/> usa service
/// location <em>intencionalmente</em>, sob a política <c>ServiceLocationPolicy.NotAllowed</c>.
/// </summary>
/// <remarks>
/// A unidade de trabalho encaminha para a MESMA instância do contexto, registrada como lambda scoped, opaca ao codegen. Registrá-la
/// por tipo (<c>AddScoped&lt;IUnitOfWork, UniPlusDbContext&gt;()</c>) criaria uma segunda instância por escopo e quebraria a
/// atomicidade entre a gravação e o outbox (ADR-0004). <c>AlwaysUseServiceLocationFor&lt;T&gt;()</c> é o mecanismo sancionado
/// pelo Wolverine para esse caso, no lugar de afrouxar a política globalmente. É composto pelo host no <c>configureRouting</c>.
/// </remarks>
public static class MonolitoCodegenRegistration
{
    public static void ConfigurarCodegenWolverine(WolverineOptions opts)
    {
        ArgumentNullException.ThrowIfNull(opts);

        opts.CodeGeneration.AlwaysUseServiceLocationFor<IUnitOfWork>();
    }
}
