namespace Unifesspa.UniPlus.Selecao.Domain.Leiautes;

/// <summary>
/// O resultado de ler uma célula: o valor lido ou a falha. Nunca carrega o texto da célula, que
/// pode ser CPF ou nota.
/// </summary>
public readonly record struct LeituraDeCelula(object? Valor, FalhaDeCelula? Falha)
{
    public bool Valida => Falha is null;

    public static LeituraDeCelula Com(object? valor) => new(valor, null);

    public static LeituraDeCelula Invalida(FalhaDeCelula falha) => new(null, falha);
}
