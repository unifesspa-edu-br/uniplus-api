namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Sementes;

using System.Reflection;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// Os dez tipos de banca existem ao subir o sistema, cada um com o papel, as marcas e os tipos de decisão que julga,
/// gravados pelo script de sementes do módulo. O banco da coleção é compartilhado com testes que cadastram tipos, então cada
/// teste parte de uma tabela vazia dentro de uma transação desfeita ao fim.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
public sealed class SementeTiposDeBancaTests
{
    private const string TodosOsTipos =
        "SELECT codigo || '|' || papel || '|' || exige_duas_ou_mais_bancas::text || '|' || encerra_recurso_na_primeira_instancia::text || '|' || array_to_string(tipos_de_decisao_julgados, ',') AS \"Value\" FROM configuracao.tipos_de_banca WHERE NOT is_deleted ORDER BY id";

    private static readonly string[] Esperados =
    [
        "BANCA_ANALISE_DOCUMENTAL|AVALIA_O_MERITO|false|false|ANALISE_DOCUMENTAL,ISENCAO_DA_TAXA",
        "BANCA_ENTREVISTA|AVALIA_O_MERITO|false|false|NOTA_DA_ETAPA",
        "BANCA_CORRECAO_REDACOES|AVALIA_O_MERITO|false|false|NOTA_DA_ETAPA",
        "BANCA_ANALISE_RECURSOS|JULGA_A_PRIMEIRA_INSTANCIA|false|false|",
        "BANCA_HETEROIDENTIFICACAO|AVALIA_O_MERITO|true|true|HETEROIDENTIFICACAO",
        "BANCA_BIOPSICOSSOCIAL|AVALIA_O_MERITO|true|false|AVALIACAO_BIOPSICOSSOCIAL",
        "BANCA_HOMOLOGACAO_INSCRICOES|AVALIA_O_MERITO|false|false|HOMOLOGACAO_DAS_INSCRICOES",
        "BANCA_IMPORTACAO_NOTAS_INEP|IMPORTA_AS_NOTAS_DO_INEP|false|false|",
        "BANCA_IMPORTACAO_CANDIDATOS|IMPORTA_CANDIDATOS|false|false|",
        "BANCA_ANALISE_RECURSOS_SEGUNDA_INSTANCIA|JULGA_A_SEGUNDA_INSTANCIA|false|false|",
    ];

    private readonly ConfiguracaoDbFixture _fixture;

    public SementeTiposDeBancaTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "O script de sementes grava os dez tipos de banca com o papel, as marcas e os tipos de decisão do cadastro inicial")]
    public async Task Script_GravaOsDezTiposComAAtuacao()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        await using IDbContextTransaction transacao = await ctx.Database.BeginTransactionAsync();
        await ctx.Database.ExecuteSqlRawAsync("DELETE FROM configuracao.tipos_de_banca");

        await ReaplicarOScriptAsync(ctx);

        (await LerAsync<string>(ctx, TodosOsTipos)).Should().Equal(Esperados);
        await transacao.RollbackAsync();
    }

    [Fact(DisplayName = "Reaplicar o script de sementes não duplica nada nem devolve o que a administração editou ou removeu")]
    public async Task Reaplicar_PreservaAsEdicoesDaAdministracao()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        await using IDbContextTransaction transacao = await ctx.Database.BeginTransactionAsync();
        await ctx.Database.ExecuteSqlRawAsync("DELETE FROM configuracao.tipos_de_banca");
        await ReaplicarOScriptAsync(ctx);
        await EditarERemoverAsync(ctx);

        await ReaplicarOScriptAsync(ctx);

        (await LerAsync<string>(ctx, "SELECT nome || '|' || exige_duas_ou_mais_bancas::text AS \"Value\" FROM configuracao.tipos_de_banca WHERE codigo = 'BANCA_ENTREVISTA'"))
            .Should().Equal(["Entrevista da unidade|true"], "a edição da administração não é sobrescrita");
        (await LerAsync<string>(ctx, TodosOsTipos)).Should().HaveCount(9, "o tipo removido não volta");
        (await LerAsync<int>(ctx, "SELECT count(*)::int AS \"Value\" FROM configuracao.tipos_de_banca")).Should().Equal([10], "nenhuma linha é duplicada");
        await transacao.RollbackAsync();
    }

    [Fact(DisplayName = "O código que a administração ocupou com outro registro e removeu não volta na reaplicação do script")]
    public async Task Reaplicar_NaoRecriaOCodigoOcupadoERemovido()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        await using IDbContextTransaction transacao = await ctx.Database.BeginTransactionAsync();
        await ctx.Database.ExecuteSqlRawAsync("DELETE FROM configuracao.tipos_de_banca");
        await OcuparERemoverOCodigoDaEntrevistaAsync(ctx);

        await ReaplicarOScriptAsync(ctx);

        (await LerAsync<int>(ctx, "SELECT count(*)::int AS \"Value\" FROM configuracao.tipos_de_banca WHERE codigo = 'BANCA_ENTREVISTA' AND NOT is_deleted"))
            .Should().Equal([0], "o código removido pela administração não é recriado");
        await transacao.RollbackAsync();
    }

    private static Task<int> OcuparERemoverOCodigoDaEntrevistaAsync(UniPlusDbContext ctx) =>
        ctx.Database.ExecuteSqlRawAsync(
            "INSERT INTO configuracao.tipos_de_banca (id, codigo, nome, papel, exige_duas_ou_mais_bancas, encerra_recurso_na_primeira_instancia, tipos_de_decisao_julgados, created_at, is_deleted) "
            + "VALUES (gen_random_uuid(), 'BANCA_ENTREVISTA', 'Entrevista removida', 'IMPORTA_CANDIDATOS', false, false, ARRAY[]::text[], now(), true)");

    private static async Task EditarERemoverAsync(UniPlusDbContext ctx)
    {
        await ctx.Database.ExecuteSqlRawAsync(
            "UPDATE configuracao.tipos_de_banca SET nome = 'Entrevista da unidade', exige_duas_ou_mais_bancas = true WHERE codigo = 'BANCA_ENTREVISTA'");
        await ctx.Database.ExecuteSqlRawAsync(
            "UPDATE configuracao.tipos_de_banca SET is_deleted = true WHERE codigo = 'BANCA_IMPORTACAO_CANDIDATOS'");
    }

    private static Task<List<T>> LerAsync<T>(UniPlusDbContext ctx, string sql) =>
        ctx.Database.SqlQueryRaw<T>(sql).ToListAsync();

    private static async Task ReaplicarOScriptAsync(UniPlusDbContext ctx) =>
        await ctx.Database.ExecuteSqlRawAsync(ScriptDeSemente());

    private static string ScriptDeSemente()
    {
        Assembly infra = typeof(ConfiguracaoDbSets).Assembly;
        string recurso = infra.GetManifestResourceNames().Single(static n => n.Contains("007_semente_dos_tipos_de_banca", StringComparison.Ordinal));
        using var leitor = new StreamReader(infra.GetManifestResourceStream(recurso)!);
        return leitor.ReadToEnd();
    }
}
