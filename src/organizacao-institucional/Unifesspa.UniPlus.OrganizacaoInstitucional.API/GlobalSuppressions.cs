using System.Diagnostics.CodeAnalysis;

// OrganizacaoApiAssemblyMarker é referenciado pelo projeto Unifesspa.UniPlus.ArchTests
// como âncora typeof().Assembly para regras de solution. Top-level statements deixam
// Program no namespace global e os 4 entry points (Selecao/Ingresso/Portal/Organizacao)
// compartilham o mesmo nome — um marker dedicado evita a ambiguidade.
[assembly: SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "Marker público referenciado por Unifesspa.UniPlus.ArchTests para carregar este assembly inequivocamente.",
    Scope = "type",
    Target = "~T:Unifesspa.UniPlus.OrganizacaoInstitucional.API.OrganizacaoApiAssemblyMarker")]
