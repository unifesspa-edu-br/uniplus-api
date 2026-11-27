namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Projeta no envelope o quadro de vagas de cada oferta de curso, na forma do ramo que o produziu: a Lei de
/// Cotas traz a conferência da reserva legal, e o quadro fixado no edital traz o que o ajuste retirou. O
/// ajuste que o edital declarou fica entre os insumos da distribuição.
/// </summary>
internal static class SerializacaoDoQuadroDeVagas
{
    internal static JsonArray SerializarVagas(ProcessoSeletivo processo) =>
        new([.. processo.DistribuicaoVagas
            .OrderBy(static d => d.OfertaCursoOrigemId)
            .Select(static configuracao => (JsonNode)SerializarVagasDaOferta(configuracao))]);

    /// <summary>
    /// O quadro de uma oferta. A leitura do envelope a compara com o quadro que a declaração monta, e é
    /// por isso que a escrita e a conferência usam a mesma projeção.
    /// </summary>
    internal static JsonObject SerializarVagasDaOferta(ConfiguracaoDistribuicaoVagas configuracao) => new()
    {
        ["ofertaCursoOrigemId"] = configuracao.OfertaCursoOrigemId,
        ["quadro"] = SerializarLinhas(configuracao.Quadro.VagasPeloCodigoDaModalidade()),
        ["totalDeVagasDoEdital"] = configuracao.TotalDeVagasDoEdital,
        ["conferenciaDaReservaLegal"] = configuracao.Quadro is QuadroDeVagasDaLeiDeCotas leiDeCotas
            ? SerializarConferencia(leiDeCotas.ConferenciaDaReservaLegal)
            : null,
        ["ajusteDoQuadroFixado"] = configuracao.Quadro is QuadroDeVagasFixadoNoEdital fixado ? SerializarRetiradas(fixado) : null,
    };

    internal static JsonObject? SerializarAjusteDoQuadro(ParametrosRegraAjusteDistribuicao? ajuste) => ajuste switch
    {
        null => null,
        ParametrosReduzirDe reduzirDe => SerializarReduzirDe(reduzirDe),
        ParametrosReduzirProporcionalEm proporcional => SerializarReduzirProporcionalEm(proporcional),
        _ => throw new InvalidOperationException($"Ajuste do quadro sem projeção no envelope: {ajuste.GetType().Name}."),
    };

    private static JsonObject SerializarReduzirDe(ParametrosReduzirDe ajuste) => new()
    {
        ["$tipo"] = TiposDeAjusteDoQuadro.ReduzirDe,
        ["modalidadeCodigo"] = HashCanonicalComputer.NormalizeNfc(ajuste.ModalidadeCodigo),
    };

    // O conjunto de modalidades não tem ordem que importe ao ajuste: o desempate do maior resto é pela posição no quadro.
    private static JsonObject SerializarReduzirProporcionalEm(ParametrosReduzirProporcionalEm ajuste) => new()
    {
        ["$tipo"] = TiposDeAjusteDoQuadro.ReduzirProporcionalEm,
        ["modalidadeCodigos"] = new JsonArray([.. ajuste.ModalidadeCodigos
            .Select(static codigo => HashCanonicalComputer.NormalizeNfc(codigo))
            .Order(StringComparer.Ordinal)
            .Select(static codigo => (JsonNode?)JsonValue.Create(codigo))]),
    };

    private static JsonObject SerializarConferencia(ConferenciaDaReservaLegal conferencia) => new()
    {
        ["reservaLegalNominal"] = conferencia.ReservaLegalNominal,
        ["reservaLegalEfetiva"] = conferencia.ReservaLegalEfetiva,
        ["excedenteSobreAReservaLegal"] = conferencia.ExcedenteSobreAReservaLegal,
        ["vagasCortadasPorEscassez"] = conferencia.VagasCortadasPorEscassez,
    };

    private static JsonObject SerializarRetiradas(QuadroDeVagasFixadoNoEdital quadro) => new()
    {
        ["vagasRetiradas"] = SerializarLinhas(quadro.VagasRetiradasPeloCodigoDaModalidade()),
        ["totalDeVagasRetiradas"] = quadro.TotalDeVagasRetiradasNoAjuste,
    };

    private static JsonArray SerializarLinhas(IEnumerable<VagasDaModalidade> linhas) =>
        new([.. linhas
            .Select(static linha => (JsonNode)new JsonObject
            {
                ["modalidadeCodigo"] = HashCanonicalComputer.NormalizeNfc(linha.ModalidadeCodigo),
                ["quantidade"] = linha.Quantidade,
            })]);
}
