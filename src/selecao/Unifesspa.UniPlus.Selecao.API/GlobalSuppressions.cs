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

// Os enums de permissão e suas extensões são consumidos pelos testes e pelo
// composition root fora deste assembly.
[assembly: SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "Enum de permissões e extensões são referenciados pelos testes e pelo host fora deste assembly.",
    Scope = "namespaceanddescendants",
    Target = "~N:Unifesspa.UniPlus.Selecao.API.Permissoes")]
