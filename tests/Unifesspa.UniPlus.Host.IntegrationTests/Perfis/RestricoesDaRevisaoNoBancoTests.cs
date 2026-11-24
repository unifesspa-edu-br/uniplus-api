namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Net;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// O que o banco barra por conta própria na trilha da revisão das concessões, nos envios, nas linhas e na revogação pela
/// revisão, com gravação direta que contorna o agregado: cada ramo inválido recusado, as gravações válidas aceitas, os
/// índices únicos parciais e as chaves estrangeiras que restringem a exclusão.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class RestricoesDaRevisaoNoBancoTests : RevisaoDasConcessoesNoHostTestBase
{
    private const string Decisoes = "decisoes_da_migracao";
    private const string Violacao = "23514";
    private const string ChaveEstrangeira = "23503";
    private const string Restricao = "23001";
    private const string Duplicidade = "23505";

    public RestricoesDaRevisaoNoBancoTests(MonolitoDaInstalacaoFixture fixture, HostDaRevisaoDasConcessoes hostDaClasse)
        : base(fixture, hostDaClasse)
    {
    }

    public static TheoryData<string, string, string, string> DecisoesInvalidas => new()
    {
        { "envio sem etapa do envio", "protecao", "envio_etapa = NULL", "ck_decisoes_da_migracao_envio" },
        { "envio com etapa fora do catálogo", "protecao", "envio_etapa = 'outra'", "ck_decisoes_da_migracao_envio" },
        { "envio de origem sem etapa", "humana", "envio_de_origem_etapa = NULL", "ck_decisoes_da_migracao_envio_de_origem" },
        { "envio de origem da proteção de dados", "humana", "envio_de_origem_etapa = 'PROTECAO_DE_DADOS'", "ck_decisoes_da_migracao_envio_de_origem" },
        { "humana com resultado do sistema", "humana", "decisao = 'RETER'", "ck_decisoes_da_migracao_ramo" },
        { "humana sem decisor", "humana", "decisor_conta_id = NULL", "ck_decisoes_da_migracao_ramo" },
        { "humana sem envio de origem", "humana", "envio_de_origem_id = NULL, envio_de_origem_etapa = NULL", "ck_decisoes_da_migracao_ramo" },
        { "humana da proteção sem envio", "protecao", "envio_id = NULL, envio_etapa = NULL", "ck_decisoes_da_migracao_ramo" },
        { "humana da gestão com envio diferente do original", "humana", "envio_id = gen_random_uuid(), envio_etapa = 'GESTAO_DE_ACESSOS'", "ck_decisoes_da_migracao_ramo" },
        { "sistema com decisor", "SISTEMA", "decisor_conta_id = 'alguem'", "ck_decisoes_da_migracao_ramo" },
        { "sistema com resultado humano", "SISTEMA", "decisao = 'MIGRAR'", "ck_decisoes_da_migracao_ramo" },
        { "sistema retendo sem envio de origem", "SISTEMA", "envio_de_origem_id = NULL, envio_de_origem_etapa = NULL", "ck_decisoes_da_migracao_ramo" },
        { "sistema com etapa do sistema e resultado de retenção", "SISTEMA", "etapa = 'SISTEMA'", "ck_decisoes_da_migracao_ramo" },
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

    [Theory(DisplayName = "A trilha da migração aceita a retenção, a decisão da retida e a decisão do sistema com e sem envio")]
    [InlineData("humana", "id = gen_random_uuid()")]
    [InlineData("protecao", "id = gen_random_uuid()")]
    [InlineData("SISTEMA", "id = gen_random_uuid()")]
    [InlineData("SISTEMA", "etapa = 'SISTEMA', decisao = 'NAO_MIGRADA'")]
    [InlineData("SISTEMA", "etapa = 'SISTEMA', decisao = 'FORA_DA_LISTA', envio_de_origem_id = NULL, envio_de_origem_etapa = NULL")]
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

    [Fact(DisplayName = "O envio de origem de outra revisão é barrado pela chave composta")]
    public async Task Trilha_EnvioDeOutraRevisao_BarradoPelaChaveComposta()
    {
        Cenario cenario = await MontarAsync();

        PostgresException erro = await FalharAsync(await CopiarLinhaAsync(Decisoes, cenario.Decisao("humana"), "revisao_id = gen_random_uuid()"));

        erro.SqlState.Should().Be(ChaveEstrangeira);
    }

    [Fact(DisplayName = "O envio da gestão é um só por revisão, e a linha da concessão é uma só por revisão")]
    public async Task IndicesUnicosParciais_BarramDuplicidade()
    {
        Cenario cenario = await MontarAsync();

        PostgresException segundoEnvio = await FalharAsync(await CopiarLinhaAsync("envios_de_decisoes", cenario.EnvioDaGestao, "id = gen_random_uuid()"));
        PostgresException segundaLinha = await FalharAsync(await CopiarLinhaAsync("migracoes_da_concessao", cenario.Linha, "id = gen_random_uuid()"));
        PostgresException segundaRevisao = await FalharAsync(await CopiarLinhaAsync("revisoes_das_concessoes", cenario.Revisao, "id = gen_random_uuid()"));

        segundoEnvio.SqlState.Should().Be(Duplicidade);
        segundoEnvio.ConstraintName.Should().Be("ux_envios_de_decisoes_gestao");
        segundaLinha.SqlState.Should().Be(Duplicidade);
        segundaLinha.ConstraintName.Should().Be("ux_migracoes_da_concessao_revisao_concessao");
        segundaRevisao.SqlState.Should().Be(Duplicidade);
    }

    [Fact(DisplayName = "As chaves estrangeiras da trilha restringem a exclusão da concessão, do envio e da revisão")]
    public async Task ChavesEstrangeiras_RestringemAExclusao()
    {
        Cenario cenario = await MontarAsync();

        PostgresException concessao = await FalharAsync($"DELETE FROM autorizacao.concessoes WHERE id = '{cenario.ConcessaoDeLeandro}'");
        PostgresException envio = await FalharAsync($"DELETE FROM autorizacao.envios_de_decisoes WHERE id = '{cenario.EnvioDaGestao}'");
        PostgresException revisao = await FalharAsync($"DELETE FROM autorizacao.revisoes_das_concessoes WHERE id = '{cenario.Revisao}'");

        concessao.SqlState.Should().Be(Restricao);
        envio.SqlState.Should().Be(Restricao);
        revisao.SqlState.Should().Be(Restricao);
    }

    [Fact(DisplayName = "A revogação pela revisão é recusada em cada ramo incompleto e aceita quando completa e da revisão do perfil")]
    public async Task Revogacao_RecusaRamoIncompletoEAceitaCompleta()
    {
        Cenario cenario = await MontarAsync();
        string revisao = cenario.Revisao.ToString();

        PostgresException semMotivo = await FalharAsync(Revogar(cenario, $"revogada_em = now(), revogada_pela_revisao_id = '{revisao}'"));
        PostgresException comPessoa = await FalharAsync(Revogar(
            cenario, $"revogada_em = now(), motivo_revogacao = 'x', revogada_por_emissor = 'e', revogada_por_conta_id = 's', revogada_pela_revisao_id = '{revisao}'"));
        PostgresException soRevisao = await FalharAsync(Revogar(cenario, $"revogada_pela_revisao_id = '{revisao}'"));
        PostgresException outraRevisao = await FalharAsync(Revogar(
            cenario, "revogada_em = now(), motivo_revogacao = 'x', revogada_pela_revisao_id = gen_random_uuid()"));

        new[] { semMotivo, comPessoa, soRevisao }.Should().OnlyContain(static e => e.ConstraintName == "ck_concessoes_revogacao_completa");
        outraRevisao.SqlState.Should().Be(ChaveEstrangeira);
        (await ExecutarAsync(Revogar(cenario, $"revogada_em = now(), motivo_revogacao = 'x', revogada_pela_revisao_id = '{revisao}'"))).Should().Be(1);
    }

    private static string Revogar(Cenario cenario, string colunas) =>
        $"UPDATE autorizacao.concessoes SET {colunas} WHERE id = '{cenario.ConcessaoDeIsabella}'";

    // Copia a linha modelo com as colunas alteradas ("coluna = expressão, ..."), sempre com identificador novo: a gravação
    // direta parte de uma linha que o agregado gravou e muda só o que o caso quer testar. A coluna de identidade fica de fora: o
    // banco gera o número da cópia.
    private async Task<string> CopiarLinhaAsync(string tabela, Guid modelo, string alteracoes)
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();
        List<string> colunas = await escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>().Database
            .SqlQuery<string>($"SELECT column_name AS \"Value\" FROM information_schema.columns WHERE table_schema = 'autorizacao' AND table_name = {tabela} AND is_identity = 'NO' ORDER BY ordinal_position")
            .ToListAsync();
        Dictionary<string, string> trocas = alteracoes.Split(", ").Select(t => t.Split(" = ", 2)).ToDictionary(t => t[0], t => t[1]);
        trocas.TryAdd("id", "gen_random_uuid()");

        return $"INSERT INTO autorizacao.{tabela} ({string.Join(", ", colunas)}) "
            + $"SELECT {string.Join(", ", colunas.Select(c => trocas.GetValueOrDefault(c, c)))} FROM autorizacao.{tabela} WHERE id = '{modelo}'";
    }

    private async Task<Cenario> MontarAsync()
    {
        RevisaoAberta revisao = await AbrirRevisaoAsync(Isabella, Leandro);
        await MigrarNasDuasEtapasAsync(revisao.PerfilId, revisao.Concessoes[Leandro]);

        return new Cenario(
            await UnicoAsync("SELECT id AS \"Value\" FROM autorizacao.revisoes_das_concessoes"),
            await UnicoAsync("SELECT id AS \"Value\" FROM autorizacao.envios_de_decisoes WHERE etapa = 'GESTAO_DE_ACESSOS'"),
            await UnicoAsync($"SELECT id AS \"Value\" FROM autorizacao.migracoes_da_concessao WHERE concessao_id = '{revisao.Concessoes[Leandro]}'"),
            revisao.Concessoes[Leandro],
            revisao.Concessoes[Isabella],
            await UnicoAsync($"SELECT id AS \"Value\" FROM autorizacao.decisoes_da_migracao WHERE concessao_id = '{revisao.Concessoes[Leandro]}' AND etapa = 'GESTAO_DE_ACESSOS'"),
            await UnicoAsync($"SELECT id AS \"Value\" FROM autorizacao.decisoes_da_migracao WHERE concessao_id = '{revisao.Concessoes[Leandro]}' AND etapa = 'PROTECAO_DE_DADOS'"),
            await UnicoAsync($"SELECT id AS \"Value\" FROM autorizacao.decisoes_da_migracao WHERE concessao_id = '{revisao.Concessoes[Isabella]}' AND ator = 'SISTEMA'"));
    }

    private async Task<Guid> UnicoAsync(string sql)
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();

        return await escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>().Database.SqlQueryRaw<Guid>(sql).SingleAsync();
    }

    private async Task<int> ExecutarAsync(string sql)
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();

        return await escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>().Database.ExecuteSqlRawAsync(sql);
    }

    private async Task<PostgresException> FalharAsync(string sql)
    {
        Func<Task> gravacao = () => ExecutarAsync(sql);

        return (await gravacao.Should().ThrowAsync<PostgresException>()).Which;
    }

    private sealed record Cenario(
        Guid Revisao, Guid EnvioDaGestao, Guid Linha, Guid ConcessaoDeLeandro, Guid ConcessaoDeIsabella,
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
        using HttpResponseMessage envio = await EnviarDecisoesDaGestaoAsync(gestao, perfilId, [Decisao(concessao, "MIGRAR")]);
        await CorpoAsync(envio, HttpStatusCode.OK);
        using HttpClient protecao = await ProtecaoAsync();
        using HttpResponseMessage etapa = await EnviarAsync(
            protecao, HttpMethod.Post, RotaDaRevisao(perfilId, "/protecao-de-dados"), new { decisoes = new[] { Decisao(concessao, "MIGRAR") } });
        await CorpoAsync(etapa, HttpStatusCode.OK);
    }
}
