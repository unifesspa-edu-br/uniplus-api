namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

internal static class SerializacaoDaProvaPresencial
{
    internal static JsonObject Serializar(DeclaracaoDaProvaPresencial? declaracao) => new()
    {
        ["temProvaPresencial"] = declaracao?.TemProvaPresencial ?? false,
        ["municipios"] = new JsonArray([.. (declaracao?.Municipios ?? []).Select(SerializarMunicipio)]),
    };

    private static JsonNode SerializarMunicipio(MunicipioDeProva municipio) => new JsonObject
    {
        ["codigoIbge"] = municipio.CodigoIbge,
        ["uf"] = municipio.Uf,
        ["nome"] = municipio.Nome is null ? null : HashCanonicalComputer.NormalizeNfc(municipio.Nome),
    };
}
