namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;

using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;

/// <summary>
/// Os quatro identificadores de uma Unidade que têm histórico de vigência, como o
/// usuário os informou: a validação e a normalização são deste objeto. O código da unidade é citado
/// pelo documento de configuração do processo, e o formato dele é o que esse documento aceita.
/// </summary>
public readonly partial record struct IdentificadoresDaUnidade(string? Slug, string? Sigla, string? Codigo, string? Alias)
{
    private const int SiglaMaxLength = 50;
    private const int SiglaMinLength = 1;
    private const int CodigoMaxLength = 50;
    private const int CodigoMinLength = 1;
    private const int AliasMaxLength = 100;

    internal IReadOnlyList<FieldError> Validar() =>
        [.. ValidarSlug(), .. ValidarSigla(), .. ValidarCodigo(), .. ValidarAlias()];

    // Só vale depois de Validar, que já provou o formato do slug.
    internal IdentificadoresNormalizados Normalizar() =>
        new(ValueObjects.Slug.From(Slug).Value!, Sigla!.Trim().ToUpperInvariant(), Codigo!.Trim(), Alias?.Trim());

    private IEnumerable<FieldError> ValidarSlug()
    {
        Result<Slug> slug = ValueObjects.Slug.From(Slug);
        if (slug.IsFailure)
        {
            yield return new FieldError("slug", slug.Error!);
        }
    }

    private IEnumerable<FieldError> ValidarSigla()
    {
        if (string.IsNullOrWhiteSpace(Sigla))
        {
            yield return new FieldError("sigla", new DomainError(
                UnidadeErrorCodes.SiglaObrigatoria, "Sigla da Unidade é obrigatória."));
        }
        else if (Sigla.Trim().Length is < SiglaMinLength or > SiglaMaxLength)
        {
            yield return new FieldError("sigla", new DomainError(
                UnidadeErrorCodes.SiglaTamanho,
                $"Sigla da Unidade deve ter entre {SiglaMinLength} e {SiglaMaxLength} caracteres."));
        }
    }

    private IEnumerable<FieldError> ValidarCodigo() =>
        RecusaDoCodigo(Codigo) is { } recusa ? [new FieldError("codigo", recusa)] : [];

    // Uma só recusa, na ordem em que o usuário a corrige: o que falta, o tamanho, o formato.
    internal static DomainError? RecusaDoCodigo(string? codigo) => codigo?.Trim() switch
    {
        null or "" => new DomainError(UnidadeErrorCodes.CodigoObrigatorio, "Código da Unidade é obrigatório."),
        { Length: > CodigoMaxLength } => new DomainError(
            UnidadeErrorCodes.CodigoTamanho,
            $"Código da Unidade deve ter entre {CodigoMinLength} e {CodigoMaxLength} caracteres."),
        string valor when !FormatoDoCodigo().IsMatch(valor) || Guid.TryParseExact(valor, "D", out _) => new DomainError(
            UnidadeErrorCodes.CodigoFormatoInvalido,
            "Código da Unidade deve usar letras sem acento, números, ponto, hífen e sublinhado, começando por letra ou número, e não pode ter a forma de um identificador único (UUID)."),
        _ => null,
    };

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_.-]*\z", RegexOptions.CultureInvariant)]
    private static partial Regex FormatoDoCodigo();

    private IEnumerable<FieldError> ValidarAlias()
    {
        if (Alias is not null && Alias.Trim().Length > AliasMaxLength)
        {
            yield return new FieldError("alias", new DomainError(
                UnidadeErrorCodes.AliasTamanho,
                $"Alias da Unidade não pode ter mais que {AliasMaxLength} caracteres."));
        }
    }
}
