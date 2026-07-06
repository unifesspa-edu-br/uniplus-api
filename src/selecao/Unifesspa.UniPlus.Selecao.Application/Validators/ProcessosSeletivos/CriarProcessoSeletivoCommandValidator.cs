namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using FluentValidation;

using Commands.ProcessosSeletivos;
using Domain.Enums;

public sealed class CriarProcessoSeletivoCommandValidator : AbstractValidator<CriarProcessoSeletivoCommand>
{
    public CriarProcessoSeletivoCommandValidator()
    {
        RuleFor(x => x.Nome)
            .NotEmpty()
            .WithMessage("Nome do processo seletivo é obrigatório.")
            .MaximumLength(300)
            .WithMessage("Nome do processo seletivo deve ter no máximo 300 caracteres.");

        RuleFor(x => x.Tipo)
            .NotEqual(TipoProcesso.Nenhum)
            .WithMessage("Tipo do processo seletivo é obrigatório.")
            .IsInEnum()
            .WithMessage("Tipo do processo seletivo inválido.");
    }
}
