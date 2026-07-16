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

        // Story #851 §3.4: OrigemCandidatos é NOT NULL e exigido na criação — o piso
        // mínimo do cronograma deriva dela, nunca do Tipo.
        RuleFor(x => x.OrigemCandidatos)
            .NotEqual(OrigemCandidatos.Nenhuma)
            .WithMessage("Origem dos candidatos é obrigatória.")
            .IsInEnum()
            .WithMessage("Origem dos candidatos inválida.");
    }
}
