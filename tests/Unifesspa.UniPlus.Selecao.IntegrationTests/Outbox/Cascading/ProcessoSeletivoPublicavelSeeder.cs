namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using AwesomeAssertions;

using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Monta e persiste um <see cref="ProcessoSeletivo"/> minimamente conforme
/// (CA-07: etapas, atendimento especializado, distribuição de vagas,
/// classificação) mais um <see cref="DocumentoAPublicar"/> já confirmado — o
/// par exigido por <c>PublicarProcessoSeletivoCommand</c> para publicar com
/// sucesso. Reusado pelos cenários de cascading (V8/V9) e pelos testes de
/// endpoint HTTP — todos exercitam a publicação real, não um agregado de
/// brinquedo (diferente do precedente <c>Edital</c>, cujo <c>Criar/Publicar</c>
/// trivial não existe mais no modelo atual).
/// </summary>
internal static class ProcessoSeletivoPublicavelSeeder
{
    // 64 caracteres hex minúsculos — shape de SHA-256 exigido por
    // HashCanonicalComputer.IsValidHashShape; valor arbitrário (não é
    // recomputado, só precisa satisfazer o formato).
    private static readonly string HashFixo = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];

    private static FaseCronograma FaseDeResultadoFinal(
        bool coletaSolicitacaoIsencao,
        DateTimeOffset? fimDasInscricoes,
        UnidadeResponsavelSnapshot? unidadeResponsavel,
        IReadOnlyList<ProdutoDaFase>? produtosDaFase) => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(
            OrigemId: Guid.CreateVersion7(), Codigo: "RESULTADO_FINAL", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: false, ColetaInscricao: true,
            ColetaSolicitacaoIsencao: coletaSolicitacaoIsencao),
        new DecisoesDeCronogramaDaFase(
            Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: fimDasInscricoes ?? new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), FaseConcluinteCodigo: null,
            UnidadeResponsavel: unidadeResponsavel ?? UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: produtosDaFase ?? [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    /// <param name="coletaSolicitacaoIsencao">
    /// Se a fase única também abre o pedido de isenção, dividindo a fase entre a inscrição e a isenção.
    /// </param>
    /// <param name="complementar">Configuração a mais do cenário, aplicada antes de gravar.</param>
    /// <param name="tipo">O tipo do processo; sem ele, SiSU.</param>
    /// <param name="produtosDaFase">O que a fase única declara publicar; sem isso, o resultado definitivo da classificação.</param>
    /// <param name="unidadeResponsavel">A unidade que responde pela fase única; sem ela, o CEPS de teste.</param>
    /// <param name="fimDasInscricoes">O fim do período de inscrição da fase única; sem ele, 31/01/2026.</param>
    public static async Task<(ProcessoSeletivo Processo, DocumentoAPublicar Documento)> SemearAsync(
        UniPlusDbContext db,
        string nome,
        bool coletaSolicitacaoIsencao = false,
        Action<ProcessoSeletivo>? complementar = null,
        TipoProcessoSnapshot? tipo = null,
        IReadOnlyList<ProdutoDaFase>? produtosDaFase = null,
        UnidadeResponsavelSnapshot? unidadeResponsavel = null,
        DateTimeOffset? fimDasInscricoes = null)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);

        ProcessoSeletivo processo = ProcessoSeletivo.Criar(nome, tipo ?? TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());

        Result atendimentoResult = processo.DefinirAtendimentoEspecializado(
            AtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente);
        atendimentoResult.IsSuccess.Should().BeTrue(atendimentoResult.Error?.Message);

        ReferenciaRegra regraDistribuicao = ReferenciaRegra.De(
            RegraDistribuicaoVagasCodigo.Institucional, "v1", HashFixo);
        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            modalidadeOrigemId: Guid.CreateVersion7(),
            codigo: "AC",
            descricao: "Ampla concorrência",
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
        Result<ConfiguracaoDistribuicaoVagas> distribuicaoResult = ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 40, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = regraDistribuicao, Modalidades = [modalidade] },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina });
        distribuicaoResult.IsSuccess.Should().BeTrue(distribuicaoResult.Error?.Message);
        Result distribuicaoDefinirResult = processo.DefinirDistribuicaoVagas([distribuicaoResult.Value!], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente);
        distribuicaoDefinirResult.IsSuccess.Should().BeTrue(distribuicaoDefinirResult.Error?.Message);

        ReferenciaRegra regraCalculo = ReferenciaRegra.De(
            RegraCalculoCodigo.ClassificacaoImportada, "v1", HashFixo);
        ReferenciaRegra regraOrdemAlocacao = ReferenciaRegra.De(
            RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "v1", HashFixo);
        Result<ConfiguracaoClassificacao> classificacaoResult = ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = regraCalculo,
            RegraOrdemAlocacao = regraOrdemAlocacao,
        });
        classificacaoResult.IsSuccess.Should().BeTrue(classificacaoResult.Error?.Message);
        Result classificacaoDefinirResult = processo.DefinirClassificacao(classificacaoResult.Value!, PrecondicaoIfMatch.Ausente);
        classificacaoDefinirResult.IsSuccess.Should().BeTrue(classificacaoDefinirResult.Error?.Message);

        FaseCronograma faseConforme = FaseDeResultadoFinal(coletaSolicitacaoIsencao, fimDasInscricoes, unidadeResponsavel, produtosDaFase);
        Result cronogramaResult = processo.DefinirCronogramaFases([faseConforme], [], PrecondicaoIfMatch.Ausente);
        Result etapasResult = processo.DefinirEtapas([
            EtapaProcesso.Criar(
                new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: "RESULTADO_FINAL"),
                new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!,
        ], PrecondicaoIfMatch.Ausente);
        etapasResult.IsSuccess.Should().BeTrue(etapasResult.Error?.Message);
        cronogramaResult.IsSuccess.Should().BeTrue(cronogramaResult.Error?.Message);
        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);

        // Issue #1112: publicar sem declarar cobrança de taxa é recusado (CA-01) — o seeder
        // declara explicitamente "não cobra" para continuar representando um processo publicável.
        Result<ConfiguracaoTaxaInscricao> taxaResult = ConfiguracaoTaxaInscricao.Criar(
            cobra: false, valor: null, fundamentosCodigos: null);
        taxaResult.IsSuccess.Should().BeTrue(taxaResult.Error?.Message);
        Result taxaDefinirResult = processo.DefinirTaxaInscricao(taxaResult.Value!, PrecondicaoIfMatch.Ausente);
        taxaDefinirResult.IsSuccess.Should().BeTrue(taxaDefinirResult.Error?.Message);
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);

        complementar?.Invoke(processo);

        await db.ProcessosSeletivos.AddAsync(processo);

        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(processo.Id, FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        Result confirmarResult = documento.Confirmar(1024, HashFixo, TimeProvider.System);
        confirmarResult.IsSuccess.Should().BeTrue(confirmarResult.Error?.Message);
        await db.DocumentosAPublicar.AddAsync(documento);

        await db.SaveChangesAsync();

        // A seed bypassa o handler produtivo — drena eventos residuais (não
        // deveria haver nenhum, já que Criar/Definir* não emitem, mas evita
        // vazamento silencioso se essa invariante mudar no futuro).
        processo.DequeueDomainEvents();

        return (processo, documento);
    }
}
