namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Text.Json.Nodes;

using AwesomeAssertions;

/// <summary>
/// O grupo de comprovação declara se o indeferimento abre prazo para reenvio dos documentos: a falta recusa com 422 no campo,
/// sem gravar nada; falso declarado vale. A regra de documento não tem esse bloco e segue aceita sem ele.
/// </summary>
[Collection(CascadingCollection.Name)]
public sealed class ReenvioDoGrupoEndpointTests(CascadingFixture ambiente)
{
    [Fact(DisplayName = "O grupo sem o bloco de indeferimento é recusado no campo do reenvio, sem gravar nada")]
    public async Task GrupoSemIndeferimento_Recusa422NoCampo()
    {
        CenarioDoDocumentoExigido cenario = await CenarioDoDocumentoExigido.SemearAsync(ambiente);

        await cenario.ExigirRecusaSemGravarAsync(
            cenario.Grupo(indeferimento: null), (CenarioDoDocumentoExigido.CampoDoReenvioDoGrupo, CenarioDoDocumentoExigido.Obrigatorio));
        (await cenario.LerRegrasAsync()).Should().BeEmpty();
    }

    [Fact(DisplayName = "O grupo com o indeferimento sem a escolha do reenvio é recusado como o sem bloco")]
    public async Task GrupoSemEscolhaDeReenvio_Recusa422NoCampo()
    {
        CenarioDoDocumentoExigido cenario = await CenarioDoDocumentoExigido.SemearAsync(ambiente);

        await cenario.ExigirRecusaSemGravarAsync(
            cenario.Grupo(new JsonObject()), (CenarioDoDocumentoExigido.CampoDoReenvioDoGrupo, CenarioDoDocumentoExigido.Obrigatorio));
    }

    [Fact(DisplayName = "O grupo com a escolha do reenvio nula é recusado como a omitida")]
    public async Task GrupoComEscolhaNula_Recusa422NoCampo()
    {
        CenarioDoDocumentoExigido cenario = await CenarioDoDocumentoExigido.SemearAsync(ambiente);

        await cenario.ExigirRecusaSemGravarAsync(
            cenario.Grupo(new JsonObject { ["abrePrazoParaReenvio"] = null }), (CenarioDoDocumentoExigido.CampoDoReenvioDoGrupo, CenarioDoDocumentoExigido.Obrigatorio));
    }

    // O prazo para reenvio verdadeiro exige fase que permita complementação (regra anterior, do cronograma); o cenário usa fase que não permite.
    [Fact(DisplayName = "O grupo com a escolha do reenvio falsa é aceito e a escolha é lida de volta")]
    public async Task GrupoComEscolhaFalsa_AceitoELidoDeVolta()
    {
        CenarioDoDocumentoExigido cenario = await CenarioDoDocumentoExigido.SemearAsync(ambiente);

        await cenario.ExigirAceiteAsync(cenario.Grupo(new JsonObject { ["abrePrazoParaReenvio"] = false }));

        JsonNode grupo = (await cenario.LerRegrasAsync()).Single()!;
        grupo["indeferimento"]!["abrePrazoParaReenvio"]!.GetValue<bool>().Should().BeFalse();
    }
}
