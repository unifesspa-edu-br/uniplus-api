namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposEtapa;

using FluentValidation;

public sealed class AtualizarTipoEtapaCommandValidator : AbstractValidator<AtualizarTipoEtapaCommand>
{
    private const char CaractereNulo = (char)0;

    public AtualizarTipoEtapaCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.Nome)
            .NotEmpty()
            .MaximumLength(200)
            .Must(static nome => nome is null || !nome.Contains(CaractereNulo))
            .WithMessage("Nome do tipo de etapa não pode conter o caractere nulo (U+0000).");
        RuleFor(command => command.Descricao)
            .MaximumLength(1000)
            .Must(static descricao => descricao is null || !descricao.Contains(CaractereNulo))
            .WithMessage("Descrição do tipo de etapa não pode conter o caractere nulo (U+0000).")
            .When(command => command.Descricao is not null);
    }
}
