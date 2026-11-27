namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using static LeituraDoJsonDoDocumento;
using static PartesComunsDoDocumentoLidas;

/// <summary>
/// Copia as etapas do documento que passou pela forma, cada uma com a posição que tinha na lista enviada, a chave que o autor
/// escolheu e os códigos de catálogo como foram escritos. O que a etapa não traz fica ausente, e quem o confere é o domínio.
/// </summary>
internal static class EtapasDoDocumentoLidas
{
    internal static IReadOnlyList<EtapaNoDocumento> De(JsonNode? etapas) =>
        [.. Itens(etapas).Select(static item => Etapa(item.Posicao, item.Item))];

    private static EtapaNoDocumento Etapa(int posicao, JsonObject etapa) => new(
        posicao,
        Texto(etapa, "chave")!,
        Identificacao(etapa),
        Avaliacao(etapa),
        new(Valor<DateTimeOffset>(etapa, "inicio"), Valor<DateTimeOffset>(etapa, "fim")),
        [.. Itens(etapa, "bancas").Select(static item => Banca(item.Posicao, item.Item))])
    {
        PublicacoesPrevistas = [.. Itens(etapa, "publicacoesPrevistas").Select(static item => Produto(item.Posicao, item.Item))],
        EmiteParecerIndividual = Valor<bool>(etapa, "emiteParecerIndividual") ?? false,
        TiposDeDecisao = TiposDeDecisao(etapa),
        Recursos = [.. Itens(etapa, "recursos").Select(static item => Recurso(item.Posicao, item.Item))],
    };

    private static IdentificacaoDaEtapaNoDocumento Identificacao(JsonObject etapa) => new(
        Texto(etapa, "nome"),
        Texto(etapa, "tipoEtapaCodigo"),
        Texto(etapa, "faseChave"),
        CodigoDoCatalogo<CaraterEtapa>(Texto(etapa, "carater")),
        Valor<decimal>(etapa, "peso"),
        Valor<decimal>(etapa, "notaMinima"));

    private static AvaliacaoDaEtapaNoDocumento Avaliacao(JsonObject etapa) => new(
        Referencia(Objeto(etapa, "regraDeApuracaoDaAvaliacao")),
        CodigoDoCatalogo<FormaDeAvaliacao>(Texto(etapa, "formaDeAvaliacao")),
        Valor<decimal>(etapa, "notaMaxima"),
        Valor<decimal>(etapa, "passoDoLancamento"),
        [.. Itens(etapa, "itensDeAvaliacao").Select(static item => ItemDeAvaliacao(item.Posicao, item.Item))],
        [.. Itens(etapa, "escalaDeAtendimento").Select(static item => NivelDeAtendimento(item.Posicao, item.Item))]);

    private static ItemDeAvaliacaoNoDocumento ItemDeAvaliacao(int posicao, JsonObject item) =>
        new(posicao, Texto(item, "chave")!, Texto(item, "rotulo"), Valor<decimal>(item, "pontuacaoMaxima"));

    private static NivelDeAtendimentoNoDocumento NivelDeAtendimento(int posicao, JsonObject nivel) =>
        new(posicao, Texto(nivel, "chave")!, Texto(nivel, "rotulo"), Valor<decimal>(nivel, "fracao"));

    // O prazo, a regra e os procedimentos do recurso da etapa estão no próprio objeto, como no da fase.
    private static RecursoDaEtapaNoDocumento Recurso(int posicao, JsonObject recurso) =>
        new(posicao, CodigoDoCatalogo<EventoInicialDoPrazo>(Texto(recurso, "eventoInicialDoPrazo")), RegraDeRecurso(recurso));
}
