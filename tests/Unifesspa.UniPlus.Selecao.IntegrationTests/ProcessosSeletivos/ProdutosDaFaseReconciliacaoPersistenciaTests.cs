namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// A reconciliação dos produtos por <c>ato_codigo</c> dentro de
/// <c>FaseCronograma.AtualizarSnapshot</c>, contra Postgres real.
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
        Guid processoId;
        Guid produtoIdAntes;

        await using (SelecaoDbContext db = _fixture.CreateDbContext())
        {
            ProcessoSeletivo processo = NovoProcesso($"PS produtos {Guid.CreateVersion7()}");
            processo.DefinirCronogramaFases(
                [Fase(faseCanonicaOrigemId, [ProdutoDaFase.Criar("HOMOLOGACAO_PRELIMINAR", PapelProdutoFase.Definitivo)])],
                [],
                PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

            await db.ProcessosSeletivos.AddAsync(processo);
            await db.SaveChangesAsync();

            processoId = processo.Id;
            produtoIdAntes = processo.CronogramaFases.Single().Produtos.Single().Id;
        }

        await using (SelecaoDbContext db = _fixture.CreateDbContext())
        {
            ProcessoSeletivo processo = await CarregarAsync(db, processoId);

            // O mesmo ato_codigo com outro papel, mais um segundo produto: é a edição comum de
            // quem passa a declarar o ciclo recursal da fase.
            Result redefinicao = processo.DefinirCronogramaFases(
                [Fase(faseCanonicaOrigemId,
                [
                    ProdutoDaFase.Criar("HOMOLOGACAO_PRELIMINAR", PapelProdutoFase.Preliminar),
                    ProdutoDaFase.Criar("HOMOLOGACAO_DEFINITIVA", PapelProdutoFase.Definitivo),
                ])],
                [],
                PrecondicaoIfMatch.Curinga);

            redefinicao.IsSuccess.Should().BeTrue(redefinicao.Error?.Message);
            await db.SaveChangesAsync();
        }

        await using (SelecaoDbContext db = _fixture.CreateDbContext())
        {
            ProcessoSeletivo processo = await CarregarAsync(db, processoId);
            IReadOnlyCollection<ProdutoDaFase> produtos = processo.CronogramaFases.Single().Produtos;

            produtos.Should().HaveCount(2);
            produtos.Should().ContainSingle(p => p.AtoCodigo == "HOMOLOGACAO_PRELIMINAR")
                .Which.Should().Match<ProdutoDaFase>(p =>
                    p.Id == produtoIdAntes && p.Papel == PapelProdutoFase.Preliminar,
                    "o produto que permanece é retargetado na linha rastreada, não recriado — é o Id dele que o ato publicado usa para achar a configuração de recurso");
        }
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
        Guid processoId;
        Guid definitivaIdAntes;

        await using (SelecaoDbContext db = _fixture.CreateDbContext())
        {
            // Começa só com a definitiva porque a fase que publica preliminar precisa de quem
            // conclua o ciclo: o estado intermediário de um par pela metade o agregado recusa.
            ProcessoSeletivo processo = NovoProcesso($"PS matéria {Guid.CreateVersion7()}");
            processo.DefinirCronogramaFases(
                [Fase(faseCanonicaOrigemId, [ProdutoDaFase.Criar("RESULTADO_HOMOLOGACAO", PapelProdutoFase.Definitivo)])],
                [],
                PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

            await db.ProcessosSeletivos.AddAsync(processo);
            await db.SaveChangesAsync();

            processoId = processo.Id;
            definitivaIdAntes = processo.CronogramaFases.Single().Produtos.Single().Id;
        }

        await using (SelecaoDbContext db = _fixture.CreateDbContext())
        {
            ProcessoSeletivo processo = await CarregarAsync(db, processoId);

            Result redefinicao = processo.DefinirCronogramaFases(
                [Fase(faseCanonicaOrigemId,
                [
                    ProdutoDaFase.Criar("RESULTADO_HOMOLOGACAO", PapelProdutoFase.Preliminar),
                    ProdutoDaFase.Criar("RESULTADO_HOMOLOGACAO", PapelProdutoFase.Definitivo),
                ])],
                [],
                PrecondicaoIfMatch.Curinga);

            redefinicao.IsSuccess.Should().BeTrue(redefinicao.Error?.Message);
            await db.SaveChangesAsync();
        }

        await using (SelecaoDbContext db = _fixture.CreateDbContext())
        {
            ProcessoSeletivo processo = await CarregarAsync(db, processoId);
            IReadOnlyCollection<ProdutoDaFase> produtos = processo.CronogramaFases.Single().Produtos;

            produtos.Should().HaveCount(2);
            produtos.Should().ContainSingle(p => p.Papel == PapelProdutoFase.Definitivo)
                .Which.Id.Should().Be(definitivaIdAntes,
                    "a publicação que já existia é a mesma linha — a que chegou é a outra, e a reconciliação não pode trocar uma pela outra");
            produtos.Should().ContainSingle(p => p.Papel == PapelProdutoFase.Preliminar);
            produtos.Select(p => p.AtoCodigo).Distinct().Should().ContainSingle();
        }
    }

    [Fact(DisplayName = "Produto que sai da declaração é removido da tabela, sem sobrar linha órfã")]
    public async Task RedefinirCronograma_ProdutoRemovido_SaiDaTabela()
    {
        Guid faseCanonicaOrigemId = Guid.CreateVersion7();
        Guid processoId;

        await using (SelecaoDbContext db = _fixture.CreateDbContext())
        {
            ProcessoSeletivo processo = NovoProcesso($"PS produtos removidos {Guid.CreateVersion7()}");
            processo.DefinirCronogramaFases(
                [Fase(faseCanonicaOrigemId,
                [
                    ProdutoDaFase.Criar("HOMOLOGACAO_PRELIMINAR", PapelProdutoFase.Preliminar),
                    ProdutoDaFase.Criar("HOMOLOGACAO_DEFINITIVA", PapelProdutoFase.Definitivo),
                ])],
                [],
                PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

            await db.ProcessosSeletivos.AddAsync(processo);
            await db.SaveChangesAsync();
            processoId = processo.Id;
        }

        await using (SelecaoDbContext db = _fixture.CreateDbContext())
        {
            ProcessoSeletivo processo = await CarregarAsync(db, processoId);
            processo.DefinirCronogramaFases(
                [Fase(faseCanonicaOrigemId, [ProdutoDaFase.Criar("HOMOLOGACAO_DEFINITIVA", PapelProdutoFase.Definitivo)])],
                [],
                PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();

            await db.SaveChangesAsync();
        }

        await using (SelecaoDbContext db = _fixture.CreateDbContext())
        {
            ProcessoSeletivo processo = await CarregarAsync(db, processoId);

            processo.CronogramaFases.Single().Produtos.Should().ContainSingle()
                .Which.AtoCodigo.Should().Be("HOMOLOGACAO_DEFINITIVA");
        }
    }

    private static ProcessoSeletivo NovoProcesso(string nome) => ProcessoSeletivo.Criar(
        nome, TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

    private static FaseCronograma Fase(Guid faseCanonicaOrigemId, IReadOnlyList<ProdutoDaFase> produtos) =>
        FaseCronograma.Criar(
            ordem: 1,
            faseCanonicaOrigemId: faseCanonicaOrigemId,
            codigo: "HOMOLOGACAO",
            donoInstitucional: "CEPS",
            origemData: OrigemDataFase.Delegada,
            agrupaEtapas: false,
            permiteComplementacao: false,
            coletaInscricao: false,
            coletaSolicitacaoIsencao: false,
            inicio: null,
            fim: null,
            produtos: produtos,
            faseConcluinteCodigo: null,
            emiteParecerIndividual: false,
            bancasRequeridas: [],
            regraRecurso: null).Value!;

    private static async Task<ProcessoSeletivo> CarregarAsync(SelecaoDbContext db, Guid processoId) =>
        await db.ProcessosSeletivos
            .Include(p => p.CronogramaFases).ThenInclude(f => f.Produtos)
            .Include(p => p.CronogramaFases).ThenInclude(f => f.BancasRequeridas).ThenInclude(b => b.RecorteDeCompetencia)
            .AsSplitQuery()
            .FirstAsync(p => p.Id == processoId);
}
