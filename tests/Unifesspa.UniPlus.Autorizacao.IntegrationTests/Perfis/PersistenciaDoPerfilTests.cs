namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

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
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();

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
                                 'documentos_da_versao', 'leituras_do_documento_da_versao', 'versao_do_perfil_permissoes',
                                 'decisoes_da_versao', 'recalculos_da_aprovacao', 'avisos_emitidos')
            """).SingleAsync();

        tabelas.Should().Be(10);
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
        await LimparPendentesInseridasAsync(id);
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

    [Theory(DisplayName = "O banco barra a versão cuja situação final contradiz os campos do encerramento")]
    [InlineData("situacao = 'cancelada', forma_do_cancelamento = NULL, cancelada_em = NULL")]
    [InlineData("situacao = 'cancelada', forma_do_cancelamento = 'pessoa', cancelada_em = now(), motivo_do_cancelamento = NULL")]
    [InlineData("situacao = 'expirada', expirada_em = NULL")]
    [InlineData("origem = 'remocao', expira_em = NULL, motivo_da_remocao = NULL, versao_do_documento = NULL")]
    [InlineData("origem = 'proposta', expira_em = NULL")]
    public async Task Banco_BarraEncerramentoIncoerente(string alteracao)
    {
        Guid id = await SemearPerfilVigenteAsync(ConsultarCatalogo);
        await InserirVersaoAsync(id, 3, "pendente");
        Guid pendente = await VersaoPendenteAsync(id);

        Func<Task> acao = () => ExecutarAsync($"UPDATE autorizacao.versoes_do_perfil SET {alteracao} WHERE id = {{0}}", pendente);

        await acao.Should().ThrowAsync<Npgsql.PostgresException>();
        await LimparPendentesInseridasAsync(id);
    }

    [Fact(DisplayName = "O banco barra duas decisões da mesma etapa no mesmo ciclo e impede apagar a versão que já foi decidida")]
    public async Task Banco_BarraDecisaoRepetidaEProtegeAVersaoDecidida()
    {
        Guid id = await SemearPerfilVigenteAsync(ConsultarCatalogo);
        await InserirVersaoAsync(id, 3, "pendente");
        Guid pendente = await VersaoPendenteAsync(id);
        await InserirDecisaoAsync(pendente, 1, "gestao_de_acessos");

        Func<Task> repetida = () => InserirDecisaoAsync(pendente, 1, "gestao_de_acessos");
        Func<Task> apagar = () => ExecutarAsync("DELETE FROM autorizacao.versoes_do_perfil WHERE id = {0}", pendente);

        await InserirDecisaoAsync(pendente, 2, "gestao_de_acessos");
        await repetida.Should().ThrowAsync<Npgsql.PostgresException>();
        await apagar.Should().ThrowAsync<Npgsql.PostgresException>();
        await LimparPendentesInseridasAsync(id);
    }

    [Fact(DisplayName = "O banco barra o mesmo aviso gravado duas vezes")]
    public async Task Banco_BarraAvisoDuplicado()
    {
        Guid mensagem = Guid.CreateVersion7();
        await InserirAvisoAsync(mensagem);

        Func<Task> repetido = () => InserirAvisoAsync(mensagem);

        await repetido.Should().ThrowAsync<Npgsql.PostgresException>();
    }

    private async Task<Guid> VersaoPendenteAsync(Guid perfilId)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();

        return await db.Database
            .SqlQuery<Guid>($"SELECT id AS \"Value\" FROM autorizacao.versoes_do_perfil WHERE perfil_id = {perfilId} AND situacao = 'pendente'")
            .SingleAsync();
    }

    private Task InserirDecisaoAsync(Guid versaoId, int ciclo, string etapa) =>
        ExecutarAsync(
            """
            INSERT INTO autorizacao.decisoes_da_versao
                (id, versao_id, ciclo, etapa, resultado, decisor_emissor, decisor_sub, decisor_tem_concessao, acrescimo_avaliado, exigia_protecao_de_dados, instante)
            VALUES ({0}, {1}, {2}, {3}, 'aprovado', 'emissor', 'isabella.figueiredo', false, ARRAY[]::text[], false, now())
            """,
            Guid.CreateVersion7(), versaoId, ciclo, etapa);

    private Task InserirAvisoAsync(Guid mensagemId) =>
        ExecutarAsync(
            "INSERT INTO autorizacao.avisos_emitidos (mensagem_id, tipo, fato_id, emitido_em) VALUES ({0}, 'permissoes_removidas', {1}, now())",
            mensagemId, Guid.CreateVersion7());
}
