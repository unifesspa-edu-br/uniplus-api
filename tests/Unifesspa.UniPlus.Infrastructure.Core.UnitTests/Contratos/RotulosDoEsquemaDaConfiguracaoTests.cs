namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Contratos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using static Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Contratos.ContratoDaConfiguracao;

/// <summary>
/// Todo rótulo que o esquema declara ao lado de um campo (<c>{campo}Rotulo</c>, ou <c>{campo}Rotulos</c> quando o campo é uma lista)
/// é só de leitura, de texto, e fica ao lado do campo de código a que se refere.
/// </summary>
public sealed class RotulosDoEsquemaDaConfiguracaoTests
{
    private const string SufixoDaLista = "Rotulos";
    private const string SufixoUnico = "Rotulo";

    // O quadro de pesos por área é congelado do catálogo inteiro: o objeto, e não o rótulo, é o que a descrição declara só de leitura.
    private static readonly string[] ForaDaRegra = ["pesos-por-area.schema.json/grupoAreaEnemRotulo"];

    [Fact(DisplayName = "Todo rótulo declarado é só de leitura, de texto e acompanha o campo de código")]
    public void Rotulo_Declarado_ESoDeLeituraAoLadoDoCodigo()
    {
        string[] violacoes = [.. ArquivosDoEsquema.SelectMany(static arquivo => Violacoes(arquivo))];

        violacoes.Should().BeEmpty();
    }

    [Fact(DisplayName = "O esquema declara rótulos, para a regra acima não passar vazia")]
    public void Esquema_DeclaraRotulos()
    {
        ArquivosDoEsquema.SelectMany(static arquivo => Rotulos(arquivo)).Should().HaveCountGreaterThan(40);
    }

    [Fact(DisplayName = "A exigência com os rótulos ao lado dos códigos, inclusive em lista e em objeto aninhado, valida")]
    public void Exigencia_ComRotulos_Valida()
    {
        JsonNode exigencia = Documento("""
            { "chave": "doc_identidade", "aplicabilidade": "GERAL", "aplicabilidadeRotulo": "Geral",
              "basesLegais": [{ "referencia": "Lei 12.711/2012", "abrangencia": "FEDERAL", "abrangenciaRotulo": "Federal" }],
              "formatosPermitidos": { "lista": [{ "formato": "PDF", "formatoRotulo": "PDF", "tamanhoMaximoBytesMax": 1024 }] },
              "prazoDeEmissao": { "escolha": "COM_PRAZO", "escolhaRotulo": "Com prazo", "valor": 3, "unidade": "MESES", "unidadeRotulo": "Meses" } }
            """);
        JsonNode taxa = Documento("""{ "cobra": true, "fundamentos": ["CADASTRO_UNICO"], "fundamentosRotulos": ["Cadastro Único"] }""");

        CaminhosComErro(exigencia, "exigencias.schema.json#/$defs/exigencia").Should().BeEmpty();
        CaminhosComErro(taxa, "taxa-de-inscricao.schema.json").Should().BeEmpty();
    }

    private static IEnumerable<string> Violacoes(string arquivo) =>
        Rotulos(arquivo)
            .Where(rotulo => !ForaDaRegra.Contains($"{arquivo}/{rotulo.Nome}"))
            .SelectMany(rotulo => ProblemasDoRotulo(arquivo, rotulo.Propriedades, rotulo.Nome));

    private static IEnumerable<string> ProblemasDoRotulo(string arquivo, JsonObject propriedades, string nome)
    {
        bool lista = nome.EndsWith(SufixoDaLista, StringComparison.Ordinal);
        string campo = nome[..^(lista ? SufixoDaLista : SufixoUnico).Length];
        JsonNode rotulo = propriedades[nome]!;

        (bool Falha, string Motivo)[] checagens =
        [
            (!propriedades.ContainsKey(campo), $"não há o campo {campo} ao lado"),
            (rotulo["readOnly"]?.GetValue<bool>() != true, "não é readOnly"),
            (!TemOFormatoEsperado(rotulo, lista), lista ? "deveria ser lista de texto" : "deveria ser texto"),
        ];

        return checagens.Where(static checagem => checagem.Falha).Select(checagem => $"{arquivo}/{nome}: {checagem.Motivo}");
    }

    private static bool TemOFormatoEsperado(JsonNode rotulo, bool lista) =>
        lista
            ? rotulo["type"]?.GetValue<string>() == "array" && rotulo["items"]?["type"]?.GetValue<string>() == "string"
            : rotulo["type"]?.GetValue<string>() == "string";

    private static IEnumerable<(JsonObject Propriedades, string Nome)> Rotulos(string arquivo) =>
        Dicionarios(Esquema(arquivo))
            .SelectMany(static propriedades => propriedades
                .Select(static par => par.Key)
                .Where(static nome => nome.EndsWith(SufixoUnico, StringComparison.Ordinal) || nome.EndsWith(SufixoDaLista, StringComparison.Ordinal))
                .Select(nome => (propriedades, nome)));

    private static IEnumerable<JsonObject> Dicionarios(JsonNode? no) => no switch
    {
        JsonObject objeto => objeto.SelectMany(static par => par.Key == "properties" && par.Value is JsonObject propriedades
                ? [propriedades, .. Dicionarios(propriedades)]
                : Dicionarios(par.Value)),
        JsonArray lista => lista.SelectMany(static item => Dicionarios(item)),
        _ => [],
    };
}
