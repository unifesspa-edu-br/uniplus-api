namespace Unifesspa.UniPlus.Selecao.IntegrationTests.RolDeRegras;

using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
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
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL fixo escrito no próprio teste; o único valor externo (o código da regra) entra por DbParameter.")]
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

    [Fact(DisplayName = "CA-07 — o reader resolve a regra nova e não resolve mais a antiga")]
    public async Task Reader_ObterAsync_RegraNova_Resolve()
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        RegraCatalogoReader reader = new(context);

        RegraCatalogo? nova = await reader.ObterAsync("RECURSO-PRAZO-ANCORADO-EM-ATO", "v1", CancellationToken.None);
        nova.Should().NotBeNull();
        nova!.Tipo.Should().Be(TipoRegra.RegraPrazoRecurso);

        RegraCatalogo? antiga = await reader.ObterAsync("RECURSO-MULTI-INSTANCIA", "v1", CancellationToken.None);
        antiga.Should().BeNull("a regra que gere a segunda instância deixou de existir");
    }

    [Fact(DisplayName = "CA-08 — há exatamente uma regra de prazo de recurso — a nova")]
    public async Task Reader_ListarPorTipo_PrazoRecurso_ExatamenteUma()
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        RegraCatalogoReader reader = new(context);

        IReadOnlyList<RegraCatalogo> regras = await reader.ListarPorTipoAsync(
            TipoRegra.RegraPrazoRecurso, CancellationToken.None);

        regras.Should().ContainSingle()
            .Which.Codigo.Should().Be("RECURSO-PRAZO-ANCORADO-EM-ATO");
    }

    // O código da regra removida, como valor JSON — entre aspas. Casar o token
    // aspado (e não a substring nua) impede que um OUTRO código que apenas
    // contenha este como prefixo (ex.: RECURSO-MULTI-INSTANCIA-LEGACY) bloqueie a
    // substituição por engano: o valor JSON só bate quando é igual, delimitado.
    private const string TokenCodigoRemovido = "\"RECURSO-MULTI-INSTANCIA\"";

    private const string DetectaReferenciaJsonb = """
        WITH amostra(configuracao_congelada) AS (VALUES (@amostra::jsonb))
        SELECT count(*) FROM amostra
        WHERE configuracao_congelada::text LIKE '%' || @token || '%'
        """;

    private const string ContaReferenciasReais = """
        SELECT count(*) FROM selecao.versoes_configuracao
        WHERE configuracao_congelada::text LIKE '%' || @token || '%'
        """;

    [Fact(DisplayName = "CA-12 — nenhuma configuração congelada referencia a regra substituída (fronteira da ADR-0112)")]
    public async Task RegraCatalogoSeed_SubstituirRegraReferenciada_Falha()
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();

        // Canário positivo: o detector ENXERGA uma configuração que referencia a
        // regra removida pelo seu valor de código. Sem esta prova, a ausência real
        // abaixo não significaria nada — um detector quebrado passaria como verde.
        long detectados = await ContarAsync(
            context,
            DetectaReferenciaJsonb,
            amostra: """{"regra":{"codigo":"RECURSO-MULTI-INSTANCIA","versao":"v1"}}""");
        detectados.Should().Be(1, "o detector precisa enxergar a referência para a ausência provar algo");

        // Canário negativo: um código DISTINTO que apenas contém o removido como
        // prefixo não pode ser confundido com ele — a fronteira da ADR-0112 é por
        // identidade da regra, não por substring.
        long falsosPositivos = await ContarAsync(
            context,
            DetectaReferenciaJsonb,
            amostra: """{"regra":{"codigo":"RECURSO-MULTI-INSTANCIA-LEGACY","versao":"v1"}}""");
        falsosPositivos.Should().Be(0, "um código diferente que contém o removido como prefixo não é o removido");

        // Schema real (banco efêmero migrado): nenhuma VersaoConfiguracao congelada
        // referencia a regra que o seed substituiu — é o que torna a substituição
        // legítima no schema-alvo dos testes (ADR-0112). Para bases já implantadas,
        // a verificação da fronteira roda como precondição do fluxo de migração,
        // não neste teste.
        long referenciasReais = await ContarAsync(context, ContaReferenciasReais, amostra: null);
        referenciasReais.Should().Be(
            0,
            "substituir uma regra já congelada por uma configuração violaria o append-only (RN08)");
    }

    private static async Task<long> ContarAsync(SelecaoDbContext context, string sql, string? amostra)
    {
        DbConnection conexao = context.Database.GetDbConnection();
        if (conexao.State != System.Data.ConnectionState.Open)
        {
            await conexao.OpenAsync(CancellationToken.None);
        }

        await using DbCommand comando = conexao.CreateCommand();
        comando.CommandText = sql;
        AdicionarParametro(comando, "token", TokenCodigoRemovido);
        if (amostra is not null)
        {
            AdicionarParametro(comando, "amostra", amostra);
        }

        object? resultado = await comando.ExecuteScalarAsync(CancellationToken.None);
        return Convert.ToInt64(resultado, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void AdicionarParametro(DbCommand comando, string nome, string valor)
    {
        DbParameter parametro = comando.CreateParameter();
        parametro.ParameterName = nome;
        parametro.Value = valor;
        comando.Parameters.Add(parametro);
    }
}
