namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Xunit;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.EnvelopeDeReferencia;

/// <summary>
/// Referências congeladas no envelope canônico: toda regra é a tripla
/// <c>{codigo, versao, hash}</c> (CA-12) e o tipo de processo é um snapshot entre módulos.
/// </summary>
public sealed class EnvelopeCanonicoReferenciasTests
{
    // O candidato é identificado por ter `codigo` — NÃO por já ter a tripla completa. Exigir as
    // três chaves para depois afirmar que elas existem seria circular: uma referência incompleta
    // ficaria de fora da amostra e o teste passaria justamente no caso que deveria pegar.
    [Fact(DisplayName = "Envelope_ReferenciasDeRegraSaoTripla — toda regra congelada carrega codigo, versao e hash")]
    public void Envelope_ReferenciasDeRegraSaoTripla()
    {
        List<(string Caminho, JsonObject Objeto)> candidatos = [];
        ColetarCandidatosAReferencia(EnvelopeComoObjeto(), "$", candidatos);

        candidatos.Should().NotBeEmpty(
            "o envelope de referência congela ao menos a regra de distribuição, a de cálculo e a de ordem de alocação");

        foreach ((string caminho, JsonObject objeto) in candidatos)
        {
            AfirmarQueATriplaEstaCompleta(caminho, objeto);
        }
    }

    [Fact(DisplayName = "Envelope_TipoProcessoEhSnapshot — o tipo congelado carrega origem, código e nome")]
    public void Envelope_TipoProcessoEhSnapshot()
    {
        JsonObject tipoProcesso = EnvelopeComoObjeto()["tipoProcesso"]!.AsObject();

        tipoProcesso.Select(static propriedade => propriedade.Key).Should().BeEquivalentTo(
            ["origemId", "codigo", "nome"],
            "o tipo de processo é uma cópia entre módulos: a origem permite rastrear a escolha e código/nome " +
            "preservam a interpretação publicada mesmo que o catálogo seja alterado depois");
        tipoProcesso["origemId"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
        tipoProcesso["codigo"]!.GetValue<string>().Should().Be("SISU");
        tipoProcesso["nome"]!.GetValue<string>().Should().Be("SiSU");
    }

    private static void AfirmarQueATriplaEstaCompleta(string caminho, JsonObject objeto)
    {
        objeto.Should().ContainKey("versao",
            $"a referência de regra em '{caminho}' precisa da tripla completa — é ela que garante que uma NOVA " +
            "versão de regra não retroage a um processo já publicado");
        objeto.Should().ContainKey("hash",
            $"a referência de regra em '{caminho}' precisa da tripla completa (o hash é content-addressable)");
    }

    /// <summary>
    /// Candidato a referência de regra = qualquer objeto com a chave <c>codigo</c>, exceto
    /// snapshots cross-módulo identificados por <c>origemId</c>. Deliberadamente frouxo para
    /// referências de regra: é a asserção, não o coletor, que exige a tripla.
    /// </summary>
    private static void ColetarCandidatosAReferencia(JsonNode? node, string caminho, List<(string Caminho, JsonObject Objeto)> acumulador)
    {
        switch (node)
        {
            case JsonObject obj:
                ColetarDoObjeto(obj, caminho, acumulador);
                break;

            case JsonArray arr:
                ColetarDoArray(arr, caminho, acumulador);
                break;

            default:
                break;
        }
    }

    private static void ColetarDoArray(JsonArray arr, string caminho, List<(string Caminho, JsonObject Objeto)> acumulador)
    {
        for (int i = 0; i < arr.Count; i++)
        {
            ColetarCandidatosAReferencia(arr[i], $"{caminho}[{i}]", acumulador);
        }
    }

    private static void ColetarDoObjeto(JsonObject obj, string caminho, List<(string Caminho, JsonObject Objeto)> acumulador)
    {
        if (PareceReferenciaDeRegra(obj))
        {
            acumulador.Add((caminho, obj));
        }

        foreach (KeyValuePair<string, JsonNode?> kvp in obj)
        {
            ColetarCandidatosAReferencia(kvp.Value, $"{caminho}.{kvp.Key}", acumulador);
        }
    }

    // `origemId` distingue o snapshot de TipoProcesso; `naturezaLegal` distingue a MODALIDADE (que
    // também tem `codigo`, mas não é referência de regra); `ordem` distingue a FASE do cronograma
    // (Story #554, PR #903) pela mesma razão; `unicoPorObjeto` distingue a classificação congelada
    // de um tipo de ato; `finalidadeTratamento` distingue o fato derivado congelado; `paginas` distingue o formulário.
    private static bool PareceReferenciaDeRegra(JsonObject obj) =>
        obj.ContainsKey("codigo")
        && !obj.ContainsKey("origemId")
        && !obj.ContainsKey("naturezaLegal")
        && !obj.ContainsKey("ordem")
        && !obj.ContainsKey("unicoPorObjeto")
        && !obj.ContainsKey("finalidadeTratamento")
        && !obj.ContainsKey("paginas");
}
