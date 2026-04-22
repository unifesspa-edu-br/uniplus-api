namespace Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;

public sealed record NotaFinal
{
    public decimal Valor { get; }

    private NotaFinal(decimal valor) => Valor = valor;

    public static Result<NotaFinal> Criar(decimal valor)
    {
        if (valor < 0)
            return Result<NotaFinal>.Failure(new DomainError("NotaFinal.Negativa", "Nota final não pode ser negativa."));

        return Result<NotaFinal>.Success(new NotaFinal(Math.Round(valor, 4)));
    }

    public override string ToString() => Valor.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
}
