namespace Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

using System.Text.RegularExpressions;

/// <summary>
/// A forma do rótulo opaco que nomeia o tipo da entidade a que um ato se vincula (ADR-0105):
/// letras maiúsculas e dígitos em grupos separados por <c>_</c>. Fixa uma grafia canônica — sem ela,
/// <c>PROCESSO_SELETIVO</c> e <c>processo-seletivo</c> seriam objetos distintos na consulta, e a mesma
/// entidade se partiria em duas. Verifica a forma, jamais o valor: o módulo não sabe o que o rótulo significa.
/// </summary>
public static partial class TipoDaEntidadeVinculada
{
    /// <summary>Máximo de caracteres do rótulo, depois de aparado.</summary>
    public const int TamanhoMaximo = 60;

    /// <summary>
    /// A gramática do rótulo, escrita só com o subconjunto de expressão regular comum ao .NET e ao
    /// PostgreSQL: o mesmo texto serve ao código e à restrição <c>CHECK</c> das tabelas. O <c>$</c> do .NET
    /// aceita uma quebra de linha final e o do PostgreSQL não, por isso o casamento é conferido por inteiro
    /// em <see cref="TemFormaCanonica"/>.
    /// </summary>
    public const string Padrao = "^[A-Z0-9]+(_[A-Z0-9]+)*$";

    /// <summary>Verdadeiro quando todo o <paramref name="valor"/> casa com <see cref="Padrao"/>, sem quebra de linha no fim.</summary>
    public static bool TemFormaCanonica(string? valor) =>
        valor is not null && Formato().Match(valor) is { Success: true } casamento && casamento.Length == valor.Length;

    /// <summary>
    /// O rótulo aparado, ou a exceção de última linha quando está em branco, passa de
    /// <see cref="TamanhoMaximo"/> ou foge da forma canônica. O validator recusa o mesmo antes, com 422.
    /// </summary>
    internal static string Conferido(string? entidadeTipo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entidadeTipo);
        string tipo = entidadeTipo.Trim();
        if (tipo.Length > TamanhoMaximo)
        {
            throw new ArgumentException($"Tipo da entidade deve ter no máximo {TamanhoMaximo} caracteres.", nameof(entidadeTipo));
        }

        if (!TemFormaCanonica(tipo))
        {
            throw new ArgumentException("Tipo da entidade deve ser um rótulo em UPPER_SNAKE.", nameof(entidadeTipo));
        }

        return tipo;
    }

    [GeneratedRegex(Padrao, RegexOptions.CultureInvariant)]
    private static partial Regex Formato();
}
