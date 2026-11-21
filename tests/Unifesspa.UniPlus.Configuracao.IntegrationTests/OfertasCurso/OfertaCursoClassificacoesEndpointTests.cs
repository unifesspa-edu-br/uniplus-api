namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.OfertasCurso;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.OfertasCurso.ApoioDosEndpointsDeOfertaCurso;

/// <summary>
/// O programa e o formato da oferta nas bordas HTTP: a resposta leva código e rótulo, e o pedido recusa, por comparação
/// exata, o que não for um dos códigos aceitos.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class OfertaCursoClassificacoesEndpointTests
{
    private const string CodigoDoProgramaInvalido = "uniplus.configuracao.oferta_curso.programa_de_oferta_invalido";
    private const string CodigoDoFormatoInvalido = "uniplus.configuracao.oferta_curso.formato_pedagogico_invalido";

    private readonly ConfiguracaoEndpointFixture _fixture;
    private readonly ApoioDosEndpointsDeOfertaCurso _apoio;

    public OfertaCursoClassificacoesEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
        _apoio = new ApoioDosEndpointsDeOfertaCurso(fixture);
    }

    [Fact(DisplayName = "POST e PUT respondem programa e formato como código e rótulo, na leitura por id e na listagem")]
    public async Task CriarEAtualizar_RespondemCodigoERotulo()
    {
        DependenciasDaOferta dependencias = await _apoio.SemearDependenciasAsync("classificacao-ok");
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarOfertaAsync(client, CorpoOfertaCom(dependencias, ("programaDeOferta", "PARFOR"), ("formatoPedagogico", "EAD"), ("baseLegal", "Lei de teste")));

        await ConferirClassificacaoAsync(client, id, ("PARFOR", "Parfor"), ("EAD", "A distância"));
        await ConferirNaListagemAsync(client, dependencias.CursoId, id, "PARFOR", "EAD");
        using HttpResponseMessage atualizacao = await EnviarPutAdmin(client, id, CorpoDeAtualizacaoCom(id, ("programaDeOferta", "FORMA_PARA"), ("formatoPedagogico", "SEMIPRESENCIAL"), ("baseLegal", "Lei de teste")));

        atualizacao.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await ConferirClassificacaoAsync(client, id, ("FORMA_PARA", "Forma Pará"), ("SEMIPRESENCIAL", "Semipresencial"));
    }

    [Theory(DisplayName = "POST com programa ausente, em branco, com espaço, em outra caixa ou desconhecido devolve 422 com o código publicado")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" REGULAR")]
    [InlineData("REGULAR ")]
    [InlineData("regular")]
    [InlineData("PROUNI")]
    public async Task Criar_ProgramaForaDosCodigosAceitos_Devolve422(string? programa)
    {
        DependenciasDaOferta dependencias = await _apoio.SemearDependenciasAsync("programa-422");
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await EnviarPostAdmin(client, CorpoOfertaCom(dependencias, ("programaDeOferta", programa)));

        await ConferirRecusaAsync(resposta, CodigoDoProgramaInvalido, "programaDeOferta");
    }

    [Theory(DisplayName = "POST com formato em branco, com espaço, em outra caixa ou desconhecido devolve 422 com o código publicado")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" EAD")]
    [InlineData("EAD ")]
    [InlineData("ead")]
    [InlineData("HIBRIDO")]
    public async Task Criar_FormatoForaDosCodigosAceitos_Devolve422(string formato)
    {
        DependenciasDaOferta dependencias = await _apoio.SemearDependenciasAsync("formato-422");
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await EnviarPostAdmin(client, CorpoOfertaCom(dependencias, ("formatoPedagogico", formato)));

        await ConferirRecusaAsync(resposta, CodigoDoFormatoInvalido, "formatoPedagogico");
    }

    [Theory(DisplayName = "POST e PUT com formato ausente valem PRESENCIAL")]
    [InlineData(null)]
    public async Task CriarEAtualizar_FormatoAusente_ValePresencial(string? formato)
    {
        DependenciasDaOferta dependencias = await _apoio.SemearDependenciasAsync("formato-padrao");
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarOfertaAsync(client, CorpoOfertaCom(dependencias, ("formatoPedagogico", "EAD")));

        using HttpResponseMessage atualizacao = await EnviarPutAdmin(client, id, CorpoDeAtualizacaoCom(id, ("formatoPedagogico", formato)));

        atualizacao.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await ConferirClassificacaoAsync(client, id, ("REGULAR", "Regular"), ("PRESENCIAL", "Presencial"));
        Guid criada = await CriarOfertaAsync(client, CorpoOfertaCom(dependencias, ("formatoPedagogico", formato)));
        await ConferirClassificacaoAsync(client, criada, ("REGULAR", "Regular"), ("PRESENCIAL", "Presencial"));
    }

    [Theory(DisplayName = "PUT com programa ausente ou desconhecido, ou formato desconhecido, devolve 422 com o código publicado")]
    [InlineData(null, "PRESENCIAL", CodigoDoProgramaInvalido, "programaDeOferta")]
    [InlineData("PROUNI", "PRESENCIAL", CodigoDoProgramaInvalido, "programaDeOferta")]
    [InlineData("REGULAR", " EAD", CodigoDoFormatoInvalido, "formatoPedagogico")]
    [InlineData("REGULAR", "HIBRIDO", CodigoDoFormatoInvalido, "formatoPedagogico")]
    public async Task Atualizar_ClassificacaoForaDosCodigosAceitos_Devolve422(string? programa, string formato, string codigo, string campo)
    {
        DependenciasDaOferta dependencias = await _apoio.SemearDependenciasAsync("classificacao-put-422");
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarOfertaAsync(client, CorpoOferta(dependencias));

        using HttpResponseMessage resposta = await EnviarPutAdmin(
            client, id, CorpoDeAtualizacaoCom(id, ("programaDeOferta", programa), ("formatoPedagogico", formato)));

        await ConferirRecusaAsync(resposta, codigo, campo);
    }

    [Fact(DisplayName = "O erro do programa acumula com o de outro campo, e a mensagem não repete o valor recebido")]
    public async Task Criar_ProgramaInvalidoComOutroErro_AcumulaSemEcoDoValor()
    {
        const string valorRecebido = "PROGRAMA_DESCONHECIDO";
        DependenciasDaOferta dependencias = await _apoio.SemearDependenciasAsync("acumulo-422");
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await EnviarPostAdmin(
            client, CorpoOfertaCom(dependencias, ("programaDeOferta", valorRecebido), ("regimeDeTurno", "PARCIAL")));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        string corpo = await resposta.Content.ReadAsStringAsync();
        CamposComErro(corpo).Should().BeEquivalentTo("programaDeOferta", "regimeDeTurno");
        corpo.Should().NotContain(valorRecebido).And.NotContain("PARCIAL");
    }

    private static async Task ConferirClassificacaoAsync(
        HttpClient client, Guid id, (string Codigo, string Rotulo) programa, (string Codigo, string Rotulo) formato)
    {
        using JsonDocument oferta = await LerOfertaAsync(client, id);

        ConferirPar(oferta.RootElement.GetProperty("programaDeOferta"), programa);
        ConferirPar(oferta.RootElement.GetProperty("formatoPedagogico"), formato);
    }

    private static void ConferirPar(JsonElement par, (string Codigo, string Rotulo) esperado)
    {
        par.GetProperty("codigo").GetString().Should().Be(esperado.Codigo);
        par.GetProperty("rotulo").GetString().Should().Be(esperado.Rotulo);
    }

    private static async Task ConferirNaListagemAsync(HttpClient client, Guid cursoId, Guid id, string programa, string formato)
    {
        using HttpResponseMessage resposta = await client.GetAsync(new Uri($"/api/configuracao/ofertas-curso?cursoId={cursoId}", UriKind.Relative));
        using JsonDocument lista = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());

        JsonElement item = lista.RootElement.EnumerateArray().Single(o => o.GetProperty("id").GetGuid() == id);
        item.GetProperty("programaDeOferta").GetProperty("codigo").GetString().Should().Be(programa);
        item.GetProperty("formatoPedagogico").GetProperty("codigo").GetString().Should().Be(formato);
    }
}
