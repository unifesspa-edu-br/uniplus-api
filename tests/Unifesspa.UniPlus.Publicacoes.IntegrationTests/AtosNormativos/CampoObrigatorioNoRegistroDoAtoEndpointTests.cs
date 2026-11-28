namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.AtosNormativos;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static AtoNormativoApoio;

/// <summary>
/// O registro do ato por HTTP entrega ao domínio o texto obrigatório que o corpo omite ou traz com <c>null</c>,
/// no campo do ato, no de um vínculo e no de um anexo da lista: a recusa é a do domínio, 422 com o código da regra e
/// o campo apontado, e as duas formas de ausência dão a mesma resposta. A data do ato, que antes virava o ano 1 em
/// silêncio, é recusada com o código genérico de campo obrigatório.
/// </summary>
[Collection(PublicacoesEndpointCollection.Name)]
public sealed class CampoObrigatorioNoRegistroDoAtoEndpointTests
{
    private const string Prefixo = "uniplus.publicacoes.ato_normativo.";
    private const string PrefixoDoAnexo = "uniplus.publicacoes.anexo_do_ato.";

    private readonly PublicacoesEndpointFixture _fixture;
    private readonly AtoNormativoApoio _apoio;

    public CampoObrigatorioNoRegistroDoAtoEndpointTests(PublicacoesEndpointFixture fixture)
    {
        _fixture = fixture;
        _apoio = new AtoNormativoApoio(fixture);
    }

    public static TheoryData<string, string, bool> CamposDoAto => new()
    {
        { "orgao", Prefixo + "texto_invalido", true },
        { "orgao", Prefixo + "texto_invalido", false },
        { "serie", Prefixo + "texto_invalido", true },
        { "serie", Prefixo + "texto_invalido", false },
        { "tipoCodigo", "uniplus.publicacoes.tipo_ato.codigo_obrigatorio", true },
        { "tipoCodigo", "uniplus.publicacoes.tipo_ato.codigo_obrigatorio", false },
        { "dataDoAto", "uniplus.validacao.obrigatorio", true },
        { "dataDoAto", "uniplus.validacao.obrigatorio", false },
        { "documentoHash", Prefixo + "hash_do_documento_invalido", true },
        { "documentoHash", Prefixo + "hash_do_documento_invalido", false },
        { "assinante", Prefixo + "texto_invalido", true },
        { "assinante", Prefixo + "texto_invalido", false },
    };

    public static TheoryData<string, string, bool> CamposDoVinculoEDoAnexo => new()
    {
        { "vinculos[0].entidadeTipo", Prefixo + "vinculo_tipo_de_entidade_invalido", true },
        { "vinculos[0].entidadeTipo", Prefixo + "vinculo_tipo_de_entidade_invalido", false },
        { "anexos[0].nome", PrefixoDoAnexo + "nome_invalido", true },
        { "anexos[0].nome", PrefixoDoAnexo + "nome_invalido", false },
        { "anexos[0].tipoMidia", PrefixoDoAnexo + "tipo_midia_invalido", true },
        { "anexos[0].tipoMidia", PrefixoDoAnexo + "tipo_midia_invalido", false },
        { "anexos[0].hash", PrefixoDoAnexo + "hash_invalido", true },
        { "anexos[0].hash", PrefixoDoAnexo + "hash_invalido", false },
    };

    [Theory(DisplayName = "Campo do ato omitido ou null chega ao domínio e é recusado com 422 e o código da regra")]
    [MemberData(nameof(CamposDoAto))]
    public async Task Registrar_CampoDoAtoAusente_RecusaPeloDominio(string campo, string codigo, bool omitido)
    {
        ArgumentNullException.ThrowIfNull(campo);
        Dictionary<string, object?> corpo = await CorpoValidoAsync();
        Ausentar(corpo, campo, omitido);

        await ExigirUnicaRecusaAsync(corpo, campo, codigo);
    }

    [Theory(DisplayName = "Campo de vínculo ou de anexo omitido ou null chega ao domínio e é recusado com 422 no item")]
    [MemberData(nameof(CamposDoVinculoEDoAnexo))]
    public async Task Registrar_CampoAninhadoAusente_RecusaPeloDominio(string caminho, string codigo, bool omitido)
    {
        ArgumentNullException.ThrowIfNull(caminho);
        Dictionary<string, object?> corpo = await CorpoValidoAsync();
        corpo["vinculos"] = new[] { new Dictionary<string, object?> { ["entidadeTipo"] = "OBJETO_A", ["entidadeId"] = Guid.CreateVersion7() } };
        corpo["anexos"] = new[] { new Dictionary<string, object?> { ["nome"] = "Anexo I.pdf", ["tipoMidia"] = "application/pdf", ["hash"] = HashValido } };
        Ausentar(corpo, caminho, omitido);

        await ExigirUnicaRecusaAsync(corpo, caminho, codigo);
    }

    [Fact(DisplayName = "Corpo completo continua sendo registrado com 201")]
    public async Task Registrar_CorpoCompleto_Retorna201()
    {
        Dictionary<string, object?> corpo = await CorpoValidoAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await PostAtoAsync(client, corpo);

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    private static void Ausentar(Dictionary<string, object?> corpo, string caminho, bool omitido)
    {
        ArgumentNullException.ThrowIfNull(caminho);
        Dictionary<string, object?> dono = caminho.Contains('.', StringComparison.Ordinal)
            ? ((Dictionary<string, object?>[])corpo[caminho[..caminho.IndexOf('[', StringComparison.Ordinal)]]!)[0]
            : corpo;
        string campo = caminho[(caminho.LastIndexOf('.') + 1)..];
        if (omitido)
        {
            dono.Remove(campo);
        }
        else
        {
            dono[campo] = null;
        }
    }

    private async Task ExigirUnicaRecusaAsync(Dictionary<string, object?> corpo, string campo, string codigo)
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await PostAtoAsync(client, corpo);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("errors").EnumerateArray()
            .Select(erro => (erro.GetProperty("field").GetString(), erro.GetProperty("code").GetString()))
            .Should().Equal((campo, codigo));
    }

    private async Task<Dictionary<string, object?>> CorpoValidoAsync() =>
        new()
        {
            ["orgao"] = "CEPS",
            ["serie"] = SerieUnica(),
            ["ano"] = 2026,
            ["numero"] = "13",
            ["tipoCodigo"] = await _apoio.CriarTipoVigenteAsync(),
            ["dataDoAto"] = Publicacao,
            ["documentoHash"] = HashValido,
            ["assinante"] = PersonasDeTeste.NomeKevinCarvalho,
        };
}
