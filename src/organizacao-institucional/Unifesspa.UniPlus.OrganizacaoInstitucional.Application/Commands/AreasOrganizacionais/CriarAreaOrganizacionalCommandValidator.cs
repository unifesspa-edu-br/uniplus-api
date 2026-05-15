namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.AreasOrganizacionais;

using FluentValidation;

using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;

/// <summary>
/// Valida shape do <see cref="CriarAreaOrganizacionalCommand"/>. Domain
/// re-valida no <c>AreaOrganizacional.Criar</c> — este validator é guarda
/// adiantada para 422 limpo via <c>WolverineValidationMiddleware</c>.
/// </summary>
public sealed class CriarAreaOrganizacionalCommandValidator : AbstractValidator<CriarAreaOrganizacionalCommand>
{
    public CriarAreaOrganizacionalCommandValidator()
    {
        RuleFor(c => c.Codigo)
            .NotEmpty().WithMessage("Código da área é obrigatório.");

        RuleFor(c => c.Nome)
            .NotEmpty().WithMessage("Nome da área é obrigatório.")
            .Length(2, 120).WithMessage("Nome deve ter entre 2 e 120 caracteres.");

        // IsInEnum rejeita inteiros fora do enum (ex.: Tipo = 99 em payload
        // não declarado no contrato); NotEqual(Nenhum) rejeita o sentinel default.
        // Sem isso, JSON com "tipo": 0 entraria como Nenhum e seria persistido — o
        // enum dize que esse estado indica corrupção, então rejeitar no boundary
        // mantém o domínio fail-fast.
        RuleFor(c => c.Tipo)
            .IsInEnum().WithMessage("Tipo de área organizacional inválido.")
            .NotEqual(TipoAreaOrganizacional.Nenhum)
            .WithMessage("Tipo de área organizacional é obrigatório.");

        RuleFor(c => c.Descricao)
            .NotEmpty().WithMessage("Descrição é obrigatória.")
            .MaximumLength(500);

        // Padrão estrito: NNNN-slug-em-kebab. Aceita "0055-foo", "0055-foo-bar";
        // rejeita "0055-", "0055--foo", "0055-foo-" (hyphens nas pontas/duplicados).
        RuleFor(c => c.AdrReferenceCode)
            .NotEmpty().WithMessage("AdrReferenceCode é obrigatório — adicionar área exige ADR (ADR-0055).")
            .MaximumLength(200)
            .Matches("^\\d{4}-[a-z0-9]+(?:-[a-z0-9]+)*$")
            .WithMessage("AdrReferenceCode deve seguir o padrão 'NNNN-slug-em-kebab' (ex.: '0055-organizacao-institucional-bounded-context').");
    }
}
