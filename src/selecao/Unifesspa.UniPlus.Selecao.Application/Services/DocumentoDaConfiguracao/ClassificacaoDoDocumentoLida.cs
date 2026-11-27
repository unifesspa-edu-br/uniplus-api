namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

using System.Text.Json;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using static LeituraDoJsonDoDocumento;

/// <summary>
/// Copia a classificação do documento que passou pela forma: as regras pelo código e pela versão, o arredondamento, a resolução dos
/// pesos por área do ENEM, as regras de eliminação e os critérios de desempate (a condição sobre dado vai como escrita, sem conferência), cada um com a posição que tinha na lista enviada.
/// O quadro de pesos por área que a exportação traz é só de leitura e não é lido. O bloco ausente é nulo.
/// </summary>
internal static class ClassificacaoDoDocumentoLida
{
    internal static ClassificacaoNoDocumento? De(JsonNode? classificacao) => classificacao is JsonObject bloco
        ? new(
            Referencia(Objeto(bloco, "regraCalculo")),
            Referencia(Objeto(bloco, "regraArredondamento")),
            Valor<int>(bloco, "casasArredondamento"),
            Valor<decimal>(bloco, "escalaNotaFinal"),
            Referencia(Objeto(bloco, "regraOrdemAlocacao")),
            Valor<bool>(bloco, "baseadoEmEnem"),
            Texto(bloco, "resolucaoPesoAreaEnem"))
        {
            RegrasEliminacao = [.. Itens(bloco, "regrasEliminacao").Select(static item => Eliminacao(item.Posicao, item.Item))],
            CriteriosDeDesempate = [.. Itens(bloco, "criteriosDeDesempate").Select(static item => Desempate(item.Posicao, item.Item))],
        }
        : null;

    private static RegraDeEliminacaoNoDocumento Eliminacao(int posicao, JsonObject regra)
    {
        JsonObject? parametros = Objeto(regra, "parametros");

        return new(
            posicao,
            ReferenciaOuVazia(regra, "regra"),
            Texto(parametros, "etapaChave"),
            Texto(parametros, "areaCodigo"),
            Valor<decimal>(parametros, "notaMinima"),
            Valor<decimal>(parametros, "minimo"));
    }

    private static CriterioDeDesempateNoDocumento Desempate(int posicao, JsonObject criterio) => new(
        posicao,
        ReferenciaOuVazia(criterio, "regra"),
        Parametros(Objeto(criterio, "parametros")));

    private static ParametrosDoDesempateNoDocumento Parametros(JsonObject? parametros) => new(
        Texto(parametros, "etapaChave"),
        Valor<int>(parametros, "idadeMinima"),
        parametros is null ? [] : Textos(parametros, "areas"))
    {
        CodigoDoDado = Texto(parametros, "codigoDoDado"),
        Operador = Texto(parametros, "operador"),
        Valor = parametros?["valor"] is { } valor ? JsonSerializer.SerializeToElement(valor) : null,
    };
}
