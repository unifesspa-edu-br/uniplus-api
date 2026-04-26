using System.Diagnostics.CodeAnalysis;

// Program is the ASP.NET Core entry point. It must be public so
// Microsoft.AspNetCore.Mvc.Testing's WebApplicationFactory<Program>
// can target it from the integration test project. CA1515 (sealed or
// internal top-level type) does not apply to this framework contract.
[assembly: SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "Program is the entry point referenced by WebApplicationFactory<Program> in integration tests.",
    Scope = "type",
    Target = "~T:Program")]

// IngressoApiAssemblyMarker é referenciado pelo projeto Unifesspa.UniPlus.ArchTests
// como âncora typeof().Assembly para o R4 (SolutionNaoTemMediatR). Top-level
// statements deixam Program no namespace global, e ambos os entry points
// (Selecao.API/Program e Ingresso.API/Program) compartilham o mesmo nome —
// um marker dedicado evita a ambiguidade no carregador de assemblies.
[assembly: SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "Marker público referenciado por Unifesspa.UniPlus.ArchTests para carregar este assembly inequivocamente.",
    Scope = "type",
    Target = "~T:Unifesspa.UniPlus.Ingresso.API.IngressoApiAssemblyMarker")]
