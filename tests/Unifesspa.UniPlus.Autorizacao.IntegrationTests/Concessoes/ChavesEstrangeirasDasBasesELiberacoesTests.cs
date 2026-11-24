namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Concessoes;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// O banco recusa a base de permissão e a liberação cujo pai não existe: a violação de chave estrangeira (SQLSTATE 23503)
/// chega com o nome da restrição que o mapeamento configura, sem corte. Cada gravação roda numa transação desfeita ao fim, e
/// nada fica no banco compartilhado pelas outras suítes.
/// </summary>
[Collection(AutorizacaoEndpointCollection.Name)]
public sealed class ChavesEstrangeirasDasBasesELiberacoesTests : ConcessoesEmVigenciaTestBase
{
    private const string ViolacaoDeChaveEstrangeira = "23503";

    private const string BaseSql =
        """
        INSERT INTO autorizacao.bases_da_permissao_concedida
            (id, perfil_id, concessao_id, codigo, classificacao, de_unidade, origem, referencia_id, ordem_no_historico)
        VALUES ({0}, {1}, {2}, 'configuracao:cursos:manter', 'INTERNO', false, 'MIGRACAO', {3}, nextval('autorizacao.ordem_do_historico_de_acesso'))
        """;

    private const string ReclassificacaoSql =
        """
        INSERT INTO autorizacao.reclassificacoes_da_permissao
            (id, implantacao_id, perfil_id, codigo, classificacao_anterior, classificacao_nova, de_unidade_anterior, de_unidade_novo, instante, ordem_no_historico)
        SELECT {0}, i.id, {1}, 'configuracao:cursos:manter', 'INTERNO', 'PESSOAL', false, false, now(), nextval('autorizacao.ordem_do_historico_de_acesso')
        FROM autorizacao.implantacoes_das_permissoes i ORDER BY i.sequencia DESC LIMIT 1
        """;

    private const string LiberacaoSql =
        """
        INSERT INTO autorizacao.liberacoes_da_reclassificacao
            (id, reclassificacao_id, perfil_id, concessao_id, etapa, resultado, motivo, decisor_emissor, decisor_conta_id, decisor_nome,
             decisor_tem_concessao, lote_id, instante, ordem_no_historico)
        VALUES ({0}, {1}, {2}, {3}, 'GESTAO_DE_ACESSOS', 'LIBERADA', NULL, 'emissor', 'diogo.souza', NULL, true, {4}, now(),
                nextval('autorizacao.ordem_do_historico_de_acesso'))
        """;

    public ChavesEstrangeirasDasBasesELiberacoesTests(AutorizacaoEndpointFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "A base da permissão numa concessão que não existe é recusada pela chave estrangeira da concessão")]
    public async Task BaseSemConcessao_ViolaAChaveEstrangeiraDaConcessao()
    {
        (Guid perfil, _) = await MontarAsync();

        PostgresException erro = await GravarRecusadoAsync(
            static (_, _) => Task.CompletedTask,
            (db, _) => db.Database.ExecuteSqlRawAsync(BaseSql, Guid.CreateVersion7(), perfil, Guid.CreateVersion7(), Guid.CreateVersion7()));

        ConferirViolacao(erro, "fk_bases_da_permissao_concedida_concessao");
    }

    [Fact(DisplayName = "A liberação numa concessão que não existe é recusada pela chave estrangeira da concessão")]
    public async Task LiberacaoSemConcessao_ViolaAChaveEstrangeiraDaConcessao()
    {
        (Guid perfil, _) = await MontarAsync();
        Guid reclassificacao = Guid.CreateVersion7();

        PostgresException erro = await GravarRecusadoAsync(
            (db, _) => db.Database.ExecuteSqlRawAsync(ReclassificacaoSql, reclassificacao, perfil),
            (db, _) => db.Database.ExecuteSqlRawAsync(LiberacaoSql, Guid.CreateVersion7(), reclassificacao, perfil, Guid.CreateVersion7(), Guid.CreateVersion7()));

        ConferirViolacao(erro, "fk_liberacoes_da_reclassificacao_concessao");
    }

    [Fact(DisplayName = "A liberação de uma reclassificação que não existe é recusada pela chave estrangeira da reclassificação")]
    public async Task LiberacaoSemReclassificacao_ViolaAChaveEstrangeiraDaReclassificacao()
    {
        (Guid perfil, Dictionary<string, Guid> concessoes) = await MontarAsync();

        PostgresException erro = await GravarRecusadoAsync(
            static (_, _) => Task.CompletedTask,
            (db, _) => db.Database.ExecuteSqlRawAsync(LiberacaoSql, Guid.CreateVersion7(), Guid.CreateVersion7(), perfil, concessoes[SemPrazo], Guid.CreateVersion7()));

        ConferirViolacao(erro, "fk_liberacoes_da_reclassificacao_reclassificacao_da_permissao");
    }

    private static void ConferirViolacao(PostgresException erro, string restricao)
    {
        erro.SqlState.Should().Be(ViolacaoDeChaveEstrangeira);
        erro.ConstraintName.Should().Be(restricao);
    }

    // O preparo e a gravação recusada dividem a transação, que nunca é confirmada.
    private async Task<PostgresException> GravarRecusadoAsync(
        Func<UniPlusDbContext, IDbContextTransaction, Task> preparo, Func<UniPlusDbContext, IDbContextTransaction, Task> gravacao)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        await using IDbContextTransaction transacao = await db.Database.BeginTransactionAsync();
        await preparo(db, transacao);

        Func<Task> gravar = () => gravacao(db, transacao);

        return (await gravar.Should().ThrowAsync<PostgresException>()).Which;
    }
}
