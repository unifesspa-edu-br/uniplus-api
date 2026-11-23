namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;

using Domain.Entities;
using Domain.ValueObjects;

/// <summary>
/// As janelas das fases e o fuso horário como a versão publicada os congelou: é neles, e nunca no cadastro vivo, que a
/// data de referência de um documento é resolvida.
/// </summary>
internal sealed record CronogramaCongelado(IReadOnlyList<IPeriodoDaFase> Fases, TimeZoneInfo Fuso)
{
    /// <summary>Lê as janelas e o fuso diretamente do documento congelado; forma inesperada ou zona desconhecida recusa a leitura.</summary>
    internal static bool TentarDoEnvelope(JsonObject envelope, [NotNullWhen(true)] out CronogramaCongelado? cronograma)
    {
        cronograma = null;
        if (!PrimitivasDaProjecao.TentarObjeto(envelope, ProjecaoDoCertamePublicado.BlocoPublico("cronogramaFases"), out JsonObject? bloco)
            || !PrimitivasDaProjecao.TentarArray(bloco, "fases", out JsonArray? array)
            || !PrimitivasDaProjecao.TentarItens<PeriodoDaFaseCongelada>(array, TentarJanela, out List<PeriodoDaFaseCongelada>? janelas)
            || !PrimitivasDaProjecao.TentarObjeto(envelope, ProjecaoDoCertamePublicado.BlocoPublico("localidade"), out JsonObject? localidade)
            || !PrimitivasDaProjecao.TentarTexto(localidade, "fusoHorario", out string fusoHorario))
        {
            return false;
        }

        cronograma = DasFases(janelas, fusoHorario);
        return cronograma is not null;
    }

    /// <summary>As fases e o fuso de uma versão já reidratada; zona desconhecida devolve nulo.</summary>
    internal static CronogramaCongelado? Reidratado(IEnumerable<FaseCronograma> fases, string fusoHorario) =>
        DasFases([.. fases], fusoHorario);

    /// <summary>A data em que a referência cai neste cronograma; nula quando ela não tem data (apresentação) ou a fase não a define.</summary>
    internal DateOnly? DataDe(DataDeReferenciaDoDocumento referencia) => referencia.DataNoCronograma(Fases, Fuso);

    private static CronogramaCongelado? DasFases(IReadOnlyList<IPeriodoDaFase> fases, string fusoHorario) =>
        TimeZoneInfo.TryFindSystemTimeZoneById(fusoHorario, out TimeZoneInfo? fuso) ? new CronogramaCongelado(fases, fuso) : null;

    private static bool TentarJanela(JsonNode? item, [NotNullWhen(true)] out PeriodoDaFaseCongelada? janela)
    {
        janela = null;
        if (item is not JsonObject fase
            || !PrimitivasDaProjecao.TentarIdentificador(fase, "id", out Guid id)
            || !PrimitivasDaProjecao.TentarBooleano(fase, "coletaInscricao", out bool coletaInscricao)
            || !PrimitivasDaProjecao.TentarInstanteOpcional(fase, "inicio", out DateTimeOffset? inicio)
            || !PrimitivasDaProjecao.TentarInstanteOpcional(fase, "fim", out DateTimeOffset? fim))
        {
            return false;
        }

        janela = new PeriodoDaFaseCongelada(id, coletaInscricao, inicio, fim);
        return true;
    }
}
