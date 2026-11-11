namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Lê, no envelope congelado de uma versão, as fases do cronograma e a fase de cada etapa. A etapa se liga à fase
/// pelo código dela, e a fase tem o identificador estável que o envelope carrega.
/// </summary>
internal static class FasesNoEnvelope
{
    internal sealed record Fase(
        Guid Id,
        Guid OrigemId,
        string Codigo,
        Guid? Unidade,
        DateTimeOffset? Inicio,
        DateTimeOffset? Fim,
        bool ColetaInscricao,
        IReadOnlyList<string> AtosDeclarados);

    internal static IReadOnlyList<Fase> Das(string? congelado) =>
        Envelope(congelado) is { } envelope ? Das(envelope) : [];

    internal static UnidadeDaFase? DaFase(string? congelado, Guid faseId) =>
        Das(congelado).FirstOrDefault(f => f.Id == faseId) is { Unidade: { } unidade } fase
            ? new UnidadeDaFase(fase.Id, unidade)
            : null;

    internal static UnidadeDaFase? DaEtapa(string? congelado, Guid etapaId)
    {
        if (Envelope(congelado) is not { } envelope || CodigoDaFaseDaEtapa(envelope, etapaId) is not { } codigo)
        {
            return null;
        }

        Fase? fase = Das(envelope).FirstOrDefault(f => string.Equals(f.Codigo, codigo, StringComparison.Ordinal));
        return fase?.Unidade is { } unidade ? new UnidadeDaFase(fase.Id, unidade) : null;
    }

    private static string? CodigoDaFaseDaEtapa(JsonObject envelope, Guid etapaId) =>
        (envelope["etapas"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .FirstOrDefault(e => GuidDe(e["id"]) == etapaId) is { } etapa
            ? TextoDe(etapa["faseCodigo"])
            : null;

    private static List<Fase> Das(JsonObject envelope) =>
        [.. ((envelope["cronogramaFases"] as JsonObject)?["fases"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Select(LerFase)
            .Where(static f => f.Id != Guid.Empty)];

    private static Fase LerFase(JsonObject f) => new(
        GuidDe(f["id"]) ?? Guid.Empty,
        GuidDe(f["faseCanonicaOrigemId"]) ?? Guid.Empty,
        TextoDe(f["codigo"]) ?? string.Empty,
        GuidDe((f["unidadeResponsavel"] as JsonObject)?["origemId"]),
        InstanteDe(f["inicio"]),
        InstanteDe(f["fim"]),
        f["coletaInscricao"] is JsonValue coleta && coleta.TryGetValue(out bool coletaInscricao) && coletaInscricao,
        [.. (f["produtos"] as JsonArray ?? []).OfType<JsonObject>().Select(static p => TextoDe(p["atoCodigo"])).OfType<string>()]);

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

    private static DateTimeOffset? InstanteDe(JsonNode? node) =>
        DateTimeOffset.TryParse(TextoDe(node), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset instante)
            ? instante
            : null;

    private static string? TextoDe(JsonNode? node) =>
        node is JsonValue valor && valor.TryGetValue(out string? texto) ? texto : null;
}
