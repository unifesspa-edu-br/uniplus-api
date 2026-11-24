namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

internal static class SerializacaoDosMotivosDeDecisao
{
    internal static JsonArray Serializar(IEnumerable<MotivoDeDecisaoDeclarado> motivos) => new(
        [.. motivos.OrderBy(static m => m.Tipo.Codigo(), StringComparer.Ordinal)
            .ThenBy(static m => HashCanonicalComputer.NormalizeNfc(m.Codigo), StringComparer.Ordinal)
            .ThenBy(static m => TipoDoLugar(m.Lugar), StringComparer.Ordinal)
            .ThenBy(static m => IdDoLugar(m.Lugar))
            .Select(static m => (JsonNode)SerializarMotivo(m))]);

    private static JsonObject SerializarMotivo(MotivoDeDecisaoDeclarado motivo)
    {
        JsonObject objeto = new()
        {
            ["tipoDeDecisaoCodigo"] = motivo.Tipo.Codigo(),
            ["codigo"] = HashCanonicalComputer.NormalizeNfc(motivo.Codigo),
            ["lugar"] = SerializarLugar(motivo.Lugar),
            ["descricao"] = HashCanonicalComputer.NormalizeNfc(motivo.Descricao),
            ["subclassificacao"] = Normalizar(motivo.Subclassificacao),
            ["sentido"] = motivo.Sentido.Codigo(),
            ["versaoDoEditalDeEntrada"] = motivo.VersaoDeEntrada,
            ["justificativaDaInclusao"] = Normalizar(motivo.JustificativaDaInclusao),
        };
        ContextoDosRotulos.Acrescentar(objeto, "tipoDeDecisaoCodigo", motivo.Tipo);
        ContextoDosRotulos.Acrescentar(objeto, "sentido", motivo.Sentido);
        return objeto;
    }

    // A identidade armazenada preserva o lugar até que o documento passe a usar a chave da fase ou etapa.
    private static JsonObject SerializarLugar(LugarDaDecisao lugar) => new()
    {
        ["tipo"] = TipoDoLugar(lugar),
        ["id"] = IdDoLugar(lugar).ToString(),
    };

    private static string TipoDoLugar(LugarDaDecisao lugar) => lugar.FaseCanonicaOrigemId.HasValue ? "FASE" : "ETAPA";
    private static Guid IdDoLugar(LugarDaDecisao lugar) => lugar.FaseCanonicaOrigemId ?? lugar.EtapaProcessoId.GetValueOrDefault();
    private static string? Normalizar(string? texto) => texto is null ? null : HashCanonicalComputer.NormalizeNfc(texto);
}
