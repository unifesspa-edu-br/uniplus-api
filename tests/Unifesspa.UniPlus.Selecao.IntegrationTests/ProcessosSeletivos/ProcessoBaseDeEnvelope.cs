namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O processo mínimo que as provas de fidelidade do envelope compartilham: oferta de atendimento
/// vazia, uma distribuição de vagas, a classificação importada, uma fase única com uma etapa, o
/// formulário, a cobrança de taxa declarada e as declarações obrigatórias.
/// </summary>
internal static class ProcessoBaseDeEnvelope
{
    public static ProcessoSeletivo Montar(string titulo, FaseCronograma fase)
    {
        ProcessoSeletivo processo = ProcessoSemConfiguracao(titulo);
        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        DefinirDistribuicaoDaModalidadeAmpla(processo);
        DefinirClassificacaoImportada(processo);
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        DefinirEtapaDeProvaObjetiva(processo, fase.Codigo);
        DefinirTaxaEDeclaracoesObrigatorias(processo);
        return processo;
    }

    private static ProcessoSeletivo ProcessoSemConfiguracao(string titulo) =>
        ProcessoSeletivo.Criar(
            titulo, TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!,
            identificadorLegivel: IdentificadoresDeTeste.Novo());

    private static void DefinirDistribuicaoDaModalidadeAmpla(ProcessoSeletivo processo)
    {
        ConfiguracaoDistribuicaoVagas distribuicao = ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoOrigemId: Guid.CreateVersion7(),
            voBase: 40,
            pr: 1m,
            regraDistribuicao: CorpusEnvelope.Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a'),
            regraAjuste: null,
            referenciaDemografica: null,
            modalidades: [ModalidadeAmpla()], ofertaCurso: OfertaCursoDeTeste.Medicina).Value!;
        processo.DefinirDistribuicaoVagas([distribuicao], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
    }

    private static ModalidadeSelecionada ModalidadeAmpla() =>
        ModalidadeSelecionada.Criar(
            modalidadeOrigemId: Guid.CreateVersion7(),
            codigo: "AC",
            descricao: null,
            naturezaLegal: NaturezaLegalModalidade.Ampla,
            composicaoVagas: ComposicaoVagasModalidade.ResidualDoVo,
            composicaoOrigemCodigo: null,
            regraRemanejamento: RegraRemanejamentoModalidade.Nenhuma,
            remanejamentoDestino: null,
            remanejamentoPar: null,
            remanejamentoFallback: null,
            regraDeAplicabilidade: RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla),
            acaoQuandoIndeferido: null,
            baseLegal: "Res. Unifesspa 532/2021",
            quantidadeDeclarada: 40).Value!;

    private static void DefinirClassificacaoImportada(ProcessoSeletivo processo) =>
        processo.DefinirClassificacao(
            ConfiguracaoClassificacao.Criar(
                regraCalculo: CorpusEnvelope.Regra(RegraCalculoCodigo.ClassificacaoImportada, 'b'),
                regraArredondamento: null,
                casasArredondamento: null,
                regraOrdemAlocacao: CorpusEnvelope.Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'c'),
                regrasEliminacao: [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: [], escalaNotaFinal: null).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

    /// <summary>A fase única do processo mínimo: sem bancas e sem recurso, com um produto definitivo.</summary>
    public static FaseCronograma FaseUnica(string codigo, string produtoCodigo, bool permiteComplementacao) =>
        FaseCronograma.Criar(
            ordem: 1, faseCanonicaOrigemId: Guid.CreateVersion7(), codigo: codigo, origemData: OrigemDataFase.NoEdital,
            agrupaEtapas: true, permiteComplementacao: permiteComplementacao, coletaInscricao: true, coletaSolicitacaoIsencao: false,
            inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            produtos: [ProdutoDaFase.Criar(produtoCodigo, PapelProdutoFase.Definitivo)],
            faseConcluinteCodigo: null, emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null,
            unidadeResponsavel: UnidadesDeTeste.Ceps).Value!;

    private static void DefinirEtapaDeProvaObjetiva(ProcessoSeletivo processo, string faseCodigo) =>
        processo.DefinirEtapas([
            EtapaProcesso.Criar(
                "Prova Objetiva", CaraterEtapa.Classificatoria,
                TipoEtapaSnapshot.Criar(new Guid("019fee1e-7000-7000-8000-000000000001"), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!,
                peso: 1m, ordem: 1, notaMaxima: 10m, formaDeAvaliacao: FormaDeAvaliacao.Objetiva, faseCodigo: faseCodigo).Value!,
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

    private static void DefinirTaxaEDeclaracoesObrigatorias(ProcessoSeletivo processo)
    {
        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);
    }
}
