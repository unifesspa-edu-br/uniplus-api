namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Perfis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using NpgsqlTypes;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// O banco recusa a divergência da governança cujos valores não batem com o tipo: a composição sem valores, a classificação
/// só com o par de classificações diferentes e o escopo só com o par de escopos diferentes (SQLSTATE 23514). Cada gravação
/// roda numa transação desfeita ao fim.
/// </summary>
[Collection(AutorizacaoEndpointCollection.Name)]
public sealed class ValoresDaDivergenciaNoBancoTests : PerfilEndpointTestBase, IAsyncLifetime
{
    private const string ViolacaoDeRestricaoDeVerificacao = "23514";
    private const string Restricao = "ck_divergencias_da_governanca_valores";

    private const string DivergenciaSql =
        """
        INSERT INTO autorizacao.divergencias_da_governanca
            (id, implantacao_id, perfil_id, codigo, tipo, classificacao_anterior, classificacao_nova, de_unidade_anterior, de_unidade_novo,
             instante, ordem_no_historico)
        SELECT @id, i.id, @perfil, 'configuracao:cursos:manter', @tipo, @classificacaoAnterior, @classificacaoNova, @deUnidadeAnterior, @deUnidadeNovo, now(),
               nextval('autorizacao.ordem_do_historico_de_acesso')
        FROM autorizacao.implantacoes_das_permissoes i ORDER BY i.sequencia DESC LIMIT 1
        """;

    private readonly CenarioDaReclassificacao _cenario;

    public ValoresDaDivergenciaNoBancoTests(AutorizacaoEndpointFixture fixture)
        : base(fixture)
    {
        _cenario = new CenarioDaReclassificacao(Fixture);
    }

    public Task InitializeAsync() => _cenario.ConciliarAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory(DisplayName = "O banco aceita a divergência cujos valores batem com o tipo")]
    [InlineData("COMPOSICAO_FALTANTE", null, null, null, null)]
    [InlineData("COMPOSICAO_EXCEDENTE", null, null, null, null)]
    [InlineData("CLASSIFICACAO", "INTERNO", "SENSIVEL", null, null)]
    [InlineData("ESCOPO", null, null, false, true)]
    public async Task ValoresCoerentes_SaoAceitos(
        string tipo, string? classificacaoAnterior, string? classificacaoNova, bool? deUnidadeAnterior, bool? deUnidadeNovo)
    {
        int gravadas = await GravarSemConfirmarAsync(tipo, classificacaoAnterior, classificacaoNova, deUnidadeAnterior, deUnidadeNovo);

        gravadas.Should().Be(1);
    }

    [Theory(DisplayName = "O banco recusa a divergência cujos valores não batem com o tipo")]
    [InlineData("COMPOSICAO_FALTANTE", "INTERNO", "SENSIVEL", null, null)]
    [InlineData("COMPOSICAO_EXCEDENTE", null, null, false, true)]
    [InlineData("CLASSIFICACAO", null, null, null, null)]
    [InlineData("CLASSIFICACAO", "INTERNO", "INTERNO", null, null)]
    [InlineData("CLASSIFICACAO", "INTERNO", null, null, null)]
    [InlineData("CLASSIFICACAO", "INTERNO", "SENSIVEL", false, true)]
    [InlineData("ESCOPO", null, null, null, null)]
    [InlineData("ESCOPO", null, null, true, true)]
    [InlineData("ESCOPO", null, null, false, null)]
    [InlineData("ESCOPO", "INTERNO", "SENSIVEL", false, true)]
    public async Task ValoresIncoerentes_SaoRecusados(
        string tipo, string? classificacaoAnterior, string? classificacaoNova, bool? deUnidadeAnterior, bool? deUnidadeNovo)
    {
        Func<Task<int>> gravar = () => GravarSemConfirmarAsync(tipo, classificacaoAnterior, classificacaoNova, deUnidadeAnterior, deUnidadeNovo);

        PostgresException erro = (await gravar.Should().ThrowAsync<PostgresException>()).Which;

        erro.SqlState.Should().Be(ViolacaoDeRestricaoDeVerificacao);
        erro.ConstraintName.Should().Be(Restricao);
    }

    // A transação nunca é confirmada: nada fica no banco compartilhado pelas outras suítes.
    private async Task<int> GravarSemConfirmarAsync(
        string tipo, string? classificacaoAnterior, string? classificacaoNova, bool? deUnidadeAnterior, bool? deUnidadeNovo)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        await using IDbContextTransaction transacao = await db.Database.BeginTransactionAsync();

        return await db.Database.ExecuteSqlRawAsync(
            DivergenciaSql,
            new NpgsqlParameter("id", Guid.CreateVersion7()),
            new NpgsqlParameter("perfil", PerfisDeSistema.GestorDeAcessosId),
            new NpgsqlParameter("tipo", tipo),
            Parametro("classificacaoAnterior", NpgsqlDbType.Varchar, classificacaoAnterior),
            Parametro("classificacaoNova", NpgsqlDbType.Varchar, classificacaoNova),
            Parametro("deUnidadeAnterior", NpgsqlDbType.Boolean, deUnidadeAnterior),
            Parametro("deUnidadeNovo", NpgsqlDbType.Boolean, deUnidadeNovo));
    }

    private static NpgsqlParameter Parametro(string nome, NpgsqlDbType tipo, object? valor) =>
        new(nome, tipo) { Value = valor ?? DBNull.Value };
}
