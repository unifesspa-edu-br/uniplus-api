namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using Domain.Enums;

using FluentValidation;

using Kernel.Domain.CodigosDeContrato;

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

        // OrigemCandidatos é NOT NULL e exigido na criação — o piso
        // mínimo do cronograma deriva dela, nunca do Tipo.
        RuleFor(x => x.OrigemCandidatos)
            .NotEmpty()
            .WithMessage("Origem dos candidatos é obrigatória.")
            .Must(static codigo => CatalogoDe<OrigemCandidatos>.TentarObter(codigo, out _))
            .WithMessage("Origem dos candidatos inválida.");

        // CA-01 e CA-04: quem administra o certame — NOT NULL,
        // exigido na criação, resolvido via IUnidadeReader no handler.
        RuleFor(x => x.UnidadeAdministradoraOrigemId)
            .NotEmpty()
            .WithMessage("Unidade administradora do processo seletivo é obrigatória.");
    }
}
