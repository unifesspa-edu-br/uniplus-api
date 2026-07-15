namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Código de um <see cref="Entities.FatoCandidato"/> (UNI-REQ-0077) — chave natural
/// classificatória do vocabulário de fatos (ex.: <c>COR_RACA</c>, <c>PCD</c>,
/// <c>MODALIDADE</c>). Value object com formato fechado <c>^[A-Z][A-Z0-9_]{1,49}$</c>
/// — começa por letra maiúscula, seguido de maiúsculas, dígitos e sublinhado, de 2
/// a 50 caracteres. Persistido como texto via conversor de valor (fail-fast na
/// reidratação).
/// </summary>
/// <remarks>
/// O código é <b>imutável</b>: é ele que entra, por valor, dentro de um predicado
/// congelado num snapshot de publicação (RN08, ADR-0061) — renomear reinterpretaria
/// o passado. A entidade não expõe método que o altere.
/// </remarks>
public sealed partial record CodigoFatoCandidato
{
    public string Valor { get; }

    private CodigoFatoCandidato(string valor) => Valor = valor;

    /// <summary>
    /// Cria um <see cref="CodigoFatoCandidato"/> validando o formato fechado. Valor
    /// nulo/em branco retorna <c>CodigoObrigatorio</c>; fora do formato retorna
    /// <c>CodigoFormatoInvalido</c>. O valor é normalizado por <c>Trim</c> antes da
    /// validação.
    /// </summary>
    public static Result<CodigoFatoCandidato> Criar(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return Result<CodigoFatoCandidato>.Failure(new DomainError(
                FatoCandidatoErrorCodes.CodigoObrigatorio,
                "Código do fato do candidato é obrigatório."));
        }

        string normalizado = valor.Trim();

        if (!FormatoValido().IsMatch(normalizado))
        {
            return Result<CodigoFatoCandidato>.Failure(new DomainError(
                FatoCandidatoErrorCodes.CodigoFormatoInvalido,
                "Código do fato deve começar por letra maiúscula e conter apenas letras maiúsculas, "
                + "dígitos e sublinhado, com 2 a 50 caracteres (ex.: COR_RACA, PCD, MODALIDADE)."));
        }

        return Result<CodigoFatoCandidato>.Success(new CodigoFatoCandidato(normalizado));
    }

    /// <summary>Indica se <paramref name="valor"/> respeita o formato fechado, sem alocar value object.</summary>
    public static bool EhValido(string? valor) =>
        !string.IsNullOrWhiteSpace(valor) && FormatoValido().IsMatch(valor.Trim());

    public override string ToString() => Valor;

    [GeneratedRegex("^[A-Z][A-Z0-9_]{1,49}$", RegexOptions.CultureInvariant)]
    private static partial Regex FormatoValido();
}
