namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Net;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;

/// <summary>
/// O que o banco barra por conta própria na trilha da janela de migração, nos lotes, nas linhas e na revogação pela
/// janela, com gravação direta que contorna o agregado: cada ramo inválido recusado, as gravações válidas aceitas, os
/// índices únicos parciais e as chaves estrangeiras que restringem a exclusão.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class RestricoesDaJanelaNoBancoTests : JanelaDeMigracaoNoHostTestBase
{
    private const string Decisoes = "decisoes_da_migracao";
    private const string Violacao = "23514";
    private const string ChaveEstrangeira = "23503";
    private const string Restricao = "23001";
    private const string Duplicidade = "23505";

    public RestricoesDaJanelaNoBancoTests(MonolitoDaInstalacaoFixture fixture, HostDaJanelaDeMigracao hostDaClasse)
        : base(fixture, hostDaClasse)
    {
    }

    public static TheoryData<string, string, string, string> DecisoesInvalidas => new()
    {
        { "envio sem etapa do envio", "protecao", "envio_etapa = NULL", "ck_decisoes_da_migracao_envio" },
        { "envio com etapa fora do catálogo", "protecao", "envio_etapa = 'outra'", "ck_decisoes_da_migracao_envio" },
        { "lote de origem sem etapa", "humana", "lote_de_origem_etapa = NULL", "ck_decisoes_da_migracao_lote_de_origem" },
        { "lote de origem da proteção de dados", "humana", "lote_de_origem_etapa = 'protecao_de_dados'", "ck_decisoes_da_migracao_lote_de_origem" },
        { "humana com resultado do sistema", "humana", "decisao = 'reter'", "ck_decisoes_da_migracao_ramo" },
        { "humana sem decisor", "humana", "decisor_sub = NULL", "ck_decisoes_da_migracao_ramo" },
        { "humana sem lote de origem", "humana", "lote_de_origem_id = NULL, lote_de_origem_etapa = NULL", "ck_decisoes_da_migracao_ramo" },
        { "humana da proteção sem envio", "protecao", "envio_id = NULL, envio_etapa = NULL", "ck_decisoes_da_migracao_ramo" },
        { "humana da gestão com envio de outro lote", "humana", "envio_id = gen_random_uuid(), envio_etapa = 'gestao_de_acessos'", "ck_decisoes_da_migracao_ramo" },
        { "sistema com decisor", "sistema", "decisor_sub = 'alguem'", "ck_decisoes_da_migracao_ramo" },
        { "sistema com resultado humano", "sistema", "decisao = 'migrar'", "ck_decisoes_da_migracao_ramo" },
        { "sistema retendo sem lote de origem", "sistema", "lote_de_origem_id = NULL, lote_de_origem_etapa = NULL", "ck_decisoes_da_migracao_ramo" },
        { "sistema com etapa do sistema e resultado de retenção", "sistema", "etapa = 'sistema'", "ck_decisoes_da_migracao_ramo" },
    };

    [Theory(DisplayName = "A trilha da migração recusa a gravação direta de cada ramo inválido")]
    [MemberData(nameof(DecisoesInvalidas))]
    public async Task Trilha_RecusaRamoInvalido(string caso, string modelo, string alteracoes, string restricao)
    {
        ArgumentNullException.ThrowIfNull(alteracoes);
        Cenario cenario = await MontarAsync();

        PostgresException erro = await FalharAsync(await CopiarLinhaAsync(Decisoes, cenario.Decisao(modelo), alteracoes));

        erro.SqlState.Should().Be(Violacao, caso);
        erro.ConstraintName.Should().Be(restricao, caso);
    }

    [Theory(DisplayName = "A trilha da migração aceita a retenção, a decisão da retida e a decisão do sistema com e sem lote")]
    [InlineData("humana", "id = gen_random_uuid()")]
    [InlineData("protecao", "id = gen_random_uuid()")]
    [InlineData("sistema", "id = gen_random_uuid()")]
    [InlineData("sistema", "etapa = 'sistema', decisao = 'nao_migrada'")]
    [InlineData("sistema", "etapa = 'sistema', decisao = 'fora_da_lista', lote_de_origem_id = NULL, lote_de_origem_etapa = NULL")]
    public async Task Trilha_AceitaGravacaoValida(string modelo, string alteracoes)
    {
        ArgumentNullException.ThrowIfNull(alteracoes);
        Cenario cenario = await MontarAsync();

        int gravadas = await ExecutarAsync(await CopiarLinhaAsync(Decisoes, cenario.Decisao(modelo), alteracoes));

        gravadas.Should().Be(1);
    }

    [Fact(DisplayName = "A decisão da proteção de dados com envio inexistente é barrada pela chave composta")]
    public async Task Trilha_EnvioInexistente_BarradoPelaChaveComposta()
    {
        Cenario cenario = await MontarAsync();

        PostgresException erro = await FalharAsync(await CopiarLinhaAsync(Decisoes, cenario.Decisao("protecao"), "envio_id = gen_random_uuid()"));

        erro.SqlState.Should().Be(ChaveEstrangeira);
    }

    [Fact(DisplayName = "O lote de origem de outra janela é barrado pela chave composta")]
    public async Task Trilha_LoteDeOutraJanela_BarradoPelaChaveComposta()
    {
        Cenario cenario = await MontarAsync();

        PostgresException erro = await FalharAsync(await CopiarLinhaAsync(Decisoes, cenario.Decisao("humana"), "janela_id = gen_random_uuid()"));

        erro.SqlState.Should().Be(ChaveEstrangeira);
    }

    [Fact(DisplayName = "O lote da gestão é um só por janela, e a linha da concessão é uma só por janela")]
    public async Task IndicesUnicosParciais_BarramDuplicidade()
    {
        Cenario cenario = await MontarAsync();

        PostgresException segundoLote = await FalharAsync(await CopiarLinhaAsync("lotes_da_migracao", cenario.LoteDaGestao, "id = gen_random_uuid()"));
        PostgresException segundaLinha = await FalharAsync(await CopiarLinhaAsync("migracoes_da_concessao", cenario.Linha, "id = gen_random_uuid()"));
        PostgresException segundaJanela = await FalharAsync(await CopiarLinhaAsync("janelas_de_migracao", cenario.Janela, "id = gen_random_uuid()"));

        segundoLote.SqlState.Should().Be(Duplicidade);
        segundoLote.ConstraintName.Should().Be("ux_lotes_da_migracao_gestao");
        segundaLinha.SqlState.Should().Be(Duplicidade);
        segundaLinha.ConstraintName.Should().Be("ux_migracoes_da_concessao_janela_concessao");
        segundaJanela.SqlState.Should().Be(Duplicidade);
    }

    [Fact(DisplayName = "As chaves estrangeiras da trilha restringem a exclusão da concessão, do lote e da janela")]
    public async Task ChavesEstrangeiras_RestringemAExclusao()
    {
        Cenario cenario = await MontarAsync();

        PostgresException concessao = await FalharAsync($"DELETE FROM autorizacao.concessoes WHERE id = '{cenario.ConcessaoDeLeandro}'");
        PostgresException lote = await FalharAsync($"DELETE FROM autorizacao.lotes_da_migracao WHERE id = '{cenario.LoteDaGestao}'");
        PostgresException janela = await FalharAsync($"DELETE FROM autorizacao.janelas_de_migracao WHERE id = '{cenario.Janela}'");

        concessao.SqlState.Should().Be(Restricao);
        lote.SqlState.Should().Be(Restricao);
        janela.SqlState.Should().Be(Restricao);
    }

    [Fact(DisplayName = "A revogação pela janela é recusada em cada ramo incompleto e aceita quando completa e da janela do perfil")]
    public async Task Revogacao_RecusaRamoIncompletoEAceitaCompleta()
    {
        Cenario cenario = await MontarAsync();
        string janela = cenario.Janela.ToString();

        PostgresException semMotivo = await FalharAsync(Revogar(cenario, $"revogada_em = now(), revogada_pela_janela_id = '{janela}'"));
        PostgresException comPessoa = await FalharAsync(Revogar(
            cenario, $"revogada_em = now(), motivo_revogacao = 'x', revogada_por_emissor = 'e', revogada_por_sub = 's', revogada_pela_janela_id = '{janela}'"));
        PostgresException soJanela = await FalharAsync(Revogar(cenario, $"revogada_pela_janela_id = '{janela}'"));
        PostgresException outraJanela = await FalharAsync(Revogar(
            cenario, "revogada_em = now(), motivo_revogacao = 'x', revogada_pela_janela_id = gen_random_uuid()"));

        new[] { semMotivo, comPessoa, soJanela }.Should().OnlyContain(static e => e.ConstraintName == "ck_concessoes_revogacao_completa");
        outraJanela.SqlState.Should().Be(ChaveEstrangeira);
        (await ExecutarAsync(Revogar(cenario, $"revogada_em = now(), motivo_revogacao = 'x', revogada_pela_janela_id = '{janela}'"))).Should().Be(1);
    }

    private static string Revogar(Cenario cenario, string colunas) =>
        $"UPDATE autorizacao.concessoes SET {colunas} WHERE id = '{cenario.ConcessaoDeIsabella}'";

    // Copia a linha modelo com as colunas alteradas ("coluna = expressão, ..."), sempre com identificador novo: a gravação
    // direta parte de uma linha que o agregado gravou e muda só o que o caso quer testar. A coluna de identidade fica de fora: o
    // banco gera o número da cópia.
    private async Task<string> CopiarLinhaAsync(string tabela, Guid modelo, string alteracoes)
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();
        List<string> colunas = await escopo.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().Database
            .SqlQuery<string>($"SELECT column_name AS \"Value\" FROM information_schema.columns WHERE table_schema = 'autorizacao' AND table_name = {tabela} AND is_identity = 'NO' ORDER BY ordinal_position")
            .ToListAsync();
        Dictionary<string, string> trocas = alteracoes.Split(", ").Select(t => t.Split(" = ", 2)).ToDictionary(t => t[0], t => t[1]);
        trocas.TryAdd("id", "gen_random_uuid()");

        return $"INSERT INTO autorizacao.{tabela} ({string.Join(", ", colunas)}) "
            + $"SELECT {string.Join(", ", colunas.Select(c => trocas.GetValueOrDefault(c, c)))} FROM autorizacao.{tabela} WHERE id = '{modelo}'";
    }

    private async Task<Cenario> MontarAsync()
    {
        JanelaAberta janela = await AbrirJanelaAsync(Isabella, Leandro);
        await MigrarNasDuasEtapasAsync(janela.PerfilId, janela.Concessoes[Leandro]);

        return new Cenario(
            await UnicoAsync("SELECT id AS \"Value\" FROM autorizacao.janelas_de_migracao"),
            await UnicoAsync("SELECT id AS \"Value\" FROM autorizacao.lotes_da_migracao WHERE etapa = 'gestao_de_acessos'"),
            await UnicoAsync($"SELECT id AS \"Value\" FROM autorizacao.migracoes_da_concessao WHERE concessao_id = '{janela.Concessoes[Leandro]}'"),
            janela.Concessoes[Leandro],
            janela.Concessoes[Isabella],
            await UnicoAsync($"SELECT id AS \"Value\" FROM autorizacao.decisoes_da_migracao WHERE concessao_id = '{janela.Concessoes[Leandro]}' AND etapa = 'gestao_de_acessos'"),
            await UnicoAsync($"SELECT id AS \"Value\" FROM autorizacao.decisoes_da_migracao WHERE concessao_id = '{janela.Concessoes[Leandro]}' AND etapa = 'protecao_de_dados'"),
            await UnicoAsync($"SELECT id AS \"Value\" FROM autorizacao.decisoes_da_migracao WHERE concessao_id = '{janela.Concessoes[Isabella]}' AND ator = 'sistema'"));
    }

    private async Task<Guid> UnicoAsync(string sql)
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();

        return await escopo.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().Database.SqlQueryRaw<Guid>(sql).SingleAsync();
    }

    private async Task<int> ExecutarAsync(string sql)
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();

        return await escopo.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().Database.ExecuteSqlRawAsync(sql);
    }

    private async Task<PostgresException> FalharAsync(string sql)
    {
        Func<Task> gravacao = () => ExecutarAsync(sql);

        return (await gravacao.Should().ThrowAsync<PostgresException>()).Which;
    }

    private sealed record Cenario(
        Guid Janela, Guid LoteDaGestao, Guid Linha, Guid ConcessaoDeLeandro, Guid ConcessaoDeIsabella,
        Guid DecisaoHumana, Guid DecisaoDaProtecao, Guid DecisaoDoSistema)
    {
        public Guid Decisao(string modelo) => modelo switch
        {
            "humana" => DecisaoHumana,
            "protecao" => DecisaoDaProtecao,
            _ => DecisaoDoSistema,
        };
    }

    private async Task MigrarNasDuasEtapasAsync(Guid perfilId, Guid concessao)
    {
        using HttpClient gestao = await GestaoAsync();
        using HttpResponseMessage lote = await EnviarLoteAsync(gestao, perfilId, [Decisao(concessao, "migrar")]);
        await CorpoAsync(lote, HttpStatusCode.OK);
        using HttpClient protecao = await ProtecaoAsync();
        using HttpResponseMessage etapa = await EnviarAsync(
            protecao, HttpMethod.Post, RotaDaJanela(perfilId, "/protecao-de-dados"), new { decisoes = new[] { Decisao(concessao, "migrar") } });
        await CorpoAsync(etapa, HttpStatusCode.OK);
    }
}
