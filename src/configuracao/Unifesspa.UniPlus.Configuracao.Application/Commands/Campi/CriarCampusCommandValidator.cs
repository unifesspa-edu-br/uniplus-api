namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Campi;

using FluentValidation;

using Unifesspa.UniPlus.Configuracao.Application.Commands.Enderecos;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;

public sealed class CriarCampusCommandValidator : AbstractValidator<CriarCampusCommand>
{
    public CriarCampusCommandValidator()
    {
        RuleFor(x => x.Sigla)
            .NotEmpty().WithMessage("Sigla do Campus é obrigatória.")
            .MaximumLength(20).WithMessage("Sigla do Campus deve ter no máximo 20 caracteres.");

        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("Nome do Campus é obrigatório.")
            .MinimumLength(2).WithMessage("Nome do Campus deve ter ao menos 2 caracteres.")
            .MaximumLength(200).WithMessage("Nome do Campus deve ter no máximo 200 caracteres.");

        RuleFor(x => x.CidadeCodigoIbge)
            .NotEmpty().WithMessage("Código IBGE da cidade é obrigatório.");

        RuleFor(x => x.CidadeNome)
            .NotEmpty().WithMessage("Nome da cidade é obrigatório.");

        RuleFor(x => x.CidadeUf)
            .NotEmpty().WithMessage("UF da cidade é obrigatória.");

        RuleFor(x => x)
            .Must(x => ReferenciaCidadeGeo.EhValida(x.CidadeCodigoIbge, x.CidadeNome, x.CidadeUf))
            .WithMessage("Referência de cidade inválida: o código IBGE deve ter 7 dígitos e o prefixo de UF deve ser coerente com a UF informada.")
            .When(x => !string.IsNullOrWhiteSpace(x.CidadeCodigoIbge)
                && !string.IsNullOrWhiteSpace(x.CidadeNome)
                && !string.IsNullOrWhiteSpace(x.CidadeUf));

        this.RegrasDeEndereco(x => x.Endereco, x => x.CidadeCodigoIbge, x => x.CidadeUf);

        RuleFor(x => x.CodigoEmec)
            .MaximumLength(20).WithMessage("Código e-MEC do Campus deve ter no máximo 20 caracteres.")
            .When(x => x.CodigoEmec is not null);
    }
}
