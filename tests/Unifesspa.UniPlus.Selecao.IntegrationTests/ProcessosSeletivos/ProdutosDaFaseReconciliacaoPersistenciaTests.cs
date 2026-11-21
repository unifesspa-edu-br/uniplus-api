namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A reconciliação dos produtos por <c>ato_codigo</c> dentro de
/// <c>FaseCronograma.AtualizarDe</c>, contra Postgres real.
/// </summary>
/// <remarks>
/// <c>ux_produtos_da_fase_ato</c> torna <c>(fase, ato_codigo)</c> único, e é isso que separa esta
/// coleção das bancas requeridas: repor por <c>Clear()</c> + <c>Add</c> produziria DELETE do
/// produto antigo e INSERT de um novo com o MESMO par na mesma transação, e o EF Core não infere
/// essa ordem entre entidades sem relação de FK. O teste precisa de banco: em memória a colisão
/// não existe, e a asserção passaria com a implementação errada.
/// </remarks>
public sealed class ProdutosDaFaseReconciliacaoPersistenciaTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private readonly ProcessoSeletivoDbFixture _fixture;

    public ProdutosDaFaseReconciliacaoPersistenciaTests(ProcessoSeletivoDbFixture fixture) =>
        _fixture = fixture;

    [Fact(DisplayName = "Redefinir o cronograma trocando o papel do mesmo ato reusa a linha rastreada — o Id do produto sobrevive")]
    public async Task RedefinirCronograma_MesmoAtoComOutroPapel_ReusaALinhaEPreservaOId()
    {
        Guid faseCanonicaOrigemId = Guid.CreateVersion7();
        ProcessoSeletivo gravado = await GravarProcessoComCronogramaAsync($"PS produtos {Guid.CreateVersion7()}", faseCanonicaOrigemId, [ProdutoDaFase.Criar("RESULTADO_HOMOLOGACAO", papel: null)]);
        Guid produtoIdAntes = gravado.CronogramaFases.Single().Produtos.Single().Id;

        // O mesmo ato_codigo com outro papel, mais a publicação definitiva da mesma
        // matéria: é a edição comum de quem passa a declarar o ciclo recursal da fase.
        await RedefinirCronogramaAsync(gravado.Id, faseCanonicaOrigemId, PreliminarEDefinitivaDaHomologacao());

        AfirmarQuePreliminarReusouALinhaRastreada(await ObterProdutosDaFaseAsync(gravado.Id), produtoIdAntes);
    }

    /// <summary>
    /// O caso que o catálogo real impõe: "Resultado da homologação das inscrições" é um
    /// código só, e o ciclo recursal da matéria precisa das duas publicações — a preliminar,
    /// que o abre, e a definitiva, que o encerra. Exercita as duas defesas ao mesmo tempo: a
    /// reconciliação, que não pode casar as duas linhas novas com a única rastreada, e o
    /// índice único, que passou a incluir o papel.
    /// </summary>
    [Fact(DisplayName = "A mesma matéria ganha a publicação definitiva ao lado da preliminar que já existia")]
    public async Task RedefinirCronograma_MesmaMateriaEmDoisPapeis_PersisteAsDuas()
    {
        Guid faseCanonicaOrigemId = Guid.CreateVersion7();

        // Começa só com a definitiva porque a fase que publica preliminar precisa de quem
        // conclua o ciclo: o estado intermediário de um par pela metade o agregado recusa.
        ProcessoSeletivo gravado = await GravarProcessoComCronogramaAsync($"PS matéria {Guid.CreateVersion7()}", faseCanonicaOrigemId, [ProdutoDaFase.Criar("RESULTADO_HOMOLOGACAO", PapelProdutoFase.Definitivo)]);
        Guid definitivaIdAntes = gravado.CronogramaFases.Single().Produtos.Single().Id;

        await RedefinirCronogramaAsync(gravado.Id, faseCanonicaOrigemId, PreliminarEDefinitivaDaHomologacao());

        AfirmarQueDefinitivaPreexistenteSobreviveEPreliminarChegou(await ObterProdutosDaFaseAsync(gravado.Id), definitivaIdAntes);
    }

    [Fact(DisplayName = "Produto que sai da declaração é removido da tabela, sem sobrar linha órfã")]
    public async Task RedefinirCronograma_ProdutoRemovido_SaiDaTabela()
    {
        Guid faseCanonicaOrigemId = Guid.CreateVersion7();
        ProcessoSeletivo gravado = await GravarProcessoComCronogramaAsync(
            $"PS produtos removidos {Guid.CreateVersion7()}", faseCanonicaOrigemId, PreliminarEDefinitivaDaHomologacao());

        await RedefinirCronogramaAsync(
            gravado.Id, faseCanonicaOrigemId, [ProdutoDaFase.Criar("RESULTADO_HOMOLOGACAO", PapelProdutoFase.Definitivo)]);

        (await ObterProdutosDaFaseAsync(gravado.Id)).Should().ContainSingle()
            .Which.Papel.Should().Be(PapelProdutoFase.Definitivo);
    }

    private static void AfirmarQuePreliminarReusouALinhaRastreada(
        IReadOnlyCollection<ProdutoDaFase> produtos, Guid produtoIdAntes)
    {
        produtos.Should().HaveCount(2);
        produtos.Should().ContainSingle(p => p.Papel == PapelProdutoFase.Preliminar)
            .Which.Should().Match<ProdutoDaFase>(p =>
                p.Id == produtoIdAntes && p.AtoCodigo == "RESULTADO_HOMOLOGACAO",
                "o produto que permanece é retargetado na linha rastreada, não recriado — é o Id dele que o ato publicado usa para achar a configuração de recurso");
    }

    private static void AfirmarQueDefinitivaPreexistenteSobreviveEPreliminarChegou(
        IReadOnlyCollection<ProdutoDaFase> produtos, Guid definitivaIdAntes)
    {
        produtos.Should().HaveCount(2);
        produtos.Should().ContainSingle(p => p.Papel == PapelProdutoFase.Definitivo)
            .Which.Id.Should().Be(definitivaIdAntes,
                "a publicação que já existia é a mesma linha — a que chegou é a outra, e a reconciliação não pode trocar uma pela outra");
        produtos.Should().ContainSingle(p => p.Papel == PapelProdutoFase.Preliminar);
        produtos.Select(p => p.AtoCodigo).Distinct().Should().ContainSingle();
    }

    private static List<ProdutoDaFase> PreliminarEDefinitivaDaHomologacao() =>
    [
        ProdutoDaFase.Criar("RESULTADO_HOMOLOGACAO", PapelProdutoFase.Preliminar),
        ProdutoDaFase.Criar("RESULTADO_HOMOLOGACAO", PapelProdutoFase.Definitivo),
    ];

    private async Task<ProcessoSeletivo> GravarProcessoComCronogramaAsync(
        string nome, Guid faseCanonicaOrigemId, IReadOnlyList<ProdutoDaFase> produtos)
    {
        await using UniPlusDbContext db = _fixture.CreateDbContext();
        ProcessoSeletivo processo = NovoProcesso(nome);
        processo.DefinirCronogramaFases([Fase(faseCanonicaOrigemId, produtos)], []).IsSuccess.Should().BeTrue();

        await db.ProcessosSeletivos.AddAsync(processo);
        await db.SaveChangesAsync();
        return processo;
    }

    private async Task RedefinirCronogramaAsync(
        Guid processoId, Guid faseCanonicaOrigemId, IReadOnlyList<ProdutoDaFase> produtos)
    {
        await using UniPlusDbContext db = _fixture.CreateDbContext();
        ProcessoSeletivo processo = await CarregarAsync(db, processoId);

        Result redefinicao = processo.DefinirCronogramaFases([Fase(faseCanonicaOrigemId, produtos)], []);

        redefinicao.IsSuccess.Should().BeTrue(redefinicao.Error?.Message);
        await db.SaveChangesAsync();
    }

    private async Task<IReadOnlyCollection<ProdutoDaFase>> ObterProdutosDaFaseAsync(Guid processoId)
    {
        await using UniPlusDbContext db = _fixture.CreateDbContext();
        ProcessoSeletivo processo = await CarregarAsync(db, processoId);
        return processo.CronogramaFases.Single().Produtos;
    }

    private static ProcessoSeletivo NovoProcesso(string nome) => ProcessoSeletivo.Criar(
        nome, TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    private static FaseCronograma Fase(Guid faseCanonicaOrigemId, IReadOnlyList<ProdutoDaFase> produtos) =>
        FaseCronograma.Criar(
            new OrigemCanonicaDaFase(
                OrigemId: faseCanonicaOrigemId, Codigo: "HOMOLOGACAO_INSCRICOES", OrigemData: OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false,
                ColetaSolicitacaoIsencao: false),
            new DecisoesDeCronogramaDaFase(Ordem: 1, Inicio: null, Fim: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
            new PublicacoesDaFase(Produtos: produtos, EmiteParecerIndividual: false, RegraRecurso: null),
            bancasRequeridas: []).Value!;

    private static async Task<ProcessoSeletivo> CarregarAsync(UniPlusDbContext db, Guid processoId) =>
        await db.ProcessosSeletivos
            .Include(p => p.CronogramaFases).ThenInclude(f => f.Produtos)
            .Include(p => p.CronogramaFases).ThenInclude(f => f.BancasRequeridas).ThenInclude(b => b.CandidatosAtendidos)
            .AsSplitQuery()
            .FirstAsync(p => p.Id == processoId);
}
