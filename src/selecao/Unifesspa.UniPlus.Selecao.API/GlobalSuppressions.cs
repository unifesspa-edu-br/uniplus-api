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
