namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.AtosNormativos;

using System.Net;

using AwesomeAssertions;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Assertions;
using Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static AtoNormativoApoio;
using static AtosDaEntidadeApoio;

/// <summary>
/// O registro do ato por HTTP recusa, no envelope da ADR-0023, o texto obrigatório que o corpo omite ou traz
/// com <c>null</c>, no campo do ato e no de um vínculo da lista. As duas formas de ausência dão a mesma
/// resposta, e um corpo completo continua sendo registrado.
/// </summary>
[Collection(PublicacoesEndpointCollection.Name)]
public sealed class CampoObrigatorioNoRegistroDoAtoEndpointTests
{
    private readonly PublicacoesEndpointFixture _fixture;
    private readonly AtoNormativoApoio _apoio;

    public CampoObrigatorioNoRegistroDoAtoEndpointTests(PublicacoesEndpointFixture fixture)
    {
        _fixture = fixture;
        _apoio = new AtoNormativoApoio(fixture);
    }

    [Fact(DisplayName = "Assinante omitido no ato é recusado com 400 no envelope e o campo nomeado")]
    public async Task Registrar_AssinanteOmitido_Retorna400NoEnvelope()
    {
        string tipo = await _apoio.CriarTipoVigenteAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await PostAtoAsync(client, PayloadSemAssinante(tipo));

        await RecusaDeCampoObrigatorioAusente.ConferirAsync(resposta, "assinante");
    }

    [Fact(DisplayName = "Assinante null no ato dá a mesma resposta do omitido")]
    public async Task Registrar_AssinanteNull_DaAMesmaRespostaDoOmitido()
    {
        string tipo = await _apoio.CriarTipoVigenteAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await PostAtoAsync(client, PayloadComVinculos(tipo, assinante: null));

        await RecusaDeCampoObrigatorioAusente.ConferirAsync(resposta, "assinante");
    }

    [Fact(DisplayName = "Tipo da entidade omitido em vínculo da lista é recusado com 400 e o ponteiro com índice")]
    public async Task Registrar_EntidadeTipoOmitidaNoVinculo_Retorna400ComPonteiroDoItem()
    {
        string tipo = await _apoio.CriarTipoVigenteAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await PostAtoAsync(
            client, PayloadComVinculos(tipo, vinculos: [new { entidadeId = Guid.CreateVersion7() }]));

        await RecusaDeCampoObrigatorioAusente.ConferirAsync(resposta, "vinculos[0].entidadeTipo");
    }

    [Fact(DisplayName = "Tipo da entidade null em vínculo da lista dá a mesma resposta do omitido")]
    public async Task Registrar_EntidadeTipoNullNoVinculo_DaAMesmaRespostaDoOmitido()
    {
        string tipo = await _apoio.CriarTipoVigenteAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await PostAtoAsync(
            client, PayloadComVinculos(tipo, vinculos: [new { entidadeTipo = (string?)null, entidadeId = Guid.CreateVersion7() }]));

        await RecusaDeCampoObrigatorioAusente.ConferirAsync(resposta, "vinculos[0].entidadeTipo");
    }

    [Fact(DisplayName = "Corpo completo continua sendo registrado com 201")]
    public async Task Registrar_CorpoCompleto_Retorna201()
    {
        string tipo = await _apoio.CriarTipoVigenteAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await PostAtoAsync(
            client, PayloadComVinculos(tipo, vinculos: [Vinculo(Guid.CreateVersion7())]));

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    private static object PayloadSemAssinante(string tipoCodigo) =>
        new
        {
            orgao = "CEPS",
            serie = SerieUnica(),
            ano = 2026,
            numero = "13",
            tipoCodigo,
            dataDoAto = Publicacao,
            documentoHash = HashValido,
        };

    private static object PayloadComVinculos(
        string tipoCodigo, string? assinante = PersonasDeTeste.NomeKevinCarvalho, object[]? vinculos = null) =>
        new
        {
            orgao = "CEPS",
            serie = SerieUnica(),
            ano = 2026,
            numero = "13",
            tipoCodigo,
            dataDoAto = Publicacao,
            documentoHash = HashValido,
            assinante,
            vinculos = vinculos ?? [],
        };
}
