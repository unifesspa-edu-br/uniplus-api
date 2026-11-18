namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Leitura da referência de reserva demográfica congelada na distribuição de uma oferta.</summary>
internal static class LeituraDaReferenciaDemografica
{
    internal static ReferenciaReservaDemograficaSnapshot? Ler(LeitorEnvelope leitor, JsonObject distribuicao, string pathPai)
    {
        JsonObject? item = leitor.ObjetoOpcional(distribuicao, "referenciaDemografica", pathPai);
        return leitor.Falhou || item is null ? null : LerObjeto(leitor, item, $"{pathPai}.referenciaDemografica");
    }

    private static ReferenciaReservaDemograficaSnapshot? LerObjeto(LeitorEnvelope leitor, JsonObject item, string path)
    {
        leitor.ExigirChaves(item, path, "origemId", "censoReferencia", "ppiPercentual", "quilombolaPercentual", "pcdPercentual", "baseLegal");

        Guid origemId = leitor.Identificador(item, "origemId", path);
        string censo = leitor.TextoNaoVazio(item, "censoReferencia", path, LimitesDoEnvelope.CensoReferencia);
        decimal ppi = LerPercentual(leitor, item, "ppiPercentual", path);
        decimal quilombola = LerPercentual(leitor, item, "quilombolaPercentual", path);
        decimal pcd = LerPercentual(leitor, item, "pcdPercentual", path);
        string baseLegal = leitor.TextoNaoVazio(item, "baseLegal", path, LimitesDoEnvelope.BaseLegal);

        return leitor.Falhou ? null : Congelar(leitor, ReferenciaReservaDemograficaSnapshot.Criar(origemId, censo, ppi, quilombola, pcd, baseLegal));
    }

    private static decimal LerPercentual(LeitorEnvelope leitor, JsonObject item, string chave, string path) =>
        leitor.Decimal(item, chave, EscritaCanonica.EscalaPercentual, path, LimitesDoEnvelope.PrecisaoPercentual);

    private static ReferenciaReservaDemograficaSnapshot? Congelar(LeitorEnvelope leitor, Result<ReferenciaReservaDemograficaSnapshot> referencia) =>
        referencia.IsFailure ? leitor.Propagar<ReferenciaReservaDemograficaSnapshot>(referencia.Error!) : referencia.Value;
}
