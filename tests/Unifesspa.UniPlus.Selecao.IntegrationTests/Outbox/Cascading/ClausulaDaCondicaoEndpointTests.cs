namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.ApoioDoCronogramaDeFasesPeloEndpoint;

/// <summary>
/// A cláusula da condição do documento exigido é declarada pelo pedido: a falta recusa com 422 no campo, sem gravar nada;
/// zero declarado vale; a cláusula negativa é recusada pelo domínio.
/// </summary>
[Collection(CascadingCollection.Name)]
public sealed class ClausulaDaCondicaoEndpointTests(CascadingFixture ambiente)
{
    [Fact(DisplayName = "A cláusula omitida é recusada no campo, sem gravar nada")]
    public async Task ClausulaOmitida_Recusa422NoCampo()
    {
        CenarioDoDocumentoExigido cenario = await CenarioDoDocumentoExigido.SemearAsync(ambiente);
        JsonObject regra = cenario.Regra();
        Condicao(regra).Remove("clausula").Should().BeTrue();

        await cenario.ExigirRecusaSemGravarAsync(regra, (CenarioDoDocumentoExigido.CampoDaCondicao("clausula"), CenarioDoDocumentoExigido.Obrigatorio));
    }

    [Fact(DisplayName = "A cláusula nula é recusada como a omitida")]
    public async Task ClausulaNula_Recusa422NoCampo()
    {
        CenarioDoDocumentoExigido cenario = await CenarioDoDocumentoExigido.SemearAsync(ambiente);
        JsonObject regra = cenario.Regra();
        Condicao(regra)["clausula"] = null;

        await cenario.ExigirRecusaSemGravarAsync(regra, (CenarioDoDocumentoExigido.CampoDaCondicao("clausula"), CenarioDoDocumentoExigido.Obrigatorio));
    }

    [Fact(DisplayName = "A cláusula zero declarada é aceita e lida de volta")]
    public async Task ClausulaZero_AceitaELidaDeVolta()
    {
        CenarioDoDocumentoExigido cenario = await CenarioDoDocumentoExigido.SemearAsync(ambiente);
        JsonObject regra = cenario.Regra();
        Condicao(regra)["clausula"] = 0;

        await cenario.ExigirAceiteAsync(regra);

        JsonNode condicao = (await cenario.LerDocumentosExigidosAsync()).Single()!["aplicabilidade"]!["condicoes"]![0]!;
        condicao["clausula"]!.GetValue<int>().Should().Be(0);
    }

    [Fact(DisplayName = "A cláusula negativa é recusada com o código do domínio, e não com o de campo obrigatório")]
    public async Task ClausulaNegativa_RecusaComOCodigoDoDominio()
    {
        CenarioDoDocumentoExigido cenario = await CenarioDoDocumentoExigido.SemearAsync(ambiente);
        JsonObject regra = cenario.Regra();
        Condicao(regra)["clausula"] = -1;

        using HttpResponseMessage resposta = await cenario.EnviarAsync(regra);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await resposta.Content.ReadAsStringAsync());
        ErroDoProblema[] erros = await LerErrosDoProblemaAsync(resposta);
        erros.Should().ContainSingle().Which.Codigo.Should().Be("uniplus.selecao.condicao_gatilho.clausula_invalida");
        (await cenario.LerDocumentosExigidosAsync()).Should().BeEmpty();
    }

    private static JsonObject Condicao(JsonObject regra) =>
        CenarioDoDocumentoExigido.DocumentoDe(regra)["aplicabilidade"]!["condicoes"]![0]!.AsObject();
}
