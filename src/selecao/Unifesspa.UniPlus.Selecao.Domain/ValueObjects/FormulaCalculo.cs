namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Kernel.Results;

public sealed record FormulaCalculo
{
    public decimal FatorDivisao { get; }
    public decimal BonusRegionalPercentual { get; }

    private FormulaCalculo(decimal fatorDivisao, decimal bonusRegionalPercentual)
    {
        FatorDivisao = fatorDivisao;
        BonusRegionalPercentual = bonusRegionalPercentual;
    }

    public static Result<FormulaCalculo> Criar(decimal fatorDivisao, decimal bonusRegionalPercentual = 0)
    {
        if (fatorDivisao <= 0)
            return Result<FormulaCalculo>.Failure(new DomainError("FormulaCalculo.FatorInvalido", "Fator de divisão deve ser positivo."));

        if (bonusRegionalPercentual < 0 || bonusRegionalPercentual > 100)
            return Result<FormulaCalculo>.Failure(new DomainError("FormulaCalculo.BonusInvalido", "Bônus regional deve estar entre 0 e 100%."));

        return Result<FormulaCalculo>.Success(new FormulaCalculo(fatorDivisao, bonusRegionalPercentual));
    }
}
