namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Unidades;

using FluentValidation;

using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;

public sealed class CriarUnidadeCommandValidator : AbstractValidator<CriarUnidadeCommand>
{
    public CriarUnidadeCommandValidator()
    {
        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("Nome da Unidade é obrigatório.")
            .MinimumLength(2).WithMessage("Nome da Unidade deve ter ao menos 2 caracteres.")
            .MaximumLength(250).WithMessage("Nome da Unidade deve ter no máximo 250 caracteres.");

        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("Slug da Unidade é obrigatório.");

        RuleFor(x => x.Sigla)
            .NotEmpty().WithMessage("Sigla da Unidade é obrigatória.")
            .MaximumLength(50).WithMessage("Sigla da Unidade deve ter no máximo 50 caracteres.");

        RuleFor(x => x.Codigo)
            .NotEmpty().WithMessage("Código da Unidade é obrigatório.")
            .MaximumLength(50).WithMessage("Código da Unidade deve ter no máximo 50 caracteres.");

        RuleFor(x => x.Alias)
            .MaximumLength(100).WithMessage("Alias da Unidade deve ter no máximo 100 caracteres.")
            .When(x => x.Alias is not null);

        RuleFor(x => x.Tipo)
            .Must(t => Enum.IsDefined(t) && t != TipoUnidade.Nenhum)
            .WithMessage("Tipo de Unidade inválido.");

        RuleFor(x => x.Origem)
            .Must(o => Enum.IsDefined(o))
            .WithMessage("Origem da Unidade inválida.");

        RuleFor(x => x.VigenciaFim)
            .GreaterThanOrEqualTo(x => x.VigenciaInicio)
            .WithMessage("Data de encerramento deve ser igual ou posterior à data de início.")
            .When(x => x.VigenciaFim.HasValue);
    }
}
