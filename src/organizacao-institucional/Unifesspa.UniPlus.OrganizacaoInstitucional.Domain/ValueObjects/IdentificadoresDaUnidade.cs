namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;

/// <summary>
/// Os quatro identificadores de uma Unidade que têm histórico de vigência, como o
/// usuário os informou: a validação e a normalização são deste objeto.
/// </summary>
public readonly record struct IdentificadoresDaUnidade(string? Slug, string? Sigla, string? Codigo, string? Alias)
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

    private IEnumerable<FieldError> ValidarCodigo()
    {
        if (string.IsNullOrWhiteSpace(Codigo))
        {
            yield return new FieldError("codigo", new DomainError(
                UnidadeErrorCodes.CodigoObrigatorio, "Código da Unidade é obrigatório."));
        }
        else if (Codigo.Trim().Length is < CodigoMinLength or > CodigoMaxLength)
        {
            yield return new FieldError("codigo", new DomainError(
                UnidadeErrorCodes.CodigoTamanho,
                $"Código da Unidade deve ter entre {CodigoMinLength} e {CodigoMaxLength} caracteres."));
        }
    }

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
