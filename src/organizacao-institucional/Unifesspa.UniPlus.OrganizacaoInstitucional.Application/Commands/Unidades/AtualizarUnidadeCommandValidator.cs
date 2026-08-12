namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Unidades;

using FluentValidation;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;

public sealed class AtualizarUnidadeCommandValidator : AbstractValidator<AtualizarUnidadeCommand>
{
    public AtualizarUnidadeCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id da Unidade é obrigatório.");

        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("Nome da Unidade é obrigatório.")
            .MinimumLength(2).WithMessage("Nome da Unidade deve ter ao menos 2 caracteres.")
            .MaximumLength(250).WithMessage("Nome da Unidade deve ter no máximo 250 caracteres.");

        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("Slug da Unidade é obrigatório.");

        RuleFor(x => x.Sigla)
            .NotEmpty().WithMessage("Sigla da Unidade é obrigatória.")
            .MaximumLength(50).WithMessage("Sigla da Unidade deve ter no máximo 50 caracteres.");

        RuleFor(x => x.Codigo)
            .NotEmpty().WithMessage("Código da Unidade é obrigatório.")
            .MaximumLength(50).WithMessage("Código da Unidade deve ter no máximo 50 caracteres.");

        RuleFor(x => x.Alias)
            .MaximumLength(100).WithMessage("Alias da Unidade deve ter no máximo 100 caracteres.")
            .When(x => x.Alias is not null);

        RuleFor(x => x.Tipo)
            .Must(t => Enum.IsDefined(t) && t != TipoUnidade.Nenhum)
            .WithMessage("Tipo de Unidade inválido.");

        // Referência de cidade do Geo (issue #1114), opcional all-or-nothing — mesmo
        // padrão de AtualizarInstituicaoCommandValidator.
        RuleFor(x => x)
            .Must(x => ReferenciaCidadeGeo.EhValida(x.CidadeCodigoIbge, x.CidadeNome, x.CidadeUf))
            .WithMessage("Referência de cidade inválida: informe o trio código IBGE (7 dígitos), nome e UF, com prefixo de UF coerente.")
            .When(x => !string.IsNullOrWhiteSpace(x.CidadeCodigoIbge)
                || !string.IsNullOrWhiteSpace(x.CidadeNome)
                || !string.IsNullOrWhiteSpace(x.CidadeUf));
    }
}
