using System.Diagnostics.CodeAnalysis;

// SelecaoApiAssemblyMarker é referenciado por carregadores de assembly
// cross-projeto (ArchUnitNET em Unifesspa.UniPlus.ArchTests, fitness tests
// que usam WebApplicationFactory<SelecaoApiAssemblyMarker>). Precisa ser
// public por mesma razão de Program.
[assembly: SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "SelecaoApiAssemblyMarker é referenciado pelo projeto Unifesspa.UniPlus.ArchTests para load do assembly.",
    Scope = "type",
    Target = "~T:Unifesspa.UniPlus.Selecao.API.SelecaoApiAssemblyMarker")]
