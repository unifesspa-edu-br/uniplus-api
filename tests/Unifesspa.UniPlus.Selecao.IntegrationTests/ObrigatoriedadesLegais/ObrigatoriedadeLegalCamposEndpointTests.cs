namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ObrigatoriedadesLegais;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Outbox.Cascading;

/// <summary>
/// A validação dos campos da regra legal é do domínio: cada violação sai com o código de domínio e o campo em
/// <c>errors[]</c>, todas juntas, e um campo ausente é violação 422, não erro de leitura do corpo.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class ObrigatoriedadeLegalCamposEndpointTests : ObrigatoriedadeLegalAdminTestBase
{
    private const string Url = "/api/selecao/admin/obrigatoriedades-legais";

    public ObrigatoriedadeLegalCamposEndpointTests(CascadingFixture fixture)
        : base(fixture)
    {
    }

    public static TheoryData<string, object?, string, string> CamposInvalidos() => new()
    {
        { "regraCodigo", null, "regra_codigo_obrigatorio", "regraCodigo" },
        { "regraCodigo", new string('a', 129), "regra_codigo_invalido", "regraCodigo" },
        { "baseLegal", null, "base_legal_obrigatoria", "baseLegal" },
        { "baseLegal", new string('a', 501), "base_legal_invalida", "baseLegal" },
        { "descricaoHumana", null, "descricao_humana_obrigatoria", "descricaoHumana" },
        { "descricaoHumana", new string('a', 1001), "descricao_humana_invalida", "descricaoHumana" },
        { "tipoProcessoCodigo", null, "tipo_processo_codigo_obrigatorio", "tipoProcessoCodigo" },
        { "tipoProcessoCodigo", new string('a', 65), "tipo_processo_codigo_invalido", "tipoProcessoCodigo" },
        { "categoria", "nenhuma", "categoria_invalida", "categoria" },
        { "atoNormativoUrl", new string('a', 1001), "ato_normativo_url_invalido", "atoNormativoUrl" },
        { "portariaInternaCodigo", new string('a', 129), "portaria_interna_codigo_invalido", "portariaInternaCodigo" },
        { "vigenciaInicio", null, "vigencia_inicio_obrigatorio", "vigenciaInicio" },
        { "vigenciaFim", "2026-01-01", "vigencia_invalida", "vigenciaFim" },
        { "predicado", null, "predicado_obrigatorio", "predicado" },
    };

    [Theory(DisplayName = "POST com campo inválido devolve 422 com o código de domínio e o campo em errors")]
    [MemberData(nameof(CamposInvalidos))]
    public async Task Criar_CampoInvalido_Retorna422ComCodigoDeDominio(string campo, object? valor, string codigo, string campoDoErro)
    {
        using HttpClient client = await ClienteComConcessaoAsync();

        HttpResponseMessage response = await PostAsync(client, Url, PayloadValido(campo, valor));

        string corpo = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, corpo);
        using JsonDocument doc = JsonDocument.Parse(corpo);
        doc.RootElement.GetProperty("type").GetString().Should().EndWith($"uniplus.selecao.obrigatoriedade_legal.{codigo}");
        doc.RootElement.GetProperty("errors").EnumerateArray().Select(static e => e.GetProperty("field").GetString())
            .Should().Contain(campoDoErro);
    }

    [Fact(DisplayName = "POST com vários campos inválidos devolve todas as violações em errors")]
    public async Task Criar_VariosCamposInvalidos_AcumulaEmErrors()
    {
        using HttpClient client = await ClienteComConcessaoAsync();
        Dictionary<string, object?> payload = PayloadValido("regraCodigo", null);
        payload["descricaoHumana"] = null;
        payload["tipoProcessoCodigo"] = "";

        HttpResponseMessage response = await PostAsync(client, Url, payload);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("errors").EnumerateArray().Select(static e => e.GetProperty("field").GetString())
            .Should().BeEquivalentTo("tipoProcessoCodigo", "regraCodigo", "descricaoHumana");
    }

    [Fact(DisplayName = "PUT com Id vazio e campos válidos devolve 404 de regra não encontrada")]
    public async Task Atualizar_IdVazio_Retorna404()
    {
        using HttpClient client = await ClienteComConcessaoAsync();
        Dictionary<string, object?> payload = PayloadValido("id", Guid.Empty);

        HttpResponseMessage response = await PutAsync(client, $"{Url}/{Guid.Empty}", payload);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound, await response.Content.ReadAsStringAsync());
    }

    private static Dictionary<string, object?> PayloadValido(string campo, object? valor)
    {
        Dictionary<string, object?> payload = new(StringComparer.Ordinal)
        {
            ["id"] = Guid.Empty,
            ["tipoProcessoCodigo"] = "*",
            ["categoria"] = "outros",
            ["regraCodigo"] = UniqueRegraCodigo(),
            ["predicado"] = PredicadoConcorrenciaDupla,
            ["descricaoHumana"] = "Descrição da regra",
            ["baseLegal"] = "Lei 12.711/2012",
            ["vigenciaInicio"] = "2026-06-01",
            ["vigenciaFim"] = null,
            ["atoNormativoUrl"] = null,
            ["portariaInternaCodigo"] = null,
        };
        payload[campo] = valor;
        return payload;
    }
}
