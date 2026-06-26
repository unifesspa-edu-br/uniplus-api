using System.Diagnostics.CodeAnalysis;

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
