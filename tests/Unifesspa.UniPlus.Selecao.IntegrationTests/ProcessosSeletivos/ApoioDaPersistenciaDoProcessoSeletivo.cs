namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

using Npgsql;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Interceptors;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Monolito.Persistencia;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

internal sealed class ApoioDaPersistenciaDoProcessoSeletivo
{
    private readonly ProcessoSeletivoDbFixture _fixture;

    internal ApoioDaPersistenciaDoProcessoSeletivo(ProcessoSeletivoDbFixture fixture)
    {
        _fixture = fixture;
    }

    internal sealed record GrafoDeTodasAsColecoes(
        ProcessoSeletivo Processo, Guid CondicaoOrigemId, Guid RecursoOrigemId, Guid ModalidadeOrigemId);

    internal static GrafoDeTodasAsColecoes MontarGrafoDeTodasAsColecoes()
    {
        GrafoDeTodasAsColecoes grafo = new(NovoProcessoDoSplitQuery(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        EtapaProcesso etapa = DefinirEtapaDoSplitQuery(grafo.Processo);
        DefinirAtendimentoEspecializadoDoSplitQuery(grafo);
        DefinirDistribuicaoDoSplitQuery(grafo);
        DefinirClassificacaoDoSplitQuery(grafo.Processo, etapa);
        DefinirCronogramaDoSplitQuery(grafo.Processo);
        return grafo;
    }

    internal static ProcessoSeletivo NovoProcessoDoSplitQuery() =>
        ProcessoSeletivo.Criar(
            "PS 2026 — AsSplitQuery", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    internal static EtapaProcesso DefinirEtapaDoSplitQuery(ProcessoSeletivo processo)
    {
        EtapaProcesso etapa = EtapaProcesso.Criar(
            new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
        processo.DefinirEtapas([etapa]).IsSuccess.Should().BeTrue();
        return etapa;
    }

    internal static void DefinirAtendimentoEspecializadoDoSplitQuery(GrafoDeTodasAsColecoes grafo) =>
        grafo.Processo.DefinirAtendimentoEspecializado(AtendimentoEspecializado.Criar(
            condicoes: [CondicaoOfertada.Criar(grafo.CondicaoOrigemId, "PCD", "Pessoa com deficiência")],
            recursos: [RecursoOfertado.Criar(grafo.RecursoOrigemId, "LEDOR", "Ledor")],
            tiposDeficiencia: []).Value!).IsSuccess.Should().BeTrue();

    internal static void DefinirDistribuicaoDoSplitQuery(GrafoDeTodasAsColecoes grafo)
    {
        ReferenciaRegra regraDistribuicao = ReferenciaRegra.De(
            RegraDistribuicaoVagasCodigo.Institucional, "v1", HashFixoTeste);
        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            grafo.ModalidadeOrigemId, "AC", null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas,
            null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla), null,
            "Res. Unifesspa 532/2021", quantidadeDeclarada: 40).Value!;
        ConfiguracaoDistribuicaoVagas distribuicao = ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 40, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = regraDistribuicao, Modalidades = [modalidade] },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;
        grafo.Processo.DefinirDistribuicaoVagas([distribuicao], FatosDeModalidadeDeTeste.DoCatalogo).IsSuccess.Should().BeTrue();
    }

    internal static void DefinirClassificacaoDoSplitQuery(ProcessoSeletivo processo, EtapaProcesso etapa)
    {
        RegraEliminacao eliminacao = RegraEliminacao.Criar(
            ReferenciaRegra.De(RegraEliminacaoCodigo.ElimNotaMinimaEtapa, "v1", HashFixoTeste),
            new ParametrosElimNotaMinimaEtapa(etapa.Id, 4m)).Value!;
        ConfiguracaoClassificacao classificacao = ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = ReferenciaRegra.De(RegraCalculoCodigo.FormulaMediaPonderada, "v1", HashFixoTeste),
            RegraArredondamento = ReferenciaRegra.De(RegraArredondamentoCodigo.PrecisaoTruncar, "v1", HashFixoTeste),
            CasasArredondamento = 2,
            RegraOrdemAlocacao = ReferenciaRegra.De(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "v1", HashFixoTeste),
            RegrasEliminacao = [eliminacao],
            EscalaNotaFinal = 100m,
        }).Value!;
        processo.DefinirClassificacao(classificacao).IsSuccess.Should().BeTrue();
    }

    internal static void DefinirCronogramaDoSplitQuery(ProcessoSeletivo processo)
    {
        FaseCronograma fase = FaseCronograma.Criar(
            new OrigemCanonicaDaFase(
                OrigemId: Guid.CreateVersion7(), Codigo: "RESULTADO_PRELIMINAR", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: false, ColetaInscricao: false,
                ColetaSolicitacaoIsencao: false),
            new DecisoesDeCronogramaDaFase(
                Ordem: 1, Inicio: new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 3, 2, 0, 0, 0, TimeSpan.Zero), FaseConcluinteCodigo: null,
                UnidadeResponsavel: UnidadesDeTeste.Ceps),
            new PublicacoesDaFase(Produtos: [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null),
            bancasRequeridas: [BancaRequerida.Criar(Guid.CreateVersion7(), "BANCA_ANALISE_DOCUMENTAL", "Banca de teste", [])]).Value!;
        processo.DefinirCronogramaFases([fase], []).IsSuccess.Should().BeTrue();
    }

    internal async Task GravarAsync(ProcessoSeletivo processo)
    {
        await using (UniPlusDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(writeContext);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }
    }

    internal async Task<ProcessoSeletivo?> RecarregarCapturandoSqlAsync(Guid processoId, CapturadorDeSql capturador)
    {
        DbContextOptions<MonolitoDbContext> options = new DbContextOptionsBuilder<MonolitoDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(
                new SoftDeleteInterceptor(TimeProvider.System, userContext: null),
                new AuditableInterceptor(TimeProvider.System, userContext: null),
                capturador)
            .Options;

        await using MonolitoDbContext context = new(options);
        ProcessoSeletivoRepository leitura = ProcessoSeletivoRepositoryDeTeste.Criar(context);
        return await leitura.ObterComConfiguracaoAsync(processoId, CancellationToken.None);
    }

    // Contagens materializadas batem com o fixture — cada navegação afirmada tem ao
    // menos 1 item (um fixture todo vazio não distingue "coleção carregada" de
    // "coleção omitida por engano").
    internal static void ConferirAsColecoesMaterializadas(ProcessoSeletivo recarregado, GrafoDeTodasAsColecoes grafo)
    {
        recarregado.Etapas.Should().ContainSingle();
        recarregado.AtendimentoEspecializado!.Condicoes.Should().ContainSingle().Which.CondicaoOrigemId.Should().Be(grafo.CondicaoOrigemId);
        recarregado.AtendimentoEspecializado.Recursos.Should().ContainSingle().Which.RecursoOrigemId.Should().Be(grafo.RecursoOrigemId);
        recarregado.DistribuicaoVagas.Should().ContainSingle();
        recarregado.DistribuicaoVagas.Single().Modalidades.Should().ContainSingle()
            .Which.ModalidadeOrigemId.Should().Be(grafo.ModalidadeOrigemId);
        recarregado.Classificacao!.RegrasEliminacao.Should().ContainSingle();
        recarregado.CronogramaFases.Should().ContainSingle();
        recarregado.CronogramaFases.Single().BancasRequeridas.Should().ContainSingle();
    }

    internal const string HashFixoTeste = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    /// <summary>Captura o SQL emitido — mesmo padrão de <c>FronteiraDoSeletorDaVersaoVigentePersistenciaTests.CapturadorDeSql</c>.</summary>
    internal sealed class CapturadorDeSql : DbCommandInterceptor
    {
        public List<string> Comandos { get; } = [];

        public override ValueTask<InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
            System.Data.Common.DbCommand command,
            CommandEventData eventData,
            InterceptionResult<System.Data.Common.DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(command);
            Comandos.Add(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
