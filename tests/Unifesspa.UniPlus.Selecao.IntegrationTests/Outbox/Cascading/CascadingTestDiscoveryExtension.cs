namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Diagnostics.CodeAnalysis;

using Wolverine;

/// <summary>
/// <see cref="IWolverineExtension"/> que adiciona este assembly de testes à
/// discovery do Wolverine. Sem isto, os handlers convencionais
/// (<see cref="PublicarEditalCascadingHandler"/>,
/// <see cref="FalharAposSaveChangesCascadingHandler"/>,
/// <see cref="EditalPublicadoSubscriberHandler"/>) não são registrados —
/// o Program.cs produtivo só inclui o entry assembly do API e seus
/// referenciados, e o assembly de testes não está nessa cadeia.
///
/// Aplicada via <c>[assembly: WolverineModule&lt;T&gt;]</c> em
/// <see cref="OutboxCascadingAssemblyInfo"/>.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "Tipo referenciado por [WolverineModule<T>] do AssemblyInfo do projeto de testes.")]
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciado por Wolverine via [WolverineModule<T>] do AssemblyInfo do projeto de testes.")]
public sealed class CascadingTestDiscoveryExtension : IWolverineExtension
{
    public void Configure(WolverineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Discovery.IncludeAssembly(typeof(CascadingTestDiscoveryExtension).Assembly);
    }
}
