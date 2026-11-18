namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Leitura do bloco <c>vagas</c>: o quadro de cada oferta de curso, na forma do ramo que o produziu. O quadro
/// não é reidratado do envelope: é remontado da declaração, e o congelado só confere o que a montagem produz.
/// Ler e injetar o valor congelado tornaria a prova de reprodutibilidade tautológica.
/// </summary>
internal static class LeituraDoQuadroDeVagas
{
    private static readonly string[] ChavesDoQuadro =
        ["ofertaCursoOrigemId", "quadro", "totalDeVagasDoEdital", "conferenciaDaReservaLegal", "ajusteDoQuadroFixado"];

    /// <summary>A forma de cada entrada do bloco — as chaves do ramo e a oferta que ela cobre.</summary>
    internal static List<Guid> LerOfertas(LeitorEnvelope leitor, JsonArray arrayVagas)
    {
        List<Guid> ofertas = [];
        for (int i = 0; !leitor.Falhou && i < arrayVagas.Count; i++)
        {
            JsonObject item = leitor.ItemObjeto(arrayVagas, i, "vagas");
            leitor.ExigirChaves(item, $"vagas[{i}]", ChavesDoQuadro);
            ofertas.Add(leitor.Identificador(item, "ofertaCursoOrigemId", $"vagas[{i}]"));
        }

        return ofertas;
    }

    /// <summary>
    /// Recusa o envelope cujo quadro congelado difere do que a declaração monta: o quadro, o total do edital,
    /// a conferência da reserva legal e as vagas retiradas no ajuste são derivados, e um valor que a montagem
    /// não reproduz é envelope adulterado.
    /// </summary>
    internal static void ConferirComAMontagem(
        LeitorEnvelope leitor, JsonArray arrayVagas, IReadOnlyList<ConfiguracaoDistribuicaoVagas> distribuicao)
    {
        foreach (ConfiguracaoDistribuicaoVagas configuracao in distribuicao)
        {
            JsonObject montado = SerializacaoDoQuadroDeVagas.SerializarVagasDaOferta(configuracao);
            if (!JsonNode.DeepEquals(CongeladoDaOferta(arrayVagas, configuracao.OfertaCursoOrigemId), montado))
            {
                leitor.Malformado<object>(
                    $"vagas[{configuracao.OfertaCursoOrigemId}]",
                    "o quadro congelado não é o que a declaração da oferta monta.");
                return;
            }
        }
    }

    private static JsonObject? CongeladoDaOferta(JsonArray arrayVagas, Guid ofertaCursoOrigemId) =>
        arrayVagas.OfType<JsonObject>().FirstOrDefault(item =>
            Guid.TryParse(item["ofertaCursoOrigemId"]?.ToString(), out Guid id) && id == ofertaCursoOrigemId);
}
