namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Código de uma oferta de curso, o que o documento de configuração e o edital usam para citá-la. É informado pela gestão
/// e editável (a identidade da oferta continua sendo o <c>Id</c>). Formato fechado: letra maiúscula seguida de letras
/// maiúsculas, dígitos e sublinhado, de até <see cref="TamanhoMaximo"/> caracteres.
/// </summary>
public sealed record CodigoOfertaCurso
{
    public const int TamanhoMaximo = 50;

    public string Valor { get; }

    private CodigoOfertaCurso(string valor) => Valor = valor;

    /// <summary>Valida o código informado ou lido do banco; o texto é conferido como recebido.</summary>
    public static Result<CodigoOfertaCurso> Criar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return Result<CodigoOfertaCurso>.Failure(new DomainError(
                OfertaCursoErrorCodes.CodigoObrigatorio,
                "O código da oferta de curso é obrigatório."));
        }

        return EhValido(valor)
            ? Result<CodigoOfertaCurso>.Success(new CodigoOfertaCurso(valor!))
            : Result<CodigoOfertaCurso>.Failure(new DomainError(
                OfertaCursoErrorCodes.CodigoFormatoInvalido,
                $"O código da oferta de curso deve iniciar com letra maiúscula e conter apenas letras maiúsculas, dígitos e sublinhado, com no máximo {TamanhoMaximo} caracteres."));
    }

    public static bool EhValido(string? valor) =>
        valor is not null && valor.Length <= TamanhoMaximo && FormatoDoCodigo.TemFormatoValido(valor);

    public override string ToString() => Valor;
}
