namespace Unifesspa.UniPlus.Selecao.Application.Validators;

using FluentValidation;

using Unifesspa.UniPlus.Selecao.Application.Commands.Editais;

public sealed class CriarEditalCommandValidator : AbstractValidator<CriarEditalCommand>
{
    public CriarEditalCommandValidator()
    {
        RuleFor(x => x.NumeroEdital)
            .GreaterThan(0)
            .WithMessage("Número do edital deve ser positivo.");

        RuleFor(x => x.AnoEdital)
            .InclusiveBetween(2000, 2100)
            .WithMessage("Ano do edital fora do intervalo válido.");

        RuleFor(x => x.Titulo)
            .NotEmpty()
            .WithMessage("Título do edital é obrigatório.")
            .MaximumLength(500)
            .WithMessage("Título do edital deve ter no máximo 500 caracteres.");

        RuleFor(x => x.TipoProcesso)
            .IsInEnum()
            .WithMessage("Tipo de processo seletivo inválido.");

        RuleFor(x => x.MaximoOpcoesCurso)
            .InclusiveBetween(1, 2)
            .WithMessage("Máximo de opções de curso deve ser 1 ou 2.");
    }
}
