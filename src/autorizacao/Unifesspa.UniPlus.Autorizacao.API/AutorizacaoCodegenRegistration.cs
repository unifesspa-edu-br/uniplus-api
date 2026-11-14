namespace Unifesspa.UniPlus.Autorizacao.API;

using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Janelas;

using Wolverine;

/// <summary>
/// Opt-ins de codegen Wolverine do módulo Autorização (ADR-0098): portas registradas por fábrica ou de
/// implementação interna, que o código gerado resolve do contêiner sob <c>ServiceLocationPolicy.NotAllowed</c>.
/// A unidade de trabalho é declarada uma vez, em <c>MonolitoCodegenRegistration</c>. Composto pelo host no
/// <c>configureRouting</c>; o helper compartilhado permanece agnóstico dos tipos do módulo.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Referenciado pelo composition root (host do monólito modular) fora deste assembly.")]
public static class AutorizacaoCodegenRegistration
{
    /// <summary>Aplica os opt-ins de codegen da Autorização ao <paramref name="opts"/>.</summary>
    public static void ConfigurarCodegenWolverine(WolverineOptions opts)
    {
        ArgumentNullException.ThrowIfNull(opts);

        // O leitor de unidades é registrado por fábrica, sobre um concreto interno (ver AddAutorizacaoInfrastructure).
        opts.CodeGeneration.AlwaysUseServiceLocationFor<ILeitorDeUnidade>();

        // O armazenamento do PDF de designação é registrado por fábrica (credencial própria derivada das opções),
        // opaca ao codegen.
        opts.CodeGeneration.AlwaysUseServiceLocationFor<IArmazenamentoDosDocumentosDaAutorizacao>();

        // A materialização prévia abre um escopo novo para enviar o comando em transação própria: depende da fábrica de
        // escopos do contêiner, que o codegen não enxerga.
        opts.CodeGeneration.AlwaysUseServiceLocationFor<MaterializacaoPrevia>();
    }
}
