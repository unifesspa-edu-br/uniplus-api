namespace Unifesspa.UniPlus.Selecao.IntegrationTests.RolDeRegras;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Readers;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Seed;

/// <summary>
/// Cobertura de integração (Postgres real via Testcontainers) da biblioteca
/// <c>rol_de_regras</c> (Story #772): o seed das 18 regras <c>v1</c>, a
/// content-addressability do hash sobrevivendo à normalização jsonb do
/// Postgres, a coexistência de versões (<c>UNIQUE (codigo, versao)</c>) e o
/// <see cref="RegraCatalogoReader"/>.
/// </summary>
public sealed class RegraCatalogoSeedTests : IClassFixture<RegraCatalogoDbFixture>
{
    private readonly RegraCatalogoDbFixture _fixture;

    public RegraCatalogoSeedTests(RegraCatalogoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Seed materializa exatamente as 18 regras v1 do catálogo")]
    public async Task Seed_MaterializaAsDezoitoRegras()
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();

        List<RegraCatalogo> regras = await context.RolDeRegras
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);

        regras.Should().HaveCount(RegraCatalogoSeed.Itens.Count).And.HaveCount(18);
        regras.Select(r => r.Codigo).Should().OnlyHaveUniqueItems();
        regras.Should().OnlyContain(r => r.Versao == RegraCatalogoSeed.VersaoV1);
        regras.Should().Contain(r => r.Codigo == "DISTRIB-VAGAS-LEI-12711" && r.Tipo == TipoRegra.RegraDistribuicaoVagas);
        regras.Should().Contain(r => r.Codigo == "FORMULA-MEDIA-PONDERADA" && r.Tipo == TipoRegra.RegraCalculo);
    }

    [Fact(DisplayName = "Hash gravado é content-addressable: recomputar após round-trip jsonb bate")]
    public async Task Seed_HashContentAddressable_RecomputeBate()
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();

        List<RegraCatalogo> regras = await context.RolDeRegras
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);

        // A normalização jsonb do Postgres pode reordenar chaves/remover espaços;
        // como o hash canoniza recursivamente, recomputar a partir do estado
        // materializado deve reproduzir o hash gravado — content-addressability.
        foreach (RegraCatalogo regra in regras)
        {
            regra.RecomputeHash().Should().Be(regra.Hash, $"a regra {regra.Codigo} deve ser content-addressable");
        }

        // E o hash gravado deve bater com o computado pela fonte única do seed.
        foreach (RegraCatalogoSeedItem item in RegraCatalogoSeed.Itens)
        {
            RegraCatalogo persistida = regras.Single(r => r.Codigo == item.Codigo && r.Versao == item.Versao);
            persistida.Hash.Should().Be(item.ComputarHash());
        }
    }

    [Fact(DisplayName = "esquema_args/invariantes preservam o conteúdo semântico após o round-trip jsonb")]
    public async Task Seed_PayloadJsonb_PreservaConteudo()
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();

        RegraCatalogo bonus = await context.RolDeRegras
            .AsNoTracking()
            .SingleAsync(r => r.Codigo == "BONUS-MULTIPLICATIVO", CancellationToken.None);

        bonus.EsquemaArgs.ValueKind.Should().Be(JsonValueKind.Object);
        bonus.EsquemaArgs.GetProperty("fator").GetString().Should().Be("numeric");
        bonus.Invariantes.ValueKind.Should().Be(JsonValueKind.Array);
        bonus.Invariantes.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact(DisplayName = "v1 e v2 de um mesmo código coexistem (UNIQUE por codigo+versao)")]
    public async Task Codigo_V1EV2_Coexistem()
    {
        // Este teste muta o banco compartilhado do fixture (insere uma v2). Para
        // não vazar estado para as demais asserções de contagem do seed (que
        // exigem exatamente as 18 v1), a v2 é removida ao final — a ordem de
        // execução dentro da classe deixa de importar.
        Guid v2Id;
        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            using JsonDocument esquema = JsonDocument.Parse("""{"fonte_pesos":["etapa"]}""");
            using JsonDocument invariantes = JsonDocument.Parse("""["divisor = Σ(peso)"]""");

            Result<RegraCatalogo> v2 = RegraCatalogo.Criar(
                "FORMULA-MEDIA-PONDERADA", "v2", TipoRegra.RegraCalculo,
                esquema.RootElement, invariantes.RootElement, "Revisão da fórmula (nova versão)");
            v2.IsSuccess.Should().BeTrue();
            v2Id = v2.Value!.Id;

            writeContext.RolDeRegras.Add(v2.Value!);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        try
        {
            await using SelecaoDbContext readContext = _fixture.CreateDbContext();
            List<RegraCatalogo> versoes = await readContext.RolDeRegras
                .AsNoTracking()
                .Where(r => r.Codigo == "FORMULA-MEDIA-PONDERADA")
                .OrderBy(r => r.Versao)
                .ToListAsync(CancellationToken.None);

            versoes.Select(r => r.Versao).Should().Equal("v1", "v2");
        }
        finally
        {
            await using SelecaoDbContext cleanupContext = _fixture.CreateDbContext();
            await cleanupContext.RolDeRegras
                .Where(r => r.Id == v2Id)
                .ExecuteDeleteAsync(CancellationToken.None);
        }
    }

    [Fact(DisplayName = "Reader resolve regra por (codigo, versao) e filtra por tipo")]
    public async Task Reader_ObterEListar()
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        RegraCatalogoReader reader = new(context);

        RegraCatalogo? formula = await reader.ObterAsync("FORMULA-MEDIA-PONDERADA", "v1", CancellationToken.None);
        formula.Should().NotBeNull();
        formula!.Tipo.Should().Be(TipoRegra.RegraCalculo);

        RegraCatalogo? inexistente = await reader.ObterAsync("FORMULA-MEDIA-PONDERADA", "v99", CancellationToken.None);
        inexistente.Should().BeNull();

        IReadOnlyList<RegraCatalogo> desempates = await reader.ListarPorTipoAsync(TipoRegra.CriterioDesempate, CancellationToken.None);
        desempates.Should().HaveCount(4);
        desempates.Should().OnlyContain(r => r.Tipo == TipoRegra.CriterioDesempate);
    }
}
