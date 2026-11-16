namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A escala de um conceito numérico: do mínimo ao máximo, com um número fixo de casas decimais. Só existe
/// conferida, dentro de um <see cref="ConceitoDaEtapa"/>.
/// </summary>
public sealed record EscalaNumerica
{
    private const int MaximoDeCasasDecimais = 4;

    private EscalaNumerica(decimal minima, decimal maxima, int casasDecimais)
    {
        Minima = minima;
        Maxima = maxima;
        CasasDecimais = casasDecimais;
    }

    public decimal Minima { get; }
    public decimal Maxima { get; }
    public int CasasDecimais { get; }

    /// <summary>A escala copiada, ou <see langword="null"/> com as recusas acumuladas em <paramref name="erros"/>.</summary>
    internal static EscalaNumerica? Conferir(EscalaInformada informada, List<FieldError> erros)
    {
        int errosAntes = erros.Count;
        erros.AddRange(RecusasDaFaixa(informada).Concat(RecusasDasCasas(informada)));

        return erros.Count == errosAntes
            ? new EscalaNumerica(informada.Minima, informada.Maxima, informada.CasasDecimais)
            : null;
    }

    private static IEnumerable<FieldError> RecusasDaFaixa(EscalaInformada informada)
    {
        if (informada.Minima >= informada.Maxima)
        {
            yield return new("escala", new DomainError(
                ConceitoDaEtapaErrorCodes.EscalaInvalida, "A escala mínima do conceito deve ser menor que a escala máxima."));
        }
    }

    private static IEnumerable<FieldError> RecusasDasCasas(EscalaInformada informada)
    {
        if (informada.CasasDecimais is < 0 or > MaximoDeCasasDecimais)
        {
            yield return new("escala.casasDecimais", new DomainError(
                ConceitoDaEtapaErrorCodes.CasasDecimaisInvalidas,
                $"As casas decimais da escala do conceito devem estar entre 0 e {MaximoDeCasasDecimais}."));
        }
    }
}
