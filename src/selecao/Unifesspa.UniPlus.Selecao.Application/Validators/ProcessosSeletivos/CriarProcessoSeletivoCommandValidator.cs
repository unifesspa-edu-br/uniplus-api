namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using Domain.Enums;

using FluentValidation;

public sealed class CriarProcessoSeletivoCommandValidator : AbstractValidator<CriarProcessoSeletivoCommand>
{
    public CriarProcessoSeletivoCommandValidator()
    {
        RuleFor(x => x.Nome)
            .NotEmpty()
            .WithMessage("Nome do processo seletivo é obrigatório.")
            .MaximumLength(300)
            .WithMessage("Nome do processo seletivo deve ter no máximo 300 caracteres.");

        RuleFor(x => x.TipoProcessoOrigemId)
            .NotEmpty()
            .WithMessage("Tipo do processo seletivo é obrigatório.");

        // Story #851 §3.4: OrigemCandidatos é NOT NULL e exigido na criação — o piso
        // mínimo do cronograma deriva dela, nunca do Tipo.
        RuleFor(x => x.OrigemCandidatos)
            .NotEqual(OrigemCandidatos.Nenhuma)
            .WithMessage("Origem dos candidatos é obrigatória.")
            .IsInEnum()
            .WithMessage("Origem dos candidatos inválida.");

        // Issue #849, CA-01: quem administra o certame (CA-04 da Feature #40) — NOT NULL,
        // exigido na criação, resolvido via IUnidadeReader no handler.
        RuleFor(x => x.UnidadeAdministradoraOrigemId)
            .NotEmpty()
            .WithMessage("Unidade administradora do processo seletivo é obrigatória.");
    }
}
