namespace Unifesspa.UniPlus.Configuracao.API;

using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Configuracao.Contracts;

using Wolverine;

/// <summary>
/// Opt-ins de codegen Wolverine do módulo Configuração (ADR-0098): portas de implementação interna
/// que o código gerado resolve do contêiner, sob <c>ServiceLocationPolicy.NotAllowed</c>. A unidade
/// de trabalho é declarada uma vez, em <c>MonolitoCodegenRegistration</c>. Composto pelo host no
/// <c>configureRouting</c>; o helper compartilhado permanece agnóstico dos tipos do módulo.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Referenciado pelo composition root (host do monólito modular) fora deste assembly.")]
public static class ConfiguracaoCodegenRegistration
{
    /// <summary>
    /// Aplica os opt-ins de codegen da Configuração ao <paramref name="opts"/>.
    /// </summary>
    public static void ConfigurarCodegenWolverine(WolverineOptions opts)
    {
        ArgumentNullException.ThrowIfNull(opts);

        // O leitor do catálogo de fatos é injetado no handler de query e sua
        // implementação é internal — o codegen do Wolverine não a instancia
        // diretamente, então o service location é o opt-in sancionado (ADR-0098),
        // mesmo mecanismo dos readers cross-módulo consumidos pela Seleção.
        opts.CodeGeneration.AlwaysUseServiceLocationFor<IFatoCandidatoReader>();

        // A escrita do modelo de formulário confere os termos e o tipo de processo pelos mesmos
        // leitores que a Seleção usa, também internal.
        opts.CodeGeneration.AlwaysUseServiceLocationFor<ITermoConsentimentoReader>();
        opts.CodeGeneration.AlwaysUseServiceLocationFor<ITipoProcessoReader>();

        // O verificador de acesso e a leitura dos titulares têm implementação interna, que o código gerado não
        // consegue construir de outro assembly (ADR-0098).
        opts.CodeGeneration.AlwaysUseServiceLocationFor<IVerificadorDeAcesso>();
        opts.CodeGeneration.AlwaysUseServiceLocationFor<ITitularesDaPermissao>();
    }
}
