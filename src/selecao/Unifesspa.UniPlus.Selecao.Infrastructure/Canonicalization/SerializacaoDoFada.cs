namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Projeta no envelope o Fator de Atenuação das Desigualdades de Acesso (FADA).
/// </summary>
internal static class SerializacaoDoFada
{
    internal static JsonObject SerializarFada(ProcessoSeletivo processo)
    {
        if (processo.BonusRegional is not { } fada)
        {
            return new JsonObject { ["presente"] = false };
        }

        JsonObject json = ParametrosDaFada(fada);
        AcrescentarBaseLegalCongelada(json, fada);
        return json;
    }

    private static JsonObject ParametrosDaFada(ConfiguracaoBonusRegional fada) =>
        new()
        {
            ["presente"] = true,
            ["regra"] = EscritaCanonica.SerializarReferenciaRegra(fada.Regra),
            ["fator"] = HashCanonicalComputer.SerializeDecimalCanonical(fada.Fator, EscritaCanonica.EscalaPadrao),
            ["teto"] = fada.Teto is { } teto ? HashCanonicalComputer.SerializeDecimalCanonical(teto, EscritaCanonica.EscalaPadrao) : null,
            ["codigoDoDadoDeElegibilidade"] = fada.FatoElegibilidadeCodigo,
        };

    private static void AcrescentarBaseLegalCongelada(JsonObject json, ConfiguracaoBonusRegional fada)
    {
        json["baseLegalId"] = fada.BaseLegalBonusRegionalId;
        json["baseLegalCodigo"] = HashCanonicalComputer.NormalizeNfc(fada.BaseLegalBonusRegionalCodigo);
        json["tipoInstrumento"] = HashCanonicalComputer.NormalizeNfc(fada.TipoInstrumento);
        json["identificacao"] = HashCanonicalComputer.NormalizeNfc(fada.Identificacao);
        json["descricao"] = HashCanonicalComputer.NormalizeNfc(fada.Descricao);
        json["municipios"] = SerializarMunicipiosDaFada(fada);
    }

    private static JsonArray SerializarMunicipiosDaFada(ConfiguracaoBonusRegional fada) =>
        new([.. fada.Municipios
            .OrderBy(static m => m.CodigoIbge, StringComparer.Ordinal)
            .ThenBy(static m => HashCanonicalComputer.NormalizeNfc(m.Nome), StringComparer.Ordinal)
            .ThenBy(static m => HashCanonicalComputer.NormalizeNfc(m.Uf), StringComparer.Ordinal)
            .Select(static m => (JsonNode)new JsonObject
            {
                ["codigoIbge"] = HashCanonicalComputer.NormalizeNfc(m.CodigoIbge),
                ["nome"] = HashCanonicalComputer.NormalizeNfc(m.Nome),
                ["uf"] = HashCanonicalComputer.NormalizeNfc(m.Uf),
            })]);
}
