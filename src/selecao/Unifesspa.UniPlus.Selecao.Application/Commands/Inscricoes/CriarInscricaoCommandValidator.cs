namespace Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;

using FluentValidation;

public sealed class CriarInscricaoCommandValidator : AbstractValidator<CriarInscricaoCommand>
{
    public CriarInscricaoCommandValidator()
    {
        RuleFor(x => x.CandidatoId)
            .NotEmpty()
            .WithMessage("Identificador do candidato é obrigatório.");

        RuleFor(x => x.EditalId)
            .NotEmpty()
            .WithMessage("Identificador do edital é obrigatório.");

        RuleFor(x => x.Modalidade)
            .IsInEnum()
            .WithMessage("Modalidade de concorrência inválida.");

        RuleFor(x => x.CodigoCursoPrimeiraOpcao)
            .NotEmpty()
            .WithMessage("Código do curso de primeira opção é obrigatório.");
    }
}
