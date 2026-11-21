namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using AwesomeAssertions;

using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

using Kernel.Results;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;
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
    private static readonly string[] TiposDeDecisaoDaFaseUnica = [TipoMotivoDeDecisao.HomologacaoDasInscricoes.Codigo()];

    /// <summary>A fase que coleta a inscrição, agrupa as etapas e publica o resultado que homologa as inscrições.</summary>
    internal static FaseCronograma FaseDeResultadoFinal(OpcoesDoProcessoSeletivoPublicavel opcoes) => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(
            OrigemId: opcoes.OrigemDaFaseDeResultado ?? Guid.CreateVersion7(), Codigo: "RESULTADO_FINAL", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: false, ColetaInscricao: true,
            ColetaSolicitacaoIsencao: opcoes.ColetaSolicitacaoIsencao),
        new DecisoesDeCronogramaDaFase(
            Ordem: opcoes.OrdemDaFaseDeResultado, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: opcoes.FimDasInscricoes ?? new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), UnidadeResponsavel: opcoes.UnidadeResponsavel ?? UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(
            Produtos: opcoes.ProdutosDaFase ?? [ProdutoDaFase.Criar(HomologacaoDasInscricoesDeTeste.ResultadoDaFase, PapelProdutoFase.Definitivo)],
            EmiteParecerIndividual: false,
            RegraRecurso: opcoes.RegraDeRecursoDaFase,
            TiposDeDecisao: TiposDeDecisaoDaFaseUnica,
            RegraDeHomologacao: HomologacaoDasInscricoesDeTeste.Regra(opcoes.ProdutosDaFase is { Count: > 0 } ? opcoes.ProdutosDaFase[0].AtoCodigo : HomologacaoDasInscricoesDeTeste.ResultadoDaFase)),
        bancasRequeridas: BancasDaFaseDeResultado(opcoes)).Value!;

    private static IReadOnlyList<BancaRequerida> BancasDaFaseDeResultado(OpcoesDoProcessoSeletivoPublicavel opcoes) =>
        opcoes.ComBancaDeImportacaoDasNotas
            ? [BancasDeTeste.DaHomologacaoDasInscricoesDaFase(), BancasDeTeste.DaFase(TipoDeBanca.ImportacaoDasNotasDoInep)]
            : [BancasDeTeste.DaHomologacaoDasInscricoesDaFase()];

    public static async Task<(ProcessoSeletivo Processo, DocumentoAPublicar Documento)> SemearAsync(
        UniPlusDbContext db, OpcoesDoProcessoSeletivoPublicavel opcoes)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(opcoes);
        ValidarNome(opcoes.Nome);
        ProcessoSeletivo processo = CriarProcesso(db, opcoes);
        DefinirAtendimentoEspecializado(processo);
        Dictionary<string, ReferenciaRegra> regras = await RegrasAsync(db);
        DefinirDistribuicaoDeVagas(processo, regras);
        DefinirClassificacao(processo, regras);
        DefinirCronogramaEDeclaracoes(processo, opcoes, regras);
        opcoes.Complementar?.Invoke(processo);
        DocumentoAPublicar documento = CriarDocumentoConfirmado(processo);
        await PersistirProcessoEDocumentoAsync(db, processo, documento);
        return (processo, documento);
    }

    private static void DefinirCronogramaEDeclaracoes(
        ProcessoSeletivo processo, OpcoesDoProcessoSeletivoPublicavel opcoes, Dictionary<string, ReferenciaRegra> regras)
    {
        DefinirCronograma(processo, opcoes);
        DefinirTaxaEDeclaracoes(processo);
        DefinirDesempate(processo, regras);
    }

    private static Task<Dictionary<string, ReferenciaRegra>> RegrasAsync(UniPlusDbContext db) =>
        db.RolDeRegras.Where(r => r.Versao == "v1").ToDictionaryAsync(r => r.Codigo, r => r.Referencia());

    private static void DefinirDesempate(ProcessoSeletivo processo, Dictionary<string, ReferenciaRegra> regras)
    {
        CriterioDesempate criterio = CriterioDesempate.Criar(1, regras[CriterioDesempateCodigo.MaiorIdade], new ParametrosDesempateMaiorIdade()).Value!;
        processo.DefinirCriteriosDesempate([criterio]).IsSuccess.Should().BeTrue();
    }

    private static async Task PersistirProcessoEDocumentoAsync(
        UniPlusDbContext db, ProcessoSeletivo processo, DocumentoAPublicar documento)
    {
        await db.ProcessosSeletivos.AddAsync(processo);
        await MotivosDaHomologacaoSemeados.EscolherAsync(db, processo);
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

    private static void DefinirDistribuicaoDeVagas(ProcessoSeletivo processo, Dictionary<string, ReferenciaRegra> regras)
    {
        ReferenciaRegra regraDistribuicao = regras[RegraDistribuicaoVagasCodigo.Institucional];
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

    private static void DefinirClassificacao(ProcessoSeletivo processo, Dictionary<string, ReferenciaRegra> regras)
    {
        ReferenciaRegra regraCalculo = regras[RegraCalculoCodigo.ClassificacaoImportada];
        ReferenciaRegra regraOrdemAlocacao = regras[RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria];
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
        Result cronogramaResult = processo.DefinirCronogramaFases([FaseDeResultadoFinal(opcoes), .. opcoes.FasesAdicionais], []);
        DefinirEtapas(processo, opcoes);
        cronogramaResult.IsSuccess.Should().BeTrue(cronogramaResult.Error?.Message);
        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);
    }

    private static void DefinirEtapas(ProcessoSeletivo processo, OpcoesDoProcessoSeletivoPublicavel opcoes)
    {
        Result etapasResult = processo.DefinirEtapas([
            EtapaProcesso.Criar(
                new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: "RESULTADO_FINAL"),
                new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!,
            .. opcoes.EtapasAdicionais,
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

    /// <summary>Acrescenta à fase de resultado a banca de importação das notas do INEP, que o processo com nota do ENEM exige.</summary>
    public bool ComBancaDeImportacaoDasNotas { get; init; }
    public Action<ProcessoSeletivo>? Complementar { get; init; }
    public TipoProcessoSnapshot? Tipo { get; init; }
    public IReadOnlyList<ProdutoDaFase>? ProdutosDaFase { get; init; }
    public RegraRecursoFase? RegraDeRecursoDaFase { get; init; }
    public UnidadeResponsavelSnapshot? UnidadeResponsavel { get; init; }
    public DateTimeOffset? FimDasInscricoes { get; init; }
    public Guid? ProcessoSeletivoId { get; init; }

    /// <summary>A posição da fase de resultado no cronograma; a das fases adicionais é a que elas mesmas declaram.</summary>
    public int OrdemDaFaseDeResultado { get; init; } = 1;

    /// <summary>A origem canônica da fase de resultado; ausente, uma nova, como na primeira semeadura do processo.</summary>
    public Guid? OrigemDaFaseDeResultado { get; init; }

    /// <summary>Fases que acompanham a de resultado no cronograma.</summary>
    public IReadOnlyList<FaseCronograma> FasesAdicionais { get; init; } = [];

    /// <summary>Etapas que acompanham a prova objetiva da fase de resultado.</summary>
    public IReadOnlyList<EtapaProcesso> EtapasAdicionais { get; init; } = [];
}
