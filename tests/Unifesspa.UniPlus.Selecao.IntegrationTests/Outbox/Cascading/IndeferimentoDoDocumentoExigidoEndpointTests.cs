namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Text.Json.Nodes;

using AwesomeAssertions;

/// <summary>
/// As duas escolhas do indeferimento do documento exigido (se o documento é obrigatório e se o indeferimento abre prazo para
/// reenvio) são declaradas pelo pedido: a falta recusa com 422 no campo, sem gravar nada, e falso declarado vale.
/// </summary>
[Collection(CascadingCollection.Name)]
public sealed class IndeferimentoDoDocumentoExigidoEndpointTests(CascadingFixture ambiente)
{
    [Theory(DisplayName = "A escolha omitida do indeferimento é recusada no campo, sem gravar nada")]
    [InlineData("obrigatorio")]
    [InlineData("abrePrazoParaReenvio")]
    public async Task EscolhaOmitida_Recusa422NoCampo(string campo)
    {
        CenarioDoDocumentoExigido cenario = await CenarioDoDocumentoExigido.SemearAsync(ambiente);
        JsonObject regra = cenario.Regra();
        regra["documento"]!["indeferimento"]!.AsObject().Remove(campo).Should().BeTrue();

        await cenario.ExigirRecusaSemGravarAsync(regra, (CenarioDoDocumentoExigido.CampoDoIndeferimento(campo), CenarioDoDocumentoExigido.Obrigatorio));
    }

    [Theory(DisplayName = "A escolha nula do indeferimento é recusada como a omitida")]
    [InlineData("obrigatorio")]
    [InlineData("abrePrazoParaReenvio")]
    public async Task EscolhaNula_Recusa422NoCampo(string campo)
    {
        CenarioDoDocumentoExigido cenario = await CenarioDoDocumentoExigido.SemearAsync(ambiente);
        JsonObject regra = cenario.Regra();
        regra["documento"]!["indeferimento"]![campo] = null;

        await cenario.ExigirRecusaSemGravarAsync(regra, (CenarioDoDocumentoExigido.CampoDoIndeferimento(campo), CenarioDoDocumentoExigido.Obrigatorio));
    }

    // O prazo para reenvio verdadeiro exige fase que permita complementação (regra anterior, do cronograma); o cenário usa fase que não permite,
    // então a aceitação do verdadeiro fica fora daqui e o que se prova é que falso declarado vale.
    [Theory(DisplayName = "A escolha declarada falsa é aceita e lida de volta, com a obrigatoriedade como foi declarada")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EscolhaFalsa_Aceita(bool obrigatorio)
    {
        CenarioDoDocumentoExigido cenario = await CenarioDoDocumentoExigido.SemearAsync(ambiente);
        JsonObject regra = cenario.Regra();
        JsonNode indeferimento = regra["documento"]!["indeferimento"]!;
        indeferimento["obrigatorio"] = obrigatorio;
        indeferimento["abrePrazoParaReenvio"] = false;

        await cenario.ExigirAceiteAsync(regra);

        JsonNode lido = (await cenario.LerDocumentosExigidosAsync()).Single()!["indeferimento"]!;
        lido["obrigatorio"]!.GetValue<bool>().Should().Be(obrigatorio);
        lido["abrePrazoParaReenvio"]!.GetValue<bool>().Should().BeFalse();
    }

    [Fact(DisplayName = "O indeferimento ausente recusa as duas escolhas, cada uma no seu campo")]
    public async Task IndeferimentoAusente_RecusaAsDuasEscolhas()
    {
        CenarioDoDocumentoExigido cenario = await CenarioDoDocumentoExigido.SemearAsync(ambiente);
        JsonObject regra = cenario.Regra();
        regra["documento"]!.AsObject().Remove("indeferimento").Should().BeTrue();

        await cenario.ExigirRecusaSemGravarAsync(
            regra,
            (CenarioDoDocumentoExigido.CampoDoIndeferimento("obrigatorio"), CenarioDoDocumentoExigido.Obrigatorio),
            (CenarioDoDocumentoExigido.CampoDoIndeferimento("abrePrazoParaReenvio"), CenarioDoDocumentoExigido.Obrigatorio));
    }

    [Fact(DisplayName = "A regra de documento sem bloco de indeferimento de grupo é aceita")]
    public async Task RegraDeDocumentoSemBlocoDeGrupo_Aceita()
    {
        CenarioDoDocumentoExigido cenario = await CenarioDoDocumentoExigido.SemearAsync(ambiente);
        JsonObject regra = cenario.Regra();
        regra.ContainsKey("indeferimento").Should().BeFalse();

        await cenario.ExigirAceiteAsync(regra);

        (await cenario.LerDocumentosExigidosAsync()).Should().ContainSingle();
    }
}
