namespace Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

using Results;

public sealed record NotaFinal
{
    /// <summary>Dígitos totais da coluna (<c>numeric(9,4)</c>): comporta notas até 99.999,9999.</summary>
    public const int Precisao = 9;

    /// <summary>Casas decimais da nota, as do arredondamento aplicado na criação.</summary>
    public const int Escala = 4;

    public decimal Valor { get; }

    private NotaFinal(decimal valor) => Valor = valor;

    public static Result<NotaFinal> Criar(decimal valor)
    {
        if (valor < 0)
            return Result<NotaFinal>.Failure(new DomainError("NotaFinal.Negativa", "Nota final não pode ser negativa."));

        return Result<NotaFinal>.Success(new NotaFinal(Math.Round(valor, Escala)));
    }

    public override string ToString() => Valor.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
}
