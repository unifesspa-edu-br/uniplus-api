using System.Diagnostics.CodeAnalysis;

// AutorizacaoApiAssemblyMarker é referenciado pelo projeto Unifesspa.UniPlus.ArchTests como âncora
// typeof().Assembly para regras de solution, e pelas suítes de integração para carregar este
// assembly sem ambiguidade com o entry point do host.
[assembly: SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "Marker público referenciado por Unifesspa.UniPlus.ArchTests para carregar este assembly inequivocamente.",
    Scope = "type",
    Target = "~T:Unifesspa.UniPlus.Autorizacao.API.AutorizacaoApiAssemblyMarker")]
