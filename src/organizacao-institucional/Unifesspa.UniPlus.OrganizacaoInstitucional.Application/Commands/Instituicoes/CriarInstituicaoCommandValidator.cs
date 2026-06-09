namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Instituicoes;

using FluentValidation;

/// <summary>
/// Validação de formato dos campos da criação da Instituição (CA-03). As regras
/// que dependem do banco — singleton (CA-02) e tipo da Unidade raiz (CA-04) —
/// ficam no handler, espelhando o padrão do cadastro de Unidade.
/// </summary>
public sealed class CriarInstituicaoCommandValidator : AbstractValidator<CriarInstituicaoCommand>
{
    public CriarInstituicaoCommandValidator()
    {
        RuleFor(x => x.CodigoEmec)
            .NotEmpty().WithMessage("Código e-MEC da Instituição é obrigatório.")
            .MaximumLength(20).WithMessage("Código e-MEC deve ter no máximo 20 caracteres.");

        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("Nome da Instituição é obrigatório.")
            .MaximumLength(250).WithMessage("Nome da Instituição deve ter no máximo 250 caracteres.");

        RuleFor(x => x.Sigla)
            .NotEmpty().WithMessage("Sigla da Instituição é obrigatória.")
            .MaximumLength(50).WithMessage("Sigla da Instituição deve ter no máximo 50 caracteres.");

        RuleFor(x => x.OrganizacaoAcademica)
            .NotEmpty().WithMessage("Organização acadêmica da Instituição é obrigatória.")
            .MaximumLength(100).WithMessage("Organização acadêmica deve ter no máximo 100 caracteres.");

        RuleFor(x => x.CategoriaAdministrativa)
            .NotEmpty().WithMessage("Categoria administrativa da Instituição é obrigatória.")
            .MaximumLength(100).WithMessage("Categoria administrativa deve ter no máximo 100 caracteres.");

        RuleFor(x => x.Website)
            .MaximumLength(255).WithMessage("Website deve ter no máximo 255 caracteres.")
            .When(x => x.Website is not null);
    }
}
