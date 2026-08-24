namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Código do tipo de deficiência (UNI-REQ-0012 e UNI-REQ-0061, módulo
/// Configuração) — identidade semântica classificatória (ex.:
/// <c>DEFICIENCIA_VISUAL</c>, <c>TEA</c>). Value object com formato fechado:
/// <c>^[A-Z][A-Z0-9_]{1,49}$</c> — inicia com letra maiúscula, segue com letras
/// maiúsculas, dígitos e sublinhado, total de 2 a 50 caracteres. Persistido como
/// <c>varchar</c> via conversor de valor (fail-fast na reidratação).
/// </summary>
/// <remarks>
/// Espelha <see cref="CodigoCondicao"/> em formato e disciplina de erro, mas sem
/// código reservado: nenhum tipo de deficiência é referenciado literalmente por
/// norma, então não há análogo ao <c>PCD</c> nem proteção contra renome/remoção.
/// </remarks>
public sealed partial record CodigoTipoDeficiencia
{
    private const int TamanhoMinimo = 2;
    private const int TamanhoMaximo = 50;

    public string Valor { get; }

    private CodigoTipoDeficiencia(string valor) => Valor = valor;

    /// <summary>
    /// Cria um <see cref="CodigoTipoDeficiencia"/> validando o formato fechado.
    /// Valor nulo/em branco retorna <c>CodigoObrigatorio</c>; fora do formato
    /// retorna <c>CodigoFormatoInvalido</c>. O valor é normalizado por <c>Trim</c>
    /// antes da validação.
    /// </summary>
    public static Result<CodigoTipoDeficiencia> Criar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return Result<CodigoTipoDeficiencia>.Failure(new DomainError(
                TipoDeficienciaErrorCodes.CodigoObrigatorio,
                "Código do tipo de deficiência é obrigatório."));
        }

        string normalizado = valor.Trim();

        if (!FormatoValido().IsMatch(normalizado))
        {
            return Result<CodigoTipoDeficiencia>.Failure(new DomainError(
                TipoDeficienciaErrorCodes.CodigoFormatoInvalido,
                "Código do tipo de deficiência deve iniciar com letra maiúscula e conter apenas "
                + $"letras maiúsculas, dígitos e sublinhado, com {TamanhoMinimo} a {TamanhoMaximo} "
                + "caracteres (ex.: DEFICIENCIA_VISUAL, TEA)."));
        }

        return Result<CodigoTipoDeficiencia>.Success(new CodigoTipoDeficiencia(normalizado));
    }

    /// <summary>Indica se <paramref name="valor"/> respeita o formato fechado, sem alocar value object.</summary>
    public static bool EhValido(string valor) =>
        !string.IsNullOrWhiteSpace(valor) && FormatoValido().IsMatch(valor.Trim());

    public override string ToString() => Valor;

    [GeneratedRegex("^[A-Z][A-Z0-9_]{1,49}$", RegexOptions.CultureInvariant)]
    private static partial Regex FormatoValido();
}
