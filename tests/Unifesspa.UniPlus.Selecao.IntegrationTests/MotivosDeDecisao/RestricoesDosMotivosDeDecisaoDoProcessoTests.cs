namespace Unifesspa.UniPlus.Selecao.IntegrationTests.MotivosDeDecisao;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Outbox.Cascading;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// O que o banco garante sobre a escolha dos motivos nos lugares, contra Postgres via Testcontainers: o motivo vigente
/// vale uma vez por lugar, o lugar é exatamente a fase ou a etapa, e a vigência no edital é coerente.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class RestricoesDosMotivosDeDecisaoDoProcessoTests : IAsyncLifetime
{
    private const string IndiceDaEscolhaVigente = "ux_motivos_de_decisao_do_processo_vigente";

    private readonly CascadingFixture _fixture;
    private readonly Guid _processoId = Guid.CreateVersion7();
    private readonly Guid _motivoId = Guid.CreateVersion7();
    private readonly Guid _fase = Guid.CreateVersion7();
    private readonly Guid _etapa = Guid.CreateVersion7();

    public RestricoesDosMotivosDeDecisaoDoProcessoTests(CascadingFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        _fixture = fixture;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        await db.Database.ExecuteSqlAsync($"DELETE FROM selecao.motivos_de_decisao_do_processo WHERE processo_seletivo_id = {_processoId}");
    }

    [Fact(DisplayName = "O mesmo motivo vigente no mesmo lugar, fase ou etapa, é recusado pelo índice único, que trata o lugar ausente como igual")]
    public async Task MotivoVigenteRepetidoNoLugar_ViolaOIndice()
    {
        await InserirAsync(_fase, etapa: null);
        await InserirAsync(etapa: _etapa, fase: null);

        PostgresException naFase = await ViolacaoAoInserirAsync(_fase, etapa: null);
        PostgresException naEtapa = await ViolacaoAoInserirAsync(fase: null, _etapa);

        naFase.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
        naFase.ConstraintName.Should().Be(IndiceDaEscolhaVigente);
        naEtapa.ConstraintName.Should().Be(IndiceDaEscolhaVigente);
    }

    [Fact(DisplayName = "O mesmo motivo em outro lugar, ou no mesmo lugar depois que a escolha anterior saiu, é aceito")]
    public async Task MotivoEmOutroLugarOuAposASaida_Aceita()
    {
        await InserirAsync(_fase, etapa: null);
        await InserirAsync(fase: null, _etapa);
        await InserirAsync(Guid.CreateVersion7(), etapa: null);

        await ExecutarAsync(db => db.Database.ExecuteSqlAsync(
            $"UPDATE selecao.motivos_de_decisao_do_processo SET versao_do_edital_de_saida = 2 WHERE processo_seletivo_id = {_processoId} AND tipo_fase_origem_id = {_fase}"));
        await InserirAsync(_fase, etapa: null, entrada: 2);

        (await ContarAsync()).Should().Be(4);
    }

    [Fact(DisplayName = "A escolha sem lugar, ou com fase e etapa juntas, viola a restrição do lugar")]
    public async Task LugarAusenteOuDuplo_ViolaARestricao()
    {
        PostgresException semLugar = await ViolacaoAoInserirAsync(fase: null, etapa: null);
        PostgresException comOsDois = await ViolacaoAoInserirAsync(_fase, _etapa);

        semLugar.ConstraintName.Should().Be("ck_motivos_de_decisao_do_processo_lugar");
        comOsDois.ConstraintName.Should().Be("ck_motivos_de_decisao_do_processo_lugar");
    }

    [Theory(DisplayName = "A entrada abaixo de 1, e a saída que não vem depois da entrada, violam a restrição da vigência")]
    [InlineData(0, null)]
    [InlineData(2, 2)]
    [InlineData(3, 2)]
    public async Task VigenciaIncoerente_ViolaARestricao(int entrada, int? saida)
    {
        PostgresException violacao = await ViolacaoAoInserirAsync(_fase, etapa: null, entrada, saida);

        violacao.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
        violacao.ConstraintName.Should().Be("ck_motivos_de_decisao_do_processo_vigencia");
    }

    private async Task<PostgresException> ViolacaoAoInserirAsync(Guid? fase, Guid? etapa, int entrada = 1, int? saida = null)
    {
        Func<Task> inserindo = () => InserirAsync(fase, etapa, entrada, saida);

        return (await inserindo.Should().ThrowAsync<PostgresException>()).Which;
    }

    private Task InserirAsync(Guid? fase, Guid? etapa, int entrada = 1, int? saida = null) =>
        ExecutarAsync(db => db.Database.ExecuteSqlAsync($"""
            INSERT INTO selecao.motivos_de_decisao_do_processo
                (id, processo_seletivo_id, motivo_de_decisao_origem_id, tipo, codigo, descricao, sentido,
                 tipo_fase_origem_id, etapa_processo_id, versao_do_edital_de_entrada, versao_do_edital_de_saida, created_at)
            VALUES
                ({Guid.CreateVersion7()}, {_processoId}, {_motivoId}, 'CLASSIFICACAO', 'TEST_RMP_NOTA', 'Nota abaixo do mínimo', 'DESFAVORAVEL',
                 {fase}, {etapa}, {entrada}, {saida}, now())
            """));

    private async Task ExecutarAsync(Func<UniPlusDbContext, Task> comando)
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        await comando(escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>());
    }

    private async Task<int> ContarAsync()
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();

        return await db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM selecao.motivos_de_decisao_do_processo WHERE processo_seletivo_id = {_processoId}").SingleAsync();
    }
}
