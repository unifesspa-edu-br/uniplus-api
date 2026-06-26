namespace Unifesspa.UniPlus.OrganizacaoInstitucional.API;

using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Abstractions;

using Wolverine;

/// <summary>
/// Opt-ins de codegen Wolverine do módulo Organização Institucional (ADR-0098).
/// Declara que <see cref="IOrganizacaoInstitucionalUnitOfWork"/> usa service
/// location <em>intencionalmente</em> sob a política
/// <c>ServiceLocationPolicy.NotAllowed</c> (forward-compat com o default do
/// Wolverine 6.0).
/// </summary>
/// <remarks>
/// <para>A UoW encaminha (forwarding) para a MESMA instância do
/// <c>OrganizacaoInstitucionalDbContext</c> — registrada como lambda Scoped opaca
/// ao codegen. O root fix <c>AddScoped&lt;IOrganizacaoInstitucionalUnitOfWork,
/// OrganizacaoInstitucionalDbContext&gt;()</c> é PROIBIDO: criaria uma 2ª instância
/// de DbContext por escopo e quebraria a atomicidade write+evento do outbox
/// (ADR-0004). Usa-se <c>AlwaysUseServiceLocationFor&lt;T&gt;()</c>, o mecanismo
/// sancionado pela doc do Wolverine para forwarding/EF Core DbContext.</para>
/// <para>Os cache invalidators do módulo (<c>InstituicaoCacheInvalidator</c>,
/// <c>UnidadeCacheInvalidator</c>), que também disparavam service location por
/// serem concretos <c>internal</c>, foram corrigidos na RAIZ (tornados
/// <c>public</c>) — não precisam de opt-in.</para>
/// <para>Composto pelo host no <c>configureRouting</c>; o helper compartilhado
/// permanece agnóstico dos tipos do módulo (Clean Arch).</para>
/// </remarks>
[SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Referenciado pelo composition root (host do monólito modular) fora deste assembly.")]
public static class OrganizacaoInstitucionalCodegenRegistration
{
    /// <summary>
    /// Aplica os opt-ins de codegen da Organização Institucional ao <paramref name="opts"/>.
    /// </summary>
    public static void ConfigurarCodegenWolverine(WolverineOptions opts)
    {
        ArgumentNullException.ThrowIfNull(opts);

        opts.CodeGeneration.AlwaysUseServiceLocationFor<IOrganizacaoInstitucionalUnitOfWork>();
    }
}
