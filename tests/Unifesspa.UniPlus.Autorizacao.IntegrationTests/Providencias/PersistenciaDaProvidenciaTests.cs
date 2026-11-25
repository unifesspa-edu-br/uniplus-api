namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Providencias;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using NpgsqlTypes;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// O que o banco garante sozinho na providência, mesmo contra a gravação que não passa pelo agregado: no máximo uma aberta
/// por concessão, o estado gravado sem "PREJUDICADA", o desfecho inteiro ou nenhum, o fundamento nos limites e a concessão
/// alvo que não se exclui.
/// </summary>
[Collection(AutorizacaoEndpointCollection.Name)]
public sealed class PersistenciaDaProvidenciaTests : ProvidenciaEndpointTestBase
{
    private const string Inserir = """
        INSERT INTO autorizacao.providencias
            (id, concessao_id, tipo, fundamento, estado, aberta_em, solicitada_por_emissor, solicitada_por_conta_id, created_at,
             desfecho_em, desfecho_por_emissor, desfecho_por_conta_id, motivo_do_desfecho)
        VALUES ({0}, {1}, {2}, {3}, {4}, now(), 'https://idp.test', 'cecilia.moura', now(), {5}, {6}, {7}, {8})
        """;

    public PersistenciaDaProvidenciaTests(AutorizacaoEndpointFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "A segunda providência aberta para a mesma concessão esbarra no índice único, e a respondida convive com a aberta")]
    public async Task IndiceUnico_DaAberta()
    {
        Guid concessao = await ConcessaoAsync();
        await InserirAsync(concessao, "ABERTA");
        await InserirAsync(concessao, "RESPONDIDA", comDesfecho: true);

        PostgresException violacao = await FalharAsync(() => InserirAsync(concessao, "ABERTA"));

        (violacao.SqlState, violacao.ConstraintName).Should().Be(("23505", "ux_providencias_concessao_aberta"));
    }

    [Theory(DisplayName = "O desfecho é gravado inteiro na respondida e na atendida, e a aberta não tem desfecho")]
    [InlineData("RESPONDIDA", false)]
    [InlineData("ATENDIDA", false)]
    [InlineData("ABERTA", true)]
    public async Task Check_DesfechoCompleto(string estado, bool comDesfecho)
    {
        Guid concessao = await ConcessaoAsync();

        PostgresException violacao = await FalharAsync(() => InserirAsync(concessao, estado, comDesfecho: comDesfecho));

        violacao.ConstraintName.Should().Be("ck_providencias_desfecho_completo");
    }

    [Theory(DisplayName = "O banco recusa o estado prejudicada, o tipo fora do vocabulário e o fundamento fora dos limites")]
    [InlineData("PREJUDICADA", "RETIRADA", 10, "ck_providencias_estado")]
    [InlineData("ABERTA", "SUSPENSAO", 10, "ck_providencias_tipo")]
    [InlineData("ABERTA", "RETIRADA", 9, "ck_providencias_fundamento")]
    public async Task Check_Vocabulario(string estado, string tipo, int caracteres, string restricao)
    {
        Guid concessao = await ConcessaoAsync();

        // Com o desfecho preenchido fora da aberta, a recusa vem só da regra testada, e não da do desfecho completo.
        PostgresException violacao = await FalharAsync(
            () => InserirAsync(concessao, estado, tipo, new string('a', caracteres), comDesfecho: estado != "ABERTA"));

        violacao.ConstraintName.Should().Be(restricao);
    }

    [Fact(DisplayName = "A concessão com providência não se exclui")]
    public async Task ChaveEstrangeira_Restrita()
    {
        Guid concessao = await ConcessaoAsync();
        await InserirAsync(concessao, "ABERTA");

        PostgresException violacao = await FalharAsync(() => ExecutarSqlAsync("DELETE FROM autorizacao.concessoes WHERE id = {0}", concessao));

        violacao.SqlState.Should().Be(PostgresErrorCodes.RestrictViolation);
    }

    [Fact]
    public async Task ListarPaginado_SituacaoDesconhecida_LancaExcecao()
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        ProvidenciaRepository repositorio = new(scope.ServiceProvider.GetRequiredService<UniPlusDbContext>());
        FiltroDeProvidencias filtro = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), (SituacaoDaProvidencia)999);

        Func<Task> listar = async () => await repositorio.ListarPaginadoAsync(filtro, null, null, 20, PaginationDirection.Next);

        await listar.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    private async Task<Guid> ConcessaoAsync() =>
        await ConcederAsync(await CriarPerfilComumAsync(), Leandro, EscopoDaConcessao.DaUnidade(Guid.NewGuid()));

    private Task<int> InserirAsync(Guid concessaoId, string estado, string tipo = "RETIRADA", string fundamento = Fundamento, bool comDesfecho = false) =>
        ExecutarSqlAsync(
            Inserir, Guid.CreateVersion7(), concessaoId, tipo, fundamento, estado,
            Se(comDesfecho, DateTimeOffset.UtcNow, NpgsqlDbType.TimestampTz), Se(comDesfecho, "https://idp.test", NpgsqlDbType.Text),
            Se(comDesfecho, "diogo.souza", NpgsqlDbType.Text), Se(comDesfecho, Motivo, NpgsqlDbType.Text));

    // O nulo vai tipado: o EF Core não mapeia um DBNull solto num parâmetro de SQL cru.
    private static NpgsqlParameter Se(bool presente, object valor, NpgsqlDbType tipo) =>
        new() { NpgsqlDbType = tipo, Value = presente ? valor : DBNull.Value };

    private static async Task<PostgresException> FalharAsync(Func<Task<int>> gravar)
    {
        Func<Task> acao = gravar;
        return (await acao.Should().ThrowAsync<PostgresException>()).Which;
    }
}
