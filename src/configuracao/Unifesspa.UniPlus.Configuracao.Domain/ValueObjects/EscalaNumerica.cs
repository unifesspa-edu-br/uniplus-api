namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>A escala de um tipo de conceito numérico: do mínimo ao máximo, com um número fixo de casas decimais.</summary>
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

    /// <summary>A escala válida, ou <see langword="null"/> com as violações acumuladas em <paramref name="erros"/>.</summary>
    internal static EscalaNumerica? Conferir(EscalaInformada? informada, List<FieldError> erros)
    {
        ArgumentNullException.ThrowIfNull(erros);
        if (informada is not { EscalaMinima: { } minima, EscalaMaxima: { } maxima, CasasDecimais: { } casas })
        {
            erros.Add(new("numerico", new DomainError(
                TipoConceitoErrorCodes.EscalaNumericaObrigatoria,
                "Informe a escala mínima, a escala máxima e as casas decimais do tipo de conceito numérico.")));
            return null;
        }

        bool faixaValida = ConferirFaixa(minima, maxima, erros);
        bool casasValidas = ConferirCasas(casas, erros);
        return faixaValida && casasValidas ? new EscalaNumerica(minima, maxima, casas) : null;
    }

    private static bool ConferirFaixa(decimal minima, decimal maxima, List<FieldError> erros)
    {
        if (minima < maxima)
        {
            return true;
        }

        erros.Add(new("numerico", new DomainError(
            TipoConceitoErrorCodes.EscalaNumericaInvalida, "A escala mínima deve ser menor que a escala máxima.")));
        return false;
    }

    private static bool ConferirCasas(int casas, List<FieldError> erros)
    {
        if (casas is >= 0 and <= MaximoDeCasasDecimais)
        {
            return true;
        }

        erros.Add(new("numerico.casasDecimais", new DomainError(
            TipoConceitoErrorCodes.CasasDecimaisInvalidas,
            $"As casas decimais devem estar entre 0 e {MaximoDeCasasDecimais}.")));
        return false;
    }
}
