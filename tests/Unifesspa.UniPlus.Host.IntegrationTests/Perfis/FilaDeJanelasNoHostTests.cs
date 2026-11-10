namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;

/// <summary>
/// A fila de janelas de migração de cada etapa pelo host real, com o relógio controlável: o que entra e o que sai para
/// quem lê, a recusa de acesso e a ausência de dado pessoal.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class FilaDeJanelasNoHostTests : JanelaDeMigracaoNoHostTestBase
{
    private const string RotaDaFila = "/api/autorizacao/janelas-de-migracao";

    public FilaDeJanelasNoHostTests(MonolitoDaInstalacaoFixture fixture, HostDaJanelaDeMigracao hostDaClasse)
        : base(fixture, hostDaClasse)
    {
    }

    [Fact(DisplayName = "A gestão encontra a janela com o lote por enviar, com o prazo e a quantidade a decidir, sem nome de beneficiário")]
    public async Task Gestao_EncontraAJanelaComOLotePendente()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Leandro, Betina);
        using HttpClient gestao = await GestaoAsync();

        JsonElement fila = await FilaAsync(gestao, EtapaGestao);

        JsonElement item = ItemDoPerfil(fila, cenario.PerfilId);
        item.GetProperty("versaoAlvo").GetInt32().Should().Be(2);
        item.GetProperty("loteEnviado").GetBoolean().Should().BeFalse();
        item.GetProperty("quantidadeADecidir").GetInt32().Should().Be(2);
        (item.GetProperty("prazo").GetDateTimeOffset() - Relogio.GetUtcNow()).Should().BeCloseTo(TimeSpan.FromHours(72), TimeSpan.FromMinutes(5));
        item.GetProperty("perfilNome").GetString().Should().NotBeNullOrWhiteSpace();
        fila.GetRawText().Should().NotContain(Leandro.Nome).And.NotContain(Betina.Nome).And.NotContain(Leandro.Email);
    }

    [Fact(DisplayName = "Quem tem só a própria concessão encontra o lote por enviar, sem concessão a decidir")]
    public async Task Gestao_SoComAPropriaConcessao_EncontraOLoteSemQuantidade()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Diogo);
        using HttpClient diogo = await GestorAsync();

        JsonElement item = ItemDoPerfil(await FilaAsync(diogo, EtapaGestao), cenario.PerfilId);

        item.GetProperty("loteEnviado").GetBoolean().Should().BeFalse();
        item.GetProperty("quantidadeADecidir").GetInt32().Should().Be(0);
    }

    [Fact(DisplayName = "A concessão retida não aparece para o próprio beneficiário e aparece para outra pessoa da gestão")]
    public async Task Gestao_ConcessaoRetida_SoApareceParaOutraPessoa()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Diogo, Leandro);
        using HttpClient diogo = await GestorAsync();
        using HttpResponseMessage lote = await EnviarLoteAsync(diogo, cenario.PerfilId, [Decisao(cenario.Concessoes[Leandro], "recusar")], confirmacaoDeExclusao: true);
        await CorpoAsync(lote, HttpStatusCode.OK);
        using HttpClient betina = await ClienteComPermissoesAsync(Betina, AprovarFuncional);

        JsonElement doBeneficiario = await FilaAsync(diogo, EtapaGestao);
        JsonElement deOutraPessoa = await FilaAsync(betina, EtapaGestao);

        PerfisDa(doBeneficiario).Should().NotContain(cenario.PerfilId);
        JsonElement item = ItemDoPerfil(deOutraPessoa, cenario.PerfilId);
        item.GetProperty("loteEnviado").GetBoolean().Should().BeTrue();
        item.GetProperty("quantidadeADecidir").GetInt32().Should().Be(1);
    }

    [Fact(DisplayName = "A proteção de dados encontra o que a gestão migrou, e a gestão deixa de ver a janela quando nada resta para ela")]
    public async Task Protecao_EncontraAsMigracoesQueAguardamAEtapa()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Leandro);
        using HttpClient gestao = await GestaoAsync();
        using HttpClient marlene = await ProtecaoAsync();
        PerfisDa(await FilaAsync(marlene, EtapaProtecao)).Should().NotContain(cenario.PerfilId);
        using HttpResponseMessage lote = await EnviarLoteAsync(gestao, cenario.PerfilId, [Decisao(cenario.Concessoes[Leandro], "migrar")]);
        await CorpoAsync(lote, HttpStatusCode.OK);

        JsonElement item = ItemDoPerfil(await FilaAsync(marlene, EtapaProtecao), cenario.PerfilId);

        item.GetProperty("loteEnviado").GetBoolean().Should().BeTrue();
        item.GetProperty("quantidadeADecidir").GetInt32().Should().Be(1);
        PerfisDa(await FilaAsync(gestao, EtapaGestao)).Should().NotContain(cenario.PerfilId);
    }

    [Fact(DisplayName = "Quem decidiu a etapa da gestão não encontra a janela na fila da proteção de dados")]
    public async Task Protecao_QuemDecidiuAGestao_NaoEncontraAMesmaConcessao()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Leandro);
        using HttpClient ambas = await GestoraComAmbasAsync();
        using HttpResponseMessage lote = await EnviarLoteAsync(ambas, cenario.PerfilId, [Decisao(cenario.Concessoes[Leandro], "migrar")]);
        await CorpoAsync(lote, HttpStatusCode.OK);
        using HttpClient marlene = await ProtecaoAsync();

        PerfisDa(await FilaAsync(ambas, EtapaProtecao)).Should().NotContain(cenario.PerfilId);
        PerfisDa(await FilaAsync(marlene, EtapaProtecao)).Should().Contain(cenario.PerfilId);
    }

    [Fact(DisplayName = "A fila recusa, antes de ler qualquer janela, quem não tem a permissão da etapa")]
    public async Task Fila_SemPermissaoDaEtapa_Responde403SemDados()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Betina);
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

    [Fact(DisplayName = "A janela cujo prazo terminou sai da fila, mesmo com a rotina parada")]
    public async Task Fila_JanelaVencida_SaiDaFila()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Leandro);
        using HttpClient gestao = await GestaoAsync();
        PerfisDa(await FilaAsync(gestao, EtapaGestao)).Should().Contain(cenario.PerfilId);

        Relogio.Avancar(TimeSpan.FromHours(73));

        PerfisDa(await FilaAsync(gestao, EtapaGestao)).Should().NotContain(cenario.PerfilId);
    }

    [Fact(DisplayName = "A janela encerrada antes do prazo sai da fila")]
    public async Task Fila_JanelaEncerrada_SaiDaFila()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Leandro);
        using HttpClient gestao = await GestaoAsync();
        using HttpResponseMessage encerramento = await EnviarAsync(
            gestao, HttpMethod.Post, RotaDaJanela(cenario.PerfilId, "/encerramento"), new { motivo = "Prazo acordado com a gestão.", confirmacaoDeExclusao = true });
        await CorpoAsync(encerramento, HttpStatusCode.OK);

        PerfisDa(await FilaAsync(gestao, EtapaGestao)).Should().NotContain(cenario.PerfilId);
    }

    private static async Task<JsonElement> FilaAsync(HttpClient cliente, string etapa) =>
        await LerAsync(cliente, $"{RotaDaFila}?etapa={etapa}");

    private static Guid[] PerfisDa(JsonElement fila) => [.. fila.EnumerateArray().Select(i => i.GetProperty("perfilId").GetGuid())];

    private static JsonElement ItemDoPerfil(JsonElement fila, Guid perfilId) =>
        fila.EnumerateArray().Single(i => i.GetProperty("perfilId").GetGuid() == perfilId);
}
