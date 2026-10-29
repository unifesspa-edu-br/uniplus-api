namespace Unifesspa.UniPlus.Autorizacao.API;

using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;

using Wolverine;

/// <summary>
/// Opt-ins de codegen Wolverine do módulo Autorização (ADR-0098). Declara que
/// <see cref="IAutorizacaoUnitOfWork"/> usa service location <em>intencionalmente</em> sob
/// <c>ServiceLocationPolicy.NotAllowed</c>.
/// </summary>
/// <remarks>
/// A UoW encaminha para a MESMA instância do <c>AutorizacaoDbContext</c>, registrada como
/// lambda Scoped opaca ao codegen. Registrar a UoW direto sobre o DbContext criaria uma
/// segunda instância por escopo e quebraria a atomicidade write+evento do outbox (ADR-0004).
/// Composto pelo host no <c>configureRouting</c>; o helper compartilhado permanece agnóstico
/// dos tipos do módulo.
/// </remarks>
[SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Referenciado pelo composition root (host do monólito modular) fora deste assembly.")]
public static class AutorizacaoCodegenRegistration
{
    /// <summary>Aplica os opt-ins de codegen da Autorização ao <paramref name="opts"/>.</summary>
    public static void ConfigurarCodegenWolverine(WolverineOptions opts)
    {
        ArgumentNullException.ThrowIfNull(opts);

        opts.CodeGeneration.AlwaysUseServiceLocationFor<IAutorizacaoUnitOfWork>();
    }
}
