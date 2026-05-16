namespace Unifesspa.UniPlus.Selecao.Application.Validators;

using FluentValidation;

using Commands.Editais;

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

        // TipoEditalId opcional nesta Story #454 — a entidade `TipoEdital`
        // ainda não existe (será criada em #455). Quando informado, rejeita
        // Guid.Empty defensivamente: payload do cliente não deve carregar
        // valor "vazio" mascarando a ausência. `null` continua válido.
        RuleFor(x => x.TipoEditalId!.Value)
            .NotEqual(Guid.Empty)
            .When(x => x.TipoEditalId.HasValue)
            .WithMessage("TipoEditalId não pode ser Guid vazio. Omita o campo para deixá-lo nulo.");

        RuleFor(x => x.MaximoOpcoesCurso)
            .InclusiveBetween(1, 2)
            .WithMessage("Máximo de opções de curso deve ser 1 ou 2.");
    }
}
