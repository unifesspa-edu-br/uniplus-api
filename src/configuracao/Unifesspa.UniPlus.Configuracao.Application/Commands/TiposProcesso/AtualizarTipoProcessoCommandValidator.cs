namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposProcesso;

using FluentValidation;

public sealed class AtualizarTipoProcessoCommandValidator : AbstractValidator<AtualizarTipoProcessoCommand>
{
    private const char CaractereNulo = (char)0;

    public AtualizarTipoProcessoCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.Nome)
            .NotEmpty()
            .MaximumLength(200)
            .Must(static nome => nome is null || !nome.Contains(CaractereNulo))
            .WithMessage("Nome do tipo de processo seletivo não pode conter o caractere nulo (U+0000).");
        RuleFor(command => command.Descricao)
            .MaximumLength(1000)
            .Must(static descricao => descricao is null || !descricao.Contains(CaractereNulo))
            .WithMessage("Descrição do tipo de processo seletivo não pode conter o caractere nulo (U+0000).")
            .When(command => command.Descricao is not null);
    }
}
