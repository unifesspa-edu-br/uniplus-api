namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Escreve em SQL os códigos que um enum declara em <see cref="CodigoDeContratoAttribute"/>, para o CHECK de uma coluna
/// nascer do mesmo código que o conversor grava, sem segunda lista.
/// </summary>
public static class SqlDosCodigosDeContrato
{
    /// <summary>
    /// Os códigos do enum como literais de texto separados por vírgula e espaço (<c>'A', 'B'</c>), na ordem em que o enum
    /// declara os membros.
    /// </summary>
    /// <typeparam name="TEnum">Enum cujos membros declaram o código.</typeparam>
    /// <exception cref="CodigosDeContratoInvalidosException">O enum é inválido.</exception>
    public static string Lista<TEnum>()
        where TEnum : struct, Enum =>
        string.Join(", ", CatalogoDe<TEnum>.Todos().Select(static declarado => Literal(declarado.Codigo)));

    /// <summary>O código do valor como literal de texto SQL (<c>'A'</c>), com a aspa simples dobrada.</summary>
    /// <typeparam name="TEnum">Enum cujos membros declaram o código.</typeparam>
    /// <param name="valor">O valor cujo código entra como literal no SQL.</param>
    /// <exception cref="ArgumentOutOfRangeException">O valor não é membro do enum, como num cast de inteiro.</exception>
    /// <exception cref="CodigosDeContratoInvalidosException">O enum é inválido.</exception>
    public static string Literal<TEnum>(TEnum valor)
        where TEnum : struct, Enum =>
        Literal(valor.Codigo());

    private static string Literal(string codigo) => $"'{codigo.Replace("'", "''", StringComparison.Ordinal)}'";
}
