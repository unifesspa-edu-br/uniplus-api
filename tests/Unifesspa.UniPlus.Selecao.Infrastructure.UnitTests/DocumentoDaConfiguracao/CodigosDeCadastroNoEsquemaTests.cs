namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.DocumentoDaConfiguracao;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;

public sealed class CodigosDeCadastroNoEsquemaTests
{
    private static readonly EsquemaDoDocumentoDaConfiguracao Esquema = new();

    public static IEnumerable<object[]> Campos => Campos1().Concat(Campos2()).Concat(Campos3()).Concat(Campos4());

    private static IEnumerable<object[]> Campos1()
    {
        yield return ["\"atendimento\": { \"condicoes\": [{ \"condicaoCodigo\": \"VALOR\" }] }", "/atendimento/condicoes/0/condicaoCodigo"];
        yield return ["\"atendimento\": { \"recursos\": [{ \"recursoCodigo\": \"VALOR\" }] }", "/atendimento/recursos/0/recursoCodigo"];
        yield return ["\"atendimento\": { \"tiposDeficiencia\": [{ \"tipoDeficienciaCodigo\": \"VALOR\" }] }", "/atendimento/tiposDeficiencia/0/tipoDeficienciaCodigo"];
        yield return ["\"etapas\": [{ \"chave\": \"etapa\", \"bancas\": [{ \"chave\": \"banca\", \"tipoBancaCodigo\": \"BANCA_ANALISE\", \"categoriasDocumentoCodigos\": [\"VALOR\"] }] }]", "/etapas/0/bancas/0/categoriasDocumentoCodigos/0"];
    }

    private static IEnumerable<object[]> Campos2()
    {
        yield return ["\"distribuicaoVagas\": [{ \"ofertaCursoCodigo\": \"VALOR\" }]", "/distribuicaoVagas/0/ofertaCursoCodigo"];
        yield return ["\"distribuicaoVagas\": [{ \"ofertaCursoCodigo\": \"OFERTA\", \"modalidades\": [{ \"codigo\": \"VALOR\" }] }]", "/distribuicaoVagas/0/modalidades/0/codigo"];
        yield return ["\"distribuicaoVagas\": [{ \"ofertaCursoCodigo\": \"OFERTA\", \"quadro\": [{ \"modalidadeCodigo\": \"VALOR\" }] }]", "/distribuicaoVagas/0/quadro/0/modalidadeCodigo"];
        yield return ["\"distribuicaoVagas\": [{ \"ofertaCursoCodigo\": \"OFERTA\", \"ajusteDoQuadro\": { \"$tipo\": \"reduzirDe\", \"modalidadeCodigo\": \"VALOR\" } }]", "/distribuicaoVagas/0/ajusteDoQuadro/modalidadeCodigo"];
    }

    private static IEnumerable<object[]> Campos3()
    {
        yield return ["\"distribuicaoVagas\": [{ \"ofertaCursoCodigo\": \"OFERTA\", \"ajusteDoQuadro\": { \"$tipo\": \"reduzirProporcionalEm\", \"modalidadeCodigos\": [\"VALOR\"] } }]", "/distribuicaoVagas/0/ajusteDoQuadro/modalidadeCodigos/0"];
        yield return ["\"fada\": { \"aplica\": true, \"regra\": {}, \"fator\": 1, \"baseLegalCodigo\": \"VALOR\" }", "/fada/baseLegalCodigo"];
        yield return ["\"fada\": { \"aplica\": true, \"regra\": {}, \"fator\": 1, \"baseLegalCodigo\": \"LEI\", \"codigoDoDadoDeElegibilidade\": \"VALOR\" }", "/fada/codigoDoDadoDeElegibilidade"];
        yield return ["\"formularios\": [{ \"codigo\": \"INSCRICAO\", \"termos\": [{ \"codigo\": \"termo\", \"termoCodigo\": \"VALOR\", \"versaoNumero\": 1 }] }]", "/formularios/0/termos/0/termoCodigo"];
    }

    private static IEnumerable<object[]> Campos4()
    {
        yield return ["\"motivosDeDecisao\": [{ \"faseChave\": \"fase\", \"tipoDeDecisaoCodigo\": \"HOMOLOGACAO\", \"codigo\": \"VALOR\" }]", "/motivosDeDecisao/0/codigo"];
        yield return ["\"ordemDeRemanejamento\": { \"destinoFinalCodigo\": \"VALOR\" }", "/ordemDeRemanejamento/destinoFinalCodigo"];
        yield return ["\"ordemDeRemanejamento\": { \"destinos\": [{ \"modalidadeOrigemCodigo\": \"VALOR\", \"modalidadeDestinoCodigo\": \"DESTINO\" }] }", "/ordemDeRemanejamento/destinos/0/modalidadeOrigemCodigo"];
        yield return ["\"ordemDeRemanejamento\": { \"destinos\": [{ \"modalidadeOrigemCodigo\": \"ORIGEM\", \"modalidadeDestinoCodigo\": \"VALOR\" }] }", "/ordemDeRemanejamento/destinos/0/modalidadeDestinoCodigo"];
    }

    [Theory]
    [MemberData(nameof(Campos))]
    public void Avaliar_CodigoDeCadastroValido_Aceita(string trecho, string campo)
    {
        ArgumentNullException.ThrowIfNull(trecho);
        Avaliar(trecho, "LEI_12711").Should().NotContain(causa => causa.Field == campo);
        Avaliar(trecho, "A").Should().NotContain(causa => causa.Field == campo);
    }

    [Theory]
    [MemberData(nameof(Campos))]
    public void Avaliar_CodigoDeCadastroInvalido_RecusaNoCampo(string trecho, string campo)
    {
        ArgumentNullException.ThrowIfNull(trecho);
        foreach (string valor in new[] { "A__B", "minuscula" })
        {
            Avaliar(trecho, valor).Should().ContainSingle(causa => causa.Field == campo
                && causa.Error.Code == DocumentoDaConfiguracaoErrorCodes.ValorInvalido);
        }
    }

    private static IReadOnlyList<FieldError> Avaliar(string trecho, string valor)
    {
        string parte = trecho.Replace("VALOR", valor, StringComparison.Ordinal);
        JsonObject documento = JsonNode.Parse($$"""{ "formato": "uniplus.selecao.configuracao", "versaoFormato": 1, {{parte}} }""")!.AsObject();
        return Esquema.Avaliar(documento);
    }
}
