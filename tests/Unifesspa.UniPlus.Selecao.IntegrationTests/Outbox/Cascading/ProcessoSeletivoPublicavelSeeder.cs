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

    // O que a fase única declara decidir: os motivos escolhidos nela nos testes são deste tipo, e o motivo só vale no lugar que
    // declara o tipo dele.
    private static readonly string[] TiposDeDecisaoDaFaseUnica = [TipoMotivoDeDecisaoCodigo.HomologacaoDasInscricoes];

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
        new PublicacoesDaFase(
            Produtos: produtosDaFase ?? [ProdutoDaFase.Criar(HomologacaoDasInscricoesDeTeste.ResultadoDaFase, PapelProdutoFase.Definitivo)],
            EmiteParecerIndividual: false,
            RegraRecurso: null,
            TiposDeDecisao: TiposDeDecisaoDaFaseUnica,
            RegraDeHomologacao: HomologacaoDasInscricoesDeTeste.Regra(produtosDaFase is { Count: > 0 } ? produtosDaFase[0].AtoCodigo : HomologacaoDasInscricoesDeTeste.ResultadoDaFase)),
        bancasRequeridas: []).Value!;

    public static async Task<(ProcessoSeletivo Processo, DocumentoAPublicar Documento)> SemearAsync(
        UniPlusDbContext db, OpcoesDoProcessoSeletivoPublicavel opcoes)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(opcoes);
        ValidarNome(opcoes.Nome);
        ProcessoSeletivo processo = CriarProcesso(db, opcoes);
        DefinirAtendimentoEspecializado(processo);
        DefinirDistribuicaoDeVagas(processo);
        DefinirClassificacao(processo);
        DefinirCronograma(processo, opcoes);
        DefinirTaxaEDeclaracoes(processo);
        opcoes.Complementar?.Invoke(processo);
        DocumentoAPublicar documento = CriarDocumentoConfirmado(processo);
        await PersistirProcessoEDocumentoAsync(db, processo, documento);
        return (processo, documento);
    }

    private static async Task PersistirProcessoEDocumentoAsync(
        UniPlusDbContext db, ProcessoSeletivo processo, DocumentoAPublicar documento)
    {
        await db.ProcessosSeletivos.AddAsync(processo);
        await db.DocumentosAPublicar.AddAsync(documento);
        await db.SaveChangesAsync();
        processo.DequeueDomainEvents();
    }

    private static void ValidarNome(string nome) => ArgumentException.ThrowIfNullOrWhiteSpace(nome);

    private static ProcessoSeletivo CriarProcesso(UniPlusDbContext db, OpcoesDoProcessoSeletivoPublicavel opcoes)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(opcoes.Nome, opcoes.Tipo ?? TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());

        if (opcoes.ProcessoSeletivoId.HasValue)
        {
            db.Entry(processo).Property(p => p.Id).CurrentValue = opcoes.ProcessoSeletivoId.Value;
        }

        return processo;
    }

    private static void DefinirAtendimentoEspecializado(ProcessoSeletivo processo)
    {
        Result atendimentoResult = processo.DefinirAtendimentoEspecializado(
            AtendimentoEspecializado.Criar([], [], []).Value!);
        atendimentoResult.IsSuccess.Should().BeTrue(atendimentoResult.Error?.Message);
    }

    private static void DefinirDistribuicaoDeVagas(ProcessoSeletivo processo)
    {
        ReferenciaRegra regraDistribuicao = ReferenciaRegra.De(
            RegraDistribuicaoVagasCodigo.Institucional, "v1", HashFixo);
        ModalidadeSelecionada modalidade = CriarModalidadeDeAmplaConcorrencia();
        Result<ConfiguracaoDistribuicaoVagas> distribuicaoResult = ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 40, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = regraDistribuicao, Modalidades = [modalidade] },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina });
        distribuicaoResult.IsSuccess.Should().BeTrue(distribuicaoResult.Error?.Message);
        Result distribuicaoDefinirResult = processo.DefinirDistribuicaoVagas([distribuicaoResult.Value!], FatosDeModalidadeDeTeste.DoCatalogo);
        distribuicaoDefinirResult.IsSuccess.Should().BeTrue(distribuicaoDefinirResult.Error?.Message);
    }

    private static ModalidadeSelecionada CriarModalidadeDeAmplaConcorrencia() =>
        ModalidadeSelecionada.Criar(
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

    private static void DefinirClassificacao(ProcessoSeletivo processo)
    {
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
        Result classificacaoDefinirResult = processo.DefinirClassificacao(classificacaoResult.Value!);
        classificacaoDefinirResult.IsSuccess.Should().BeTrue(classificacaoDefinirResult.Error?.Message);
    }

    private static void DefinirCronograma(ProcessoSeletivo processo, OpcoesDoProcessoSeletivoPublicavel opcoes)
    {
        FaseCronograma faseConforme = FaseDeResultadoFinal(opcoes.ColetaSolicitacaoIsencao, opcoes.FimDasInscricoes, opcoes.UnidadeResponsavel, opcoes.ProdutosDaFase);
        Result cronogramaResult = processo.DefinirCronogramaFases([faseConforme], []);
        DefinirEtapas(processo);
        cronogramaResult.IsSuccess.Should().BeTrue(cronogramaResult.Error?.Message);
        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);
    }

    private static void DefinirEtapas(ProcessoSeletivo processo)
    {
        Result etapasResult = processo.DefinirEtapas([
            EtapaProcesso.Criar(
                new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: "RESULTADO_FINAL"),
                new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!,
        ]);
        etapasResult.IsSuccess.Should().BeTrue(etapasResult.Error?.Message);
    }

    private static void DefinirTaxaEDeclaracoes(ProcessoSeletivo processo)
    {
        Result<ConfiguracaoTaxaInscricao> taxaResult = ConfiguracaoTaxaInscricao.Criar(
            cobra: false, valor: null, fundamentosCodigos: null);
        taxaResult.IsSuccess.Should().BeTrue(taxaResult.Error?.Message);
        Result taxaDefinirResult = processo.DefinirTaxaInscricao(taxaResult.Value!);
        taxaDefinirResult.IsSuccess.Should().BeTrue(taxaDefinirResult.Error?.Message);
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);
    }

    private static DocumentoAPublicar CriarDocumentoConfirmado(ProcessoSeletivo processo)
    {
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(processo.Id, FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        Result confirmarResult = documento.Confirmar(1024, HashFixo, TimeProvider.System);
        confirmarResult.IsSuccess.Should().BeTrue(confirmarResult.Error?.Message);
        return documento;
    }
}

internal sealed record OpcoesDoProcessoSeletivoPublicavel
{
    public required string Nome { get; init; }
    public bool ColetaSolicitacaoIsencao { get; init; }
    public Action<ProcessoSeletivo>? Complementar { get; init; }
    public TipoProcessoSnapshot? Tipo { get; init; }
    public IReadOnlyList<ProdutoDaFase>? ProdutosDaFase { get; init; }
    public UnidadeResponsavelSnapshot? UnidadeResponsavel { get; init; }
    public DateTimeOffset? FimDasInscricoes { get; init; }
    public Guid? ProcessoSeletivoId { get; init; }
}
