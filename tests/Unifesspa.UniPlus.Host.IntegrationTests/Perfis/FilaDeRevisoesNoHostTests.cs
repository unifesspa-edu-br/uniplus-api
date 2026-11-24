namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;

/// <summary>
/// A fila de revisões das concessões de cada etapa pelo host real, com o relógio controlável: o que entra e o que sai para
/// quem lê, a recusa de acesso e a ausência de dado pessoal.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class FilaDeRevisoesNoHostTests : RevisaoDasConcessoesNoHostTestBase
{
    private const string RotaDaFila = "/api/autorizacao/revisoes-de-concessoes";

    public FilaDeRevisoesNoHostTests(MonolitoDaInstalacaoFixture fixture, HostDaRevisaoDasConcessoes hostDaClasse)
        : base(fixture, hostDaClasse)
    {
    }

    [Fact(DisplayName = "A gestão encontra a revisão com o envio por enviar, com o prazo e a quantidade a decidir, sem nome de beneficiário")]
    public async Task Gestao_EncontraARevisaoComOEnvioPendente()
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Leandro, Betina);
        using HttpClient gestao = await GestaoAsync();

        JsonElement fila = await FilaAsync(gestao, EtapaGestao);

        JsonElement item = ItemDoPerfil(fila, cenario.PerfilId);
        item.GetProperty("versaoAlvo").GetInt32().Should().Be(2);
        item.GetProperty("decisoesEnviadas").GetBoolean().Should().BeFalse();
        item.GetProperty("quantidadeADecidir").GetInt32().Should().Be(2);
        (item.GetProperty("terminaEm").GetDateTimeOffset() - Relogio.GetUtcNow()).Should().BeCloseTo(TimeSpan.FromHours(72), TimeSpan.FromMinutes(5));
        item.GetProperty("perfilNome").GetString().Should().NotBeNullOrWhiteSpace();
        fila.GetRawText().Should().NotContain(Leandro.Nome).And.NotContain(Betina.Nome).And.NotContain(Leandro.Email);
    }

    [Fact(DisplayName = "Quem tem só a própria concessão encontra o envio por enviar, sem concessão a decidir")]
    public async Task Gestao_SoComAPropriaConcessao_EncontraOEnvioSemQuantidade()
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Diogo);
        using HttpClient diogo = await GestorAsync();

        JsonElement item = ItemDoPerfil(await FilaAsync(diogo, EtapaGestao), cenario.PerfilId);

        item.GetProperty("decisoesEnviadas").GetBoolean().Should().BeFalse();
        item.GetProperty("quantidadeADecidir").GetInt32().Should().Be(0);
    }

    [Fact(DisplayName = "A concessão retida não aparece para o próprio beneficiário e aparece para outra pessoa da gestão")]
    public async Task Gestao_ConcessaoRetida_SoApareceParaOutraPessoa()
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Diogo, Leandro);
        using HttpClient diogo = await GestorAsync();
        using HttpResponseMessage envio = await EnviarDecisoesDaGestaoAsync(diogo, cenario.PerfilId, [Decisao(cenario.Concessoes[Leandro], "RECUSAR")], confirmacaoDeExclusao: true);
        await CorpoAsync(envio, HttpStatusCode.OK);
        using HttpClient betina = await ClienteComPermissoesAsync(Betina, AprovarFuncional);

        JsonElement doBeneficiario = await FilaAsync(diogo, EtapaGestao);
        JsonElement deOutraPessoa = await FilaAsync(betina, EtapaGestao);

        PerfisDa(doBeneficiario).Should().NotContain(cenario.PerfilId);
        JsonElement item = ItemDoPerfil(deOutraPessoa, cenario.PerfilId);
        item.GetProperty("decisoesEnviadas").GetBoolean().Should().BeTrue();
        item.GetProperty("quantidadeADecidir").GetInt32().Should().Be(1);
    }

    [Fact(DisplayName = "A proteção de dados encontra o que a gestão migrou, e a gestão deixa de ver a revisão quando nada resta para ela")]
    public async Task Protecao_EncontraAsMigracoesQueAguardamAEtapa()
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Leandro);
        using HttpClient gestao = await GestaoAsync();
        using HttpClient marlene = await ProtecaoAsync();
        PerfisDa(await FilaAsync(marlene, EtapaProtecao)).Should().NotContain(cenario.PerfilId);
        using HttpResponseMessage envio = await EnviarDecisoesDaGestaoAsync(gestao, cenario.PerfilId, [Decisao(cenario.Concessoes[Leandro], "MIGRAR")]);
        await CorpoAsync(envio, HttpStatusCode.OK);

        JsonElement item = ItemDoPerfil(await FilaAsync(marlene, EtapaProtecao), cenario.PerfilId);

        item.GetProperty("decisoesEnviadas").GetBoolean().Should().BeTrue();
        item.GetProperty("quantidadeADecidir").GetInt32().Should().Be(1);
        PerfisDa(await FilaAsync(gestao, EtapaGestao)).Should().NotContain(cenario.PerfilId);
    }

    [Fact(DisplayName = "Quem decidiu a etapa da gestão não encontra a revisão na fila da proteção de dados")]
    public async Task Protecao_QuemDecidiuAGestao_NaoEncontraAMesmaConcessao()
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Leandro);
        using HttpClient ambas = await GestorComAmbasAsync();
        using HttpResponseMessage envio = await EnviarDecisoesDaGestaoAsync(ambas, cenario.PerfilId, [Decisao(cenario.Concessoes[Leandro], "MIGRAR")]);
        await CorpoAsync(envio, HttpStatusCode.OK);
        using HttpClient marlene = await ProtecaoAsync();

        PerfisDa(await FilaAsync(ambas, EtapaProtecao)).Should().NotContain(cenario.PerfilId);
        PerfisDa(await FilaAsync(marlene, EtapaProtecao)).Should().Contain(cenario.PerfilId);
    }

    [Fact(DisplayName = "A proteção de dados não conta a própria concessão, e quem só tem a própria não encontra a revisão")]
    public async Task Protecao_NaoContaAPropriaConcessao()
    {
        RevisaoAberta comOutra = await AbrirRevisaoAsync(Leandro, Marlene);
        RevisaoAberta soAPropria = await AbrirRevisaoAsync(Marlene);
        await MigrarTudoNaGestaoAsync(comOutra, soAPropria);

        using HttpClient marlene = await ProtecaoAsync();
        JsonElement fila = await FilaAsync(marlene, EtapaProtecao);

        ItemDoPerfil(fila, comOutra.PerfilId).GetProperty("quantidadeADecidir").GetInt32().Should().Be(1);
        PerfisDa(fila).Should().NotContain(soAPropria.PerfilId);
    }

    [Fact(DisplayName = "A concessão vencida ou revogada deixa de contar enquanto a revisão continua aberta")]
    public async Task Gestao_ConcessaoVencidaOuRevogada_DeixaDeContar()
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Leandro, Betina, Isabella);
        await ExecutarSqlAsync(
            "UPDATE autorizacao.concessoes SET valida_ate = now() + interval '1 hour' WHERE id = {0}", cenario.Concessoes[Leandro]);
        using HttpClient diogo = await GestorAsync();
        (await QuantidadeAsync(diogo, cenario.PerfilId)).Should().Be(3);

        Relogio.Avancar(TimeSpan.FromHours(2));
        (await QuantidadeAsync(diogo, cenario.PerfilId)).Should().Be(2);

        (await RevogarAsync(diogo, cenario.Concessoes[Betina], MotivoDaRevogacao, Betina.Email)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await QuantidadeAsync(diogo, cenario.PerfilId)).Should().Be(1);
        (await RevisaoAsync(diogo, cenario.PerfilId)).GetProperty("situacao").GetString().Should().Be("aberta");
    }

    [Fact(DisplayName = "A fila recusa, antes de ler qualquer revisão, quem não tem a permissão da etapa")]
    public async Task Fila_SemPermissaoDaEtapa_Responde403SemDados()
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Betina);
        using HttpClient semPermissao = Cliente(Leandro);
        using HttpClient gestao = await GestaoAsync();

        using HttpResponseMessage daGestao = await semPermissao.GetAsync(new Uri($"{RotaDaFila}?etapa={EtapaGestao}", UriKind.Relative));
        using HttpResponseMessage daProtecao = await gestao.GetAsync(new Uri($"{RotaDaFila}?etapa={EtapaProtecao}", UriKind.Relative));

        daGestao.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        daProtecao.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await daGestao.Content.ReadAsStringAsync()).Should().NotContain(cenario.PerfilId.ToString());
        (await daProtecao.Content.ReadAsStringAsync()).Should().NotContain(cenario.PerfilId.ToString());
    }

    [Theory(DisplayName = "A fila recusa a etapa que não existe ou que falta")]
    [InlineData("?etapa=outra")]
    [InlineData("")]
    public async Task Fila_EtapaInvalida_Recusa(string consulta)
    {
        using HttpClient gestao = await GestaoAsync();

        using HttpResponseMessage resposta = await gestao.GetAsync(new Uri($"{RotaDaFila}{consulta}", UriKind.Relative));

        await ConferirRecusaDoPerfilAsync(resposta, HttpStatusCode.UnprocessableEntity, "etapa_invalida");
    }

    [Fact(DisplayName = "A revisão cujo prazo terminou sai da fila, mesmo com a rotina parada")]
    public async Task Fila_RevisaoVencida_SaiDaFila()
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Leandro);
        using HttpClient gestao = await GestaoAsync();
        PerfisDa(await FilaAsync(gestao, EtapaGestao)).Should().Contain(cenario.PerfilId);

        Relogio.Avancar(TimeSpan.FromHours(73));

        PerfisDa(await FilaAsync(gestao, EtapaGestao)).Should().NotContain(cenario.PerfilId);
    }

    [Fact(DisplayName = "A revisão encerrada antes do prazo sai da fila")]
    public async Task Fila_RevisaoEncerrada_SaiDaFila()
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Leandro);
        using HttpClient gestao = await GestaoAsync();
        using HttpResponseMessage encerramento = await EnviarAsync(
            gestao, HttpMethod.Post, RotaDaRevisao(cenario.PerfilId, "/encerramento"), new { motivo = "Prazo acordado com a gestão.", confirmacaoDeExclusao = true });
        await CorpoAsync(encerramento, HttpStatusCode.OK);

        PerfisDa(await FilaAsync(gestao, EtapaGestao)).Should().NotContain(cenario.PerfilId);
    }

    private static async Task<JsonElement> FilaAsync(HttpClient cliente, string etapa) =>
        await LerAsync(cliente, $"{RotaDaFila}?etapa={etapa}");

    private static async Task<int> QuantidadeAsync(HttpClient cliente, Guid perfilId) =>
        ItemDoPerfil(await FilaAsync(cliente, EtapaGestao), perfilId).GetProperty("quantidadeADecidir").GetInt32();

    private static Guid[] PerfisDa(JsonElement fila) => [.. fila.EnumerateArray().Select(i => i.GetProperty("perfilId").GetGuid())];

    private static JsonElement ItemDoPerfil(JsonElement fila, Guid perfilId) =>
        fila.EnumerateArray().Single(i => i.GetProperty("perfilId").GetGuid() == perfilId);

    // A gestão envia, em cada revisão, o envio que migra todas as concessões.
    private async Task MigrarTudoNaGestaoAsync(params RevisaoAberta[] revisoes)
    {
        using HttpClient gestao = await GestaoAsync();
        foreach (RevisaoAberta revisao in revisoes)
        {
            using HttpResponseMessage envio = await EnviarDecisoesDaGestaoAsync(gestao, revisao.PerfilId, [.. revisao.Concessoes.Values.Select(id => Decisao(id, "MIGRAR"))]);
            await CorpoAsync(envio, HttpStatusCode.OK);
        }
    }
}
