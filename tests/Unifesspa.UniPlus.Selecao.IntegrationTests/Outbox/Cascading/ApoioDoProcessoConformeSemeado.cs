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
/// Montagem comum dos processos semeados nos testes de integração que partem de um processo
/// conforme em todas as conferências estruturais e acrescentam, cada um, só o defeito que querem isolar.
/// </summary>
internal static class ApoioDoProcessoConformeSemeado
{
    // 64 caracteres hex minúsculos — shape de SHA-256 exigido por
    // HashCanonicalComputer.IsValidHashShape; valor arbitrário (não é recomputado).
    private static readonly string HashFixo = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];

    internal static ProcessoSeletivo ProcessoBaseConforme(
        UniPlusDbContext db, string nome,
        bool definirAtendimentoEspecializado = true,
        IReadOnlyList<ConfiguracaoDistribuicaoVagas>? distribuicao = null)
    {
        ProcessoSeletivo processo = ProcessoComAtendimento(nome, definirAtendimentoEspecializado);

        IReadOnlyList<ConfiguracaoDistribuicaoVagas> distribuicaoEfetiva = distribuicao ?? [DistribuicaoAmplaPadrao(db)];
        Result distribuicaoDefinirResult = processo.DefinirDistribuicaoVagas(distribuicaoEfetiva, FatosDeModalidadeDeTeste.DoCatalogo);
        distribuicaoDefinirResult.IsSuccess.Should().BeTrue(distribuicaoDefinirResult.Error?.Message);

        DefinirClassificacaoImportada(processo, db);

        DefinirTaxaSemCobranca(processo);
        processo.DefinirCriteriosDesempate([CriterioDesempate.Criar(1, RegraDoCatalogo(db, CriterioDesempateCodigo.MaiorIdade), new ParametrosDesempateMaiorIdade()).Value!]).IsSuccess.Should().BeTrue();

        return processo;
    }

    internal static FaseCronograma FaseDeResultadoFinal(bool coletaInscricao) => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(
            OrigemId: Guid.CreateVersion7(), Codigo: "RESULTADO_FINAL", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: false, ColetaInscricao: coletaInscricao,
            ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(
            Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), FaseConcluinteCodigo: null,
            UnidadeResponsavel: UnidadesDeTeste.Ceps),
        HomologacaoDasInscricoesDeTeste.PublicacoesDaFaseQueHomologa(),
        bancasRequeridas: []).Value!;

    internal static void DefinirCronogramaEEtapaDaFase(ProcessoSeletivo processo, IReadOnlyList<FaseCronograma> fases)
    {
        Result cronogramaResult = processo.DefinirCronogramaFases(fases, []);
        cronogramaResult.IsSuccess.Should().BeTrue(cronogramaResult.Error?.Message);
        DefinirEtapaDaFase(processo);
    }

    internal static void DefinirEtapaDaFase(ProcessoSeletivo processo)
    {
        Result etapasResult = processo.DefinirEtapas([
            EtapaProcesso.Criar(
                new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: "RESULTADO_FINAL"),
                new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!,
        ]);
        etapasResult.IsSuccess.Should().BeTrue(etapasResult.Error?.Message);
    }

    internal static ConfiguracaoDistribuicaoVagas DistribuicaoAmplaPadrao(UniPlusDbContext db, (string? Codigo, string? Rotulo)? grupoAreaEnem = null)
    {
        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            modalidadeOrigemId: Guid.CreateVersion7(), codigo: "AC", descricao: "Ampla concorrência",
            naturezaLegal: NaturezaLegalModalidade.Ampla, composicaoVagas: ComposicaoVagasModalidade.RestanteDasVagasOfertadas,
            composicaoOrigemCodigo: null, regraRemanejamento: RegraRemanejamentoModalidade.Nenhuma,
            remanejamentoDestino: null, remanejamentoPar: null, remanejamentoFallback: null,
            regraDeAplicabilidade: RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla), acaoQuandoIndeferido: null, baseLegal: "Res. Unifesspa 532/2021",
            quantidadeDeclarada: 40).Value!;
        return ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 40, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = RegraDoCatalogo(db, RegraDistribuicaoVagasCodigo.Institucional), Modalidades = [modalidade] },
            new FatosDaOfertaNoCadastro { GrupoAreaEnem = grupoAreaEnem, OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;
    }

    internal static ReferenciaRegra RegraDoCatalogo(UniPlusDbContext db, string codigo) =>
        db.RolDeRegras.Single(r => r.Codigo == codigo && r.Versao == "v1").Referencia();

    internal static async Task<(ProcessoSeletivo Processo, DocumentoAPublicar Documento)> PersistirComDocumentoConfirmadoAsync(
        UniPlusDbContext db, ProcessoSeletivo processo)
    {
        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);
        await db.ProcessosSeletivos.AddAsync(processo);
        await MotivosDaHomologacaoSemeados.EscolherAsync(db, processo);

        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(processo.Id, FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        Result confirmarResult = documento.Confirmar(1024, HashFixo, TimeProvider.System);
        confirmarResult.IsSuccess.Should().BeTrue(confirmarResult.Error?.Message);
        await db.DocumentosAPublicar.AddAsync(documento);

        await db.SaveChangesAsync();
        processo.DequeueDomainEvents();

        return (processo, documento);
    }

    internal static async Task<(ProcessoSeletivo Processo, DocumentoAPublicar Documento)> PersistirComDocumentoPendenteAsync(
        UniPlusDbContext db, ProcessoSeletivo processo)
    {
        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);
        await db.ProcessosSeletivos.AddAsync(processo);
        await MotivosDaHomologacaoSemeados.EscolherAsync(db, processo);

        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(processo.Id, FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        await db.DocumentosAPublicar.AddAsync(documento);

        await db.SaveChangesAsync();
        processo.DequeueDomainEvents();

        return (processo, documento);
    }

    private static void DefinirClassificacaoImportada(ProcessoSeletivo processo, UniPlusDbContext db)
    {
        ReferenciaRegra regraCalculo = RegraDoCatalogo(db, RegraCalculoCodigo.ClassificacaoImportada);
        ReferenciaRegra regraOrdemAlocacao = RegraDoCatalogo(db, RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria);
        Result<ConfiguracaoClassificacao> classificacaoResult = ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = regraCalculo,
            RegraOrdemAlocacao = regraOrdemAlocacao,
        });
        classificacaoResult.IsSuccess.Should().BeTrue(classificacaoResult.Error?.Message);
        Result classificacaoDefinirResult = processo.DefinirClassificacao(classificacaoResult.Value!);
        classificacaoDefinirResult.IsSuccess.Should().BeTrue(classificacaoDefinirResult.Error?.Message);
    }

    private static void DefinirTaxaSemCobranca(ProcessoSeletivo processo)
    {
        // Publicar sem declarar cobrança de taxa é recusado; a taxa é declarada aqui para que
        // cada seeder isole só o defeito que se propõe a isolar.
        Result taxaResult = processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!);
        taxaResult.IsSuccess.Should().BeTrue(taxaResult.Error?.Message);
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);
    }

    private static ProcessoSeletivo ProcessoComAtendimento(string nome, bool definirAtendimentoEspecializado)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            nome, TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());

        DefinirAtendimento(processo, definirAtendimentoEspecializado);

        return processo;
    }

    private static void DefinirAtendimento(ProcessoSeletivo processo, bool definirAtendimentoEspecializado)
    {
        if (definirAtendimentoEspecializado)
        {
            Result atendimentoResult = processo.DefinirAtendimentoEspecializado(
                AtendimentoEspecializado.Criar([], [], []).Value!);
            atendimentoResult.IsSuccess.Should().BeTrue(atendimentoResult.Error?.Message);
        }
    }
}
