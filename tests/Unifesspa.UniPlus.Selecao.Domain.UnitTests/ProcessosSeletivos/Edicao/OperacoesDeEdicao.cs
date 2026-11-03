namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Edicao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// As 21 operações de escrita da configuração que passam pelo protocolo comum de edição, cada uma
/// com a chamada que reaplica o valor que o processo já tem (sucesso garantido sobre um processo
/// conforme) e, nas que verificam argumento antes da guarda, a chamada com o argumento nulo.
/// </summary>
internal sealed record OperacaoDeEdicao(
    string Nome,
    Func<ProcessoSeletivo, PrecondicaoIfMatch, Result> Reaplicar,
    Func<ProcessoSeletivo, PrecondicaoIfMatch, Result>? ComArgumentoInvalido = null)
{
    private const FinalidadeFormulario Inscricao = FinalidadeFormulario.Inscricao;

    internal static readonly IReadOnlyList<OperacaoDeEdicao> Todas =
    [
        new(nameof(ProcessoSeletivo.DefinirOpcoesDeclaradas),
            (p, pre) => p.DefinirOpcoesDeclaradas("FATO_DE_TESTE", [OpcaoDeclaradaFato.Criar("FATO_DE_TESTE", "A", "A", 0).Value!], pre),
            (p, pre) => p.DefinirOpcoesDeclaradas("FATO_DE_TESTE", null!, pre)),
        new(nameof(ProcessoSeletivo.DefinirEtapas),
            (p, pre) => p.DefinirEtapas([.. p.Etapas], pre),
            (p, pre) => p.DefinirEtapas(null!, pre)),
        new(nameof(ProcessoSeletivo.DefinirOfertaAtendimento),
            (p, pre) => p.DefinirOfertaAtendimento(p.OfertaAtendimento!, pre),
            (p, pre) => p.DefinirOfertaAtendimento(null!, pre)),
        new(nameof(ProcessoSeletivo.DefinirDistribuicaoVagas),
            (p, pre) => p.DefinirDistribuicaoVagas([.. p.DistribuicaoVagas], FatosDeModalidadeDeTeste.DoCatalogo, pre),
            (p, pre) => p.DefinirDistribuicaoVagas(null!, FatosDeModalidadeDeTeste.DoCatalogo, pre)),
        new(nameof(ProcessoSeletivo.DefinirOpcoesDeCurso),
            (p, pre) => p.DefinirOpcoesDeCurso(p.NumeroDeOpcoesDeCurso!.Value, pre)),
        new(nameof(ProcessoSeletivo.DefinirBonusRegional),
            (p, pre) => p.DefinirBonusRegional(p.AplicaBonusRegional!.Value, p.BonusRegional, pre)),
        new(nameof(ProcessoSeletivo.DefinirConfiguracaoDivulgacao),
            (p, pre) => p.DefinirConfiguracaoDivulgacao(p.ConfiguracaoDivulgacao, pre)),
        new(nameof(ProcessoSeletivo.DefinirTaxaInscricao),
            (p, pre) => p.DefinirTaxaInscricao(p.ConfiguracaoTaxaInscricao, pre)),
        new(nameof(ProcessoSeletivo.DefinirCascataRemanejamento),
            (p, pre) => p.DefinirCascataRemanejamento(p.Cascata, pre)),
        new(nameof(ProcessoSeletivo.DefinirCriteriosDesempate),
            (p, pre) => p.DefinirCriteriosDesempate([.. p.CriteriosDesempate], pre),
            (p, pre) => p.DefinirCriteriosDesempate(null!, pre)),
        new(nameof(ProcessoSeletivo.DefinirClassificacao),
            (p, pre) => p.DefinirClassificacao(p.Classificacao!, pre),
            (p, pre) => p.DefinirClassificacao(null!, pre)),
        new(nameof(ProcessoSeletivo.DefinirFormulario),
            (p, pre) =>
            {
                FormularioProcesso formulario = p.Formularios.Single(f => f.Finalidade == Inscricao);
                return p.DefinirFormulario(Inscricao, formulario.FaseId, formulario.Titulo, [.. formulario.Etapas], pre);
            },
            (p, pre) => p.DefinirFormulario(Inscricao, null, null, null!, pre)),
        new(nameof(ProcessoSeletivo.DefinirTermosDoFormulario),
            (p, pre) => p.DefinirTermosDoFormulario(Inscricao, [], pre),
            (p, pre) => p.DefinirTermosDoFormulario(Inscricao, null!, pre)),
        new(nameof(ProcessoSeletivo.DefinirCronogramaFases),
            (p, pre) => p.DefinirCronogramaFases([.. p.CronogramaFases], [], pre),
            (p, pre) => p.DefinirCronogramaFases(null!, [], pre)),
        new(nameof(ProcessoSeletivo.DefinirDocumentosExigidos),
            (p, pre) => p.DefinirDocumentosExigidos([.. p.RaizesDeExigencia], pre),
            (p, pre) => p.DefinirDocumentosExigidos(null!, pre)),
        new(nameof(ProcessoSeletivo.DefinirFatosColetados),
            (p, pre) => p.DefinirFatosColetados(Inscricao, [.. p.FatosColetados.Where(static f => f.Finalidade == Inscricao)], pre),
            (p, pre) => p.DefinirFatosColetados(Inscricao, null!, pre)),
        new(nameof(ProcessoSeletivo.DefinirRegrasDerivacao),
            (p, pre) => p.DefinirRegrasDerivacao([.. p.RegrasDerivacao], pre),
            (p, pre) => p.DefinirRegrasDerivacao(null!, pre)),
        new(nameof(ProcessoSeletivo.DefinirReferenciaTemporalFatos),
            (p, pre) => p.DefinirReferenciaTemporalFatos(p.ReferenciaTemporalFatos, pre)),
        new(nameof(ProcessoSeletivo.DefinirIdentificadorLegivel),
            (p, pre) => p.DefinirIdentificadorLegivel(p.IdentificadorLegivel!.Value, pre),
            (p, pre) => p.DefinirIdentificadorLegivel(default, pre)),
        new(nameof(ProcessoSeletivo.DefinirLocalidade),
            (p, pre) => p.DefinirLocalidade(p.Localidade, pre)),
        new(nameof(ProcessoSeletivo.DefinirAlgoritmoContagemPrazo),
            (p, pre) => p.DefinirAlgoritmoContagemPrazo(
                ProcessoConformeFactory.Regra(AlgoritmoContagemPrazoCodigo.AvancaDataUtil, "d"), pre)),
    ];

    internal static OperacaoDeEdicao Por(string nome) => Todas.Single(o => o.Nome == nome);
}
