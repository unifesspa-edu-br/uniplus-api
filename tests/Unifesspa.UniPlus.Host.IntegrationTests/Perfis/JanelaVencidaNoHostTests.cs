namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// A janela vencida e ainda não materializada diante de quem não tem acesso: a tentativa recusada não grava o fim da
/// janela, que só a operação de quem pode (ou a rotina) materializa.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class JanelaVencidaNoHostTests : JanelaDeMigracaoNoHostTestBase
{
    private static readonly TimeSpan DepoisDoPrazo = TimeSpan.FromHours(73);

    public JanelaVencidaNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "A aprovação de pedido sem permissão sobre perfil com janela vencida é recusada e não materializa a janela")]
    public async Task AprovacaoSemPermissao_NaoMaterializaAJanela()
    {
        (Guid perfilId, Guid pedido) = await PedidoEmPerfilComJanelaAsync();
        Relogio.Avancar(DepoisDoPrazo);
        using HttpClient semPermissao = Cliente(Leandro);

        using HttpResponseMessage resposta = await EnviarAsync(semPermissao, HttpMethod.Post, $"{Rota}/{pedido}/aprovacoes");

        resposta.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
        (await EncerradaEmAsync(perfilId)).Should().BeNull();
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        using HttpResponseMessage comPermissao = await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{pedido}/aprovacoes");
        comPermissao.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
        (await EncerradaEmAsync(perfilId)).Should().NotBeNull();
    }

    [Fact(DisplayName = "O pedido criado depois do prazo, com a rotina parada, materializa o fim da janela e não esbarra nela")]
    public async Task Pedido_DepoisDoPrazo_SemARotina_NaoEsbarraNaJanela()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Leandro);
        using HttpClient betina = Cliente(Betina);
        Relogio.Avancar(DepoisDoPrazo);

        Guid pedido = await CriarAsync(betina, cenario.PerfilId, await SemearUnidadeAsync(), null);

        pedido.Should().NotBeEmpty();
        (await EncerradaEmAsync(cenario.PerfilId)).Should().NotBeNull();
        using HttpClient gestao = await GestaoAsync();
        SituacaoDe(await JanelaAsync(gestao, cenario.PerfilId), cenario.Concessoes[Leandro]).Should().Be("nao_migrada");
    }

    [Fact(DisplayName = "A última pendência revogada com a rotina parada deixa a janela de bloquear o pedido, e a operação seguinte a encerra")]
    public async Task UltimaPendenciaRevogada_SemARotina_LiberaOPedido()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Leandro);
        using HttpClient administrador = await ClienteComPermissoesAsync(Diogo, Administrar);
        using HttpResponseMessage revogacao = await EnviarAsync(
            administrador, HttpMethod.Post, $"/api/autorizacao/concessoes/{cenario.Concessoes[Leandro]}/revogacao",
            new { motivo = "A pessoa deixou a função.", confirmacaoEmail = Leandro.Email });
        revogacao.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await EncerradaEmAsync(cenario.PerfilId)).Should().BeNull();
        using HttpClient betina = Cliente(Betina);

        Guid pedido = await CriarAsync(betina, cenario.PerfilId, await SemearUnidadeAsync(), null);

        pedido.Should().NotBeEmpty();
        (await EncerradaEmAsync(cenario.PerfilId)).Should().NotBeNull();
    }

    [Fact(DisplayName = "O lote depois do encerramento antecipado grava o fim, é recusado e responde igual no replay")]
    public async Task Lote_DepoisDoEncerramentoAntecipado_ReplayIgual()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Leandro);
        using HttpClient gestao = await GestaoAsync();
        using HttpClient administrador = await ClienteComPermissoesAsync(Diogo, Administrar);
        (await EnviarAsync(
            administrador, HttpMethod.Post, $"/api/autorizacao/concessoes/{cenario.Concessoes[Leandro]}/revogacao",
            new { motivo = "A pessoa deixou a função.", confirmacaoEmail = Leandro.Email })).Dispose();
        string chave = Guid.NewGuid().ToString();

        using HttpResponseMessage primeira = await EnviarLoteAsync(gestao, cenario.PerfilId, [Decisao(cenario.Concessoes[Leandro], "migrar")], chave: chave);
        using HttpResponseMessage replay = await EnviarLoteAsync(gestao, cenario.PerfilId, [Decisao(cenario.Concessoes[Leandro], "migrar")], chave: chave);

        await ConferirRecusaDaMigracaoAsync(primeira, HttpStatusCode.UnprocessableEntity, "janela_encerrada");
        await ConferirRecusaDaMigracaoAsync(replay, HttpStatusCode.UnprocessableEntity, "janela_encerrada");
        (await EncerradaEmAsync(cenario.PerfilId)).Should().NotBeNull();
        (await JanelaAsync(gestao, cenario.PerfilId)).GetProperty("forma").GetString().Should().Be("concluida");
    }

    [Fact(DisplayName = "O replay do lote, da etapa e do encerramento devolve a mesma resposta sem repetir a transição")]
    public async Task Replay_DoLoteDaEtapaEDoEncerramento_NaoRepeteATransicao()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Leandro, Betina);
        using HttpClient gestao = await GestaoAsync();
        using HttpClient protecao = await ProtecaoAsync();
        object[] lote = [Decisao(cenario.Concessoes[Leandro], "migrar"), Decisao(cenario.Concessoes[Betina], "migrar")];
        object etapa = new { decisoes = new[] { Decisao(cenario.Concessoes[Leandro], "migrar") } };
        object encerramento = new { motivo = "Prazo acordado com a gestão.", confirmacaoDeExclusao = true };
        string[] chaves = [Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString()];

        string loteUm = await TextoAsync(await EnviarLoteAsync(gestao, cenario.PerfilId, lote, chave: chaves[0]));
        string loteDois = await TextoAsync(await EnviarLoteAsync(gestao, cenario.PerfilId, lote, chave: chaves[0]));
        string etapaUm = await TextoAsync(await EnviarAsync(protecao, HttpMethod.Post, RotaDaJanela(cenario.PerfilId, "/protecao-de-dados"), etapa, chaves[1]));
        string etapaDois = await TextoAsync(await EnviarAsync(protecao, HttpMethod.Post, RotaDaJanela(cenario.PerfilId, "/protecao-de-dados"), etapa, chaves[1]));
        string fimUm = await TextoAsync(await EnviarAsync(gestao, HttpMethod.Post, RotaDaJanela(cenario.PerfilId, "/encerramento"), encerramento, chaves[2]));
        string fimDois = await TextoAsync(await EnviarAsync(gestao, HttpMethod.Post, RotaDaJanela(cenario.PerfilId, "/encerramento"), encerramento, chaves[2]));

        loteDois.Should().Be(loteUm);
        etapaDois.Should().Be(etapaUm);
        fimDois.Should().Be(fimUm);
        (await ContarAsync("SELECT count(*)::int AS \"Value\" FROM autorizacao.lotes_da_migracao")).Should().Be(2);
    }

    [Fact(DisplayName = "A etapa da proteção de dados com itens válidos e inválidos não grava nada")]
    public async Task Etapa_ComItensValidosEInvalidos_NaoGravaNada()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Leandro, Betina);
        using HttpClient gestao = await GestaoAsync();
        using HttpClient protecao = await ProtecaoAsync();
        (await EnviarLoteAsync(gestao, cenario.PerfilId, [Decisao(cenario.Concessoes[Leandro], "migrar"), Decisao(cenario.Concessoes[Betina], "migrar")])).Dispose();

        using HttpResponseMessage etapa = await EnviarAsync(
            protecao,
            HttpMethod.Post,
            RotaDaJanela(cenario.PerfilId, "/protecao-de-dados"),
            new { decisoes = new[] { Decisao(cenario.Concessoes[Leandro], "migrar"), Decisao(Guid.CreateVersion7(), "migrar") } });

        etapa.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await etapa.Content.ReadAsStringAsync());
        JsonElement janela = await JanelaAsync(gestao, cenario.PerfilId);
        SituacaoDe(janela, cenario.Concessoes[Leandro]).Should().Be("aguardando_protecao_de_dados");
        SituacaoDe(janela, cenario.Concessoes[Betina]).Should().Be("aguardando_protecao_de_dados");
        (await ContarAsync("SELECT count(*)::int AS \"Value\" FROM autorizacao.lotes_da_migracao")).Should().Be(1);
    }

    [Fact(DisplayName = "A leitura da concessão não migrada mostra a revogação pela janela, sem pessoa inventada")]
    public async Task Leitura_DaConcessaoNaoMigrada_MostraARevogacaoPelaJanela()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Leandro);
        using HttpClient administrador = await ClienteComPermissoesAsync(Diogo, Administrar);
        Relogio.Avancar(DepoisDoPrazo);
        await ProcessarJanelasAsync();

        JsonElement concessao = await LerAsync(administrador, $"/api/autorizacao/concessoes/{cenario.Concessoes[Leandro]}");

        concessao.GetProperty("ativa").GetBoolean().Should().BeFalse();
        concessao.GetProperty("revogadaPor").ValueKind.Should().Be(JsonValueKind.Null);
        concessao.GetProperty("revogadaPelaJanelaId").GetGuid().Should().NotBeEmpty();
        concessao.GetProperty("revogadaEm").GetDateTimeOffset().Should().Be(
            (await JanelaAsync(await GestaoAsync(), cenario.PerfilId)).GetProperty("prazo").GetDateTimeOffset());
    }

    // O pedido nasce antes da janela, que a janela aberta o impediria; a troca de composição abre a janela como a produção.
    private async Task<(Guid PerfilId, Guid Pedido)> PedidoEmPerfilComJanelaAsync()
    {
        await ResetarAsync();
        Guid perfilId = await SemearPerfilAsync("Perfil com janela", Consultar);
        using HttpClient betina = Cliente(Betina);
        Guid pedido = await CriarAsync(betina, perfilId, await SemearUnidadeAsync(), null);
        await ConcessoesDeTeste.ConcederAsync(
            Fixture.Factory.Services, Leandro.Sub.ToString(), perfilId, EscopoDaConcessao.DaUnidade(Guid.CreateVersion7()), email: Leandro.Email);
        await TrocarComposicaoAsync(perfilId, Consultar, Sensivel);

        return (perfilId, pedido);
    }

    private static async Task<string> TextoAsync(HttpResponseMessage resposta)
    {
        using (resposta)
        {
            return $"{(int)resposta.StatusCode} {await resposta.Content.ReadAsStringAsync()}";
        }
    }

    private async Task<int> ContarAsync(string sql)
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();

        return await escopo.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().Database.SqlQueryRaw<int>(sql).SingleAsync();
    }

    private async Task<DateTimeOffset?> EncerradaEmAsync(Guid perfilId)
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();

        return await escopo.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().Database
            .SqlQuery<DateTimeOffset?>($"SELECT encerrada_em AS \"Value\" FROM autorizacao.janelas_de_migracao WHERE perfil_id = {perfilId}")
            .SingleAsync();
    }
}
