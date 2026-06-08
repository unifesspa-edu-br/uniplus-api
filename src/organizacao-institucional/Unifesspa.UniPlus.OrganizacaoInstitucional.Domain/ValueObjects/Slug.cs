namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;

using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;

/// <summary>
/// Identificador kebab-case normalizado de uma Unidade, usado em caminhos de
/// URL e integrações de identidade. Slug é escolhido e revisado no cadastro —
/// não derivado automaticamente da sigla (siglas reais têm caixa mista e
/// pontos, e há colisões de normalização que exigem decisão humana).
/// </summary>
/// <remarks>
/// Formato: <c>^[a-z][a-z0-9-]{1,62}[a-z0-9]$</c> — inicia com letra
/// minúscula, termina com letra minúscula ou dígito, comprimento 3-64 chars.
/// O valor normalizado é armazenado em lowercase, sem espaços ou acentos.
/// </remarks>
public readonly partial record struct Slug
{
    private const int ComprimentoMinimo = 3;
    private const int ComprimentoMaximo = 64;

    public string Valor { get; }

    private Slug(string valor) => Valor = valor;

    public static Result<Slug> From(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return Result<Slug>.Failure(new DomainError(
                UnidadeErrorCodes.SlugObrigatorio,
                "Slug da Unidade é obrigatório."));
        }

        string normalizado = valor.Trim();

        if (normalizado.Length < ComprimentoMinimo || normalizado.Length > ComprimentoMaximo)
        {
            return Result<Slug>.Failure(new DomainError(
                UnidadeErrorCodes.SlugTamanho,
                $"Slug deve ter entre {ComprimentoMinimo} e {ComprimentoMaximo} caracteres."));
        }

        if (!FormatoValido().IsMatch(normalizado))
        {
            return Result<Slug>.Failure(new DomainError(
                UnidadeErrorCodes.SlugFormatoInvalido,
                "Slug deve estar no formato kebab-case: iniciar com letra minúscula, "
                + "conter apenas letras minúsculas, dígitos e hífens, e terminar com "
                + "letra minúscula ou dígito (ex.: ceps, faculdade-de-ciencias)."));
        }

        return Result<Slug>.Success(new Slug(normalizado));
    }

    public override string ToString() => Valor ?? string.Empty;

    [GeneratedRegex(@"^[a-z][a-z0-9-]{1,62}[a-z0-9]$", RegexOptions.CultureInvariant)]
    private static partial Regex FormatoValido();
}
