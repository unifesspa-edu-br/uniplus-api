namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Edicao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// As 21 operações de escrita da configuração que só o rascunho aceita, cada uma
/// com a chamada que reaplica o valor que o processo já tem (sucesso garantido sobre um processo
/// conforme) e, nas que verificam argumento antes da guarda, a chamada com o argumento nulo.
/// </summary>
internal sealed record OperacaoDeEdicao(
    string Nome,
    Func<ProcessoSeletivo, Result> Reaplicar,
    Func<ProcessoSeletivo, Result>? ComArgumentoInvalido = null)
{
    private const FinalidadeFormulario Inscricao = FinalidadeFormulario.Inscricao;

    internal static readonly IReadOnlyList<OperacaoDeEdicao> Todas =
    [
        new(nameof(ProcessoSeletivo.DefinirOpcoesDeclaradas),
            p => p.DefinirOpcoesDeclaradas("FATO_DE_TESTE", [OpcaoDeclaradaFato.Criar("FATO_DE_TESTE", "A", "A", 0).Value!]),
            p => p.DefinirOpcoesDeclaradas("FATO_DE_TESTE", null!)),
        new(nameof(ProcessoSeletivo.DefinirEtapas),
            p => p.DefinirEtapas([.. p.Etapas]),
            p => p.DefinirEtapas(null!)),
        new(nameof(ProcessoSeletivo.DefinirAtendimentoEspecializado),
            p => p.DefinirAtendimentoEspecializado(p.AtendimentoEspecializado!),
            p => p.DefinirAtendimentoEspecializado(null!)),
        new(nameof(ProcessoSeletivo.DefinirDistribuicaoVagas),
            p => p.DefinirDistribuicaoVagas([.. p.DistribuicaoVagas], FatosDeModalidadeDeTeste.DoCatalogo),
            p => p.DefinirDistribuicaoVagas(null!, FatosDeModalidadeDeTeste.DoCatalogo)),
        new(nameof(ProcessoSeletivo.DefinirOpcoesDeCurso),
            p => p.DefinirOpcoesDeCurso(p.NumeroDeOpcoesDeCurso!.Value)),
        new(nameof(ProcessoSeletivo.DefinirBonusRegional),
            p => p.DefinirBonusRegional(p.AplicaBonusRegional!.Value, p.BonusRegional)),
        new(nameof(ProcessoSeletivo.DefinirConfiguracaoDivulgacao),
            p => p.DefinirConfiguracaoDivulgacao(p.ConfiguracaoDivulgacao)),
        new(nameof(ProcessoSeletivo.DefinirTaxaInscricao),
            p => p.DefinirTaxaInscricao(p.ConfiguracaoTaxaInscricao)),
        new(nameof(ProcessoSeletivo.DefinirCascataRemanejamento),
            p => p.DefinirCascataRemanejamento(p.Cascata)),
        new(nameof(ProcessoSeletivo.DefinirCriteriosDesempate),
            p => p.DefinirCriteriosDesempate([.. p.CriteriosDesempate]),
            p => p.DefinirCriteriosDesempate(null!)),
        new(nameof(ProcessoSeletivo.DefinirClassificacao),
            p => p.DefinirClassificacao(p.Classificacao!),
            p => p.DefinirClassificacao(null!)),
        new(nameof(ProcessoSeletivo.DefinirFormulario),
            p =>
            {
                FormularioProcesso formulario = p.Formularios.Single(f => f.Finalidade == Inscricao);
                return p.DefinirFormulario(Inscricao, formulario.FaseId, formulario.Titulo, [.. formulario.Etapas]);
            },
            p => p.DefinirFormulario(Inscricao, null, null, null!)),
        new(nameof(ProcessoSeletivo.DefinirTermosDoFormulario),
            p => p.DefinirTermosDoFormulario(Inscricao, []),
            p => p.DefinirTermosDoFormulario(Inscricao, null!)),
        new(nameof(ProcessoSeletivo.DefinirCronogramaFases),
            p => p.DefinirCronogramaFases([.. p.CronogramaFases], []),
            p => p.DefinirCronogramaFases(null!, [])),
        new(nameof(ProcessoSeletivo.DefinirDocumentosExigidos),
            p => p.DefinirDocumentosExigidos([.. p.RegrasDeComprovacaoSemGrupo]),
            p => p.DefinirDocumentosExigidos(null!)),
        new(nameof(ProcessoSeletivo.DefinirFatosColetados),
            p => p.DefinirFatosColetados(Inscricao, [.. p.FatosColetados.Where(static f => f.Finalidade == Inscricao)]),
            p => p.DefinirFatosColetados(Inscricao, null!)),
        new(nameof(ProcessoSeletivo.DefinirRegrasDerivacao),
            p => p.DefinirRegrasDerivacao([.. p.RegrasDerivacao]),
            p => p.DefinirRegrasDerivacao(null!)),
        new(nameof(ProcessoSeletivo.DefinirReferenciaTemporalFatos),
            p => p.DefinirReferenciaTemporalFatos(p.ReferenciaTemporalFatos)),
        new(nameof(ProcessoSeletivo.DefinirIdentificadorLegivel),
            p => p.DefinirIdentificadorLegivel(p.IdentificadorLegivel!.Value),
            p => p.DefinirIdentificadorLegivel(default)),
        new(nameof(ProcessoSeletivo.DefinirLocalidade),
            p => p.DefinirLocalidade(p.Localidade)),
        new(nameof(ProcessoSeletivo.DefinirAlgoritmoContagemPrazo),
            p => p.DefinirAlgoritmoContagemPrazo(
                ProcessoConformeFactory.Regra(AlgoritmoContagemPrazoCodigo.AvancaDataUtil, "d"))),
    ];

    internal static OperacaoDeEdicao Por(string nome) => Todas.Single(o => o.Nome == nome);
}
