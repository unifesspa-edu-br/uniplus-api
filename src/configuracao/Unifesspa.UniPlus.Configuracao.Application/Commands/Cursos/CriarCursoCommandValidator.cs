namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Cursos;

using FluentValidation;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

public sealed class CriarCursoCommandValidator
    : AbstractValidator<CriarCursoCommand>
{
    public CriarCursoCommandValidator()
    {
        RuleFor(x => x.Codigo)
            .NotEmpty().WithMessage("Código do curso é obrigatório.")
            .MaximumLength(60).WithMessage("Código do curso deve ter no máximo 60 caracteres.");

        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("Nome do curso é obrigatório.")
            .MaximumLength(200).WithMessage("Nome do curso deve ter no máximo 200 caracteres.");

        RuleFor(x => x.Grau)
            .NotEmpty().WithMessage("Grau do curso é obrigatório.")
            .MaximumLength(60).WithMessage("Grau do curso deve ter no máximo 60 caracteres.");

        RuleFor(x => x.NivelEnsino)
            .NotEmpty().WithMessage("Nível de ensino do curso é obrigatório.")
            .MaximumLength(60).WithMessage("Nível de ensino do curso deve ter no máximo 60 caracteres.");

        // Grupo em branco equivale a "sem grupo" (o domínio normaliza para nulo);
        // só valida o domínio fechado quando há valor efetivo.
        RuleFor(x => x.GrupoAreaEnem)
            .Must(v => GrupoCurso.EhValido(v!))
            .WithMessage($"Grupo de área do ENEM deve ser um de: {string.Join(", ", GrupoCurso.Valores)}.")
            .When(x => !string.IsNullOrWhiteSpace(x.GrupoAreaEnem));
    }
}
