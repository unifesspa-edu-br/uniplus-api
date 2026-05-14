namespace Unifesspa.UniPlus.Governance.Contracts;

using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Governance.Contracts.Serialization;

/// <summary>
/// Identificador strongly-typed de uma <c>AreaOrganizacional</c> (ADR-0055).
/// Usado em todo o sistema como tipo de <c>Proprietario</c> e dos elementos de
/// <c>AreasDeInteresse</c> nas entidades área-scoped, e nas roles de área
/// derivadas do JWT — elimina a classe de bugs de typo <c>"ceps"</c> vs
/// <c>"CEPS"</c> e centraliza o ponto de validação/normalização.
/// </summary>
/// <remarks>
/// Construtor privado: a única forma de obter um valor válido é
/// <see cref="From"/>, que retorna <see cref="Result{T}"/> conforme o padrão
/// de Value Object do projeto (<c>Cpf.Criar</c>, <c>Email.Criar</c>). O valor
/// é normalizado para uppercase antes de ser armazenado.
/// <para>
/// Por ser <c>record struct</c>, <c>default(AreaCodigo)</c> existe e tem
/// <see cref="Value"/> nulo — estado que nunca é produzido por <see cref="From"/>.
/// Os membros (<see cref="ToString"/>, <see cref="CompareTo"/>) tratam esse
/// caso graciosamente; o <c>AreaCodigoValueConverter</c> faz fail-fast ao
/// materializar dado inválido vindo do banco.
/// </para>
/// </remarks>
[JsonConverter(typeof(AreaCodigoJsonConverter))]
public readonly partial record struct AreaCodigo : IComparable<AreaCodigo>
{
    /// <summary>Código de erro de domínio para entrada inválida em <see cref="From"/>.</summary>
    public const string CodigoErroInvalido = "AreaCodigo.Invalido";

    private const int ComprimentoMinimo = 2;
    private const int ComprimentoMaximo = 32;

    /// <summary>Valor normalizado (uppercase). É <see langword="null"/> em <c>default(AreaCodigo)</c>.</summary>
    public string Value { get; }

    private AreaCodigo(string value) => Value = value;

    /// <summary>
    /// Cria um <see cref="AreaCodigo"/> validado a partir de uma string.
    /// Regras (ADR-0055): 2 a 32 caracteres, apenas letras ASCII maiúsculas,
    /// dígitos e underscore, sem iniciar por dígito. A entrada é normalizada
    /// para uppercase antes da validação — <c>"ceps"</c> vira <c>"CEPS"</c>.
    /// </summary>
    /// <param name="codigo">A string de entrada (qualquer caixa).</param>
    /// <returns>
    /// <see cref="Result{T}"/> de sucesso com o código normalizado, ou falha
    /// com <see cref="DomainError"/> de código <see cref="CodigoErroInvalido"/>.
    /// </returns>
    public static Result<AreaCodigo> From(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return Result<AreaCodigo>.Failure(new DomainError(
                CodigoErroInvalido,
                "Código de área é obrigatório."));
        }

        string normalizado = codigo.Trim().ToUpperInvariant();

        if (normalizado.Length is < ComprimentoMinimo or > ComprimentoMaximo)
        {
            return Result<AreaCodigo>.Failure(new DomainError(
                CodigoErroInvalido,
                $"Código de área deve ter entre {ComprimentoMinimo} e {ComprimentoMaximo} caracteres."));
        }

        if (!FormatoValido().IsMatch(normalizado))
        {
            return Result<AreaCodigo>.Failure(new DomainError(
                CodigoErroInvalido,
                "Código de área deve conter apenas letras maiúsculas ASCII, dígitos e underscore, "
                + "e não pode iniciar por dígito."));
        }

        return Result<AreaCodigo>.Success(new AreaCodigo(normalizado));
    }

    /// <summary>Ordenação ordinal por <see cref="Value"/>; <c>default</c> ordena antes de qualquer código válido.</summary>
    public int CompareTo(AreaCodigo other) => string.CompareOrdinal(Value, other.Value);

    public static bool operator <(AreaCodigo left, AreaCodigo right) => left.CompareTo(right) < 0;

    public static bool operator <=(AreaCodigo left, AreaCodigo right) => left.CompareTo(right) <= 0;

    public static bool operator >(AreaCodigo left, AreaCodigo right) => left.CompareTo(right) > 0;

    public static bool operator >=(AreaCodigo left, AreaCodigo right) => left.CompareTo(right) >= 0;

    /// <summary>Retorna o valor normalizado, ou string vazia para <c>default(AreaCodigo)</c>.</summary>
    public override string ToString() => Value ?? string.Empty;

    // Primeiro caractere letra ou underscore (nunca dígito), demais
    // alfanuméricos ou underscore. Comprimento já validado fora da regex.
    [GeneratedRegex("^[A-Z_][A-Z0-9_]*$")]
    private static partial Regex FormatoValido();
}
