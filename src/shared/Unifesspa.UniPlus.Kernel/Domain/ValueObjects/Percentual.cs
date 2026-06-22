namespace Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

using Results;

/// <summary>
/// Percentual no intervalo fechado de 0 a 100 (inclusive). Value object de
/// domínio reutilizável por cadastros que armazenam proporções — ex.: os
/// percentuais demográficos da reserva de vagas (PPI, quilombola, PcD) da
/// Lei 12.711/2012. Persistido como <c>numeric(5,2)</c>.
/// </summary>
public sealed record Percentual
{
    /// <summary>Limite inferior inclusivo do intervalo válido.</summary>
    public const decimal Minimo = 0m;

    /// <summary>Limite superior inclusivo do intervalo válido.</summary>
    public const decimal Maximo = 100m;

    public decimal Valor { get; }

    private Percentual(decimal valor) => Valor = valor;

    /// <summary>
    /// Cria um <see cref="Percentual"/> validando o intervalo fechado
    /// [<see cref="Minimo"/>, <see cref="Maximo"/>]. Valor fora da faixa
    /// retorna falha de domínio; os limites 0 e 100 são aceitos.
    /// </summary>
    public static Result<Percentual> Criar(decimal valor)
    {
        if (valor is < Minimo or > Maximo)
        {
            return Result<Percentual>.Failure(new DomainError(
                "Percentual.ForaDeFaixa",
                $"Percentual deve estar entre {Minimo} e {Maximo}."));
        }

        return Result<Percentual>.Success(new Percentual(valor));
    }

    /// <summary>Indica se <paramref name="valor"/> está no intervalo válido, sem alocar.</summary>
    public static bool EhValido(decimal valor) => valor is >= Minimo and <= Maximo;

    public override string ToString() =>
        Valor.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
}
