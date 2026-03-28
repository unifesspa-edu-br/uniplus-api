namespace Unifesspa.UniPlus.Ingresso.Application.Commands.Matriculas;

using FluentValidation;

public sealed class IniciarMatriculaCommandValidator : AbstractValidator<IniciarMatriculaCommand>
{
    public IniciarMatriculaCommandValidator()
    {
        RuleFor(x => x.ConvocacaoId)
            .NotEmpty()
            .WithMessage("Identificador da convocação é obrigatório.");

        RuleFor(x => x.CandidatoId)
            .NotEmpty()
            .WithMessage("Identificador do candidato é obrigatório.");

        RuleFor(x => x.CodigoCurso)
            .NotEmpty()
            .WithMessage("Código do curso é obrigatório.");
    }
}
