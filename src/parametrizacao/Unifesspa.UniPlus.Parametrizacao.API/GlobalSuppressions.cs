using System.Diagnostics.CodeAnalysis;

// Program é o entry point ASP.NET Core; precisa ser public para
// WebApplicationFactory<Program> dos integration tests futuros.
[assembly: SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "Program é o entry point referenciado por WebApplicationFactory<Program>.",
    Scope = "type",
    Target = "~T:Program")]

// ParametrizacaoApiAssemblyMarker é âncora para carregadores de assembly
// (ArchUnitNET). Top-level statements deixam Program no namespace global e
// os 5 entry points compartilham o nome — marker dedicado evita ambiguidade.
[assembly: SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "Marker público referenciado por Unifesspa.UniPlus.ArchTests.",
    Scope = "type",
    Target = "~T:Unifesspa.UniPlus.Parametrizacao.API.ParametrizacaoApiAssemblyMarker")]
