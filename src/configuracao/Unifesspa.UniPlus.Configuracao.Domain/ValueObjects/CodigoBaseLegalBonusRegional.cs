namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Código estável de uma base legal de bônus regional, o que o documento de configuração usa para referenciá-la. É formado
/// uma vez, na criação, a partir do tipo do instrumento e da identificação (ex.: <c>PORTARIA_UNIFESSPA_NO_2514_2023</c>), e
/// nunca muda, mesmo que o tipo ou a identificação sejam editados depois. Formato fechado: letra maiúscula seguida de
/// letras maiúsculas, dígitos e sublinhado, até <see cref="TamanhoMaximo"/> caracteres.
/// </summary>
/// <remarks>
/// O código identifica o ato no momento do cadastro; a decisão de negócio usa sempre o <c>Id</c>. Identificações que só
/// diferem em acento, caixa ou pontuação geram o mesmo código e são o mesmo ato.
/// </remarks>
public sealed partial record CodigoBaseLegalBonusRegional
{
    public const int TamanhoMaximo = 100;

    public string Valor { get; }

    private CodigoBaseLegalBonusRegional(string valor) => Valor = valor;

    /// <summary>Forma o código do ato a partir do tipo e da identificação, sem repetir o tipo quando a identificação já o traz.</summary>
    public static Result<CodigoBaseLegalBonusRegional> Gerar(TipoInstrumentoNormativo tipoInstrumento, string identificacao)
    {
        ArgumentNullException.ThrowIfNull(identificacao);

        string normalizada = GeradorDeCodigoEstavel.Normalizar(identificacao);

        return normalizada.Length == 0
            ? Result<CodigoBaseLegalBonusRegional>.Failure(new DomainError(
                BaseLegalBonusRegionalErrorCodes.CodigoFormatoInvalido,
                "A identificação precisa ter ao menos uma letra ou um dígito para formar o código da base legal."))
            : LimitarTamanho(ComporComOTipo(tipoInstrumento.ToCodigo(), normalizada));
    }

    /// <summary>Valida um código já formado, vindo de uma referência do documento ou do banco.</summary>
    public static Result<CodigoBaseLegalBonusRegional> Criar(string? valor)
    {
        string normalizado = valor?.Trim() ?? string.Empty;

        return FormatoValido().IsMatch(normalizado)
            ? Result<CodigoBaseLegalBonusRegional>.Success(new CodigoBaseLegalBonusRegional(normalizado))
            : Result<CodigoBaseLegalBonusRegional>.Failure(new DomainError(
                BaseLegalBonusRegionalErrorCodes.CodigoFormatoInvalido,
                $"O código da base legal deve iniciar com letra maiúscula e conter apenas letras maiúsculas, dígitos e sublinhado, com 2 a {TamanhoMaximo} caracteres."));
    }

    public override string ToString() => Valor;

    private static string ComporComOTipo(string prefixo, string normalizada) =>
        normalizada == prefixo || normalizada.StartsWith(prefixo + "_", StringComparison.Ordinal)
            ? normalizada
            : $"{prefixo}_{normalizada}";

    private static Result<CodigoBaseLegalBonusRegional> LimitarTamanho(string codigo) =>
        codigo.Length > TamanhoMaximo
            ? Result<CodigoBaseLegalBonusRegional>.Failure(new DomainError(
                BaseLegalBonusRegionalErrorCodes.CodigoTamanho,
                $"O código da base legal, formado pelo tipo e pela identificação, passa de {TamanhoMaximo} caracteres: abrevie a identificação."))
            : Result<CodigoBaseLegalBonusRegional>.Success(new CodigoBaseLegalBonusRegional(codigo));

    [GeneratedRegex("^[A-Z][A-Z0-9_]{1,99}$", RegexOptions.CultureInvariant)]
    private static partial Regex FormatoValido();
}
