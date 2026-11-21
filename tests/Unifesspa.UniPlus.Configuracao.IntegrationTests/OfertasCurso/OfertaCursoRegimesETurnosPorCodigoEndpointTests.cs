namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.OfertasCurso;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.OfertasCurso.ApoioDosEndpointsDeOfertaCurso;

/// <summary>
/// Os regimes e os turnos da oferta nas bordas HTTP: a resposta leva código e rótulo, e o pedido recusa, por comparação
/// exata, o que não for um dos códigos aceitos, sem repetir o valor recebido.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class OfertaCursoRegimesETurnosPorCodigoEndpointTests
{
    private const string Prefixo = "uniplus.configuracao.oferta_curso.";

    public static TheoryData<string, object?, string> RecusasDoFuncionamento => new()
    {
        { "regimeDeFuncionamento", null, "regime_de_funcionamento_obrigatorio" },
        { "regimeDeFuncionamento", string.Empty, "regime_de_funcionamento_obrigatorio" },
        { "regimeDeFuncionamento", " EXTENSIVO", "regime_de_funcionamento_invalido" },
        { "regimeDeFuncionamento", "EXTENSIVO ", "regime_de_funcionamento_invalido" },
        { "regimeDeFuncionamento", "extensivo", "regime_de_funcionamento_invalido" },
        { "regimeDeFuncionamento", "SEMI_INTENSIVO", "regime_de_funcionamento_invalido" },
    };

    public static TheoryData<string, object?, string> RecusasDoRegimeDeTurno => new()
    {
        { "regimeDeTurno", null, "regime_de_turno_obrigatorio" },
        { "regimeDeTurno", string.Empty, "regime_de_turno_obrigatorio" },
        { "regimeDeTurno", " REGULAR", "regime_de_turno_invalido" },
        { "regimeDeTurno", "regular", "regime_de_turno_invalido" },
        { "regimeDeTurno", "PARCIAL", "regime_de_turno_invalido" },
    };

    public static TheoryData<string, object?, string> RecusasDosTurnos => new()
    {
        { "turnos", null, "turnos_obrigatorios" },
        { "turnos", Array.Empty<string>(), "turnos_obrigatorios" },
        { "turnos", new string?[] { null }, "turno_invalido" },
        { "turnos", new[] { string.Empty }, "turno_invalido" },
        { "turnos", new[] { " " }, "turno_invalido" },
        { "turnos", new[] { " MATUTINO" }, "turno_invalido" },
        { "turnos", new[] { "matutino" }, "turno_invalido" },
        { "turnos", new[] { "INTEGRAL" }, "turno_invalido" },
    };

    private readonly ConfiguracaoEndpointFixture _fixture;
    private readonly ApoioDosEndpointsDeOfertaCurso _apoio;

    public OfertaCursoRegimesETurnosPorCodigoEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
        _apoio = new ApoioDosEndpointsDeOfertaCurso(fixture);
    }

    [Fact(DisplayName = "POST e PUT respondem os regimes e os turnos como código e rótulo, na leitura por id e na listagem")]
    public async Task CriarEAtualizar_RespondemCodigoERotulo()
    {
        DependenciasDaOferta dependencias = await _apoio.SemearDependenciasAsync("regimes-codigo");
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarEmAlternanciaAsync(client, dependencias);

        await ConferirNaLeituraEnaListagemAsync(
            client, dependencias.CursoId, id, ("ALTERNANCIA_PEDAGOGICA", "Alternância pedagógica"), ("INTEGRAL", "Integral"),
            [("MATUTINO", "Matutino"), ("NOTURNO", "Noturno")]);
        await AtualizarParaIntensivoAsync(client, id);
        await ConferirNaLeituraEnaListagemAsync(
            client, dependencias.CursoId, id, ("INTENSIVO", "Intensivo"), ("INTEGRAL", "Integral"),
            [("VESPERTINO", "Vespertino"), ("NOTURNO", "Noturno")]);
    }

    [Theory(DisplayName = "POST com regime ou turnos fora dos códigos aceitos devolve 422 com o código publicado, no campo certo")]
    [MemberData(nameof(RecusasDoFuncionamento))]
    [MemberData(nameof(RecusasDoRegimeDeTurno))]
    [MemberData(nameof(RecusasDosTurnos))]
    public async Task Criar_ForaDosCodigosAceitos_Devolve422(string campo, object? valor, string codigoDoErro)
    {
        DependenciasDaOferta dependencias = await _apoio.SemearDependenciasAsync("regimes-post-422");
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await EnviarPostAdmin(client, CorpoOfertaCom(dependencias, (campo, valor)));

        await ConferirRecusaAsync(resposta, Prefixo + codigoDoErro, campo);
    }

    [Theory(DisplayName = "PUT com regime ou turnos fora dos códigos aceitos devolve 422 com o código publicado, no campo certo")]
    [MemberData(nameof(RecusasDoFuncionamento))]
    [MemberData(nameof(RecusasDoRegimeDeTurno))]
    [MemberData(nameof(RecusasDosTurnos))]
    public async Task Atualizar_ForaDosCodigosAceitos_Devolve422(string campo, object? valor, string codigoDoErro)
    {
        DependenciasDaOferta dependencias = await _apoio.SemearDependenciasAsync("regimes-put-422");
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarOfertaAsync(client, CorpoOferta(dependencias));

        using HttpResponseMessage resposta = await EnviarPutAdmin(client, id, CorpoDeAtualizacaoCom(id, (campo, valor)));

        await ConferirRecusaAsync(resposta, Prefixo + codigoDoErro, campo);
    }

    [Fact(DisplayName = "POST acumula os erros de regime e de turnos com o de outro campo, e a mensagem não repete os valores recebidos")]
    public async Task Criar_ErrosDeVariosCampos_AcumulaSemEcoDosValores()
    {
        DependenciasDaOferta dependencias = await _apoio.SemearDependenciasAsync("regimes-acumulo");
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await EnviarPostAdmin(client, CorpoComValoresDesconhecidos(dependencias));

        await ConferirAcumuloSemEcoAsync(resposta);
    }

    [Fact(DisplayName = "PUT acumula os erros de regime e de turnos com o de outro campo, e a mensagem não repete os valores recebidos")]
    public async Task Atualizar_ErrosDeVariosCampos_AcumulaSemEcoDosValores()
    {
        DependenciasDaOferta dependencias = await _apoio.SemearDependenciasAsync("regimes-acumulo-put");
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarOfertaAsync(client, CorpoOferta(dependencias));
        Dictionary<string, object?> corpo = CorpoDeAtualizacaoCom(id, ("programaDeOferta", "PROGRAMA_X"));
        corpo["regimeDeFuncionamento"] = "FUNCIONAMENTO_X";
        corpo["regimeDeTurno"] = "REGIME_X";
        corpo["turnos"] = new[] { "TURNO_X" };

        using HttpResponseMessage resposta = await EnviarPutAdmin(client, id, corpo);

        await ConferirAcumuloSemEcoAsync(resposta);
    }

    private static Task<Guid> CriarEmAlternanciaAsync(HttpClient client, DependenciasDaOferta dependencias) =>
        CriarOfertaAsync(client, CorpoOfertaCom(
            dependencias,
            ("regimeDeFuncionamento", "ALTERNANCIA_PEDAGOGICA"),
            ("regimeDeTurno", "INTEGRAL"),
            ("turnos", new[] { "NOTURNO", "MATUTINO" })));

    private static async Task AtualizarParaIntensivoAsync(HttpClient client, Guid id)
    {
        using HttpResponseMessage atualizacao = await EnviarPutAdmin(client, id, CorpoDeAtualizacaoCom(
            id,
            ("regimeDeFuncionamento", "INTENSIVO"),
            ("regimeDeTurno", "INTEGRAL"),
            ("turnos", new[] { "VESPERTINO", "NOTURNO" })));

        atualizacao.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private static Dictionary<string, object?> CorpoComValoresDesconhecidos(DependenciasDaOferta dependencias) =>
        CorpoOfertaCom(
            dependencias,
            ("programaDeOferta", "PROGRAMA_X"),
            ("regimeDeFuncionamento", "FUNCIONAMENTO_X"),
            ("regimeDeTurno", "REGIME_X"),
            ("turnos", new[] { "TURNO_X" }));

    private static async Task ConferirAcumuloSemEcoAsync(HttpResponseMessage resposta)
    {
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        string corpo = await resposta.Content.ReadAsStringAsync();

        CamposComErro(corpo).Should().BeEquivalentTo("programaDeOferta", "regimeDeFuncionamento", "regimeDeTurno", "turnos");
        corpo.Should().NotContain("PROGRAMA_X").And.NotContain("FUNCIONAMENTO_X").And.NotContain("REGIME_X").And.NotContain("TURNO_X");
    }

    private static async Task ConferirNaLeituraEnaListagemAsync(
        HttpClient client,
        Guid cursoId,
        Guid id,
        (string Codigo, string Rotulo) funcionamento,
        (string Codigo, string Rotulo) regime,
        (string Codigo, string Rotulo)[] turnos)
    {
        using JsonDocument leitura = await LerOfertaAsync(client, id);
        using HttpResponseMessage resposta = await client.GetAsync(new Uri($"/api/configuracao/ofertas-curso?cursoId={cursoId}", UriKind.Relative));
        using JsonDocument lista = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        JsonElement itemDaLista = lista.RootElement.EnumerateArray().Single(o => o.GetProperty("id").GetGuid() == id);

        foreach (JsonElement oferta in new[] { leitura.RootElement, itemDaLista })
        {
            ParDe(oferta.GetProperty("regimeDeFuncionamento")).Should().Be(funcionamento);
            ParDe(oferta.GetProperty("regimeDeTurno")).Should().Be(regime);
            oferta.GetProperty("turnos").EnumerateArray().Select(ParDe).Should().Equal(turnos);
        }
    }

    private static (string Codigo, string Rotulo) ParDe(JsonElement par) =>
        (par.GetProperty("codigo").GetString() ?? string.Empty, par.GetProperty("rotulo").GetString() ?? string.Empty);
}
