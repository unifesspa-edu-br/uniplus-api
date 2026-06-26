namespace Unifesspa.UniPlus.Geo.API;

using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.Infrastructure.Core.Caching;

using Wolverine;

/// <summary>
/// Opt-ins de codegen Wolverine do host Geo (ADR-0098). Declara o único tipo que
/// usa service location <em>intencionalmente</em> sob
/// <c>ServiceLocationPolicy.NotAllowed</c>: <c>Lazy&lt;ICacheService&gt;</c>.
/// </summary>
/// <remarks>
/// <para>Os readers do Geo (<c>EstadoReader</c>, <c>CepResolver</c>,
/// <c>CepReader</c> etc.) eram concretos <c>internal</c> e foram corrigidos na RAIZ
/// (tornados <c>public</c>) para que o codegen os construa inline. Ao construir o
/// <c>CepResolver</c> inline, porém, o codegen alcança <c>Lazy&lt;ICacheService&gt;</c> —
/// registrado como <c>AddScoped(sp =&gt; new Lazy&lt;ICacheService&gt;(...))</c> para
/// DIFERIR o connect do Redis ao 1º uso (cache-aside do lookup de CEP,
/// ADR-0090/0092). Esse <c>Lazy&lt;T&gt;</c> é uma lambda factory inerentemente opaca
/// — não há forma <c>AddScoped&lt;Lazy&lt;ICacheService&gt;, TImpl&gt;()</c> equivalente —,
/// então o root fix não se aplica e usa-se <c>AlwaysUseServiceLocationFor&lt;T&gt;()</c>,
/// o mecanismo sancionado pela doc do Wolverine para registros opacos.</para>
/// <para>A UoW base do Geo (<c>IUnitOfWork</c>) não é injetada em handler algum
/// (cargas ETL rodam em hosted services), por isso não precisa de opt-in.</para>
/// </remarks>
[SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Referenciado pelo composition root (Program.cs do host Geo).")]
public static class GeoCodegenRegistration
{
    /// <summary>
    /// Aplica os opt-ins de codegen do host Geo ao <paramref name="opts"/>.
    /// </summary>
    public static void ConfigurarCodegenWolverine(WolverineOptions opts)
    {
        ArgumentNullException.ThrowIfNull(opts);

        opts.CodeGeneration.AlwaysUseServiceLocationFor<Lazy<ICacheService>>();
    }
}
