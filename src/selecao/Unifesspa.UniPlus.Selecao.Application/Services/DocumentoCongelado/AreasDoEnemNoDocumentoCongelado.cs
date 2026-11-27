namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoCongelado;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Como o documento congelado escreve o grupo de áreas do ENEM e o quadro de pesos por área. É a regra única da gravação do
/// congelado e da exportação do rascunho: grupos e áreas em ordem ordinal do código, textos em NFC, peso e corte como texto
/// com a escala de <see cref="GrupoPesoAreaEnemCongelado.EscalaDoPesoEDoCorte"/> casas e <c>corte</c> nulo escrito de forma explícita.
/// </summary>
public static class AreasDoEnemNoDocumentoCongelado
{
    public static JsonObject Grupo(GrupoAreaEnemSnapshot grupo)
    {
        ArgumentNullException.ThrowIfNull(grupo);
        return new()
        {
            ["codigo"] = HashCanonicalComputer.NormalizeNfc(grupo.Codigo),
            ["rotulo"] = HashCanonicalComputer.NormalizeNfc(grupo.Rotulo),
        };
    }

    public static JsonArray QuadroDePesos(IEnumerable<GrupoPesoAreaEnemCongelado> quadro)
    {
        ArgumentNullException.ThrowIfNull(quadro);
        return new([.. quadro
            .OrderBy(static grupo => HashCanonicalComputer.NormalizeNfc(grupo.GrupoAreaEnem.Codigo), StringComparer.Ordinal)
            .Select(static grupo => (JsonNode)QuadroDoGrupo(grupo))]);
    }

    private static JsonObject QuadroDoGrupo(GrupoPesoAreaEnemCongelado grupo) => new()
    {
        ["grupoAreaEnem"] = Grupo(grupo.GrupoAreaEnem),
        ["baseLegal"] = HashCanonicalComputer.NormalizeNfc(grupo.BaseLegal),
        ["areas"] = Areas(grupo),
    };

    private static JsonArray Areas(GrupoPesoAreaEnemCongelado grupo) =>
        new([.. grupo.Areas
            .OrderBy(static area => HashCanonicalComputer.NormalizeNfc(area.Codigo), StringComparer.Ordinal)
            .Select(static area => (JsonNode)Area(area))]);

    private static JsonObject Area(AreaPesoAreaEnemCongelada area) => new()
    {
        ["codigo"] = HashCanonicalComputer.NormalizeNfc(area.Codigo),
        ["rotulo"] = HashCanonicalComputer.NormalizeNfc(area.Rotulo),
        ["peso"] = DecimalCanonico(area.Peso),
        ["corte"] = area.Corte is { } corte ? DecimalCanonico(corte) : null,
    };

    private static string DecimalCanonico(decimal valor) =>
        HashCanonicalComputer.SerializeDecimalCanonical(valor, GrupoPesoAreaEnemCongelado.EscalaDoPesoEDoCorte);
}
