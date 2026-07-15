namespace Unifesspa.UniPlus.Configuracao.API;

using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Contracts;

using Wolverine;

/// <summary>
/// Opt-ins de codegen Wolverine do módulo Configuração (ADR-0098). Declara que
/// <see cref="IConfiguracaoUnitOfWork"/> usa service location <em>intencionalmente</em>
/// sob a política <c>ServiceLocationPolicy.NotAllowed</c> (forward-compat com o
/// default do Wolverine 6.0).
/// </summary>
/// <remarks>
/// <para>A UoW encaminha (forwarding) para a MESMA instância do
/// <c>ConfiguracaoDbContext</c> — registrada como lambda Scoped opaca ao codegen.
/// O root fix <c>AddScoped&lt;IConfiguracaoUnitOfWork, ConfiguracaoDbContext&gt;()</c>
/// é PROIBIDO: criaria uma 2ª instância de DbContext por escopo e quebraria a
/// atomicidade write+evento do outbox (ADR-0004). Usa-se
/// <c>AlwaysUseServiceLocationFor&lt;T&gt;()</c>, o mecanismo sancionado pela doc do
/// Wolverine para forwarding/EF Core DbContext.</para>
/// <para>Composto pelo host no <c>configureRouting</c>; o helper compartilhado
/// permanece agnóstico dos tipos do módulo (Clean Arch).</para>
/// </remarks>
[SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Referenciado pelo composition root (host do monólito modular) fora deste assembly.")]
public static class ConfiguracaoCodegenRegistration
{
    /// <summary>
    /// Aplica os opt-ins de codegen da Configuração ao <paramref name="opts"/>.
    /// </summary>
    public static void ConfigurarCodegenWolverine(WolverineOptions opts)
    {
        ArgumentNullException.ThrowIfNull(opts);

        opts.CodeGeneration.AlwaysUseServiceLocationFor<IConfiguracaoUnitOfWork>();

        // O leitor do catálogo de fatos é injetado no handler de query e sua
        // implementação é internal — o codegen do Wolverine não a instancia
        // diretamente, então o service location é o opt-in sancionado (ADR-0098),
        // mesmo mecanismo dos readers cross-módulo consumidos pela Seleção.
        opts.CodeGeneration.AlwaysUseServiceLocationFor<IFatoCandidatoReader>();
    }
}
