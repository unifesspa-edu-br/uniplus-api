namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>O que o banco garante sozinho na contenção do perfil: tabelas, unicidade do que está aberto, coerência de cada registro e ordem única da trilha.</summary>
[Collection(AutorizacaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class PersistenciaDaContencaoTests : PerfilEndpointTestBase
{
    private const string Motivo = "Uso indevido apontado pela gestão.";

    public PersistenciaDaContencaoTests(AutorizacaoEndpointFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "A migration cria as quatro tabelas da contenção, a sequência da trilha e nenhuma chave estrangeira para fora do schema")]
    public async Task Migration_CriaTabelasESequencia()
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();

        int tabelas = await db.Database.SqlQuery<int>($"""
            SELECT count(*)::int AS "Value" FROM information_schema.tables
            WHERE table_schema = 'autorizacao'
              AND table_name IN ('suspensoes_do_perfil', 'desativacoes_do_perfil', 'decisoes_da_retomada', 'eventos_da_contencao')
            """).SingleAsync();
        int sequencias = await db.Database.SqlQuery<int>($"""
            SELECT count(*)::int AS "Value" FROM information_schema.sequences
            WHERE sequence_schema = 'autorizacao' AND sequence_name = 'ordem_da_trilha_de_contencao'
            """).SingleAsync();
        int fkParaFora = await db.Database.SqlQuery<int>($"""
            SELECT count(*)::int AS "Value"
            FROM pg_constraint c
            JOIN pg_class origem ON origem.oid = c.conrelid
            JOIN pg_class alvo ON alvo.oid = c.confrelid
            JOIN pg_namespace na ON na.oid = alvo.relnamespace
            WHERE c.contype = 'f' AND origem.relname IN ('suspensoes_do_perfil', 'desativacoes_do_perfil', 'decisoes_da_retomada', 'eventos_da_contencao')
              AND na.nspname <> 'autorizacao'
            """).SingleAsync();

        tabelas.Should().Be(4);
        sequencias.Should().Be(1);
        fkParaFora.Should().Be(0);
    }

    [Fact(DisplayName = "O banco barra duas suspensões abertas e duas desativações abertas do mesmo perfil, mas admite outra depois de fechar")]
    public async Task Banco_BarraDuplicidadeDoQueEstaAberto()
    {
        Guid perfil = await SemearPerfilVigenteAsync(ConsultarCatalogo);
        Guid suspensao = await InserirSuspensaoAsync(perfil);
        Guid desativacao = await InserirDesativacaoAsync(perfil);

        Func<Task> outraSuspensao = () => InserirSuspensaoAsync(perfil);
        Func<Task> outraDesativacao = () => InserirDesativacaoAsync(perfil);

        await outraSuspensao.Should().ThrowAsync<Npgsql.PostgresException>();
        await outraDesativacao.Should().ThrowAsync<Npgsql.PostgresException>();
        await ExecutarAsync("UPDATE autorizacao.suspensoes_do_perfil SET retomada_em = now() WHERE id = {0}", suspensao);
        await ExecutarAsync(
            "UPDATE autorizacao.desativacoes_do_perfil SET reativada_em = now(), reativada_por_emissor = 'e', reativada_por_sub = 's', motivo_da_reativacao = 'm' WHERE id = {0}",
            desativacao);
        await InserirSuspensaoAsync(perfil);
        await InserirDesativacaoAsync(perfil);
    }

    [Fact(DisplayName = "O banco barra o papel inválido, a etapa repetida na suspensão e a etapa inválida")]
    public async Task Banco_BarraPapelEEtapaInvalidos()
    {
        Guid perfil = await SemearPerfilVigenteAsync(ConsultarCatalogo);
        Func<Task> papelInvalido = () => InserirSuspensaoAsync(perfil, papel: "outro");
        Guid suspensao = await InserirSuspensaoAsync(perfil);
        await InserirDecisaoAsync(suspensao, "gestao_de_acessos");

        Func<Task> repetida = () => InserirDecisaoAsync(suspensao, "gestao_de_acessos");
        Func<Task> etapaInvalida = () => InserirDecisaoAsync(suspensao, "outra");

        await papelInvalido.Should().ThrowAsync<Npgsql.PostgresException>();
        await repetida.Should().ThrowAsync<Npgsql.PostgresException>();
        await etapaInvalida.Should().ThrowAsync<Npgsql.PostgresException>();
        await InserirDecisaoAsync(suspensao, "protecao_de_dados");
    }

    [Theory(DisplayName = "O banco barra a reativação incoerente: campos de quem reativou sem data, ou data sem eles")]
    [InlineData("reativada_em = now()")]
    [InlineData("reativada_por_sub = 's', motivo_da_reativacao = 'm'")]
    [InlineData("reativada_em = now(), reativada_por_emissor = 'e', reativada_por_sub = 's'")]
    public async Task Banco_BarraReativacaoIncoerente(string alteracao)
    {
        Guid perfil = await SemearPerfilVigenteAsync(ConsultarCatalogo);
        Guid desativacao = await InserirDesativacaoAsync(perfil);

        Func<Task> acao = () => ExecutarAsync($"UPDATE autorizacao.desativacoes_do_perfil SET {alteracao} WHERE id = {{0}}", desativacao);

        await acao.Should().ThrowAsync<Npgsql.PostgresException>();
    }

    [Theory(DisplayName = "O banco barra o evento cujos campos contradizem o tipo")]
    [InlineData("retomada", "humano", null, "decisao_da_etapa")]
    [InlineData("retomada", "sistema", null, null)]
    [InlineData("suspensao", "sistema", null, null)]
    [InlineData("suspensao", "humano", "decisao_da_etapa", null)]
    [InlineData("outro", "humano", null, null)]
    public async Task Banco_BarraEventoIncoerente(string tipo, string ator, string? origem, string? permissao)
    {
        Guid perfil = await SemearPerfilVigenteAsync(ConsultarCatalogo);
        Guid suspensao = await InserirSuspensaoAsync(perfil);

        Func<Task> acao = () => ExecutarAsync(
            $$"""
            INSERT INTO autorizacao.eventos_da_contencao
                (id, perfil_id, instante, tipo, suspensao_id, ator, origem, permissao_codigo, ator_emissor, ator_sub, papel, motivo, perfil_protegido, ordem_na_trilha)
            VALUES ({0}, {1}, now(), {2}, {3}, {4}, {{Literal(origem)}}, {{Literal(permissao)}}, 'e', 's', 'gestao_de_acessos', 'm', false, nextval('autorizacao.ordem_da_trilha_de_contencao'))
            """,
            Guid.CreateVersion7(), perfil, tipo, suspensao, ator);

        await acao.Should().ThrowAsync<Npgsql.PostgresException>();
    }

    [Fact(DisplayName = "O banco barra a retomada sem origem, mesmo com a referência da decisão preenchida")]
    public async Task Banco_BarraRetomadaSemOrigem()
    {
        Guid perfil = await SemearPerfilVigenteAsync(ConsultarCatalogo);
        Guid suspensao = await InserirSuspensaoAsync(perfil);
        await InserirDecisaoAsync(suspensao, "gestao_de_acessos");

        Func<Task> acao = () => ExecutarAsync(
            """
            INSERT INTO autorizacao.eventos_da_contencao
                (id, perfil_id, instante, tipo, suspensao_id, ator, decisao_id, perfil_protegido, ordem_na_trilha)
            SELECT {0}, {1}, now(), 'retomada', {2}, 'sistema', d.id, false, nextval('autorizacao.ordem_da_trilha_de_contencao')
            FROM autorizacao.decisoes_da_retomada d WHERE d.suspensao_id = {2}
            """,
            Guid.CreateVersion7(), perfil, suspensao);

        await acao.Should().ThrowAsync<Npgsql.PostgresException>();
    }

    [Fact(DisplayName = "A gravação síncrona numera o registro novo da trilha pela sequence, como a assíncrona")]
    public async Task Trilha_GravacaoSincrona_NumeraORegistro()
    {
        Guid perfilId = await SemearPerfilVigenteAsync(ConsultarCatalogo);
        await using (AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope())
        {
            IServiceProvider servicos = scope.ServiceProvider;
            Perfil perfil = (await servicos.GetRequiredService<IPerfilRepository>().ObterParaEditarAsync(perfilId))!;
            perfil.Suspender(
                new Identidade("https://idp.test/realms/unifesspa", "diogo.souza", "Diogo Souza"),
                EtapaDaVersao.GestaoDeAcessos,
                Motivo,
                DateTimeOffset.UtcNow,
                servicos.GetRequiredService<ICatalogoDePermissoesConsultavel>());

            servicos.GetRequiredService<UniPlusDbContext>().SaveChanges();
        }

        (string Tipo, long Ordem)[] registros = await OrdemDosRegistrosAsync(perfilId);

        registros.Should().ContainSingle().Which.Ordem.Should().BePositive();
    }

    [Fact(DisplayName = "A ordem da trilha é única e crescente entre as quatro tabelas, na ordem em que as operações aconteceram")]
    public async Task Trilha_OrdemUnicaEntreAsTabelas()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();
        using HttpClient isabella = await ClienteDeOutroGestorAsync();
        Guid perfil = await SemearPerfilVigenteAsync(ConsultarCatalogo);

        await ConferirAsync(await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{perfil}/suspensao", new { motivo = Motivo }), HttpStatusCode.NoContent);
        await ConferirAsync(
            await EnviarAsync(isabella, HttpMethod.Post, $"{Rota}/{perfil}/suspensao/retomada", new { etapa = "gestao_de_acessos", motivo = Motivo }),
            HttpStatusCode.OK);
        await ConferirAsync(await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{perfil}/desativacao", new { motivo = Motivo }), HttpStatusCode.NoContent);
        await ConferirAsync(await EnviarAsync(isabella, HttpMethod.Post, $"{Rota}/{perfil}/reativacao", new { motivo = Motivo }), HttpStatusCode.NoContent);

        (string Tipo, long Ordem)[] registros = await OrdemDosRegistrosAsync(perfil);

        registros.Select(r => r.Ordem).Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
        registros.Select(r => r.Tipo).Should().Equal(
            "suspensao", "decisao_da_retomada", "retomada", "desativacao", "reativacao");
        using JsonDocument trilha = JsonDocument.Parse(await (await diogo.GetAsync(new Uri($"{Rota}/{perfil}/contencao", UriKind.Relative))).Content.ReadAsStringAsync());
        trilha.RootElement.EnumerateArray().Select(r => r.GetProperty("tipo").GetString()).Should().Equal(registros.Select(r => r.Tipo));
    }

    private static string Literal(string? valor) => valor is null ? "NULL" : $"'{valor}'";

    private static async Task ConferirAsync(HttpResponseMessage resposta, HttpStatusCode esperado)
    {
        using HttpResponseMessage descartavel = resposta;
        descartavel.StatusCode.Should().Be(esperado, await descartavel.Content.ReadAsStringAsync());
    }

    private async Task<(string Tipo, long Ordem)[]> OrdemDosRegistrosAsync(Guid perfilId)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        List<RegistroDaOrdem> linhas = await db.Database.SqlQuery<RegistroDaOrdem>($"""
            SELECT tipo AS "Tipo", ordem_na_trilha AS "Ordem" FROM autorizacao.eventos_da_contencao WHERE perfil_id = {perfilId}
            UNION ALL
            SELECT 'decisao_da_retomada', d.ordem_na_trilha FROM autorizacao.decisoes_da_retomada d
              JOIN autorizacao.suspensoes_do_perfil s ON s.id = d.suspensao_id WHERE s.perfil_id = {perfilId}
            ORDER BY 2
            """).ToListAsync();

        return [.. linhas.Select(static l => (l.Tipo, l.Ordem))];
    }

    private async Task<Guid> InserirSuspensaoAsync(Guid perfilId, string papel = "gestao_de_acessos")
    {
        Guid id = Guid.CreateVersion7();
        await ExecutarAsync(
            """
            INSERT INTO autorizacao.suspensoes_do_perfil
                (id, perfil_id, suspensa_em, suspensa_por_emissor, suspensa_por_sub, papel_de_quem_suspendeu, motivo, ordem_na_trilha, created_at)
            VALUES ({0}, {1}, now(), 'emissor', 'diogo.souza', {2}, 'm', nextval('autorizacao.ordem_da_trilha_de_contencao'), now())
            """,
            id, perfilId, papel);

        return id;
    }

    private async Task<Guid> InserirDesativacaoAsync(Guid perfilId)
    {
        Guid id = Guid.CreateVersion7();
        await ExecutarAsync(
            """
            INSERT INTO autorizacao.desativacoes_do_perfil
                (id, perfil_id, desativada_em, desativada_por_emissor, desativada_por_sub, motivo, ordem_na_trilha, created_at)
            VALUES ({0}, {1}, now(), 'emissor', 'diogo.souza', 'm', nextval('autorizacao.ordem_da_trilha_de_contencao'), now())
            """,
            id, perfilId);

        return id;
    }

    private Task InserirDecisaoAsync(Guid suspensaoId, string etapa) =>
        ExecutarAsync(
            """
            INSERT INTO autorizacao.decisoes_da_retomada
                (id, suspensao_id, etapa, decisor_emissor, decisor_sub, decisor_tem_concessao, motivo, perfil_protegido_na_decisao, instante, ordem_na_trilha)
            VALUES ({0}, {1}, {2}, 'emissor', 'isabella.figueiredo', false, 'm', false, now(), nextval('autorizacao.ordem_da_trilha_de_contencao'))
            """,
            Guid.CreateVersion7(), suspensaoId, etapa);

    private sealed record RegistroDaOrdem(string Tipo, long Ordem);
}
