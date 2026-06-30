namespace Unifesspa.UniPlus.Configuracao.Application.Commands.RecursosAcessibilidade;

using FluentValidation;

public sealed class AtualizarRecursoAcessibilidadeCommandValidator
    : AbstractValidator<AtualizarRecursoAcessibilidadeCommand>
{
    public AtualizarRecursoAcessibilidadeCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id do recurso de acessibilidade é obrigatório.");

        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("Nome do recurso de acessibilidade é obrigatório.")
            .MaximumLength(200).WithMessage("Nome do recurso de acessibilidade deve ter no máximo 200 caracteres.");

        RuleFor(x => x.Descricao)
            .MaximumLength(1000).WithMessage("Descrição do recurso de acessibilidade deve ter no máximo 1000 caracteres.")
            .When(x => x.Descricao is not null);
    }
}
