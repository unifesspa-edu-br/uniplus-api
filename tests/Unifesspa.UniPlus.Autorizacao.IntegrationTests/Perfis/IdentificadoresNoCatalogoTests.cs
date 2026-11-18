namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Perfis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

[Collection(AutorizacaoEndpointCollection.Name)]
public sealed class IdentificadoresNoCatalogoTests(AutorizacaoEndpointFixture fixture) : PerfilEndpointTestBase(fixture)
{
    [Fact]
    public async Task Migration_IdentificadoresDeAutorizacao_RespeitaLimiteENomesExplicitos()
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        string[] nomes = await NomesDoCatalogoAsync(db);

        nomes.Should().OnlyContain(nome => System.Text.Encoding.UTF8.GetByteCount(nome) <= 63);
        nomes.Should().Contain([
            "fk_bases_da_permissao_concedida_concessao",
            "fk_liberacoes_da_reclassificacao_concessao",
            "fk_liberacoes_da_reclassificacao_reclassificacao_da_permissao",
            "ix_bases_da_permissao_concessao_codigo",
            "ix_bases_da_permissao_concedida_perfil_concessao",
            "ix_liberacoes_da_reclassificacao_concessao",
        ]);
    }

    private static Task<string[]> NomesDoCatalogoAsync(UniPlusDbContext db) =>
        db.Database.SqlQuery<string>($"""
            SELECT c.relname::text AS "Value" FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'autorizacao' AND c.relkind IN ('r', 'p', 'i')
            UNION ALL
            SELECT a.attname::text FROM pg_attribute a
            JOIN pg_class c ON c.oid = a.attrelid
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'autorizacao' AND a.attnum > 0 AND NOT a.attisdropped
            UNION ALL
            SELECT con.conname::text FROM pg_constraint con
            JOIN pg_namespace n ON n.oid = con.connamespace
            WHERE n.nspname = 'autorizacao'
            """).ToArrayAsync();
}
