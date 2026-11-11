namespace Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Quando começa a avaliação de um tipo de decisão no processo: o que vier primeiro entre o
/// início da janela da fase em que a decisão é avaliada (ou de uma etapa que ela abriga) e o
/// primeiro registro de decisão do tipo — a mesma regra de fase iniciada do UNI-REQ-0333.
/// Depois disso, o processo não inclui motivo novo daquele tipo (UNI-REQ-0285).
/// </summary>
public static class InicioDaAvaliacao
{
    /// <summary>O instante em que a avaliação começa, ou nulo enquanto nada a marca.</summary>
    /// <param name="primeiroRegistroDaDecisao">O instante do primeiro registro de decisão do tipo, quando houver.</param>
    public static DateTimeOffset? Instante(ProcessoSeletivo processo, TipoMotivoDeDecisao tipo, DateTimeOffset? primeiroRegistroDaDecisao)
    {
        ArgumentNullException.ThrowIfNull(processo);

        IPoliticaDoTipoDeMotivo politica = PoliticasDosTiposDeMotivo.Do(tipo)
            ?? throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de motivo sem política.");

        IEnumerable<DateTimeOffset?> inicios = processo.CronogramaFases
            .Where(politica.AvaliaNaFase)
            .SelectMany(fase => processo.EtapasDaFase(fase.Codigo).Select(static e => e.Inicio).Append(fase.Inicio))
            .Append(primeiroRegistroDaDecisao);

        return inicios.Where(static i => i.HasValue).Min();
    }

    /// <summary>Se a avaliação do tipo já começou em <paramref name="agora"/>; o próprio instante de início conta como começada.</summary>
    public static bool JaComecou(ProcessoSeletivo processo, TipoMotivoDeDecisao tipo, DateTimeOffset? primeiroRegistroDaDecisao, DateTimeOffset agora) =>
        Instante(processo, tipo, primeiroRegistroDaDecisao) is { } inicio && inicio <= agora;
}
