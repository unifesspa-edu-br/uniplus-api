namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

using System.Globalization;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using static LeituraDoJsonDoDocumento;

/// <summary>
/// Copia os requisitos de uma exigência de documento: os formatos aceitos, o prazo de emissão e a validade, com a data de referência
/// de cada um. Os rótulos são só de leitura e não são lidos; o bloco ausente é nulo.
/// </summary>
internal static class RequisitosDaExigenciaLidos
{
    internal static FormatosPermitidosNoDocumento? Formatos(JsonObject? bloco) => bloco is null
        ? null
        : new(
            Valor<bool>(bloco, "qualquer") ?? false,
            [.. Itens(bloco, "lista").Select(static item => Formato(item.Posicao, item.Item))]);

    internal static PrazoDeEmissaoNoDocumento? PrazoDeEmissao(JsonObject? bloco) => bloco is null
        ? null
        : new(
            CodigoDoCatalogo<EscolhaDoPrazoDeEmissao>(Texto(bloco, "escolha")),
            Valor<int>(bloco, "valor"),
            CodigoDoCatalogo<UnidadeDoPrazoDeEmissao>(Texto(bloco, "unidade")),
            DataDeReferencia(Objeto(bloco, "referencia")),
            CodigoDoCatalogo<EfeitoNaAnaliseDoDocumento>(Texto(bloco, "efeitoForaDoPrazo")));

    internal static ValidadeNoDocumento? Validade(JsonObject? bloco) => bloco is null
        ? null
        : new(
            CodigoDoCatalogo<EscolhaDaValidade>(Texto(bloco, "escolha")),
            DataDeReferencia(Objeto(bloco, "referencia")),
            CodigoDoCatalogo<EfeitoNaAnaliseDoDocumento>(Texto(bloco, "efeitoDoVencimento")));

    // O esquema fixa o formato da data (AAAA-MM-DD); o texto que não é data fica ausente, e o domínio o recusa.
    internal static DateOnly? Data(JsonObject? pai, string propriedade) =>
        DateOnly.TryParseExact(Texto(pai, propriedade), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly data) ? data : null;

    private static FormatoPermitidoNoDocumento Formato(int posicao, JsonObject formato) => new(
        posicao,
        CodigoDoCatalogo<FormatoPermitido>(Texto(formato, "formato")),
        Valor<int>(formato, "tamanhoMaximoBytesMax"));

    private static DataDeReferenciaNoDocumento? DataDeReferencia(JsonObject? bloco) => bloco is null
        ? null
        : new(CodigoDoCatalogo<TipoDaDataDeReferencia>(Texto(bloco, "tipo")), Data(bloco, "data"), Texto(bloco, "faseChave"));
}
