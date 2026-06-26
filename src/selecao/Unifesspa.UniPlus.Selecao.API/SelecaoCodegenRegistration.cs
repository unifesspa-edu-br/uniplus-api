namespace Unifesspa.UniPlus.Selecao.API;

using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;

using Wolverine;

/// <summary>
/// Opt-ins de codegen Wolverine do módulo Seleção (ADR-0098). Declara que
/// <see cref="ISelecaoUnitOfWork"/> usa service location <em>intencionalmente</em>
/// sob a política <c>ServiceLocationPolicy.NotAllowed</c> (forward-compat com o
/// default do Wolverine 6.0).
/// </summary>
/// <remarks>
/// <para>A UoW encaminha (forwarding) para a MESMA instância do
/// <c>SelecaoDbContext</c> — registrada como <c>AddScoped&lt;ISelecaoUnitOfWork&gt;(sp =&gt;
/// sp.GetRequiredService&lt;SelecaoDbContext&gt;())</c>, uma lambda Scoped opaca ao
/// codegen do Wolverine. O root fix preferido (<c>AddScoped&lt;ISelecaoUnitOfWork,
/// SelecaoDbContext&gt;()</c>) é PROIBIDO aqui: criaria uma 2ª instância de
/// <c>SelecaoDbContext</c> por escopo, fazendo o repositório escrever num contexto
/// e o <c>SaveChanges</c> da UoW commitar outro — quebrando a atomicidade
/// write+evento do outbox transacional (ADR-0004,
/// <c>UseEntityFrameworkCoreTransactions</c> + <c>AutoApplyTransactions</c> enrolam
/// o MESMO DbContext).</para>
/// <para>Por isso usa-se <c>AlwaysUseServiceLocationFor&lt;T&gt;()</c> — o mecanismo
/// sancionado pela doc do Wolverine (<see href="https://wolverinefx.net/guide/codegen.html"/>)
/// justamente para registros opacos/forwarding e EF Core DbContext, em vez de
/// afrouxar a política globalmente.</para>
/// <para>Composto pelo host (composition root) no <c>configureRouting</c> de
/// <c>UseWolverineOutboxCascading</c> — o helper compartilhado
/// <c>WolverineOutboxConfiguration</c> permanece agnóstico dos tipos de cada
/// módulo (Clean Arch: <c>Infrastructure.Core</c> não referencia a Application dos
/// módulos).</para>
/// </remarks>
[SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Referenciado pelo composition root (host do monólito modular) fora deste assembly.")]
public static class SelecaoCodegenRegistration
{
    /// <summary>
    /// Aplica os opt-ins de codegen do Seleção ao <paramref name="opts"/>.
    /// </summary>
    public static void ConfigurarCodegenWolverine(WolverineOptions opts)
    {
        ArgumentNullException.ThrowIfNull(opts);

        opts.CodeGeneration.AlwaysUseServiceLocationFor<ISelecaoUnitOfWork>();
    }
}
