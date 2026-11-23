namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O processo mínimo que as provas de fidelidade do envelope compartilham: atendimento especializado
/// vazia, uma distribuição de vagas, a classificação importada, uma fase única com uma etapa, o
/// formulário, a cobrança de taxa declarada e as declarações obrigatórias.
/// </summary>
internal static class ProcessoBaseDeEnvelope
{
    public static ProcessoSeletivo Montar(string titulo, FaseCronograma fase)
    {
        ProcessoSeletivo processo = ProcessoSemConfiguracao(titulo);
        processo.DefinirAtendimentoEspecializado(
            AtendimentoEspecializado.Criar([], [], []).Value!).IsSuccess.Should().BeTrue();
        DefinirDistribuicaoDaModalidadeAmpla(processo);
        DefinirClassificacaoImportada(processo);
        processo.DefinirCronogramaFases([fase], []).IsSuccess.Should().BeTrue();
        DefinirEtapaDeProvaObjetiva(processo, fase.Codigo);
        DefinirTaxaEDeclaracoesObrigatorias(processo);
        return processo;
    }

    private static ProcessoSeletivo ProcessoSemConfiguracao(string titulo) =>
        ProcessoSeletivo.Criar(
            titulo, TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!,
            identificadorLegivel: IdentificadoresDeTeste.Novo());

    private static void DefinirDistribuicaoDaModalidadeAmpla(ProcessoSeletivo processo)
    {
        ConfiguracaoDistribuicaoVagas distribuicao = ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 40, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = CorpusEnvelope.Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a'), Modalidades = [ModalidadeAmpla()] },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;
        processo.DefinirDistribuicaoVagas([distribuicao], FatosDeModalidadeDeTeste.DoCatalogo)
            .IsSuccess.Should().BeTrue();
    }

    private static ModalidadeSelecionada ModalidadeAmpla() =>
        ModalidadeSelecionada.Criar(
            modalidadeOrigemId: Guid.CreateVersion7(),
            codigo: "AC",
            descricao: null,
            naturezaLegal: NaturezaLegalModalidade.Ampla,
            composicaoVagas: ComposicaoVagasModalidade.RestanteDasVagasOfertadas,
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
            ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
            {
                RegraCalculo = CorpusEnvelope.Regra(RegraCalculoCodigo.ClassificacaoImportada, 'b'),
                RegraOrdemAlocacao = CorpusEnvelope.Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'c'),
            }).Value!).IsSuccess.Should().BeTrue();

    /// <summary>A fase única do processo mínimo: sem bancas e sem recurso, com um produto definitivo.</summary>
    public static FaseCronograma FaseUnica(string codigo, string produtoCodigo, bool permiteComplementacao) =>
        FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
            new OrigemCanonicaDaFase(
                OrigemId: Guid.CreateVersion7(), Codigo: codigo, OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: permiteComplementacao, ColetaInscricao: true,
                ColetaSolicitacaoIsencao: false),
            new DecisoesDeCronogramaDaFase(
                Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), UnidadeResponsavel: UnidadesDeTeste.Ceps),
            HomologacaoDasInscricoesDeTeste.PublicacoesDaFaseQueHomologa(resultado: produtoCodigo),
            bancasRequeridas: [BancasDeTeste.DaHomologacaoDasInscricoesDaFase()]).Value!;

    private static void DefinirEtapaDeProvaObjetiva(ProcessoSeletivo processo, string faseCodigo) =>
        processo.DefinirEtapas([
            EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
                new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(new Guid("019fee1e-7000-7000-8000-000000000001"), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: faseCodigo),
                new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!,
        ]).IsSuccess.Should().BeTrue();

    private static void DefinirTaxaEDeclaracoesObrigatorias(ProcessoSeletivo processo)
    {
        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!).IsSuccess.Should().BeTrue();
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);
    }
}
