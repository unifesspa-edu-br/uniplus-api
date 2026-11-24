namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.DocumentoDaConfiguracao;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;

/// <summary>
/// A recusa dos parâmetros de uma regra de eliminação ou de desempate, pelo caminho que o documento percorre de verdade: cada
/// recusa tem um ponteiro e um código só, os do parâmetro e não os da regra inteira.
/// </summary>
public sealed class ParametrosDasRegrasNoEsquemaTests
{
    private const string Eliminacao = "regrasEliminacao";
    private const string Desempate = "criteriosDeDesempate";

    private static readonly EsquemaDoDocumentoDaConfiguracao Esquema = new();

    private static (string? Ponteiro, string Codigo)[] Recusas(string lista, string regra, string parametros)
    {
        JsonObject documento = JsonNode.Parse($$"""
            { "formato": "uniplus.selecao.configuracao", "versaoFormato": 1,
              "classificacao": { "{{lista}}": [ { "regra": { "codigo": "{{regra}}", "versao": "v1" }, "parametros": {{parametros}} } ] } }
            """)!.AsObject();

        return [.. Esquema.Avaliar(documento).Select(static causa => (causa.Field, causa.Error.Code))];
    }

    [Theory(DisplayName = "O parâmetro que a regra não declara é recusado como campo desconhecido, no próprio campo")]
    [InlineData(Eliminacao, "ELIMINACAO_CORTE_EM_AREA", """{ "areaCodigo": "MAT", "minimo": 5, "invento": 1 }""", "/parametros/invento")]
    [InlineData(Desempate, "DESEMPATE_IDOSO", """{ "idadeMinima": 60, "invento": 1 }""", "/parametros/invento")]
    public void ParametroDesconhecido_ApontaOProprioCampo(string lista, string regra, string parametros, string campo) =>
        Recusas(lista, regra, parametros).Should().Equal(($"/classificacao/{lista}/0{campo}", DocumentoDaConfiguracaoErrorCodes.CampoDesconhecido));

    [Theory(DisplayName = "O parâmetro de outra regra é recusado como campo desconhecido, só no campo alheio")]
    [InlineData(Eliminacao, "ELIMINACAO_CORTE_EM_AREA", """{ "areaCodigo": "MAT", "minimo": 5, "etapaChave": "prova" }""", "/parametros/etapaChave")]
    [InlineData(Desempate, "DESEMPATE_IDOSO", """{ "idadeMinima": 60, "etapaChave": "prova" }""", "/parametros/etapaChave")]
    public void ParametroDeOutraRegra_ApontaOCampoAlheio(string lista, string regra, string parametros, string campo) =>
        Recusas(lista, regra, parametros).Should().Equal(($"/classificacao/{lista}/0{campo}", DocumentoDaConfiguracaoErrorCodes.CampoDesconhecido));

    [Theory(DisplayName = "O parâmetro obrigatório ausente é recusado no ponteiro do parâmetro, e só ele")]
    [InlineData(Eliminacao, "ELIMINACAO_CORTE_EM_AREA", """{ "areaCodigo": "MAT" }""", "/parametros/minimo")]
    [InlineData(Desempate, "DESEMPATE_IDOSO", "{}", "/parametros/idadeMinima")]
    public void ParametroAusente_ApontaOParametroAusente(string lista, string regra, string parametros, string campo) =>
        Recusas(lista, regra, parametros).Should().Equal(($"/classificacao/{lista}/0{campo}", DocumentoDaConfiguracaoErrorCodes.CampoObrigatorioAusente));

    [Theory(DisplayName = "O valor do parâmetro fora do limite é recusado como valor inválido, e só ele")]
    [InlineData(Eliminacao, "ELIMINACAO_CORTE_EM_AREA", """{ "areaCodigo": "MAT", "minimo": -1 }""", "/parametros/minimo")]
    [InlineData(Desempate, "DESEMPATE_IDOSO", """{ "idadeMinima": 0 }""", "/parametros/idadeMinima")]
    public void ValorInvalido_ApontaOParametro(string lista, string regra, string parametros, string campo) =>
        Recusas(lista, regra, parametros).Should().Equal(($"/classificacao/{lista}/0{campo}", DocumentoDaConfiguracaoErrorCodes.ValorInvalido));
}
