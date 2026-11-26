namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

public sealed class DefinirCadastroDeReservaCommandValidator : AbstractValidator<DefinirCadastroDeReservaCommand>
{
    public DefinirCadastroDeReservaCommandValidator()
    {
        RuleFor(x => x.ProcessoSeletivoId).NotEmpty().WithMessage("O processo seletivo é obrigatório.");
        RuleFor(x => x.Chamadas).NotNull().WithMessage("A lista de chamadas é obrigatória.");
    }
}
