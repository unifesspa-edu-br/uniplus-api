namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Sementes;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.Sementes.BancoDaSementeParaTeste;

/// <summary>
/// A semente de termos de consentimento, contra Postgres real, com o que a administração cadastrou e removeu: o termo
/// que ela removeu não volta, e o código que outro termo, vivo ou removido, ocupa faz a semente pular o termo e a versão
/// dele, sem falhar nem alterar o termo que ocupa o código. Cada teste desfaz o que gravou, porque o banco da coleção é
/// compartilhado.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
public sealed class SementeDeTermoRemovidoTests
{
    private const string NomeDoTermo = "Termo fictício da semente de teste";
    private const string CodigoDoTermo = "TERMO_FICTICIO_DA_SEMENTE_DE_TESTE";
    private const string OutroNome = "Outro termo fictício de teste";

    private static readonly TermoDaSemente Semente = new(
        Guid.Parse("7e570001-0000-7000-8000-000000000001"), Guid.Parse("7e570002-0000-7000-8000-000000000001"),
        NomeDoTermo, "Texto fictício do termo de teste.", "Base legal fictícia.");

    private static readonly Guid OutroTermoId = Guid.Parse("7e570001-0000-7000-8000-000000000002");

    private static readonly Guid OutraVersaoId = Guid.Parse("7e570002-0000-7000-8000-000000000002");

    private static readonly DeclaracaoDaSemente Declaracao = new(
        "de teste", "semente-de-teste", new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero));

    private readonly ConfiguracaoDbFixture _fixture;

    public SementeDeTermoRemovidoTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "No banco sem o termo, a semente grava o termo e a versão promovida")]
    public async Task SemOTermo_GravaOTermoEAVersao()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        await using IDbContextTransaction transacao = await ctx.Database.BeginTransactionAsync();

        await AplicarAsync(ctx);

        (await ContarTermosComIdAsync(ctx, Semente.TermoId)).Should().Be(1);
        (await ContarVersoesAsync(ctx)).Should().Be(1);
        await transacao.RollbackAsync();
    }

    [Fact(DisplayName = "O termo da semente removido pela administração não volta na nova preparação do banco")]
    public async Task TermoDaSementeRemovido_NaoVolta()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        await using IDbContextTransaction transacao = await ctx.Database.BeginTransactionAsync();
        await AplicarAsync(ctx);
        await AdministracaoRemoveAsync(ctx, Semente.TermoId);

        await AplicarAsync(ctx);

        (await ContarTermosComIdAsync(ctx, Semente.TermoId)).Should().Be(1, "o termo removido continua na tabela");
        (await ContarTermosRemovidosComIdAsync(ctx)).Should().Be(1, "e continua removido");
        (await ContarTermosComCodigoAsync(ctx)).Should().Be(1, "nenhum registro novo ocupa o código");
        await transacao.RollbackAsync();
    }

    [Theory(DisplayName = "O código ocupado por outro termo, removido ou vivo, faz a semente pular o termo e a versão, sem falhar nem alterar o termo que o ocupa")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CodigoOcupadoPorOutroTermo_PulaOTermoEAVersao(bool ocupanteRemovido)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        await using IDbContextTransaction transacao = await ctx.Database.BeginTransactionAsync();
        await AdministracaoCadastraAsync(ctx, OutroTermoId, CodigoDoTermo, OutroNome, removido: ocupanteRemovido);

        await AplicarAsync(ctx);

        await ConfirmarQueATermoDaSementeNaoEntrouAsync(ctx);
        (await ContarTermosComCodigoAsync(ctx)).Should().Be(1, "só o termo que ocupa o código fica no ambiente");
        (await ContarTermosComIdAsync(ctx, OutroTermoId)).Should().Be(1);
        await transacao.RollbackAsync();
    }

    [Fact(DisplayName = "O código ocupado por termo vivo e por termo removido ao mesmo tempo também faz a semente pular o termo")]
    public async Task CodigoOcupadoPorTermoVivoEPorTermoRemovido_PulaOTermoEAVersao()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        await using IDbContextTransaction transacao = await ctx.Database.BeginTransactionAsync();
        await AdministracaoCadastraAsync(ctx, Guid.Parse("7e570001-0000-7000-8000-000000000003"), CodigoDoTermo, OutroNome, removido: true);
        await AdministracaoCadastraAsync(ctx, OutroTermoId, CodigoDoTermo, OutroNome, removido: false);

        await AplicarAsync(ctx);

        await ConfirmarQueATermoDaSementeNaoEntrouAsync(ctx);
        (await ContarTermosComCodigoAsync(ctx)).Should().Be(2);
        await transacao.RollbackAsync();
    }

    [Fact(DisplayName = "Reaplicar a semente em banco já preparado não duplica nem altera o que a administração editou")]
    public async Task Reaplicar_NaoDuplicaNemAlteraOQueAAdministracaoEditou()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        await using IDbContextTransaction transacao = await ctx.Database.BeginTransactionAsync();
        await AplicarAsync(ctx);
        await AdministracaoEditaAsync(ctx, Semente.TermoId);
        string antes = await RetratoAsync(ctx);

        await AplicarAsync(ctx);

        (await RetratoAsync(ctx)).Should().Be(antes);
        (await ContarTermosComIdAsync(ctx, Semente.TermoId)).Should().Be(1);
        (await ContarVersoesAsync(ctx)).Should().Be(1);
        await transacao.RollbackAsync();
    }

    [Fact(DisplayName = "O termo da semente já com uma versão de mesmo número e outro identificador não faz a semente falhar nem criar outra versão")]
    public async Task VersaoDeOutroIdentificadorComOMesmoNumero_NaoFalhaENaoCriaOutraVersao()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        await using IDbContextTransaction transacao = await ctx.Database.BeginTransactionAsync();
        await AdministracaoCadastraAsync(ctx, Semente.TermoId, CodigoDoTermo, NomeDoTermo, removido: false);
        await CadastrarVersaoAsync(ctx, OutraVersaoId, Semente.TermoId, numero: 1);

        await AplicarAsync(ctx);

        (await ContarVersoesAsync(ctx)).Should().Be(0, "a versão da semente não entra");
        (await ContarVersoesDoTermoAsync(ctx)).Should().Be(1, "só a versão que já estava no termo continua");
        await transacao.RollbackAsync();
    }

    private static async Task ConfirmarQueATermoDaSementeNaoEntrouAsync(UniPlusDbContext ctx)
    {
        (await ContarTermosComIdAsync(ctx, Semente.TermoId)).Should().Be(0, "o termo da semente não é criado");
        (await ContarVersoesAsync(ctx)).Should().Be(0, "a versão não aponta para um termo que não entrou");
    }

    private static async Task AplicarAsync(UniPlusDbContext ctx)
    {
        foreach (ComandoDaSemente comando in SqlDaSemente.ComandosDoTermo(Semente, Declaracao))
        {
            // O texto do termo pode trazer chaves, que não são marcadores de parâmetro.
            await ctx.Database.ExecuteSqlRawAsync(comando.Texto.Replace("{", "{{", StringComparison.Ordinal).Replace("}", "}}", StringComparison.Ordinal));
        }
    }

    private static async Task AdministracaoCadastraAsync(UniPlusDbContext ctx, Guid id, string codigo, string nome, bool removido) =>
        await ctx.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO configuracao.termos_de_consentimento
                (id, codigo, nome, forma_aceite_rascunho, revisado, is_deleted, deleted_at, deleted_by, created_at)
            VALUES ({id}, {codigo}, {nome}, 'REGISTRO_DIGITAL_COM_LOG_IP', FALSE, {removido},
                    CASE WHEN {removido} THEN now() END, CASE WHEN {removido} THEN 'administracao-de-teste' END, now())
            """);

    private static async Task AdministracaoRemoveAsync(UniPlusDbContext ctx, Guid id) =>
        await ctx.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE configuracao.termos_de_consentimento SET is_deleted = TRUE, deleted_at = now(), deleted_by = 'administracao-de-teste' WHERE id = {id}");

    private static async Task AdministracaoEditaAsync(UniPlusDbContext ctx, Guid id) =>
        await ctx.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE configuracao.termos_de_consentimento SET nome = 'Termo fictício editado', texto_rascunho = 'Texto fictício editado.', revisado = FALSE WHERE id = {id}");

    private static async Task<int> ContarTermosComIdAsync(UniPlusDbContext ctx, Guid id) =>
        await ctx.Database.SqlQuery<int>(
            $"SELECT count(*)::int AS \"Value\" FROM configuracao.termos_de_consentimento WHERE id = {id}").SingleAsync();

    private static async Task<int> ContarTermosRemovidosComIdAsync(UniPlusDbContext ctx) =>
        await ctx.Database.SqlQuery<int>(
            $"SELECT count(*)::int AS \"Value\" FROM configuracao.termos_de_consentimento WHERE id = {Semente.TermoId} AND is_deleted").SingleAsync();

    private static async Task<int> ContarTermosComCodigoAsync(UniPlusDbContext ctx) =>
        await ctx.Database.SqlQuery<int>(
            $"SELECT count(*)::int AS \"Value\" FROM configuracao.termos_de_consentimento WHERE codigo = {CodigoDoTermo}").SingleAsync();

    private static async Task<int> ContarVersoesAsync(UniPlusDbContext ctx) =>
        await ctx.Database.SqlQuery<int>(
            $"SELECT count(*)::int AS \"Value\" FROM configuracao.versoes_do_termo_consentimento WHERE id = {Semente.VersaoId}").SingleAsync();

    private static async Task<int> ContarVersoesDoTermoAsync(UniPlusDbContext ctx) =>
        await ctx.Database.SqlQuery<int>(
            $"SELECT count(*)::int AS \"Value\" FROM configuracao.versoes_do_termo_consentimento WHERE termo_consentimento_id = {Semente.TermoId}").SingleAsync();

    /// <summary>O conteúdo das linhas que a semente grava e a administração edita, para comparar antes e depois.</summary>
    private static async Task<string> RetratoAsync(UniPlusDbContext ctx)
    {
        List<string> linhas = await ctx.Database.SqlQueryRaw<string>(
            """
            SELECT concat_ws('|', 'termo', id::text, codigo, nome, texto_rascunho, revisado::text, is_deleted::text) AS "Value"
              FROM configuracao.termos_de_consentimento WHERE id::text LIKE '7e57%'
            UNION ALL SELECT concat_ws('|', 'versao', id::text, termo_consentimento_id::text, hash)
              FROM configuracao.versoes_do_termo_consentimento WHERE id::text LIKE '7e57%'
            """).ToListAsync();
        return string.Join("\n", linhas.Order(StringComparer.Ordinal));
    }
}
