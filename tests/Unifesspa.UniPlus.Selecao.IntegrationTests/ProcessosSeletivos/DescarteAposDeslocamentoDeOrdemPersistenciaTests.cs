namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// O descarte de uma sessão editorial que <b>deslocou as ordens do cronograma</b>, contra
/// Postgres real.
/// </summary>
/// <remarks>
/// <para>
/// É o cruzamento de duas reconciliações que não conversam: <c>ProcessoSeletivo.AplicarGrafo</c>
/// casa fase por <c>Ordem</c>, e <c>FaseCronograma.AtualizarSnapshot</c> casa produto por
/// <c>AtoCodigo</c> dentro da fase. Inserir uma fase no meio do cronograma faz a fase congelada
/// de uma ordem cair sobre uma fase viva diferente, e o produto congelado — que carrega
/// <c>Id</c> — passa a atravessar a fronteira da fase que o congelou.
/// </para>
/// <para>
/// O teste precisa de banco: o estrago é do identity map do EF e das linhas de
/// <c>produtos_da_fase</c>, e nenhuma das duas coisas existe num agregado em memória. A
/// mutação intermediária tem de ser <c>DefinirCronogramaFases</c> — e não outra reposição —
/// porque é ela que preserva o <c>Id</c> dos produtos ao renumerar as fases, que é a
/// pré-condição do defeito.
/// </para>
/// </remarks>
public sealed class DescarteAposDeslocamentoDeOrdemPersistenciaTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private static readonly string HashFixo = string.Concat(Enumerable.Repeat("de01234567", 7))[..64];
    private static readonly DateTimeOffset Agora = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly ProcessoSeletivoDbFixture _fixture;

    public DescarteAposDeslocamentoDeOrdemPersistenciaTests(ProcessoSeletivoDbFixture fixture) =>
        _fixture = fixture;

    [Fact(DisplayName = "Descartar a sessão que inseriu uma fase no meio repõe os produtos nas fases que os congelaram, sem duplicar identidade")]
    public async Task Descarte_AposInserirFaseNoMeio_ReporProdutosSemColidirNoIdentityMap()
    {
        Guid processoId;
        Guid versaoId;
        Guid produtoPreliminarId;
        Guid produtoFinalId;
        VersaoConfiguracao versao;
        Guid faseInscricaoOrigem = Guid.CreateVersion7();
        Guid famePreliminarOrigem = Guid.CreateVersion7();
        Guid faseFinalOrigem = Guid.CreateVersion7();

        await using (SelecaoDbContext db = _fixture.CreateDbContext())
        {
            ProcessoSeletivo processo = NovoProcesso($"PS deslocamento {Guid.CreateVersion7()}");
            processo.DefinirCronogramaFases(
                [
                    Fase(1, "INSCRICAO", faseInscricaoOrigem, []),
                    Fase(2, "RESULTADO_PRELIMINAR", famePreliminarOrigem,
                        [ProdutoDaFase.Criar("RESULTADO_PRELIMINAR", PapelProdutoFase.Preliminar)],
                        faseConcluinteCodigo: "RESULTADO_FINAL"),
                    Fase(3, "RESULTADO_FINAL", faseFinalOrigem,
                        [ProdutoDaFase.Criar("RESULTADO_FINAL", PapelProdutoFase.Definitivo)]),
                ],
                [],
                PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

            Result<VersaoConfiguracao> publicacao = Publicar(processo);
            publicacao.IsSuccess.Should().BeTrue(publicacao.Error?.Message);
            processo.ClearDomainEvents();
            versao = publicacao.Value!;

            await db.ProcessosSeletivos.AddAsync(processo);
            await db.AddAsync(versao);
            await db.SaveChangesAsync();

            processoId = processo.Id;
            versaoId = versao.Id;
            produtoPreliminarId = ProdutoDe(processo, "RESULTADO_PRELIMINAR").Id;
            produtoFinalId = ProdutoDe(processo, "RESULTADO_FINAL").Id;
        }

        // A sessão editorial insere HOMOLOGACAO na ordem 2 e empurra as duas fases de
        // resultado para 3 e 4. Nenhuma ordem forma ciclo fechado, então
        // PermutacaoDeOrdemNaoSuportada não recusa; as fases são reconciliadas por
        // FaseCanonicaOrigemId e os produtos mantêm seus Ids no banco, agora sob outras ordens.
        await using (SelecaoDbContext sessao = _fixture.CreateDbContext())
        {
            ProcessoSeletivo tracked = await CarregarAsync(sessao, processoId);
            tracked.AbrirRetificacao("Insere a homologação no meio do cronograma", versao, "teste", Agora)
                .IsSuccess.Should().BeTrue();

            Result deslocamento = tracked.DefinirCronogramaFases(
                [
                    Fase(1, "INSCRICAO", faseInscricaoOrigem, []),
                    Fase(2, "HOMOLOGACAO", Guid.CreateVersion7(),
                        [ProdutoDaFase.Criar("HOMOLOGACAO_INSCRICOES", PapelProdutoFase.Definitivo)]),
                    Fase(3, "RESULTADO_PRELIMINAR", famePreliminarOrigem,
                        [ProdutoDaFase.Criar("RESULTADO_PRELIMINAR", PapelProdutoFase.Preliminar)],
                        faseConcluinteCodigo: "RESULTADO_FINAL"),
                    Fase(4, "RESULTADO_FINAL", faseFinalOrigem,
                        [ProdutoDaFase.Criar("RESULTADO_FINAL", PapelProdutoFase.Definitivo)]),
                ],
                [],
                PrecondicaoIfMatch.Curinga);

            deslocamento.IsSuccess.Should().BeTrue(deslocamento.Error?.Message);
            await sessao.SaveChangesAsync();
        }

        await using (SelecaoDbContext conferencia = _fixture.CreateDbContext())
        {
            ProcessoSeletivo deslocado = await CarregarAsync(conferencia, processoId);
            deslocado.CronogramaFases.Should().HaveCount(4, "pré-condição: a sessão inseriu a fase no meio");
            ProdutoDe(deslocado, "RESULTADO_PRELIMINAR").Id.Should().Be(produtoPreliminarId,
                "pré-condição: renumerar a fase preserva o Id do produto — é o que faz o Id congelado colidir no descarte");
        }

        // O DESCARTE: repõe o grafo congelado — três fases, com a de ordem 2 caindo sobre a
        // HOMOLOGACAO viva — e encerra a sessão.
        await using (SelecaoDbContext descarte = _fixture.CreateDbContext())
        {
            ProcessoSeletivo tracked = await CarregarAsync(descarte, processoId);
            VersaoConfiguracao versaoDoBanco = await descarte.Set<VersaoConfiguracao>().FirstAsync(v => v.Id == versaoId);

            Result reposicao = tracked.RestaurarConfiguracaoCongelada(
                versaoDoBanco,
                GrafoCongelado(faseInscricaoOrigem, famePreliminarOrigem, faseFinalOrigem, produtoPreliminarId, produtoFinalId));
            reposicao.IsSuccess.Should().BeTrue(reposicao.Error?.Message);

            Result descarteResult = tracked.DescartarRetificacao(PrecondicaoIfMatch.Curinga);
            descarteResult.IsSuccess.Should().BeTrue(descarteResult.Error?.Message);

            Func<Task> salvar = async () => await descarte.SaveChangesAsync();

            await salvar.Should().NotThrowAsync(
                "deixar o Id congelado de um produto entrar numa fase que não é a que o congelou põe a MESMA " +
                "linha em duas fases dentro da mesma transação — a rastreada saindo de uma como órfã e a " +
                "congelada entrando na outra —, e o EF derruba o descarte inteiro, deixando o operador sem " +
                "como desfazer o deslocamento");
        }

        await using (SelecaoDbContext verificacao = _fixture.CreateDbContext())
        {
            ProcessoSeletivo reposto = await CarregarAsync(verificacao, processoId);

            reposto.CronogramaFases.Should().HaveCount(3, "o descarte repõe o cronograma congelado");

            FaseCronograma preliminar = reposto.CronogramaFases.Single(f => f.Codigo == "RESULTADO_PRELIMINAR");
            FaseCronograma final = reposto.CronogramaFases.Single(f => f.Codigo == "RESULTADO_FINAL");

            preliminar.Produtos.Should().ContainSingle()
                .Which.AtoCodigo.Should().Be("RESULTADO_PRELIMINAR");
            final.Produtos.Should().ContainSingle()
                .Which.AtoCodigo.Should().Be("RESULTADO_FINAL");

            reposto.CronogramaFases.SelectMany(f => f.Produtos).Select(p => p.Id)
                .Should().OnlyHaveUniqueItems("nenhum produto pode existir em duas fases ao mesmo tempo");

            foreach (FaseCronograma fase in reposto.CronogramaFases)
            {
                fase.Produtos.Should().OnlyContain(p => p.FaseCronogramaId == fase.Id,
                    "o produto pertence à fase que o declara");
            }
        }
    }

    /// <summary>
    /// O grafo tal como o codec o entrega ao descarte: fases e produtos reidratados com os
    /// <c>Id</c>s congelados no envelope.
    /// </summary>
    private static GrafoConfiguracao GrafoCongelado(
        Guid faseInscricaoOrigem,
        Guid fasePreliminarOrigem,
        Guid faseFinalOrigem,
        Guid produtoPreliminarId,
        Guid produtoFinalId) => new(
        etapas: [],
        ofertaAtendimento: OfertaAtendimentoEspecializado.Criar([], [], []).Value!,
        distribuicaoVagas: [Distribuicao()],
        bonusRegional: null,
        criteriosDesempate: [],
        classificacao: Classificacao(),
        cronogramaFases:
        [
            FaseReidratada(1, "INSCRICAO", faseInscricaoOrigem, [], null),
            FaseReidratada(2, "RESULTADO_PRELIMINAR", fasePreliminarOrigem,
                [ProdutoDaFase.Reidratar(produtoPreliminarId, "RESULTADO_PRELIMINAR", PapelProdutoFase.Preliminar)],
                "RESULTADO_FINAL"),
            FaseReidratada(3, "RESULTADO_FINAL", faseFinalOrigem,
                [ProdutoDaFase.Reidratar(produtoFinalId, "RESULTADO_FINAL", PapelProdutoFase.Definitivo)],
                null),
        ],
        documentosExigidos: [],
        nosExigencia: [],
        referenciaTemporalFatos: null,
        configuracaoTaxaInscricao: ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!,
        localidade: LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

    private static ProcessoSeletivo NovoProcesso(string nome)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            nome, TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirDistribuicaoVagas([Distribuicao()], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirClassificacao(Classificacao(), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        return processo;
    }

    private static ConfiguracaoDistribuicaoVagas Distribuicao() => ConfiguracaoDistribuicaoVagas.Criar(
        Guid.CreateVersion7(), 40, 1m, Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a'),
        null, null,
        [
            ModalidadeSelecionada.Criar(
                Guid.CreateVersion7(), "AC", null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo,
                null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, [], null, "Res. Unifesspa 532/2021",
                quantidadeDeclarada: 40).Value!,
        ]).Value!;

    private static ConfiguracaoClassificacao Classificacao() => ConfiguracaoClassificacao.Criar(
        Regra(RegraCalculoCodigo.ClassificacaoImportada, 'b'), null, null,
        Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, 'c'), 1, [], baseadoEmEnem: false).Value!;

    private static Result<VersaoConfiguracao> Publicar(ProcessoSeletivo processo) => processo.Publicar(
        DadosEdital.Criar(
            "001/2026",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
            new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)),
            Guid.CreateVersion7()).Value!,
        "{}"u8.ToArray(),
        "1.1",
        "canonical-json/sha256@v1",
        HashFixo,
        "teste",
        TimeProvider.System,
        ContextoDeContagemDePrazos.SemCalendario);

    private static ProdutoDaFase ProdutoDe(ProcessoSeletivo processo, string atoCodigo) =>
        processo.CronogramaFases.SelectMany(f => f.Produtos).Single(p => p.AtoCodigo == atoCodigo);

    private static FaseCronograma Fase(
        int ordem,
        string codigo,
        Guid faseCanonicaOrigemId,
        IReadOnlyList<ProdutoDaFase> produtos,
        string? faseConcluinteCodigo = null) =>
        FaseCronograma.Criar(
            ordem, faseCanonicaOrigemId, codigo, "CEPS", OrigemDataFase.Delegada,
            agrupaEtapas: false, permiteComplementacao: false,
            coletaInscricao: false, coletaSolicitacaoIsencao: false,
            inicio: null, fim: null,
            produtos, faseConcluinteCodigo, emiteParecerIndividual: false,
            bancasRequeridas: [], regraRecurso: null).Value!;

    private static FaseCronograma FaseReidratada(
        int ordem,
        string codigo,
        Guid faseCanonicaOrigemId,
        IReadOnlyList<ProdutoDaFase> produtos,
        string? faseConcluinteCodigo) =>
        FaseCronograma.Reidratar(
            Guid.CreateVersion7(), ordem, faseCanonicaOrigemId, codigo, "CEPS", OrigemDataFase.Delegada,
            agrupaEtapas: false, permiteComplementacao: false,
            coletaInscricao: false, coletaSolicitacaoIsencao: false,
            inicio: null, fim: null,
            produtos, faseConcluinteCodigo, emiteParecerIndividual: false,
            bancasRequeridas: [], regraRecurso: null);

    private static ReferenciaRegra Regra(string codigo, char semente) =>
        ReferenciaRegra.Criar(codigo, "v1", new string(semente, 64)).Value!;

    private static async Task<ProcessoSeletivo> CarregarAsync(SelecaoDbContext db, Guid processoId) =>
        await db.ProcessosSeletivos
            .Include(p => p.DistribuicaoVagas).ThenInclude(d => d.Modalidades)
            .Include(p => p.Classificacao)
            .Include(p => p.OfertaAtendimento)
            .Include(p => p.CronogramaFases).ThenInclude(f => f.Produtos)
            .Include(p => p.CronogramaFases).ThenInclude(f => f.BancasRequeridas)
            .Include(p => p.CronogramaFases).ThenInclude(f => f.RegraRecurso)
            .Include(p => p.ConfiguracaoTaxaInscricao)
            .Include(p => p.Rascunho)
            .AsSplitQuery()
            .FirstAsync(p => p.Id == processoId);
}
