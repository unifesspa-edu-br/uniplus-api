namespace Unifesspa.UniPlus.Infrastructure.Core.Errors;

using FluentValidation;
using FluentValidation.Validators;

/// <summary>
/// Taxonomia fechada dos códigos que o validator FluentValidation publica em
/// <c>errors[].code</c> do 422 (ADR-0023). Sem ela, o código de uma regra sem
/// <c>.WithErrorCode(...)</c> é o nome da classe interna do FluentValidation
/// (<c>NotEmptyValidator</c>, <c>MaximumLengthValidator</c>…) — detalhe de implementação
/// que vazava para o contrato e que o frontend já passava a consumir.
/// </summary>
/// <remarks>
/// <para>
/// A tradução é pelo tipo do validator de propriedade, e não por anotação regra a regra:
/// a ADR-0125 descartou <c>.WithErrorCode(...)</c> disciplinado justamente por não haver
/// garantia estrutural contra esquecimento. Uma regra nova cai sozinha num código do
/// catálogo; <c>.WithErrorCode("uniplus.…")</c> continua valendo como exceção explícita,
/// para a regra que mereça código próprio — o FluentValidation só consulta o resolver
/// quando a regra não declara código.
/// </para>
/// <para>
/// Os seis códigos são registrados em <see cref="KernelDomainErrorRegistration"/>, e é pelo
/// <see cref="IDomainErrorMapper"/> que o <c>GlobalExceptionMiddleware</c> resolve
/// <c>code</c>/<c>type</c>/<c>title</c>, como qualquer outro emissor de erro.
/// </para>
/// </remarks>
public static class ValidationErrorCodes
{
    /// <summary>Raiz do 422 de validação: o <c>code</c> do corpo, não de cada violação.</summary>
    public const string Raiz = "uniplus.validacao";

    /// <summary><c>NotNull</c> e <c>NotEmpty</c>.</summary>
    public const string Obrigatorio = "uniplus.validacao.obrigatorio";

    /// <summary><c>Length</c>, <c>MinimumLength</c>, <c>MaximumLength</c> e o tamanho exato.</summary>
    public const string Tamanho = "uniplus.validacao.tamanho";

    /// <summary><c>Matches</c>, <c>EmailAddress</c> e <c>PrecisionScale</c> (quantidade de dígitos e casas decimais).</summary>
    public const string Formato = "uniplus.validacao.formato";

    /// <summary>Comparadores de ordem (<c>GreaterThan</c>, <c>LessThanOrEqualTo</c>…) e os <c>Between</c>.</summary>
    public const string Faixa = "uniplus.validacao.faixa";

    /// <summary><c>Must</c> e qualquer outra regra — o destino de todo validator que as demais categorias não descrevem.</summary>
    public const string Regra = "uniplus.validacao.regra";

    /// <summary>
    /// Instala <see cref="Resolve"/> como <see cref="ValidatorConfiguration.ErrorCodeResolver"/>
    /// global. Idempotente: a configuração é estática do processo, e repetir a atribuição
    /// não muda nada.
    /// </summary>
    public static void InstallGlobalResolver() => ValidatorOptions.Global.ErrorCodeResolver = Resolve;

    /// <summary>
    /// Código do catálogo para a falha de <paramref name="validator"/> numa regra que não
    /// declara <c>.WithErrorCode(...)</c>.
    /// </summary>
    public static string Resolve(IPropertyValidator validator)
    {
        ArgumentNullException.ThrowIfNull(validator);

        return validator switch
        {
            INotNullValidator or INotEmptyValidator => Obrigatorio,
            ILengthValidator => Tamanho,
            IRegularExpressionValidator or IEmailValidator => Formato,
            _ when EhPrecisionScale(validator) => Formato,
            IBetweenValidator => Faixa,
            // Equal/NotEqual também são IComparisonValidator, mas não descrevem faixa: dizem
            // "tem de ser (ou não pode ser) exatamente este valor", que é regra.
            IComparisonValidator { Comparison: not (Comparison.Equal or Comparison.NotEqual) } => Faixa,
            _ => Regra,
        };
    }

    // PrecisionScaleValidator não expõe interface própria como os demais.
    private static bool EhPrecisionScale(IPropertyValidator validator)
    {
        Type tipo = validator.GetType();
        return tipo.IsGenericType && tipo.GetGenericTypeDefinition() == typeof(PrecisionScaleValidator<>);
    }
}
