namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;

/// <summary>
/// Identificador kebab-case normalizado de uma Unidade, usado em caminhos de
/// URL e integrações de identidade. Slug é escolhido e revisado no cadastro —
/// não derivado automaticamente da sigla (siglas reais têm caixa mista e
/// pontos, e há colisões de normalização que exigem decisão humana).
/// </summary>
/// <remarks>
/// Formato e comprimento são os de <see cref="FormatoKebab"/>, compartilhados com
/// os demais identificadores legíveis do sistema. O valor normalizado é armazenado
/// em lowercase, sem espaços ou acentos.
/// </remarks>
public readonly record struct Slug
{
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

        if (!FormatoKebab.TemComprimentoValido(normalizado))
        {
            return Result<Slug>.Failure(new DomainError(
                UnidadeErrorCodes.SlugTamanho,
                $"Slug deve ter entre {FormatoKebab.ComprimentoMinimo} e {FormatoKebab.ComprimentoMaximo} caracteres."));
        }

        if (!FormatoKebab.TemFormatoValido(normalizado))
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
}
