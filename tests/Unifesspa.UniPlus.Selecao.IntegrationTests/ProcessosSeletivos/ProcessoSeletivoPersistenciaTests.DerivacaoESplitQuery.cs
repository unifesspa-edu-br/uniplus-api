namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Interceptors;
using Unifesspa.UniPlus.Monolito.Persistencia;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed partial class ProcessoSeletivoPersistenciaTests
{
    [Fact(DisplayName = "Redefinir a regra de derivação sobre o agregado carregado apaga a árvore antiga inteira (cascade órfão nos três níveis)")]
    public async Task RedefinirRegraDerivacao_SobreAgregadoTracked_ApagaArvoreAntiga()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — Redefinição", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        processo.DefinirRegrasDerivacao(
        [
            ConfiguracaoDerivacaoFato.Criar("REGIME_DE_VAGA",
            [
                RegraDerivacaoConfigurada.Criar(0, "AC", condicoes: null).Value!,
                RegraDerivacaoConfigurada.Criar(1, "AC_PCD",
                    [CondicaoRegraDerivacao.Criar(1, "CONCORRER_PCD", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!]).Value!,
            ]).Value!,
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        await using (UniPlusDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        // Substitui a configuração por inteiro sobre o agregado JÁ tracked (fluxo real de reconfiguração):
        // Clear+Add deve marcar as regras e condições antigas como órfãs e o cascade deve deletá-las.
        await using (UniPlusDbContext mutateContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(mutateContext, TimeProvider.System);
            ProcessoSeletivo carregado = (await repository.ObterComConfiguracaoAsync(processo.Id, CancellationToken.None))!;

            carregado.DefinirRegrasDerivacao(
            [
                ConfiguracaoDerivacaoFato.Criar("REGIME_DE_VAGA",
                [
                    RegraDerivacaoConfigurada.Criar(0, "AC", condicoes: null).Value!,
                ]).Value!,
            ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

            await mutateContext.SaveChangesAsync(CancellationToken.None);
        }

        // Contexto novo: a árvore antiga inteira sumiu — sobra só a única regra âncora sem condição.
        // Contagens escopadas a ESTE processo (a fixture é compartilhada entre testes da classe).
        await using UniPlusDbContext readContext = _fixture.CreateDbContext();
        Guid configId = await readContext.Set<ConfiguracaoDerivacaoFato>()
            .Where(c => c.ProcessoSeletivoId == processo.Id).Select(c => c.Id).SingleAsync(CancellationToken.None);

        List<Guid> regraIds = await readContext.Set<RegraDerivacaoConfigurada>()
            .Where(r => r.ConfiguracaoDerivacaoFatoId == configId).Select(r => r.Id).ToListAsync(CancellationToken.None);
        regraIds.Should().HaveCount(1, "a regra AC_PCD antiga foi apagada como órfã");

        int condicoesRestantes = await readContext.Set<CondicaoRegraDerivacao>()
            .CountAsync(c => regraIds.Contains(c.RegraDerivacaoConfiguradaId), CancellationToken.None);
        condicoesRestantes.Should().Be(0, "a condição da regra AC_PCD antiga foi apagada em cascata");

        ProcessoSeletivoRepository readRepo = new(readContext, TimeProvider.System);
        ProcessoSeletivo recarregado = (await readRepo.ObterComConfiguracaoAsync(processo.Id, CancellationToken.None))!;
        recarregado.RegrasDerivacao.Single().Regras.Should().ContainSingle(r => r.Contribui == "AC");
    }

    /// <summary>
    /// Prova mecânica (issue #850, §3.4/CA-05) de que <c>ObterComConfiguracaoAsync</c> emite
    /// <b>mais de um</b> <c>SELECT</c> para o grafo de <c>Include</c> — o comportamento que
    /// <c>.AsSplitQuery()</c> já produz em <c>ComConfiguracao</c> (adicionado na story do
    /// cronograma de fases, 15/07). Sem esta prova, remover o <c>.AsSplitQuery()</c> ou
    /// acrescentar uma nova coleção sem ele voltaria a arriscar o produto cartesiano de
    /// TODAS as coleções irmãs (etapas × modalidades × eliminação × fases × bancas…) num
    /// único <c>JOIN</c> — o defeito que a guarda de fitness test (ArchTests) impede em
    /// texto, e que este teste prova em runtime.
    /// </summary>
    [Fact(DisplayName = "ObterComConfiguracaoAsync com AsSplitQuery emite mais de um SELECT (#850, CA-05)")]
    public async Task ObterComConfiguracaoAsync_ComAsSplitQuery_EmiteMultiplosSelects()
    {
        GrafoDeTodasAsColecoes grafo = MontarGrafoDeTodasAsColecoes();
        await GravarAsync(grafo.Processo);
        CapturadorDeSql capturador = new();

        ProcessoSeletivo? recarregado = await RecarregarCapturandoSqlAsync(grafo.Processo.Id, capturador);

        recarregado.Should().NotBeNull();
        capturador.Comandos.Where(c => c.Contains("SELECT", StringComparison.OrdinalIgnoreCase)).Should()
            .HaveCountGreaterThan(1,
                "AsSplitQuery() emite uma consulta própria por coleção incluída — um único SELECT indicaria que " +
                "a flag foi removida e o carregamento voltou a produzir o produto cartesiano num JOIN só");
        ConferirAsColecoesMaterializadas(recarregado!, grafo);
    }

    private sealed record GrafoDeTodasAsColecoes(
        ProcessoSeletivo Processo, Guid CondicaoOrigemId, Guid RecursoOrigemId, Guid ModalidadeOrigemId);

    private static GrafoDeTodasAsColecoes MontarGrafoDeTodasAsColecoes()
    {
        GrafoDeTodasAsColecoes grafo = new(NovoProcessoDoSplitQuery(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        EtapaProcesso etapa = DefinirEtapaDoSplitQuery(grafo.Processo);
        DefinirOfertaDeAtendimentoDoSplitQuery(grafo);
        DefinirDistribuicaoDoSplitQuery(grafo);
        DefinirClassificacaoDoSplitQuery(grafo.Processo, etapa);
        DefinirCronogramaDoSplitQuery(grafo.Processo);
        return grafo;
    }

    private static ProcessoSeletivo NovoProcessoDoSplitQuery() =>
        ProcessoSeletivo.Criar(
            "PS 2026 — AsSplitQuery", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    private static EtapaProcesso DefinirEtapaDoSplitQuery(ProcessoSeletivo processo)
    {
        EtapaProcesso etapa = EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, peso: 1m, ordem: 1).Value!;
        processo.DefinirEtapas([etapa], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        return etapa;
    }

    private static void DefinirOfertaDeAtendimentoDoSplitQuery(GrafoDeTodasAsColecoes grafo) =>
        grafo.Processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar(
            condicoes: [OfertaCondicao.Criar(grafo.CondicaoOrigemId, "PCD", "Pessoa com deficiência")],
            recursos: [OfertaRecurso.Criar(grafo.RecursoOrigemId, "LEDOR", "Ledor")],
            tiposDeficiencia: []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

    private static void DefinirDistribuicaoDoSplitQuery(GrafoDeTodasAsColecoes grafo)
    {
        ReferenciaRegra regraDistribuicao = ReferenciaRegra.Criar(
            RegraDistribuicaoVagasCodigo.Institucional, "v1", HashFixoTeste).Value!;
        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            grafo.ModalidadeOrigemId, "AC", null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo,
            null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla), null,
            "Res. Unifesspa 532/2021", quantidadeDeclarada: 40).Value!;
        ConfiguracaoDistribuicaoVagas distribuicao = ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase: 40, pr: 1m, regraDistribuicao, regraAjuste: null,
            referenciaDemografica: null, [modalidade], ofertaCurso: OfertaCursoDeTeste.Medicina).Value!;
        grafo.Processo.DefinirDistribuicaoVagas([distribuicao], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
    }

    private static void DefinirClassificacaoDoSplitQuery(ProcessoSeletivo processo, EtapaProcesso etapa)
    {
        RegraEliminacao eliminacao = RegraEliminacao.Criar(
            ReferenciaRegra.Criar(RegraEliminacaoCodigo.ElimNotaMinimaEtapa, "v1", HashFixoTeste).Value!,
            new ArgsElimNotaMinimaEtapa(etapa.Id, 4m)).Value!;
        ConfiguracaoClassificacao classificacao = ConfiguracaoClassificacao.Criar(
            ReferenciaRegra.Criar(RegraCalculoCodigo.FormulaMediaPonderada, "v1", HashFixoTeste).Value!,
            ReferenciaRegra.Criar(RegraArredondamentoCodigo.PrecisaoTruncar, "v1", HashFixoTeste).Value!,
            2,
            ReferenciaRegra.Criar(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "v1", HashFixoTeste).Value!, [eliminacao], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: [], escalaNotaFinal: 100m).Value!;
        processo.DefinirClassificacao(classificacao, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
    }

    private static void DefinirCronogramaDoSplitQuery(ProcessoSeletivo processo)
    {
        FaseCronograma fase = FaseCronograma.Criar(
            ordem: 1, faseCanonicaOrigemId: Guid.CreateVersion7(), codigo: "RESULTADO_PRELIMINAR",
            origemData: OrigemDataFase.NoEdital, agrupaEtapas: true,
            permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false,
            inicio: new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
            fim: new DateTimeOffset(2026, 3, 2, 0, 0, 0, TimeSpan.Zero),
            produtos: [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)],
            faseConcluinteCodigo: null, emiteParecerIndividual: false,
            bancasRequeridas: [BancaRequerida.Criar(Guid.CreateVersion7(), "BANCA_ANALISE_DOCUMENTAL", "Banca de teste", [])],
            regraRecurso: null, unidadeResponsavel: UnidadesDeTeste.Ceps).Value!;
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
    }

    private async Task GravarAsync(ProcessoSeletivo processo)
    {
        await using (UniPlusDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }
    }

    private async Task<ProcessoSeletivo?> RecarregarCapturandoSqlAsync(Guid processoId, CapturadorDeSql capturador)
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
        ProcessoSeletivoRepository leitura = new(context, TimeProvider.System);
        return await leitura.ObterComConfiguracaoAsync(processoId, CancellationToken.None);
    }

    // Contagens materializadas batem com o fixture — cada navegação afirmada tem ao
    // menos 1 item (um fixture todo vazio não distingue "coleção carregada" de
    // "coleção omitida por engano").
    private static void ConferirAsColecoesMaterializadas(ProcessoSeletivo recarregado, GrafoDeTodasAsColecoes grafo)
    {
        recarregado.Etapas.Should().ContainSingle();
        recarregado.OfertaAtendimento!.Condicoes.Should().ContainSingle().Which.CondicaoOrigemId.Should().Be(grafo.CondicaoOrigemId);
        recarregado.OfertaAtendimento.Recursos.Should().ContainSingle().Which.RecursoOrigemId.Should().Be(grafo.RecursoOrigemId);
        recarregado.DistribuicaoVagas.Should().ContainSingle();
        recarregado.DistribuicaoVagas.Single().Modalidades.Should().ContainSingle()
            .Which.ModalidadeOrigemId.Should().Be(grafo.ModalidadeOrigemId);
        recarregado.Classificacao!.RegrasEliminacao.Should().ContainSingle();
        recarregado.CronogramaFases.Should().ContainSingle();
        recarregado.CronogramaFases.Single().BancasRequeridas.Should().ContainSingle();
    }

    private const string HashFixoTeste = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    /// <summary>Captura o SQL emitido — mesmo padrão de <c>VersaoVigentePersistenciaTests.CapturadorDeSql</c>.</summary>
    private sealed class CapturadorDeSql : DbCommandInterceptor
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
