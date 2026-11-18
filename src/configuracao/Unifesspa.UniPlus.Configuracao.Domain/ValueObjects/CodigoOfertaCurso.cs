namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Código de uma oferta de curso, o que o documento de configuração e o edital usam para citá-la. É informado pela gestão
/// e editável (a identidade da oferta continua sendo o <c>Id</c>). Formato fechado: letra maiúscula seguida de letras
/// maiúsculas, dígitos e sublinhado, de 2 a <see cref="TamanhoMaximo"/> caracteres.
/// </summary>
public sealed partial record CodigoOfertaCurso
{
    public const int TamanhoMaximo = 50;

    public string Valor { get; }

    private CodigoOfertaCurso(string valor) => Valor = valor;

    /// <summary>Valida o código informado ou lido do banco; os espaços nas pontas são descartados.</summary>
    public static Result<CodigoOfertaCurso> Criar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return Result<CodigoOfertaCurso>.Failure(new DomainError(
                OfertaCursoErrorCodes.CodigoObrigatorio,
                "O código da oferta de curso é obrigatório."));
        }

        string normalizado = valor.Trim();

        return FormatoValido().IsMatch(normalizado)
            ? Result<CodigoOfertaCurso>.Success(new CodigoOfertaCurso(normalizado))
            : Result<CodigoOfertaCurso>.Failure(new DomainError(
                OfertaCursoErrorCodes.CodigoFormatoInvalido,
                $"O código da oferta de curso deve iniciar com letra maiúscula e conter apenas letras maiúsculas, dígitos e sublinhado, com 2 a {TamanhoMaximo} caracteres."));
    }

    public override string ToString() => Valor;

    [GeneratedRegex("^[A-Z][A-Z0-9_]{1,49}$", RegexOptions.CultureInvariant)]
    private static partial Regex FormatoValido();
}
