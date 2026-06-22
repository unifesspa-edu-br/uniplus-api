namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Instituicoes;

using FluentValidation;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Enderecos;

/// <summary>
/// Validação de formato dos campos da atualização da Instituição. As regras que
/// dependem do banco (existência, tipo da Unidade raiz) ficam no handler.
/// </summary>
public sealed class AtualizarInstituicaoCommandValidator : AbstractValidator<AtualizarInstituicaoCommand>
{
    public AtualizarInstituicaoCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id da Instituição é obrigatório.");

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

        // Referência de cidade do Geo (ADR-0090): opcional all-or-nothing — só
        // valida quando algum fragmento do trio veio; aí exige o trio coerente.
        RuleFor(x => x)
            .Must(x => ReferenciaCidadeGeo.EhValida(x.CidadeCodigoIbge, x.CidadeNome, x.CidadeUf))
            .WithMessage("Referência de cidade inválida: informe o trio código IBGE (7 dígitos), nome e UF, com prefixo de UF coerente.")
            .When(x => !string.IsNullOrWhiteSpace(x.CidadeCodigoIbge)
                || !string.IsNullOrWhiteSpace(x.CidadeNome)
                || !string.IsNullOrWhiteSpace(x.CidadeUf));

        this.RegrasDeEndereco(x => x.Endereco, x => x.CidadeCodigoIbge, x => x.CidadeUf);
    }
}
