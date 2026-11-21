namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using System.Text.Json;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

/// <summary>
/// Lê, no envelope congelado de uma versão, as fases do cronograma e a fase de cada etapa. A etapa se liga à fase
/// pelo código dela, e a fase tem o identificador estável que o envelope carrega.
/// </summary>
internal static class FasesNoEnvelope
{
    internal static IReadOnlyList<FaseDaVersaoPublicada> Das(string? congelado) =>
        Envelope(congelado) is { } envelope ? Das(envelope) : [];

    internal static UnidadeDaFase? DaFase(string? congelado, Guid faseId) =>
        Das(congelado).FirstOrDefault(f => f.Id == faseId) is { UnidadeResponsavelOrigemId: { } unidade } fase
            ? new UnidadeDaFase(fase.Id, unidade)
            : null;

    internal static UnidadeDaFase? DaEtapa(string? congelado, Guid etapaId)
    {
        if (Envelope(congelado) is not { } envelope || CodigoDaFaseDaEtapa(envelope, etapaId) is not { } codigo)
        {
            return null;
        }

        FaseDaVersaoPublicada? fase = Das(envelope).FirstOrDefault(f => string.Equals(f.Codigo, codigo, StringComparison.Ordinal));
        return fase?.UnidadeResponsavelOrigemId is { } unidade ? new UnidadeDaFase(fase.Id, unidade) : null;
    }

    private static string? CodigoDaFaseDaEtapa(JsonObject envelope, Guid etapaId) =>
        (envelope["etapas"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .FirstOrDefault(e => GuidDe(e["id"]) == etapaId) is { } etapa
            ? TextoDe(etapa["faseCodigo"])
            : null;

    private static List<FaseDaVersaoPublicada> Das(JsonObject envelope)
    {
        List<(EtapaDaVersaoPublicada Etapa, string? FaseCodigo)> etapas = [.. (envelope["etapas"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Select(static e => (
                new EtapaDaVersaoPublicada(GuidDe(e["id"]) ?? Guid.Empty, TextoDe(e["nome"]) ?? string.Empty, new JanelaDeTempo(InstanteDe(e["inicio"]), InstanteDe(e["fim"]))),
                TextoDe(e["faseCodigo"])))];

        return [.. ((envelope["cronogramaFases"] as JsonObject)?["fases"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Select(f => LerFase(f, etapas))
            .Where(static f => f.Id != Guid.Empty)];
    }

    private static FaseDaVersaoPublicada LerFase(JsonObject f, List<(EtapaDaVersaoPublicada Etapa, string? FaseCodigo)> etapas) => new(
        GuidDe(f["id"]) ?? Guid.Empty,
        GuidDe(f["faseCanonicaOrigemId"]) ?? Guid.Empty,
        TextoDe(f["codigo"]) ?? string.Empty,
        GuidDe((f["unidadeResponsavel"] as JsonObject)?["origemId"]),
        new JanelaDeTempo(InstanteDe(f["inicio"]), InstanteDe(f["fim"])),
        f["coletaInscricao"] is JsonValue coleta && coleta.TryGetValue(out bool coletaInscricao) && coletaInscricao,
        [.. etapas.Where(e => string.Equals(e.FaseCodigo, TextoDe(f["codigo"]), StringComparison.Ordinal)).Select(static e => e.Etapa)]);

    private static JsonObject? Envelope(string? congelado)
    {
        if (string.IsNullOrWhiteSpace(congelado))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(congelado) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Guid? GuidDe(JsonNode? node) => Guid.TryParse(TextoDe(node), out Guid id) ? id : null;

    /// <summary>
    /// Sem período quando o nó é ausente ou <c>null</c>. Qualquer outro valor que não esteja na forma canônica
    /// do envelope é falha, não ausência: tratar o instante ilegível como "sem período" faria a fase passar
    /// por não iniciada.
    /// </summary>
    private static DateTimeOffset? InstanteDe(JsonNode? node)
    {
        if (node is null)
        {
            return null;
        }

        return TextoDe(node) is { } texto && HashCanonicalComputer.TryParseInstantCanonical(texto, out DateTimeOffset instante)
            ? instante
            : throw new PayloadForaDoPerfilCanonicoException(
                $"O envelope congelado traz um instante fora da forma canônica '{HashCanonicalComputer.CanonicalInstantFormat}'.");
    }

    private static string? TextoDe(JsonNode? node) =>
        node is JsonValue valor && valor.TryGetValue(out string? texto) ? texto : null;
}
