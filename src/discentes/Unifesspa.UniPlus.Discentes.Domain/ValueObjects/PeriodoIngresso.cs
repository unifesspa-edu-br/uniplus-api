namespace Unifesspa.UniPlus.Discentes.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Ano e período letivo de ingresso do discente (ex.: 2024.1). Não impõe allowlist
/// de período — o conjunto válido é definido pelo SIGAA, não por este módulo.
/// </summary>
public sealed record PeriodoIngresso
{
    public int Ano { get; }
    public int Periodo { get; }

    private PeriodoIngresso(int ano, int periodo)
    {
        Ano = ano;
        Periodo = periodo;
    }

    public static Result<PeriodoIngresso> Criar(int ano, int periodo)
    {
        if (ano <= 0)
            return Result<PeriodoIngresso>.Failure(new DomainError("PeriodoIngresso.AnoInvalido", "Ano de ingresso deve ser positivo."));

        if (periodo <= 0)
            return Result<PeriodoIngresso>.Failure(new DomainError("PeriodoIngresso.PeriodoInvalido", "Período de ingresso deve ser positivo."));

        return Result<PeriodoIngresso>.Success(new PeriodoIngresso(ano, periodo));
    }

    public override string ToString() => $"{Ano}.{Periodo}";
}
