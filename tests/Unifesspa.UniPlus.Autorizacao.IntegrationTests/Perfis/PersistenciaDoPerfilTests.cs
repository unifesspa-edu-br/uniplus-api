namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>O que o banco garante sozinho: schema sem chave estrangeira para fora, unicidade de vigente, pendente, vigência aberta e documento vivo.</summary>
[Collection(AutorizacaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class PersistenciaDoPerfilTests : PerfilEndpointTestBase
{
    public PersistenciaDoPerfilTests(AutorizacaoEndpointFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "A migration cria o schema autorizacao sem chave estrangeira para outro schema")]
    public async Task Migration_NaoCriaFkEntreSchemas()
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        AutorizacaoDbContext db = scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>();

        int fkParaFora = await db.Database.SqlQuery<int>($"""
            SELECT count(*)::int AS "Value"
            FROM pg_constraint c
            JOIN pg_class origem ON origem.oid = c.conrelid
            JOIN pg_namespace no ON no.oid = origem.relnamespace
            JOIN pg_class alvo ON alvo.oid = c.confrelid
            JOIN pg_namespace na ON na.oid = alvo.relnamespace
            WHERE c.contype = 'f' AND no.nspname = 'autorizacao' AND na.nspname <> 'autorizacao'
            """).SingleAsync();
        int tabelas = await db.Database.SqlQuery<int>($"""
            SELECT count(*)::int AS "Value" FROM information_schema.tables
            WHERE table_schema = 'autorizacao'
              AND table_name IN ('perfis', 'perfil_permissoes', 'versoes_do_perfil', 'vigencias_do_perfil',
                                 'documentos_da_versao', 'leituras_do_documento_da_versao', 'versao_do_perfil_permissoes')
            """).SingleAsync();

        tabelas.Should().Be(7);
        fkParaFora.Should().Be(0);
    }

    [Fact(DisplayName = "O banco barra duas versões vigentes, duas pendentes e duas vigências abertas do mesmo perfil")]
    public async Task Banco_BarraDuplicidadeDeVigenciaEPendencia()
    {
        Guid id = await SemearPerfilVigenteAsync(ConsultarCatalogo);
        Guid vigente = await VersaoVigenteAsync(id);

        Func<Task> outraVigente = () => InserirVersaoAsync(id, 2, "vigente");
        Func<Task> vigenciaAberta = () => InserirVigenciaAsync(id, vigente);
        await InserirVersaoAsync(id, 3, "pendente");
        Func<Task> outraPendente = () => InserirVersaoAsync(id, 4, "pendente");

        await outraVigente.Should().ThrowAsync<Npgsql.PostgresException>();
        await vigenciaAberta.Should().ThrowAsync<Npgsql.PostgresException>();
        await outraPendente.Should().ThrowAsync<Npgsql.PostgresException>();
    }

    [Fact(DisplayName = "O banco barra vigência que aponta para versão de outro perfil")]
    public async Task Banco_BarraVigenciaDeVersaoDeOutroPerfil()
    {
        Guid um = await SemearPerfilVigenteAsync(ConsultarCatalogo);
        Guid outro = await SemearPerfilVigenteAsync(ConsultarCatalogo);
        Guid versaoDoOutro = await VersaoVigenteAsync(outro);
        await EncerrarVigenciasAsync(um);

        Func<Task> acao = () => InserirVigenciaAsync(um, versaoDoOutro);

        await acao.Should().ThrowAsync<Npgsql.PostgresException>();
    }

    [Fact(DisplayName = "O banco barra dois documentos vivos na mesma versão e admite outro depois da recusa")]
    public async Task Banco_BarraDoisDocumentosVivos()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();
        using HttpResponseMessage criada = await EnviarAsync(diogo, HttpMethod.Post, Rota, Corpo(Nome(), [ConsultarCatalogo]));
        Envio envio = LerEnvio(await criada.Content.ReadAsStringAsync());

        Func<Task> segundoVivo = () => InserirDocumentoAsync(envio, "aguardando_envio");
        await ExecutarAsync("UPDATE autorizacao.documentos_da_versao SET situacao = 'recusado' WHERE id = {0}", envio.DocumentoId);

        await InserirDocumentoAsync(envio, "aguardando_envio");
        await segundoVivo.Should().ThrowAsync<Npgsql.PostgresException>();
    }
}
